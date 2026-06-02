namespace PromptInputMethod.Core.Prompt;

public sealed record SceneDetectionResult(PromptScene Scene, string DisplayName, double Confidence, string Reason);
