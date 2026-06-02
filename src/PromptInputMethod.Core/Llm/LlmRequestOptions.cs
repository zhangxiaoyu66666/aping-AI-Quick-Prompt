namespace PromptInputMethod.Core.Llm;

public sealed record LlmRequestOptions(
    bool Enabled,
    string ProviderId,
    string BaseUrl,
    string Model,
    string? ApiKey,
    int TimeoutSeconds)
{
    public bool IsConfigured => Enabled
        && !string.IsNullOrWhiteSpace(BaseUrl)
        && !string.IsNullOrWhiteSpace(Model)
        && !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed record LlmRequest(string Prompt, IReadOnlyList<LlmImageAttachment> Images, LlmReasoningOptions? Reasoning = null)
{
    public static LlmRequest TextOnly(string prompt) => new(prompt, Array.Empty<LlmImageAttachment>());
}

public sealed record LlmImageAttachment(string DataUrl, string FileName, string MimeType);

public sealed record LlmReasoningOptions(
    string ProviderId,
    string RequestKind,
    string Effort = "high",
    bool IncludeThoughts = true);

public sealed record LlmCompletionResult(string Content, string? ReasoningContent);

public sealed record LlmModelInfo(string Id, string? OwnedBy);
