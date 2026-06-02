using System.Text.RegularExpressions;

namespace PromptInputMethod.App.Services;

public sealed class SensitiveTextRedactor
{
    private static readonly Regex ApiKeyRegex = new(@"(?i)\b(api[_-]?key|secret[_-]?key|access[_-]?key)\b\s*[:=]\s*['"" ]?([A-Za-z0-9_\-\.]{12,})", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"(?i)\b(token|bearer|authorization|session[_-]?id|refresh[_-]?token)\b\s*[:=]\s*['"" ]?([A-Za-z0-9_\-\.=]{12,})", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PhoneRegex = new(@"(?<!\d)(?:\+?86[- ]?)?1[3-9]\d{9}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex ChinaIdRegex = new(@"(?<!\d)\d{6}(?:18|19|20)\d{2}(?:0[1-9]|1[0-2])(?:0[1-9]|[12]\d|3[01])\d{3}[0-9Xx](?!\d)", RegexOptions.Compiled);
    private static readonly Regex BankCardRegex = new(@"(?<!\d)(?:\d[ -]?){15,19}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex SensitiveUrlQueryRegex = new(@"(?i)([?&](?:key|api_key|apikey|password|passwd|pwd|token|access_token|refresh_token|session|sessionid|session_id|code)=)[^&#\s]+", RegexOptions.Compiled);

    public string Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var redacted = ApiKeyRegex.Replace(text, match => $"{match.Groups[1].Value}=[REDACTED_API_KEY]");
        redacted = TokenRegex.Replace(redacted, match => $"{match.Groups[1].Value}=[REDACTED_TOKEN]");
        redacted = EmailRegex.Replace(redacted, "[REDACTED_EMAIL]");
        redacted = PhoneRegex.Replace(redacted, "[REDACTED_PHONE]");
        redacted = ChinaIdRegex.Replace(redacted, "[REDACTED_ID]");
        redacted = SensitiveUrlQueryRegex.Replace(redacted, match => $"{match.Groups[1].Value}[REDACTED_URL_PARAM]");
        redacted = BankCardRegex.Replace(redacted, match => LooksLikeBankCard(match.Value) ? "[REDACTED_BANK_CARD]" : match.Value);
        return redacted;
    }

    private static bool LooksLikeBankCard(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is >= 16 and <= 19 && PassesLuhn(digits);
    }

    private static bool PassesLuhn(string digits)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
