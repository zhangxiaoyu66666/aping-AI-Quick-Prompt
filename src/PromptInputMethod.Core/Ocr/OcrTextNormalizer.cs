using System.Text;
using System.Text.RegularExpressions;

namespace PromptInputMethod.Core.Ocr;

public sealed class OcrTextNormalizer
{
    public string Normalize(OcrResult result)
    {
        var rawLines = result.Lines.Count > 0
            ? result.Lines.Select(line => line.Text)
            : SplitLines(result.Text);
        var lines = rawLines
            .Select(NormalizeLine)
            .Where(IsUsefulLine)
            .ToList();

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        lines = RemoveDuplicates(lines);
        return MergeLines(lines).Trim();
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return (text ?? string.Empty).Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
    }

    private static string NormalizeLine(string line)
    {
        var normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s+([,.;:!?，。；：！？、])", "$1");
        normalized = Regex.Replace(normalized, @"([，。；：！？、])\s+", "$1");
        normalized = Regex.Replace(normalized, @"([（\(\[【])\s+", "$1");
        normalized = Regex.Replace(normalized, @"\s+([）\)\]】])", "$1");
        normalized = Regex.Replace(normalized, @"(?<=\p{IsCJKUnifiedIdeographs})\s+(?=\p{IsCJKUnifiedIdeographs})", string.Empty);
        return normalized;
    }

    private static bool IsUsefulLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"^[-_=*·•]{3,}$"))
        {
            return false;
        }

        var lettersOrDigits = Regex.Replace(line, @"[^\p{L}\p{N}\p{IsCJKUnifiedIdeographs}]", string.Empty);
        return lettersOrDigits.Length > 0 || line.Length > 16;
    }

    private static List<string> RemoveDuplicates(IReadOnlyList<string> lines)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (result.Count > 0 && string.Equals(result[^1], line, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = Regex.Replace(line, @"\s+", string.Empty);
            if (key.Length >= 4 && !seen.Add(key))
            {
                continue;
            }

            result.Add(line);
        }

        return result;
    }

    private static string MergeLines(IReadOnlyList<string> lines)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            var current = lines[i];
            var next = i + 1 < lines.Count ? lines[i + 1] : null;

            builder.Append(current);
            if (next is null)
            {
                break;
            }

            builder.Append(ShouldMergeWithNext(current, next) ? ' ' : '\n');
        }

        return builder.ToString();
    }

    private static bool ShouldMergeWithNext(string current, string next)
    {
        if (IsStructureLine(current) || IsStructureLine(next))
        {
            return false;
        }

        if (EndsSentence(current))
        {
            return false;
        }

        return LooksLikeParagraphText(current) && LooksLikeParagraphText(next);
    }

    private static bool IsStructureLine(string line)
    {
        return Regex.IsMatch(line, @"^\s*([-*•]|\d+[\.)]|[A-Za-z]:\\|at\s+\S+|[\w.]+Exception\b|error\b|warning\b|错误|异常|警告)", RegexOptions.IgnoreCase)
            || line.Contains("{")
            || line.Contains("}")
            || line.Contains("=>")
            || line.Contains("==")
            || line.Contains("::")
            || line.Count(ch => ch == '|') >= 2;
    }

    private static bool EndsSentence(string line)
    {
        return Regex.IsMatch(line, @"[。.!?！？；;：:]$|```$");
    }

    private static bool LooksLikeParagraphText(string line)
    {
        return line.Length >= 8 && Regex.IsMatch(line, @"[\p{L}\p{IsCJKUnifiedIdeographs}]");
    }
}
