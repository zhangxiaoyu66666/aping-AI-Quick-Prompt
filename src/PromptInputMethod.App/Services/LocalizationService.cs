using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Globalization;

namespace PromptInputMethod.App.Services;

public sealed class LocalizationService
{
    private const string AutoLanguageCode = "auto";
    private const string BuiltInLocalizationPriFileName = "localization.pri";
    private const string ResourceSubtree = "Resources";
    private readonly Lazy<IReadOnlyDictionary<string, string>> _sourceTextByLocalizedText = new(LoadBuiltInSourceTextMap);

    public LocalizationPack Load(string? languageCode, string? mountedLanguagePackPath)
    {
        var resolvedCode = ResolveLanguageCode(languageCode);
        ApplyRuntimeLanguageOverride(resolvedCode);
        var mountedSource = LoadMountedSource(mountedLanguagePackPath, resolvedCode);
        var builtInReswSource = LoadBuiltInReswSource(resolvedCode);
        var appSource = MrtLocalizationSource.CreateForApp(resolvedCode);
        var displayName = mountedSource?.DisplayName ?? GetDisplayName(resolvedCode);
        var sourcePath = mountedSource?.SourcePath ?? string.Empty;
        return new LocalizationPack(resolvedCode, displayName, sourcePath, mountedSource, builtInReswSource, appSource);
    }

    public LocalizationPack? Mount(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var source = LoadMountedSource(sourcePath, fallbackLanguageCode: null);
        if (source is null)
        {
            return null;
        }

        var destination = CopyLanguagePack(sourcePath, source.Code);
        return Load(source.Code, destination);
    }

    public IReadOnlyList<LocalizationChoice> GetBuiltInChoices()
    {
        return
        [
            new(AutoLanguageCode, "自动 / Auto"),
            new("zh-CN", "中文"),
            new("en-US", "English")
        ];
    }

    public string ResolveLanguageCode(string? languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode) && !string.Equals(languageCode, AutoLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return languageCode.Trim();
        }

