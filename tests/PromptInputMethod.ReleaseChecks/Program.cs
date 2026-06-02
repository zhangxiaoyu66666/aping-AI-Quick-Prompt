using System.Text.Json;
using System.Text.Json.Serialization;
using PromptInputMethod.Core.Prompt;

var repoRoot = FindRepositoryRoot();
var checks = new ReleaseCheck[]
{
    new("Prompt routing", CheckPromptRouting),
    new("Template data import/export", () => CheckTemplateData(repoRoot)),
    new("Skill matching inputs", () => CheckSkillMatchingInputs(repoRoot)),
    new("Provider validation", () => CheckProviderValidation(repoRoot)),
    new("Accessibility and layout coverage", () => CheckAccessibilityAndLayoutCoverage(repoRoot)),
    new("Public demo packaging policy", () => CheckPublicDemoPackagingPolicy(repoRoot))
};

var failures = new List<string>();
foreach (var check in checks)
{
    try
    {
        check.Run();
        Console.WriteLine($"[pass] {check.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{check.Name}: {ex.Message}");
        Console.Error.WriteLine($"[fail] {check.Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("Release checks failed:");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine($"- {failure}");
    }

    Environment.Exit(1);
}

Console.WriteLine("All release checks passed.");

static void CheckPromptRouting()
{
    var detector = new SceneDetector();
    var router = new ScenePromptRouter();

    var codeRoute = router.Route(detector.Detect(new WindowContext("devenv", "Unhandled exception in App.xaml.cs", "Window")));
    AssertEqual(PromptScene.Code, codeRoute.Scene, "Visual Studio exception windows should route to coding prompts.");
    AssertContains(codeRoute.ExpectedOutput, "验证方式", "Coding prompts should ask for verification details.");

    var chatRoute = router.Route(detector.Detect(new WindowContext("chrome", "ChatGPT", "Chrome_WidgetWin_1")));
    AssertEqual(PromptScene.ChatModel, chatRoute.Scene, "ChatGPT browser windows should route to chat-model prompts.");

    var unknownRoute = router.Route(new SceneDetectionResult(PromptScene.Code, "代码", 0.1, "low confidence"));
    AssertEqual(PromptScene.Unknown, unknownRoute.Scene, "Low-confidence detections should fall back to the generic route.");
}

static void CheckTemplateData(string repoRoot)
{
    var templatePath = Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Data", "prompts-chat-templates.json");
    AssertFileExists(templatePath);

    var json = File.ReadAllText(templatePath);
    var templates = JsonSerializer.Deserialize<List<TemplateRecord>>(json, JsonOptions())
        ?? throw new InvalidOperationException("prompts.chat template JSON did not deserialize.");

    Assert(templates.Count >= 100, "Generated prompts.chat data should contain the public template dataset, not a tiny placeholder.");
    Assert(templates.Any(item => string.Equals(item.Source, "prompts.chat", StringComparison.OrdinalIgnoreCase)), "prompts.chat source labels must be preserved.");
    Assert(templates.All(item => !string.IsNullOrWhiteSpace(item.Id)
        && !string.IsNullOrWhiteSpace(item.Title)
        && !string.IsNullOrWhiteSpace(item.Source)
        && !string.IsNullOrWhiteSpace(item.Category)
        && !string.IsNullOrWhiteSpace(item.Text)), "Each generated template needs id, title, source, category, and text.");

    var roundTrip = JsonSerializer.Deserialize<List<TemplateRecord>>(
        JsonSerializer.Serialize(templates.Take(12).ToArray(), JsonOptions()),
        JsonOptions());
    AssertEqual(12, roundTrip?.Count ?? 0, "Template export/import roundtrip should preserve record count.");
}

static void CheckSkillMatchingInputs(string repoRoot)
{
    var skillPath = Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Data", "skills", "female-portrait-director", "SKILL.md");
    AssertFileExists(skillPath);

    var skill = File.ReadAllText(skillPath);
    AssertContains(skill, "female-portrait-director", "Bundled Skill must keep its stable source identifier.");
    AssertContains(skill, "reference image", "Bundled Skill must include English workflow terms.");

    var catalogSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "PromptTemplateCatalogService.cs"));
    AssertContains(catalogSource, "女性人像提示词导演 Skill", "Built-in catalog must expose the Skill as a selectable source.");
    AssertContains(catalogSource, "关键词：女性人像", "Built-in Skill catalog entry must provide trigger keywords for matching.");
}

