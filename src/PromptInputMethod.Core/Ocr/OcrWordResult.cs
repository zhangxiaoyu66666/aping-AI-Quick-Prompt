namespace PromptInputMethod.Core.Ocr;

public sealed record OcrWordResult(string Text, OcrBoundingBox BoundingBox, double? Confidence = null);
