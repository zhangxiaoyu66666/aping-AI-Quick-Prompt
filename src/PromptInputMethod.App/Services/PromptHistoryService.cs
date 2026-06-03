namespace PromptInputMethod.App.Services;

public sealed class PromptHistoryService
{
    private readonly AppDatabaseService _database = new();
    private readonly object _cacheGate = new();
    private IReadOnlyList<PromptHistoryItem>? _cachedRecentItems;

    public IReadOnlyList<PromptHistoryItem> Load()
    {
        lock (_cacheGate)
        {
            if (_cachedRecentItems is not null)
            {
                return _cachedRecentItems;
            }
        }

        var items = _database.LoadHistory();
        SetRecentCache(items);
        return items;
    }

    public IReadOnlyList<PromptHistoryItem> LoadForSync(int limit = int.MaxValue)
    {
        return _database.LoadHistory(limit);
    }

    public IReadOnlyList<PromptHistoryItem> Search(string query, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Load().Take(limit).ToArray();
        }

        var results = _database.SearchHistory(query, limit);
        if (results.Count > 0)
        {
            return results;
        }

        return Load()
            .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.UserRequest.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.ChinesePrompt.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.EnglishPrompt.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Scene.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Mode.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToArray();
    }

    public PromptHistoryItem Save(
        string userRequest,
        string chinesePrompt,
        string englishPrompt,
        string scene,
        string mode,
        IReadOnlyList<PromptConversationMessage>? messages = null,
        string? existingId = null)
    {
        if (string.IsNullOrWhiteSpace(userRequest) && string.IsNullOrWhiteSpace(chinesePrompt))
        {
            throw new InvalidOperationException("没有可保存的历史记录。");
        }

        var savedAt = DateTimeOffset.UtcNow;
        var item = new PromptHistoryItem(
            string.IsNullOrWhiteSpace(existingId) ? Guid.NewGuid().ToString("N") : existingId.Trim(),
            BuildTitle(userRequest, chinesePrompt),
            userRequest.Trim(),
            chinesePrompt.Trim(),
            englishPrompt.Trim(),
            scene,
            mode,
            savedAt,
            NormalizeMessages(messages),
            savedAt);

        _database.SaveHistory(item);
        UpdateRecentCache(item);
        return item;
    }

    private static IReadOnlyList<PromptConversationMessage> NormalizeMessages(IReadOnlyList<PromptConversationMessage>? messages)
    {
        return (messages ?? [])
            .Where(message => !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message with
            {
                Role = NormalizeRole(message.Role),
                Text = message.Text.Trim(),
                CreatedAt = message.CreatedAt == default ? DateTimeOffset.UtcNow : message.CreatedAt
            })
            .ToArray();
    }

    private static string NormalizeRole(string role)
    {
        if (string.Equals(role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return "user";
        }

        if (string.Equals(role, "thinking", StringComparison.OrdinalIgnoreCase))
        {
            return "thinking";
        }

        return "assistant";
    }

    public bool Delete(string id)
    {
        var removed = _database.DeleteHistory(id);
        if (removed)
        {
            InvalidateRecentCache();
        }

        return removed;
    }

    public int ImportForSync(IEnumerable<PromptHistoryItem> items)
    {
        var existing = LoadForSync()
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var materialized = items
            .Where(remote => !existing.TryGetValue(remote.Id, out var local)
                || remote.EffectiveUpdatedAt > local.EffectiveUpdatedAt)
            .ToArray();
        if (materialized.Length > 0)
        {
            _database.ImportHistory(materialized);
            InvalidateRecentCache();
        }

        return materialized.Length;
    }

    public int Clear()
    {
        var removed = _database.ClearHistory();
        if (removed > 0)
        {
            SetRecentCache(Array.Empty<PromptHistoryItem>());
        }

        return removed;
    }

    private void SetRecentCache(IReadOnlyList<PromptHistoryItem> items)
    {
        lock (_cacheGate)
        {
            _cachedRecentItems = items
                .OrderByDescending(item => item.CreatedAt)
                .Take(200)
                .ToArray();
        }
    }

    private void UpdateRecentCache(PromptHistoryItem item)
    {
        lock (_cacheGate)
        {
            if (_cachedRecentItems is null)
            {
                return;
            }

            _cachedRecentItems = _cachedRecentItems
                .Where(current => !string.Equals(current.Id, item.Id, StringComparison.OrdinalIgnoreCase))
                .Prepend(item)
                .OrderByDescending(current => current.CreatedAt)
                .Take(200)
                .ToArray();
        }
    }

    private void InvalidateRecentCache()
    {
        lock (_cacheGate)
        {
            _cachedRecentItems = null;
        }
    }

    private static string BuildTitle(string userRequest, string chinesePrompt)
    {
        var source = string.IsNullOrWhiteSpace(userRequest) ? chinesePrompt : userRequest;
        var title = source.Replace("\r", " ").Replace("\n", " ").Trim();
        while (title.Contains("  ", StringComparison.Ordinal))
        {
            title = title.Replace("  ", " ");
        }

        return title.Length <= 36 ? title : $"{title[..36]}...";
    }

}

public sealed record PromptHistoryItem(
    string Id,
    string Title,
    string UserRequest,
    string ChinesePrompt,
    string EnglishPrompt,
    string Scene,
    string Mode,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PromptConversationMessage>? Messages = null,
    DateTimeOffset UpdatedAt = default)
{
    public DateTimeOffset EffectiveUpdatedAt => UpdatedAt == default ? CreatedAt : UpdatedAt;

    public override string ToString()
    {
        return Title;
    }
}

public sealed record PromptConversationMessage(
    string Role,
    string Text,
    DateTimeOffset CreatedAt)
{
    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
}
