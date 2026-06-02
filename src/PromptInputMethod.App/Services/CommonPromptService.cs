using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class CommonPromptService
{
    private readonly AppDatabaseService _database = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<CommonPromptItem> Load()
    {
        var databaseItems = _database.LoadRecords<CommonPromptItem>(AppDatabaseService.KindCommonPrompt, 500);
        if (databaseItems.Count > 0)
        {
            var orderedItems = databaseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
            _database.ReplaceCommonPromptIndex(orderedItems);
            return orderedItems;
        }

        var defaults = BuildDefaultItems();
        SaveRecords(defaults);
        _database.ReplaceCommonPromptIndex(defaults);
        return defaults;
    }

    public IReadOnlyList<CommonPromptItem> Search(string query, int limit = 200)
    {
        var items = Load();
        if (string.IsNullOrWhiteSpace(query))
        {
            return items.Take(limit).ToArray();
        }

        var results = _database.SearchCommonPrompts(query, limit);
        if (results.Count > 0)
        {
            return results;
        }

        return items
            .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Text.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToArray();
    }

    public CommonPromptItem Save(string title, string text, string? category = null)
    {
        return SaveOrUpdate(null, title, text, category);
    }

    public CommonPromptItem SaveOrUpdate(string? id, string title, string text, string? category = null)
    {
        var normalizedText = text.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new InvalidOperationException("请输入常用提示词内容。");
        }

        var normalizedTitle = string.IsNullOrWhiteSpace(title) ? BuildTitle(normalizedText) : title.Trim();
        var items = Load().ToList();
        var now = DateTimeOffset.UtcNow;
        var existing = !string.IsNullOrWhiteSpace(id)
            ? items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            : items.FirstOrDefault(item => string.Equals(item.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            items.Remove(existing);
            existing = existing with
            {
                Title = normalizedTitle,
                Text = normalizedText,
                Category = NormalizeCategory(category, existing.Category),
                UpdatedAt = now
            };
            items.Insert(0, existing);
            SaveRecords(items);
            _database.ReplaceCommonPromptIndex(items);
            return existing;
        }

        var item = new CommonPromptItem(Guid.NewGuid().ToString("N"), normalizedTitle, normalizedText, now, now, NormalizeCategory(category, "未分类"));
        items.Insert(0, item);
        var savedItems = items.Take(200).ToArray();
        SaveRecords(savedItems);
        _database.ReplaceCommonPromptIndex(savedItems);
        return item;
    }

    public bool Delete(string id)
    {
        var items = Load().ToList();
        var removed = items.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveRecords(items);
            _database.ReplaceCommonPromptIndex(items);
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

            var normalizedTitle = string.IsNullOrWhiteSpace(item.Title) ? BuildTitle(normalizedText) : item.Title.Trim();
            var existing = items.FirstOrDefault(current => string.Equals(current.Title, normalizedTitle, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                items.Remove(existing);
            }

            items.Insert(0, new CommonPromptItem(
                Guid.NewGuid().ToString("N"),
                normalizedTitle,
                normalizedText,
                item.CreatedAt == default ? now : item.CreatedAt,
                now,
                NormalizeCategory(item.Category, "用户导入")));
            changed++;
        }

        var savedItems = items.Take(500).ToArray();
        SaveRecords(savedItems);
        _database.ReplaceCommonPromptIndex(savedItems);
        return changed;
    }

    public void ExportToFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SaveStore(path, Load());
    }

    private static CommonPromptItem[] BuildDefaultItems()
    {
        var now = DateTimeOffset.UtcNow;
        return
        [
            new("builtin-role", "角色定位与任务目标", "角色定位：你是一个专业提示词优化器，需要保留用户原意并提升可执行性。", now, now, "提示词工程"),
            new("builtin-format", "输出格式与验收标准", "输出格式：目标、背景、约束、步骤、交付物、验收标准。", now, now, "提示词工程"),
            new("builtin-privacy", "隐私和事实约束", "约束：不要编造事实，不要泄露隐私，不要输出无关寒暄。", now, now, "约束"),
            new("builtin-video", "视频镜头序列", "请按时间顺序拆成 3-5 个镜头，包含每个镜头的时长、主体、动作、景别和转场。", now, now, "视频")
        ];
    }

    private static void SaveStore(string path, IReadOnlyList<CommonPromptItem> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new CommonPromptStore("prompt_input_method.common_prompts.v1", items), JsonOptions));
    }

    private static IReadOnlyList<CommonPromptItem> LoadItemsFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<CommonPromptItem>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<CommonPromptStore>(json, JsonOptions);
            var items = store?.Items;
            if (items is null)
            {
                items = JsonSerializer.Deserialize<CommonPromptItem[]>(json, JsonOptions) ?? [];
            }

            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                .ToArray();
        }
        catch
        {
            return Array.Empty<CommonPromptItem>();
        }
    }

    private void SaveRecords(IReadOnlyList<CommonPromptItem> items)
    {
        _database.ReplaceRecords(
            AppDatabaseService.KindCommonPrompt,
            items.Select(item => new AppRecordItem(
                AppDatabaseService.KindCommonPrompt,
                item.Id,
                item.Title,
                item.Category,
                "常用提示词",
                item.Text,
                JsonSerializer.Serialize(item, JsonOptions),
                item.CreatedAt,
                item.UpdatedAt)),
            updateSearchIndex: true);
    }

    private static string BuildTitle(string text)
    {
        var title = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (title.Contains("  ", StringComparison.Ordinal))
        {
            title = title.Replace("  ", " ");
        }

        return title.Length <= 28 ? title : $"{title[..28]}...";
    }

    private static string NormalizeCategory(string? category, string fallback)
    {
        return string.IsNullOrWhiteSpace(category) ? fallback : category.Trim();
    }

}

public sealed record CommonPromptItem(
    string Id,
    string Title,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Category = "未分类");

internal sealed record CommonPromptStore(string Schema, IReadOnlyList<CommonPromptItem> Items);
