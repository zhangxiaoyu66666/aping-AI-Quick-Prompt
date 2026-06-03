using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class PromptFavoriteService
{
    private readonly AppDatabaseService _database = new();
    private readonly object _cacheGate = new();
    private IReadOnlyList<PromptFavorite>? _cachedItems;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<PromptFavorite> Load()
    {
        lock (_cacheGate)
        {
            if (_cachedItems is not null)
            {
                return _cachedItems;
            }
        }

        var databaseItems = _database.LoadRecords<PromptFavorite>(AppDatabaseService.KindFavorite, 500);
        if (databaseItems.Count > 0)
        {
            var orderedItems = databaseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
            SetCachedItems(orderedItems);
            return orderedItems;
        }

        var empty = Array.Empty<PromptFavorite>();
        SetCachedItems(empty);
        return empty;
    }

    public PromptFavorite Save(string text, string? scene, string? source, string? category = null, string? title = null)
        => SaveOrUpdate(null, text, scene, source, category, title);

    public PromptFavorite SaveOrUpdate(string? id, string text, string? scene, string? source, string? category = null, string? title = null)
    {
        var normalized = text.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("没有可收藏的提示词。");
        }

        var items = Load().ToList();
        var now = DateTimeOffset.UtcNow;
        var existing = string.IsNullOrWhiteSpace(id)
            ? items.FirstOrDefault(item => string.Equals(item.Text, normalized, StringComparison.Ordinal))
            : items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            items.Remove(existing);
            existing = existing with
            {
                Text = normalized,
                Scene = scene,
                Source = source,
                Title = NormalizeTitle(title, existing.Title, normalized),
                Category = NormalizeCategory(category, existing.Category),
                UpdatedAt = now
            };
            items.Insert(0, existing);
            SaveRecords(items);
            SetCachedItems(items);
            return existing;
        }

        var favorite = new PromptFavorite(
            Guid.NewGuid().ToString("N"),
            NormalizeTitle(title, null, normalized),
            normalized,
            scene,
            source,
            now,
            now,
            NormalizeCategory(category, "未分类"));
        items.Insert(0, favorite);
        var savedItems = items.Take(100).ToArray();
        SaveRecords(savedItems);
        SetCachedItems(savedItems);
        return favorite;
    }

    public bool Delete(string id)
    {
        var items = Load().ToList();
        var removed = items.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveRecords(items);
            SetCachedItems(items);
        }

        return removed > 0;
    }

    public int ImportFromFile(string path)
    {
        var imported = LoadItemsFromFile(path);
        if (imported.Count == 0)
        {
            return 0;
        }

        var items = Load().ToList();
        var now = DateTimeOffset.UtcNow;
        var changed = 0;
        foreach (var item in imported)
        {
            var normalizedText = item.Text.Trim();
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                continue;
            }

            var existing = items.FirstOrDefault(current => string.Equals(current.Text, normalizedText, StringComparison.Ordinal));
            if (existing is not null)
            {
                items.Remove(existing);
            }

            items.Insert(0, new PromptFavorite(
                Guid.NewGuid().ToString("N"),
                string.IsNullOrWhiteSpace(item.Title) ? BuildTitle(normalizedText) : item.Title.Trim(),
                normalizedText,
                item.Scene,
                string.IsNullOrWhiteSpace(item.Source) ? "用户导入" : item.Source,
                item.CreatedAt == default ? now : item.CreatedAt,
                now,
                NormalizeCategory(item.Category, "用户导入")));
            changed++;
        }

        var savedItems = items.Take(500).ToArray();
        SaveRecords(savedItems);
        SetCachedItems(savedItems);
        return changed;
    }

    public void ExportToFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SaveStore(path, Load());
    }

    public void ExportCatalogToFile(string path, IEnumerable<PromptTemplateCatalogItem> templates)
    {
        var now = DateTimeOffset.UtcNow;
        var items = templates
            .Where(template => !string.IsNullOrWhiteSpace(template.Text))
            .Select(template => new PromptFavorite(
                template.Id,
                template.Title,
                template.Text,
                null,
                template.Source,
                now,
                now,
                template.Category))
            .ToArray();
        SaveStore(path, items);
    }

    private static void SaveStore(string path, IReadOnlyList<PromptFavorite> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var store = new PromptFavoriteStore("prompt_input_method.prompt_favorites.v1", items);
        File.WriteAllText(path, JsonSerializer.Serialize(store, JsonOptions));
    }

    private static IReadOnlyList<PromptFavorite> LoadItemsFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<PromptFavorite>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<PromptFavoriteStore>(json, JsonOptions);
            var items = store?.Items;
            if (items is null)
            {
                items = JsonSerializer.Deserialize<PromptFavorite[]>(json, JsonOptions) ?? [];
            }

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .ToArray();
        }
        catch
        {
            return Array.Empty<PromptFavorite>();
        }
    }

    private void SaveRecords(IReadOnlyList<PromptFavorite> items)
    {
        _database.ReplaceRecords(
            AppDatabaseService.KindFavorite,
            items.Select(item => new AppRecordItem(
                AppDatabaseService.KindFavorite,
                item.Id,
                item.Title,
                item.Category,
                item.Source ?? "我的模板",
                item.Text,
                JsonSerializer.Serialize(item, JsonOptions),
                item.CreatedAt,
                item.UpdatedAt)),
            updateSearchIndex: false);
    }

    private void SetCachedItems(IReadOnlyList<PromptFavorite> items)
    {
        lock (_cacheGate)
        {
            _cachedItems = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
        }
    }

    private static string BuildTitle(string text)
    {
        var title = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (title.Contains("  ", StringComparison.Ordinal))
        {
            title = title.Replace("  ", " ");
        }

        return title.Length <= 32 ? title : $"{title[..32]}...";
    }

    private static string NormalizeTitle(string? title, string? fallbackTitle, string fallbackText)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var normalized = title.Trim();
            return normalized.Length <= 32 ? normalized : $"{normalized[..32]}...";
        }

        return string.IsNullOrWhiteSpace(fallbackTitle) ? BuildTitle(fallbackText) : fallbackTitle.Trim();
    }

    private static string NormalizeCategory(string? category, string fallback)
    {
        return string.IsNullOrWhiteSpace(category) ? fallback : category.Trim();
    }

}

public sealed record PromptFavorite(
    string Id,
    string Title,
    string Text,
    string? Scene,
    string? Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Category = "未分类")
{
    public override string ToString()
    {
        return Title;
    }
}

internal sealed record PromptFavoriteStore(string Schema, IReadOnlyList<PromptFavorite> Items);