        var cultureSignals = new[]
        {
            CultureInfo.CurrentUICulture,
            CultureInfo.CurrentCulture,
            CultureInfo.InstalledUICulture
        };
        if (cultureSignals.Any(culture => culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)))
        {
            return "zh-CN";
        }

        try
        {
            var region = RegionInfo.CurrentRegion.TwoLetterISORegionName;
            if (region is "CN" or "HK" or "MO" or "TW" or "SG")
            {
                return "zh-CN";
            }
        }
        catch
        {
            // Some Windows culture configurations do not expose a concrete region.
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";
    }

    public static string GetResourceId(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"S_{Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    public string GetSourceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return _sourceTextByLocalizedText.Value.TryGetValue(text, out var sourceText)
            ? sourceText
            : text;
    }

    private static ILocalizationSource? LoadMountedSource(string? path, string? fallbackLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".resw" => LoadReswSource(path, fallbackLanguageCode),
            ".pri" => MrtLocalizationSource.CreateForPri(path, fallbackLanguageCode),
            _ => null
        };
    }

    private static ILocalizationSource? LoadBuiltInReswSource(string languageCode)
    {
        foreach (var path in EnumerateBuiltInReswFiles())
        {
            var directoryName = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
            if (string.Equals(directoryName, languageCode, StringComparison.OrdinalIgnoreCase))
            {
                return LoadReswSource(path, languageCode);
            }
        }

        return null;
    }

    private static void ApplyRuntimeLanguageOverride(string languageCode)
    {
        if (!TryNormalizeCulture(languageCode, out var normalized))
        {
            return;
        }

        try
        {
            ApplicationLanguages.PrimaryLanguageOverride = normalized;
        }
        catch
        {
            // Dictionary-backed localization below still supports live language switching.
        }
    }

    private static DictionaryLocalizationSource? LoadReswSource(string path, string? fallbackLanguageCode)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var values = document
                .Descendants("data")
                .Select(data => new
                {
                    Name = data.Attribute("name")?.Value,
                    Value = data.Element("value")?.Value,
                    Comment = data.Element("comment")?.Value
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name) && item.Value is not null)
                .ToDictionary(item => item.Name!, item => item.Value!, StringComparer.OrdinalIgnoreCase);
            foreach (var item in document.Descendants("data")
                .Select(data => new
                {
                    Value = data.Element("value")?.Value,
                    Comment = data.Element("comment")?.Value
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Comment) && item.Value is not null))
            {
                values.TryAdd(item.Comment!, item.Value!);
            }

            if (values.Count == 0)
            {
                return null;
            }

            var code = DetectLanguageCodeFromPath(path, fallbackLanguageCode);
            return new DictionaryLocalizationSource(code, GetDisplayName(code), path, values);
        }
        catch
        {
            return null;
        }
    }

    private static string CopyLanguagePack(string sourcePath, string languageCode)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PromptInputMethod", "LanguagePacks");
        var directory = Path.Combine(root, SanitizeFileName(languageCode));
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var fileName = extension switch
        {
            ".resw" => "Resources.resw",
            ".pri" => "resources.pri",
            _ => Path.GetFileName(sourcePath)
        };
        var destination = Path.Combine(directory, fileName);
        File.Copy(sourcePath, destination, overwrite: true);
        return destination;
    }

    private static string DetectLanguageCodeFromPath(string path, string? fallbackLanguageCode)
    {
        var candidates = new[]
        {
            Path.GetFileNameWithoutExtension(path),
            Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty),
            fallbackLanguageCode,
            "en-US"
        };

        foreach (var candidate in candidates)
        {
            if (TryNormalizeCulture(candidate, out var normalized))
            {
                return normalized;
            }
        }

        return string.IsNullOrWhiteSpace(fallbackLanguageCode) ? "custom" : fallbackLanguageCode;
    }

    private static bool TryNormalizeCulture(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            normalized = CultureInfo.GetCultureInfo(value.Trim()).Name;
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch
        {
            return false;
        }
    }

    private static string GetDisplayName(string languageCode)
    {
        if (languageCode.Equals("zh-CN", StringComparison.OrdinalIgnoreCase))
        {
            return "中文";
        }

        if (languageCode.Equals("en-US", StringComparison.OrdinalIgnoreCase))
        {
            return "English";
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(languageCode);
            return string.IsNullOrWhiteSpace(culture.NativeName) ? languageCode : culture.NativeName;
        }
        catch
        {
            return languageCode;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "custom-language" : sanitized;
    }

    private static IReadOnlyDictionary<string, string> LoadBuiltInSourceTextMap()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in EnumerateBuiltInReswFiles())
        {
            try
            {
                var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
                foreach (var data in document.Descendants("data"))
                {
                    var value = data.Element("value")?.Value;
                    var source = data.Element("comment")?.Value;
                    if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }

                    if (ContainsCjk(source))
                    {
                        result.TryAdd(source, source);
                        result.TryAdd(value, source);
                    }
                }
            }
            catch
            {
                // Reverse lookup is a best-effort aid for live language switching.
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateBuiltInReswFiles()
    {
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Strings"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "Strings")
        };

        foreach (var root in roots)
        {
            var fullRoot = Path.GetFullPath(root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(fullRoot, "Resources.resw", SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }

    private static bool ContainsCjk(string value)
    {
        return value.Any(ch => ch is >= '\u4e00' and <= '\u9fff');
    }

    public interface ILocalizationSource
    {
        string Code { get; }

        string DisplayName { get; }

        string SourcePath { get; }

        string? Translate(string text);
    }

    private sealed class DictionaryLocalizationSource(
        string code,
        string displayName,
        string sourcePath,
        IReadOnlyDictionary<string, string> strings) : ILocalizationSource
    {
        public string Code { get; } = code;

        public string DisplayName { get; } = displayName;

        public string SourcePath { get; } = sourcePath;

        public string? Translate(string text)
        {
            if (strings.TryGetValue(text, out var direct) && !string.IsNullOrEmpty(direct))
            {
                return direct;
            }

            var resourceId = GetResourceId(text);
            return strings.TryGetValue(resourceId, out var byResourceId) && !string.IsNullOrEmpty(byResourceId)
                ? byResourceId
                : null;
        }
    }

    private sealed class MrtLocalizationSource : ILocalizationSource
    {
        private readonly ResourceManager _resourceManager;
        private readonly ResourceMap _resourceMap;
        private readonly ResourceContext _resourceContext;

        private MrtLocalizationSource(string code, string displayName, string sourcePath, ResourceManager resourceManager)
        {
            Code = code;
            DisplayName = displayName;
            SourcePath = sourcePath;
            _resourceManager = resourceManager;
            _resourceMap = _resourceManager.MainResourceMap.GetSubtree(ResourceSubtree);
            _resourceContext = _resourceManager.CreateResourceContext();
            _resourceContext.QualifierValues["Language"] = code;
        }

        public string Code { get; }

        public string DisplayName { get; }

        public string SourcePath { get; }

        public static MrtLocalizationSource? CreateForApp(string languageCode)
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, BuiltInLocalizationPriFileName);
                return File.Exists(path)
                    ? new MrtLocalizationSource(languageCode, GetDisplayName(languageCode), path, new ResourceManager(path))
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public static MrtLocalizationSource? CreateForPri(string path, string? fallbackLanguageCode)
        {
            try
            {
                var code = DetectLanguageCodeFromPath(path, fallbackLanguageCode);
                return new MrtLocalizationSource(code, GetDisplayName(code), path, new ResourceManager(path));
            }
            catch
            {
                return null;
            }
        }

        public string? Translate(string text)
        {
            var resourceId = GetResourceId(text);
            try
            {
                var candidate = _resourceMap.GetValue(resourceId, _resourceContext);
                return string.IsNullOrEmpty(candidate.ValueAsString) ? null : candidate.ValueAsString;
            }
            catch
            {
                return null;
            }
        }
    }
}

public sealed record LocalizationChoice(string Code, string DisplayName);

public sealed class LocalizationPack(
    string code,
    string displayName,
    string sourcePath,
    LocalizationService.ILocalizationSource? mountedSource,
    LocalizationService.ILocalizationSource? builtInReswSource,
    LocalizationService.ILocalizationSource? appSource)
{
    private readonly LocalizationService.ILocalizationSource? _mountedSource = mountedSource;
    private readonly LocalizationService.ILocalizationSource? _builtInReswSource = builtInReswSource;
    private readonly LocalizationService.ILocalizationSource? _appSource = appSource;

    public string Code { get; } = code;

    public string DisplayName { get; } = displayName;

    public string SourcePath { get; } = sourcePath;

    public static LocalizationPack English { get; } = new("en-US", "English", string.Empty, null, null, null);

    public string Translate(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string? translated = _mountedSource?.Translate(text);
        translated ??= _builtInReswSource?.Translate(text);
        translated ??= _appSource?.Translate(text);
        return string.IsNullOrEmpty(translated) ? text : translated;
    }
}
