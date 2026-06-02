namespace PromptInputMethod.Core.Ocr;

public sealed record OcrLineResult(string Text, IReadOnlyList<OcrWordResult> Words, OcrBoundingBox? BoundingBox = null);
