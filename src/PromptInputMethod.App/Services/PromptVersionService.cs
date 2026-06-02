using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class PromptVersionService
{
    private readonly AppDatabaseService _database = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<PromptVersionItem> Load()
    {
        var databaseItems = _database.LoadRecords<PromptVersionItem>(AppDatabaseService.KindPromptVersion, 300);
        if (databaseItems.Count > 0)
        {
            return databaseItems
                .Where(item => !string.IsNullOrWhiteSpace(item.ChinesePrompt) || !string.IsNullOrWhiteSpace(item.UserRequest))
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
        }

        return Array.Empty<PromptVersionItem>();
    }

    public PromptVersionItem Save(string userRequest, string? previousPrompt, string chinesePrompt, string englishPrompt, string scene, string mode)
    {
        if (string.IsNullOrWhiteSpace(userRequest) && string.IsNullOrWhiteSpace(chinesePrompt))
        {
            throw new InvalidOperationException("没有可保存的版本。");
        }

        var item = new PromptVersionItem(
            Guid.NewGuid().ToString("N"),
            BuildTitle(userRequest, chinesePrompt),
            userRequest.Trim(),
            previousPrompt?.Trim() ?? string.Empty,
            chinesePrompt.Trim(),
            englishPrompt.Trim(),
            scene,
            mode,
            DateTimeOffset.UtcNow);

        SaveRecords(Load().Prepend(item).Take(300).ToArray());
        return item;
    }

    public bool Delete(string id)
    {
        var items = Load().ToList();
        var removed = items.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            SaveRecords(items);
        }

        return removed > 0;
    }

    private void SaveRecords(IReadOnlyList<PromptVersionItem> items)
    {
        _database.ReplaceRecords(
            AppDatabaseService.KindPromptVersion,
            items.Select(item => new AppRecordItem(
                AppDatabaseService.KindPromptVersion,
                item.Id,
                item.Title,
                item.Scene,
                item.Mode,
                string.Join(Environment.NewLine, item.UserRequest, item.PreviousPrompt, item.ChinesePrompt, item.EnglishPrompt),
                JsonSerializer.Serialize(item, JsonOptions),
                item.CreatedAt,
                item.CreatedAt)),
            updateSearchIndex: false);
    }

    private static string BuildTitle(string userRequest, string chinesePrompt)
    {
        var source = string.IsNullOrWhiteSpace(userRequest) ? chinesePrompt : userRequest;
        var title = source.Replace("\r", " ").Replace("\n", " ").Trim();
        while (title.Contains("  ", StringComparison.Ordinal))
        {
            title = title.Replace("  ", " ");
        }

        return title.Length <= 42 ? title : $"{title[..42]}...";
    }

}

public sealed record PromptVersionItem(
    string Id,
    string Title,
    string UserRequest,
    string PreviousPrompt,
    string ChinesePrompt,
    string EnglishPrompt,
    string Scene,
    string Mode,
    DateTimeOffset CreatedAt)
{
    public override string ToString()
    {
        return Title;
    }
}
