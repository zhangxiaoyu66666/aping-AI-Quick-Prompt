namespace PromptInputMethod.Core.Ocr;

public sealed record OcrResult(string Text, IReadOnlyList<OcrLineResult> Lines, double? Confidence = null);