static void CheckProviderValidation(string repoRoot)
{
    var settings = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "appsettings.json"));
    using var settingsJson = JsonDocument.Parse(settings);
    var preferredProvider = settingsJson.RootElement.GetProperty("ocr").GetProperty("preferredProvider").GetString();
    AssertEqual("fire_eye_ocr", preferredProvider, "Fire Eye OCR must remain the default provider.");

    var routerSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OcrProviderRouter.cs"));
    AssertContains(routerSource, "public const string Auto = \"auto_ocr\"", "Auto OCR provider id must stay stable.");
    AssertContains(routerSource, "public const string WindowsMedia = \"windows_media_ocr\"", "Windows Media fallback provider id must stay stable.");
    AssertContains(routerSource, "return string.Equals(normalized, OcrProviderIds.WindowsMedia", "Direct Windows Media selection should normalize back to Fire Eye.");

    var fireEyeSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "FireEyeOcrProvider.cs"));
    AssertContains(fireEyeSource, "Utf8JsonReader", "Fire Eye worker parsing must tolerate trailing native logs after the JSON payload.");

    var ocrReview = File.ReadAllText(Path.Combine(repoRoot, "docs", "ocr-model-license-review.md"));
    AssertContains(ocrReview, "PP-OCRv5_mobile_det", "OCR model review must cover the detection model.");
    AssertContains(ocrReview, "PP-OCRv5_mobile_rec", "OCR model review must cover the recognition model.");
    AssertContains(ocrReview, "Apache-2.0", "OCR model review must record the Apache-2.0 license conclusion.");
}

static void CheckAccessibilityAndLayoutCoverage(string repoRoot)
{
    var appXaml = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml"));
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));
    var trayCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "TrayMenuWindow.xaml.cs"));

    AssertContains(appXaml, "KeyboardAccelerators", "Main window should expose keyboard shortcuts.");
    AssertContains(appXaml, "AccessKey", "Primary controls should expose access keys for keyboard navigation.");
    AssertContains(appXaml, "AutomationProperties.Name", "Important icon-only controls should have automation names.");
    AssertContains(appCode, "SidebarFullCollapseWidth", "Layout code should cover narrow/compact widths.");
    var regionSelectionCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "RegionSelectionWindow.xaml.cs"));
    AssertContains(regionSelectionCode, "PixelWidth / Math.Max(1, RootGrid.ActualWidth)", "Region selection should map UI coordinates back to screenshot pixels for high-DPI displays.");
    AssertContains(trayCode, "DesktopAcrylicBackdrop", "Tray menu should use the Acrylic WinUI surface.");

    var regressionDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "release-1.0-readiness.md"));
    AssertContains(regressionDoc, "expanded", "1.0 readiness notes must name the expanded layout checkpoint.");
    AssertContains(regressionDoc, "compact", "1.0 readiness notes must name the compact layout checkpoint.");
    AssertContains(regressionDoc, "narrow", "1.0 readiness notes must name the narrow layout checkpoint.");
    AssertContains(regressionDoc, "high-DPI", "1.0 readiness notes must name the high-DPI layout checkpoint.");
}

static void CheckPublicDemoPackagingPolicy(string repoRoot)
{
    AssertFileExists(Path.Combine(repoRoot, "scripts", "New-PublicDemoPackage.ps1"));

    var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "New-PublicDemoPackage.ps1"));
    AssertContains(script, "cargo build -p fire-eye-ocr-worker", "Public demo script should build the reviewed native OCR worker by default.");
    AssertContains(script, "docs\\ocr-model-license-review.md", "Public demo script must include the OCR license review.");
    AssertContains(script, "THIRD_PARTY_NOTICES.md", "Public demo script must include third-party notices.");
    AssertContains(script, "fire-eye-ocr-worker.exe", "Public demo script must verify the OCR worker is present or warn clearly.");
}

static string FindRepositoryRoot()
{
    var candidates = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var candidate in candidates)
    {
        for (var directory = new DirectoryInfo(candidate); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "PromptInputMethod.App")))
            {
                return directory.FullName;
            }
        }
    }

    throw new DirectoryNotFoundException("Could not locate repository root.");
}

static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFileExists(string path)
{
    Assert(File.Exists(path), $"Expected file to exist: {path}");
}

static void AssertContains(string text, string expected, string message)
{
    Assert(text.Contains(expected, StringComparison.Ordinal), message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
    }
}

internal sealed record ReleaseCheck(string Name, Action Run);

internal sealed record TemplateRecord(
    string Id,
    string Title,
    string Source,
    string Category,
    string Text);
