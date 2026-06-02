namespace PromptInputMethod.Core.Prompt;

public sealed record PromptStructuringRequest(
    string UserRequest,
    WindowContext WindowContext,
    SceneDetectionResult Scene,
    string? ContextText = null,
    string? ContextSource = null,
    string? ContextFieldName = null);
