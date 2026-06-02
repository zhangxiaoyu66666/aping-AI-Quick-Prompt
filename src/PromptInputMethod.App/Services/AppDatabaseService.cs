using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PromptInputMethod.App.Services;

public sealed class AppDatabaseService
{
    public const string DatabaseFileName = "aipin.db";
    public const string KindHistory = "history";
    public const string KindCommonPrompt = "common_prompt";
    public const string KindTemplate = "template";
    public const string KindFavorite = "favorite";
    public const string KindPromptVersion = "prompt_version";
    public const string KindModelSendAudit = "model_send_audit";
    public const string KindOptimizationTarget = "optimization_target";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly Regex QueryTokenRegex = new(@"[\p{L}\p{N}_]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CjkRunRegex = new(@"[\u3400-\u9FFF\uF900-\uFAFF]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly string _connectionString;

    public AppDatabaseService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "PromptInputMethod");
        Directory.CreateDirectory(folder);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(folder, DatabaseFileName),
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
        EnsureSchema();
    }

    public IReadOnlyList<PromptHistoryItem> LoadHistory(int limit = 200)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, title, user_request, chinese_prompt, english_prompt, scene, mode, created_at, messages
            FROM history_items
            ORDER BY created_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<PromptHistoryItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new PromptHistoryItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                ParseDateTimeOffset(reader.GetString(7)),
                DeserializePayload<PromptConversationMessage[]>(reader.GetString(8)) ?? []));
        }

        return items;
    }

    public IReadOnlyList<AppRecordItem> LoadRecords(string kind, int limit = 1000)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT kind, id, title, category, source, content, payload, created_at, updated_at
            FROM app_records
            WHERE kind = $kind
            ORDER BY updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$limit", limit);
        return ReadAppRecords(command);
    }

    public IReadOnlyList<T> LoadRecords<T>(string kind, int limit = 1000)
    {
        return LoadRecords(kind, limit)
            .Select(record => DeserializePayload<T>(record.Payload))
            .Where(item => item is not null)
            .Cast<T>()
            .ToArray();
    }

    public void ReplaceRecords(string kind, IEnumerable<AppRecordItem> records, bool updateSearchIndex)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        DeleteRecords(connection, transaction, kind, updateSearchIndex);
        foreach (var record in records)
        {
            UpsertRecord(connection, transaction, record, updateSearchIndex);
        }

        transaction.Commit();
    }

    public void UpsertRecord(AppRecordItem record, bool updateSearchIndex)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertRecord(connection, transaction, record, updateSearchIndex);
        transaction.Commit();
    }

    public bool DeleteRecord(string kind, string id, bool updateSearchIndex)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM app_records WHERE kind = $kind AND id = $id;";
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$id", id);
        var removed = command.ExecuteNonQuery();
        if (updateSearchIndex)
        {
            DeleteSearchItem(connection, transaction, BuildSearchId(kind, id));
        }

        transaction.Commit();
        return removed > 0;
    }

    public int ClearRecords(string kind, bool updateSearchIndex)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM app_records WHERE kind = $kind;";
        countCommand.Parameters.AddWithValue("$kind", kind);
        var count = Convert.ToInt32(countCommand.ExecuteScalar());
        DeleteRecords(connection, transaction, kind, updateSearchIndex);
        transaction.Commit();
        return count;
    }

    public T? LoadState<T>(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload FROM app_state WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        var payload = command.ExecuteScalar() as string;
        return string.IsNullOrWhiteSpace(payload) ? default : DeserializePayload<T>(payload);
    }

    public void SaveState<T>(string key, T value)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO app_state (key, payload, updated_at)
            VALUES ($key, $payload, $updatedAt)
            ON CONFLICT(key) DO UPDATE SET
                payload = excluded.payload,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(value, JsonOptions));
        command.Parameters.AddWithValue("$updatedAt", ToStoreDate(DateTimeOffset.UtcNow));
        command.ExecuteNonQuery();
    }

    public bool DeleteState(string key)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM app_state WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteNonQuery() > 0;
    }

    public void ImportHistory(IEnumerable<PromptHistoryItem> items)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var item in items)
        {
            UpsertHistory(connection, transaction, item);
        }

        transaction.Commit();
    }

    public void SaveHistory(PromptHistoryItem item)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        UpsertHistory(connection, transaction, item);
        PruneHistory(connection, transaction, 200);
        transaction.Commit();
    }

    public bool DeleteHistory(string id)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var historyCommand = connection.CreateCommand();
        historyCommand.Transaction = transaction;
        historyCommand.CommandText = "DELETE FROM history_items WHERE id = $id;";
        historyCommand.Parameters.AddWithValue("$id", id);
        var removed = historyCommand.ExecuteNonQuery();
        DeleteSearchItem(connection, transaction, BuildSearchId(KindHistory, id));
        transaction.Commit();
        return removed > 0;
    }

    public int ClearHistory()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = "SELECT COUNT(*) FROM history_items;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar());
        using var historyCommand = connection.CreateCommand();
        historyCommand.Transaction = transaction;
        historyCommand.CommandText = "DELETE FROM history_items;";
        historyCommand.ExecuteNonQuery();
        DeleteSearchItems(connection, transaction, KindHistory);
        transaction.Commit();
        return count;
    }

    public IReadOnlyList<PromptHistoryItem> SearchHistory(string query, int limit = 100)
    {
        return Search(query, limit, KindHistory)
            .Select(result => DeserializePayload<PromptHistoryItem>(result.Payload))
            .Where(item => item is not null)
            .Cast<PromptHistoryItem>()
            .ToArray();
    }

    public IReadOnlyList<CommonPromptItem> SearchCommonPrompts(string query, int limit = 200)
    {
        return Search(query, limit, KindCommonPrompt)
            .Select(result => DeserializePayload<CommonPromptItem>(result.Payload))
            .Where(item => item is not null)
            .Cast<CommonPromptItem>()
            .ToArray();
    }

    public void ReplaceCommonPromptIndex(IEnumerable<CommonPromptItem> items)
    {
        ReplaceSearchItems(KindCommonPrompt, items.Select(CreateCommonPromptSearchItem));
    }

    public void ReplaceTemplateIndex(IEnumerable<PromptTemplateCatalogItem> templates)
    {
        ReplaceSearchItems(KindTemplate, templates.Select(CreateTemplateSearchItem));
    }

    public void ReplaceOptimizationTargetIndex(IEnumerable<OptimizationTargetItem> targets)
    {
        ReplaceSearchItems(KindOptimizationTarget, targets.Select(CreateOptimizationTargetSearchItem));
    }

    public IReadOnlyList<SearchIndexItem> Search(string query, int limit = 80, string? kind = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return LoadRecentSearchItems(limit, kind);
        }

        var ftsQuery = BuildFtsQuery(query);
        if (!string.IsNullOrWhiteSpace(ftsQuery))
        {
            try
            {
                var ftsResults = SearchFts(ftsQuery, limit, kind);
                if (ftsResults.Count > 0)
                {
                    return ftsResults;
                }
            }
            catch (SqliteException)
            {
                // FTS query parsing can reject unusual punctuation. LIKE fallback keeps search usable.
            }
        }

        return SearchLike(query, limit, kind);
    }

    private void EnsureSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS history_items (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                user_request TEXT NOT NULL,
                chinese_prompt TEXT NOT NULL,
                english_prompt TEXT NOT NULL,
                scene TEXT NOT NULL,
                mode TEXT NOT NULL,
                created_at TEXT NOT NULL,
                messages TEXT NOT NULL DEFAULT '[]'
            );

            CREATE INDEX IF NOT EXISTS idx_history_items_created_at
            ON history_items(created_at DESC);

            CREATE TABLE IF NOT EXISTS app_records (
                kind TEXT NOT NULL,
                id TEXT NOT NULL,
                title TEXT NOT NULL,
                category TEXT NOT NULL,
                source TEXT NOT NULL,
                content TEXT NOT NULL,
                payload TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                PRIMARY KEY(kind, id)
            );

            CREATE INDEX IF NOT EXISTS idx_app_records_kind_updated
            ON app_records(kind, updated_at DESC);

            CREATE TABLE IF NOT EXISTS app_state (
                key TEXT PRIMARY KEY,
                payload TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS search_items (
                id TEXT PRIMARY KEY,
                kind TEXT NOT NULL,
                source TEXT NOT NULL,
                title TEXT NOT NULL,
                category TEXT NOT NULL,
                content TEXT NOT NULL,
                payload TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_search_items_kind_updated
            ON search_items(kind, updated_at DESC);

            CREATE VIRTUAL TABLE IF NOT EXISTS search_items_fts USING fts5(
                id UNINDEXED,
                kind UNINDEXED,
                source,
                title,
                category,
                content,
                tokenize = 'unicode61'
            );
            """;
        command.ExecuteNonQuery();
        EnsureColumn(connection, "history_items", "messages", "TEXT NOT NULL DEFAULT '[]'");
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void UpsertHistory(SqliteConnection connection, SqliteTransaction transaction, PromptHistoryItem item)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO history_items (id, title, user_request, chinese_prompt, english_prompt, scene, mode, created_at, messages)
            VALUES ($id, $title, $userRequest, $chinesePrompt, $englishPrompt, $scene, $mode, $createdAt, $messages)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                user_request = excluded.user_request,
                chinese_prompt = excluded.chinese_prompt,
                english_prompt = excluded.english_prompt,
                scene = excluded.scene,
                mode = excluded.mode,
                created_at = excluded.created_at,
                messages = excluded.messages;
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$userRequest", item.UserRequest);
        command.Parameters.AddWithValue("$chinesePrompt", item.ChinesePrompt);
        command.Parameters.AddWithValue("$englishPrompt", item.EnglishPrompt);
        command.Parameters.AddWithValue("$scene", item.Scene);
        command.Parameters.AddWithValue("$mode", item.Mode);
        command.Parameters.AddWithValue("$createdAt", ToStoreDate(item.CreatedAt));
        command.Parameters.AddWithValue("$messages", JsonSerializer.Serialize(item.Messages ?? [], JsonOptions));
        command.ExecuteNonQuery();

        UpsertSearchItem(connection, transaction, CreateHistorySearchItem(item));
    }

    private static void PruneHistory(SqliteConnection connection, SqliteTransaction transaction, int limit)
    {
        using var selectCommand = connection.CreateCommand();
        selectCommand.Transaction = transaction;
        selectCommand.CommandText = """
            SELECT id
            FROM history_items
            ORDER BY created_at DESC
            LIMIT -1 OFFSET $limit;
            """;
        selectCommand.Parameters.AddWithValue("$limit", limit);
        var staleIds = new List<string>();
        using (var reader = selectCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                staleIds.Add(reader.GetString(0));
            }
        }

        foreach (var id in staleIds)
        {
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = "DELETE FROM history_items WHERE id = $id;";
            deleteCommand.Parameters.AddWithValue("$id", id);
            deleteCommand.ExecuteNonQuery();
            DeleteSearchItem(connection, transaction, BuildSearchId(KindHistory, id));
        }
    }

    private void ReplaceSearchItems(string kind, IEnumerable<SearchIndexItem> items)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        DeleteSearchItems(connection, transaction, kind);
        foreach (var item in items)
        {
            UpsertSearchItem(connection, transaction, item);
        }

        transaction.Commit();
    }

    private static void UpsertRecord(SqliteConnection connection, SqliteTransaction transaction, AppRecordItem record, bool updateSearchIndex)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO app_records (kind, id, title, category, source, content, payload, created_at, updated_at)
            VALUES ($kind, $id, $title, $category, $source, $content, $payload, $createdAt, $updatedAt)
            ON CONFLICT(kind, id) DO UPDATE SET
                title = excluded.title,
                category = excluded.category,
                source = excluded.source,
                content = excluded.content,
                payload = excluded.payload,
                created_at = excluded.created_at,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$kind", record.Kind);
        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$title", record.Title);
        command.Parameters.AddWithValue("$category", record.Category);
        command.Parameters.AddWithValue("$source", record.Source);
        command.Parameters.AddWithValue("$content", record.Content);
        command.Parameters.AddWithValue("$payload", record.Payload);
        command.Parameters.AddWithValue("$createdAt", ToStoreDate(record.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", ToStoreDate(record.UpdatedAt));
        command.ExecuteNonQuery();

        if (updateSearchIndex)
        {
            UpsertSearchItem(connection, transaction, record.ToSearchIndexItem());
        }
    }

    private static void DeleteRecords(SqliteConnection connection, SqliteTransaction transaction, string kind, bool updateSearchIndex)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM app_records WHERE kind = $kind;";
        command.Parameters.AddWithValue("$kind", kind);
        command.ExecuteNonQuery();

        if (updateSearchIndex)
        {
            DeleteSearchItems(connection, transaction, kind);
        }
    }

    private static void UpsertSearchItem(SqliteConnection connection, SqliteTransaction transaction, SearchIndexItem item)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO search_items (id, kind, source, title, category, content, payload, updated_at)
            VALUES ($id, $kind, $source, $title, $category, $content, $payload, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                kind = excluded.kind,
                source = excluded.source,
                title = excluded.title,
                category = excluded.category,
                content = excluded.content,
                payload = excluded.payload,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$kind", item.Kind);
        command.Parameters.AddWithValue("$source", item.Source);
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$category", item.Category);
        command.Parameters.AddWithValue("$content", item.Content);
        command.Parameters.AddWithValue("$payload", item.Payload);
        command.Parameters.AddWithValue("$updatedAt", ToStoreDate(item.UpdatedAt));
        command.ExecuteNonQuery();

        using var deleteFts = connection.CreateCommand();
        deleteFts.Transaction = transaction;
        deleteFts.CommandText = "DELETE FROM search_items_fts WHERE id = $id;";
        deleteFts.Parameters.AddWithValue("$id", item.Id);
        deleteFts.ExecuteNonQuery();

        using var fts = connection.CreateCommand();
        fts.Transaction = transaction;
        fts.CommandText = """
            INSERT INTO search_items_fts (id, kind, source, title, category, content)
            VALUES ($id, $kind, $source, $title, $category, $content);
            """;
        fts.Parameters.AddWithValue("$id", item.Id);
        fts.Parameters.AddWithValue("$kind", item.Kind);
        fts.Parameters.AddWithValue("$source", item.Source);
        fts.Parameters.AddWithValue("$title", item.Title);
        fts.Parameters.AddWithValue("$category", item.Category);
        fts.Parameters.AddWithValue("$content", BuildSearchableContent(item.Title, item.Category, item.Content));
        fts.ExecuteNonQuery();
    }

    private static void DeleteSearchItems(SqliteConnection connection, SqliteTransaction transaction, string kind)
    {
        using var fts = connection.CreateCommand();
        fts.Transaction = transaction;
        fts.CommandText = "DELETE FROM search_items_fts WHERE kind = $kind;";
        fts.Parameters.AddWithValue("$kind", kind);
        fts.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM search_items WHERE kind = $kind;";
        command.Parameters.AddWithValue("$kind", kind);
        command.ExecuteNonQuery();
    }

    private static void DeleteSearchItem(SqliteConnection connection, SqliteTransaction transaction, string id)
    {
        using var fts = connection.CreateCommand();
        fts.Transaction = transaction;
        fts.CommandText = "DELETE FROM search_items_fts WHERE id = $id;";
        fts.Parameters.AddWithValue("$id", id);
        fts.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM search_items WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private IReadOnlyList<SearchIndexItem> LoadRecentSearchItems(int limit, string? kind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, source, title, category, content, payload, updated_at
            FROM search_items
            WHERE ($kind IS NULL OR kind = $kind)
            ORDER BY updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$kind", string.IsNullOrWhiteSpace(kind) ? DBNull.Value : kind);
        command.Parameters.AddWithValue("$limit", limit);
        return ReadSearchItems(command);
    }

    private IReadOnlyList<SearchIndexItem> SearchFts(string ftsQuery, int limit, string? kind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.kind, s.source, s.title, s.category, s.content, s.payload, s.updated_at
            FROM search_items s
            WHERE ($kind IS NULL OR s.kind = $kind)
              AND s.id IN (
                  SELECT id
                  FROM search_items_fts
                  WHERE search_items_fts MATCH $query
              )
            ORDER BY s.updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$kind", string.IsNullOrWhiteSpace(kind) ? DBNull.Value : kind);
        command.Parameters.AddWithValue("$query", ftsQuery);
        command.Parameters.AddWithValue("$limit", limit);
        return ReadSearchItems(command);
    }

    private IReadOnlyList<SearchIndexItem> SearchLike(string query, int limit, string? kind)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, kind, source, title, category, content, payload, updated_at
            FROM search_items
            WHERE ($kind IS NULL OR kind = $kind)
              AND (title LIKE $query OR category LIKE $query OR source LIKE $query OR content LIKE $query)
            ORDER BY updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$kind", string.IsNullOrWhiteSpace(kind) ? DBNull.Value : kind);
        command.Parameters.AddWithValue("$query", $"%{query.Trim()}%");
        command.Parameters.AddWithValue("$limit", limit);
        return ReadSearchItems(command);
    }

    private static IReadOnlyList<SearchIndexItem> ReadSearchItems(SqliteCommand command)
    {
        var items = new List<SearchIndexItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new SearchIndexItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                ParseDateTimeOffset(reader.GetString(7))));
        }

        return items;
    }

    private static IReadOnlyList<AppRecordItem> ReadAppRecords(SqliteCommand command)
    {
        var records = new List<AppRecordItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new AppRecordItem(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                ParseDateTimeOffset(reader.GetString(7)),
                ParseDateTimeOffset(reader.GetString(8))));
        }

        return records;
    }

    private static SearchIndexItem CreateHistorySearchItem(PromptHistoryItem item)
    {
        return new SearchIndexItem(
            BuildSearchId(KindHistory, item.Id),
            KindHistory,
            item.Mode,
            item.Title,
            item.Scene,
            string.Join(Environment.NewLine, item.UserRequest, item.ChinesePrompt, item.EnglishPrompt, string.Join(Environment.NewLine, item.Messages?.Select(message => message.Text) ?? [])),
            JsonSerializer.Serialize(item, JsonOptions),
            item.CreatedAt);
    }

    private static SearchIndexItem CreateCommonPromptSearchItem(CommonPromptItem item)
    {
        return new SearchIndexItem(
            BuildSearchId(KindCommonPrompt, item.Id),
            KindCommonPrompt,
            "常用提示词",
            item.Title,
            item.Category,
            item.Text,
            JsonSerializer.Serialize(item, JsonOptions),
            item.UpdatedAt);
    }

    private static SearchIndexItem CreateTemplateSearchItem(PromptTemplateCatalogItem item)
    {
        return new SearchIndexItem(
            BuildSearchId(KindTemplate, item.Id),
            KindTemplate,
            item.Source,
            item.Title,
            item.Category,
            item.Text,
            JsonSerializer.Serialize(item, JsonOptions),
            DateTimeOffset.UtcNow);
    }

    private static SearchIndexItem CreateOptimizationTargetSearchItem(OptimizationTargetItem item)
    {
        return new SearchIndexItem(
            BuildSearchId(KindOptimizationTarget, item.Id),
            KindOptimizationTarget,
            item.TemplateSource,
            item.Title,
            item.Category,
            string.Join(Environment.NewLine, item.Description, item.Compatibility, string.Join(" ", item.Keywords), item.LocalPromptTemplate),
            JsonSerializer.Serialize(item, JsonOptions),
            item.UpdatedAt == default ? DateTimeOffset.UtcNow : item.UpdatedAt);
    }

    private static string BuildSearchableContent(params string[] parts)
    {
        var text = NormalizeIndexText(string.Join(Environment.NewLine, parts));
        var cjkGrams = BuildCjkNGrams(text);
        return string.IsNullOrWhiteSpace(cjkGrams) ? text : $"{text}{Environment.NewLine}{cjkGrams}";
    }

    private static string BuildCjkNGrams(string text)
    {
        var grams = new List<string>();
        foreach (Match match in CjkRunRegex.Matches(text))
        {
            var value = match.Value;
            for (var size = 2; size <= 3 && size <= value.Length; size++)
            {
                for (var i = 0; i <= value.Length - size; i++)
                {
                    grams.Add(value.Substring(i, size));
                    if (grams.Count >= 2000)
                    {
                        return string.Join(' ', grams.Distinct(StringComparer.Ordinal));
                    }
                }
            }
        }

        return string.Join(' ', grams.Distinct(StringComparer.Ordinal));
    }

    private static string NormalizeIndexText(string text)
    {
        var normalized = text.Replace('\0', ' ').Trim();
        return normalized.Length <= 6000 ? normalized : normalized[..6000];
    }

    private static string? BuildFtsQuery(string query)
    {
        var tokens = QueryTokenRegex.Matches(query)
            .Select(match => match.Value.Trim().Replace("\"", string.Empty, StringComparison.Ordinal))
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(token => $"{token}*")
            .ToArray();
        return tokens.Length == 0 ? null : string.Join(" OR ", tokens);
    }

    private static void EnsureColumn(SqliteConnection connection, string tableName, string columnName, string definition)
    {
        using var readCommand = connection.CreateCommand();
        readCommand.CommandText = $"PRAGMA table_info({tableName});";
        using (var reader = readCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};";
        alterCommand.ExecuteNonQuery();
    }

    private static T? DeserializePayload<T>(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static string BuildSearchId(string kind, string id)
    {
        return $"{kind}:{id}";
    }

    private static string ToStoreDate(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("O");
    }

    private static DateTimeOffset ParseDateTimeOffset(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UtcNow;
    }
}

public sealed record AppRecordItem(
    string Kind,
    string Id,
    string Title,
    string Category,
    string Source,
    string Content,
    string Payload,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public SearchIndexItem ToSearchIndexItem()
    {
        return new SearchIndexItem(
            $"{Kind}:{Id}",
            Kind,
            Source,
            Title,
            Category,
            Content,
            Payload,
            UpdatedAt);
    }
}

public sealed record SearchIndexItem(
    string Id,
    string Kind,
    string Source,
    string Title,
    string Category,
    string Content,
    string Payload,
    DateTimeOffset UpdatedAt);
