using System.Text.RegularExpressions;
using PromptInputMethod.Core.Prompt;

namespace PromptInputMethod.Core.Ocr;

public sealed class OcrContextBuilder
{
    private const int MaxContextCharacters = 12000;
    private const int HeadCharacters = 9000;
    private const int TailCharacters = 2000;

    private readonly OcrTextNormalizer _normalizer;

    public OcrContextBuilder(OcrTextNormalizer? normalizer = null)
    {
        _normalizer = normalizer ?? new OcrTextNormalizer();
    }

    public OcrContextBuildResult Build(OcrResult result, SceneDetectionResult scene)
    {
        var normalizedText = _normalizer.Normalize(result).Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return new OcrContextBuildResult(string.Empty, OcrContextField.BackgroundContext, GetFieldName(OcrContextField.BackgroundContext), false, 0, 0);
        }

        var field = Classify(normalizedText, scene.Scene);
        var originalCharacterCount = normalizedText.Length;
        var lineCount = SplitNonEmptyLines(normalizedText).Count;
        var isTruncated = originalCharacterCount > MaxContextCharacters;
        var contextText = isTruncated ? TruncateForPrompt(normalizedText, originalCharacterCount) : normalizedText;

        return new OcrContextBuildResult(contextText, field, GetFieldName(field), isTruncated, originalCharacterCount, lineCount);
    }

    private static OcrContextField Classify(string text, PromptScene scene)
    {
        if (LooksLikeError(text))
        {
            return OcrContextField.ErrorInformation;
        }

        return scene switch
        {
            PromptScene.SoftwareOperation => OcrContextField.UserInterfaceText,
            PromptScene.BrowserReading or PromptScene.Document => OcrContextField.ReferenceMaterial,
            PromptScene.Code => OcrContextField.BackgroundContext,
            _ => LooksLikeUserInterfaceText(text) ? OcrContextField.UserInterfaceText : GuessGenericField(text)
        };
    }

    private static OcrContextField GuessGenericField(string text)
    {
        return LooksLikeLongReadingText(text)
            ? OcrContextField.ReferenceMaterial
            : OcrContextField.BackgroundContext;
    }

    private static bool LooksLikeError(string text)
    {
        if (Regex.IsMatch(text, @"\b(\w+Exception|Traceback|Stack trace|fatal error|panic:|NullReference|InvalidOperation)\b|错误[:：]|异常[:：]", RegexOptions.IgnoreCase))
        {
            return true;
        }

        var keywordHits = Regex.Matches(text, @"\b(error|failed|failure|warning|denied|not found|invalid|timeout)\b|失败|警告|无法|崩溃", RegexOptions.IgnoreCase).Count;
        var stackHits = SplitNonEmptyLines(text)
            .Take(80)
            .Count(line => Regex.IsMatch(line, @"^\s*at\s+\S+\(|^\s*File \"".+\"", line \d+|:\d+:\d+", RegexOptions.IgnoreCase));

        return keywordHits >= 2 || stackHits >= 2;
    }

    private static bool LooksLikeUserInterfaceText(string text)
    {
        var lines = SplitNonEmptyLines(text).Take(80).ToArray();
        if (lines.Length == 0 || lines.Length > 60)
        {
            return false;
        }

        var shortLines = lines.Count(line => line.Length <= 32);
        var actionLines = lines.Count(line => Regex.IsMatch(line, @"^(确定|取消|保存|关闭|设置|登录|注册|下一步|上一步|完成|重试|忽略|打开|浏览|搜索|OK|Cancel|Save|Close|Settings|Next|Back|Finish|Retry|Ignore|Open|Search)\b", RegexOptions.IgnoreCase));

        return actionLines >= 2 || shortLines >= lines.Length * 0.72;
    }

    private static bool LooksLikeLongReadingText(string text)
    {
        var lines = SplitNonEmptyLines(text);
        if (text.Length < 600 || lines.Count == 0)
        {
            return false;
        }

        var averageLineLength = lines.Average(line => line.Length);
        return averageLineLength >= 35;
    }

    private static string TruncateForPrompt(string text, int originalCharacterCount)
    {
        var head = text[..HeadCharacters].TrimEnd();
        var tail = text[^TailCharacters..].TrimStart();
        var omittedCharacters = originalCharacterCount - HeadCharacters - TailCharacters;

        return $"""
{head}

[OCR 文本过长，中间约 {omittedCharacters} 字已省略。请基于保留的开头和结尾判断是否需要用户补充完整内容。]

{tail}
""".Trim();
    }

    private static string GetFieldName(OcrContextField field)
    {
        return field switch
        {
            OcrContextField.ErrorInformation => "错误信息",
            OcrContextField.UserInterfaceText => "界面文字",
            OcrContextField.ReferenceMaterial => "参考材料",
            _ => "背景上下文"
        };
    }

    private static List<string> SplitNonEmptyLines(string text)
    {
        return text
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}

public enum OcrContextField
{
    BackgroundContext,
    ErrorInformation,
    UserInterfaceText,
    ReferenceMaterial
}

public sealed record OcrContextBuildResult(
    string Text,
    OcrContextField Field,
    string FieldName,
    bool IsTruncated,
    int OriginalCharacterCount,
    int LineCount);
