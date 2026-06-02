using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class ModelSendAuditService
{
    private readonly AppDatabaseService _database = new();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public IReadOnlyList<ModelSendAuditItem> Load()
    {
        var databaseItems = _database.LoadRecords<ModelSendAuditItem>(AppDatabaseService.KindModelSendAudit, 120);
        if (databaseItems.Count > 0)
        {
            return databaseItems
                .OrderByDescending(item => item.CreatedAt)
                .ToArray();
        }

        return Array.Empty<ModelSendAuditItem>();
    }

    public ModelSendAuditItem Save(
        string provider,
        string model,
        string baseUrl,
        string textSent,
        bool redacted,
        IReadOnlyList<string> imageFileNames,
        bool succeeded,
        string? error)
    {
        var normalizedText = textSent.Trim();
        var item = new ModelSendAuditItem(
            Guid.NewGuid().ToString("N"),
            string.IsNullOrWhiteSpace(provider) ? "OpenAI-compatible" : provider.Trim(),
            model.Trim(),
            baseUrl.Trim(),
            normalizedText,
            BuildPreview(normalizedText),
            redacted,
            imageFileNames.ToArray(),
            imageFileNames.Count,
            succeeded,
            error?.Trim() ?? string.Empty,
            DateTimeOffset.UtcNow);

        SaveRecords(Load().Prepend(item).Take(120).ToArray());
        return item;
    }

    public int Clear()
    {
        return _database.ClearRecords(AppDatabaseService.KindModelSendAudit, updateSearchIndex: false);
    }

    private void SaveRecords(IReadOnlyList<ModelSendAuditItem> items)
    {
        _database.ReplaceRecords(
            AppDatabaseService.KindModelSendAudit,
            items.Select(item => new AppRecordItem(
                AppDatabaseService.KindModelSendAudit,
                item.Id,
                item.Preview,
                item.Succeeded ? "成功" : "失败",
                $"{item.Provider} {item.Model}",
                item.TextSent,
                JsonSerializer.Serialize(item, JsonOptions),
                item.CreatedAt,
                item.CreatedAt)),
            updateSearchIndex: false);
    }

    private static string BuildPreview(string text)
    {
        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized.Length <= 120 ? normalized : $"{normalized[..120]}...";
    }

}

public sealed record ModelSendAuditItem(
    string Id,
    string Provider,
    string Model,
    string BaseUrl,
    string TextSent,
    string Preview,
    bool Redacted,
    IReadOnlyList<string> ImageFileNames,
    int ImageCount,
    bool Succeeded,
    string Error,
    DateTimeOffset CreatedAt)
{
    public override string ToString()
    {
        var status = Succeeded ? "成功" : "失败";
        return $"{CreatedAt:MM-dd HH:mm} · {status} · {Provider} {Model}";
    }
}
