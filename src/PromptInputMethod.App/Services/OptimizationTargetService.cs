using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class OptimizationTargetService
{
    public const string Schema = "aipin.optimization_target.v1";
    private readonly AppDatabaseService _database = new();
    private readonly object _cacheGate = new();
    private IReadOnlyList<OptimizationTargetItem>? _cachedItems;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public IReadOnlyList<OptimizationTargetItem> Load()
    {
        lock (_cacheGate)
        {
            if (_cachedItems is not null)
            {
                return _cachedItems;
            }
        }

        var databaseItems = _database.LoadRecords<OptimizationTargetItem>(AppDatabaseService.KindOptimizationTarget, 200);
        if (databaseItems.Count > 0)
        {
            var orderedItems = databaseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                    && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
            SetCachedItems(orderedItems);
            return orderedItems;
        }

        var empty = Array.Empty<OptimizationTargetItem>();
        SetCachedItems(empty);
        return empty;
    }

    public IReadOnlyList<OptimizationTargetItem> ImportFromFile(string path)
    {
        var imported = LoadItemsFromFile(path);
        if (imported.Count == 0)
        {
            return [];
        }

        var items = Load().ToList();
        var now = DateTimeOffset.UtcNow;
        var changed = new List<OptimizationTargetItem>();
        foreach (var rawItem in imported)
        {
            var item = NormalizeItem(rawItem, now);
            if (string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
            {
                continue;
            }

            var existing = items.FirstOrDefault(current =>
                string.Equals(current.Id, item.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.Title, item.Title, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                items.Remove(existing);
            }

            items.Insert(0, item);
            changed.Add(item);
        }

        SaveRecords(items.Take(100).ToArray());
        SetCachedItems(items.Take(100).ToArray());
        return changed;
    }

    public void ExportToFile(string path, OptimizationTargetItem item)
    {
        var normalized = NormalizeItem(item, DateTimeOffset.UtcNow);
        var store = new OptimizationTargetStore(Schema, [normalized]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(store, JsonOptions));
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var removed = _database.DeleteRecord(AppDatabaseService.KindOptimizationTarget, id, updateSearchIndex: true);
        if (!removed)
        {
            var items = Load().ToList();
            removed = items.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                SaveRecords(items);
                SetCachedItems(items);
            }
        }
        else
        {
            InvalidateCache();
        }

        return removed;
    }

    public static OptimizationTargetItem BuildAcademicHumanizeTarget()
    {
        var now = DateTimeOffset.UtcNow;
        return new OptimizationTargetItem
        {
            Id = "builtin-academic-humanize-cn",
            Title = "论文去AI味",
            Description = "把文本改写成自然、克制、有学术感的中文论文段落，降低模板腔和机械排比。",
            Category = "论文写作",
            TemplateSource = "ChatGPT-Shortcut",
            Compatibility = "ChatGPT / Claude / Gemini / DeepSeek / Kimi / 本地模型",
            Keywords = ["/论文人话", "/去AI味论文", "/论文自然改写", "论文去AI味"],
            LocalPromptTemplate = """
            你是一名中文论文写作润色助手。你的任务是把我给出的内容改写成“像真实学生或研究者写出来的论文段落”，而不是 AI 生成的模板文。

            请注意：我要的是自然、清楚、有学术感，不是口水话，也不是营销文，更不是机械排比句。

            写作要求：
            1. 不要使用明显 AI 腔。禁止频繁出现：“首先、其次、再次、最后、综上所述、总而言之、由此可见、不可否认的是、值得注意的是、在当今社会、随着时代的发展、在新时代背景下、具有重要意义、具有深远影响、提供了新思路、提供了新路径、注入新动能、赋能、助力、推动高质量发展”。
            2. 不要写成机械列表。除非我明确要求列点，否则请用自然段落推进逻辑。
            3. 不要堆空话。每句话都要有信息量；如果写“重要”或“有影响”，必须说明具体原因和影响位置。
            4. 保留论文感，但降低八股味。语气要准确、克制、清楚，可以有自然转折和解释，但不要像聊天、新闻通稿或宣传稿。
            5. 句式要自然。长句和短句混合使用，不要每句话都用同一种结构。
            6. 逻辑要像人写的。每段围绕一个中心意思展开，段落之间要有自然过渡，不要把观点硬拼在一起。
            7. 不要过度拔高。结论要和材料规模匹配，不要把普通现象写成时代命题。
            8. 不要编造信息。原文没有数据、案例、来源或结论时，不要自行补充；可以使用“可能说明”“在一定程度上反映”“从已有材料看”等谨慎表达。
            9. 尽量保留我的原意，不擅自改变观点，不把尖锐判断磨平成空话。
            10. 输出只给最终改写结果。不要解释改了什么，不要写“以下是润色后的版本”，不要加标题，除非我要求。

            额外禁止：
            - 禁止使用“既是……也是……更是……”
            - 禁止使用“它不仅……而且……更……”
            - 禁止连续排比
            - 禁止“第一、第二、第三”式展开
            - 禁止写成申论、公众号、新闻通稿或宣传稿

            需要处理的文本如下：
            {{userRequest}}
            """,
            ModelInstruction = """
            生成一份可复制给目标模型使用的中文论文自然改写提示词。输出只能是最终提示词正文，不要解释、不要标题、不要 Markdown 分隔线、不要 TCREI 结构。必须要求目标模型保留学术表达，但模拟真实学生或研究者的自然写作习惯；包含反 AI 腔禁用词库、禁止机械列表、禁止连续排比、禁止空话套话、禁止过度拔高、禁止编造信息、保留原意、只输出改写正文。
            """,
            EnglishTranslationRule = "Preserve the academic rewriting prompt as an executable instruction. Keep the anti-AI-tone banned phrase list, no-listing rules, no-fabrication rules, and final text-only output rule.",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static IReadOnlyList<OptimizationTargetItem> LoadItemsFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<OptimizationTargetStore>(json, JsonOptions);
            if (store?.Items is { Count: > 0 })
            {
                return NormalizeItems(store.Items);
            }

            var item = JsonSerializer.Deserialize<OptimizationTargetItem>(json, JsonOptions);
            if (item is not null && !string.IsNullOrWhiteSpace(item.Title))
            {
                return NormalizeItems([item]);
            }

            var items = JsonSerializer.Deserialize<OptimizationTargetItem[]>(json, JsonOptions) ?? [];
            return NormalizeItems(items);
        }
        catch
        {
            return Array.Empty<OptimizationTargetItem>();
        }
    }

    private static OptimizationTargetItem[] NormalizeItems(IEnumerable<OptimizationTargetItem> items)
    {
        var now = DateTimeOffset.UtcNow;
        return items
            .Select(item => NormalizeItem(item, now))
            .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArray();
    }

    private static OptimizationTargetItem NormalizeItem(OptimizationTargetItem item, DateTimeOffset now)
    {
        var id = string.IsNullOrWhiteSpace(item.Id)
            ? BuildId(item.Title)
            : item.Id.Trim();

        return item with
        {
            Id = id,
            Title = item.Title.Trim(),
            Description = item.Description?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(item.Category) ? "用户目标" : item.Category.Trim(),
            TemplateSource = string.IsNullOrWhiteSpace(item.TemplateSource) ? "ChatGPT-Shortcut" : item.TemplateSource.Trim(),
            Compatibility = string.IsNullOrWhiteSpace(item.Compatibility) ? "ChatGPT / Claude / Gemini / 本地模型" : item.Compatibility.Trim(),
            Keywords = item.Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            LocalPromptTemplate = item.LocalPromptTemplate.Trim(),
            ModelInstruction = item.ModelInstruction?.Trim() ?? string.Empty,
            EnglishTranslationRule = item.EnglishTranslationRule?.Trim() ?? string.Empty,
            CreatedAt = item.CreatedAt == default ? now : item.CreatedAt,
            UpdatedAt = now
        };
    }

    private static string BuildId(string title)
    {
        var safe = new string((title ?? "target")
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        safe = safe.Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe;
    }

    private void SaveRecords(IReadOnlyList<OptimizationTargetItem> items)
    {
        _database.ReplaceRecords(
            AppDatabaseService.KindOptimizationTarget,
            items.Select(item => new AppRecordItem(
                AppDatabaseService.KindOptimizationTarget,
                item.Id,
                item.Title,
                item.Category,
                item.TemplateSource,
                string.Join(Environment.NewLine, item.Description, item.Compatibility, string.Join(" ", item.Keywords), item.LocalPromptTemplate),
                JsonSerializer.Serialize(item, JsonOptions),
                item.CreatedAt == default ? DateTimeOffset.UtcNow : item.CreatedAt,
                item.UpdatedAt == default ? DateTimeOffset.UtcNow : item.UpdatedAt)),
            updateSearchIndex: true);
    }

    private void SetCachedItems(IReadOnlyList<OptimizationTargetItem> items)
    {
        lock (_cacheGate)
        {
            _cachedItems = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                    && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheGate)
        {
            _cachedItems = null;
        }
    }

}

public sealed record OptimizationTargetItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "用户目标";
    public string TemplateSource { get; init; } = "ChatGPT-Shortcut";
    public string Compatibility { get; init; } = "ChatGPT / Claude / Gemini / 本地模型";
    public string[] Keywords { get; init; } = [];
    public string LocalPromptTemplate { get; init; } = string.Empty;
    public string ModelInstruction { get; init; } = string.Empty;
    public string EnglishTranslationRule { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public override string ToString()
    {
        return Title;
    }
}

internal sealed record OptimizationTargetStore(string Schema, IReadOnlyList<OptimizationTargetItem> Items);
