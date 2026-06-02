namespace PromptInputMethod.App.Services;

public sealed class PromptHistoryService
{
    private readonly AppDatabaseService _database = new();

    public IReadOnlyList<PromptHistoryItem> Load()
    {
        return _database.LoadHistory();
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

        var item = new PromptHistoryItem(
            string.IsNullOrWhiteSpace(existingId) ? Guid.NewGuid().ToString("N") : existingId.Trim(),
            BuildTitle(userRequest, chinesePrompt),
            userRequest.Trim(),
            chinesePrompt.Trim(),
            englishPrompt.Trim(),
            scene,
            mode,
            DateTimeOffset.UtcNow,
            NormalizeMessages(messages));

        _database.SaveHistory(item);
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
        return removed;
    }

    public int Clear()
    {
        return _database.ClearHistory();
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
    IReadOnlyList<PromptConversationMessage>? Messages = null)
{
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
