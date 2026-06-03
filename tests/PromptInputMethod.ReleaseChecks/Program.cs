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
    new("Bilingual prompt generation protocol", () => CheckBilingualPromptGeneration(repoRoot)),
    new("Conversation pending thinking bubble", () => CheckConversationPendingThinkingBubble(repoRoot)),
    new("GitHub community sync exclusion", () => CheckGitHubCommunitySyncExclusion(repoRoot)),
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
    AssertContains(catalogSource, "builtin-jimeng-director-storyboard", "Built-in catalog must include the Jimeng director storyboard template.");
    AssertContains(catalogSource, "builtin-jimeng-seedream-image", "Built-in catalog must include the Jimeng Seedream image template.");
    AssertContains(catalogSource, "builtin-sd-comfyui-node-fields", "Built-in catalog must include the ComfyUI node-field template.");
    AssertContains(catalogSource, "builtin-sd-webui-positive-negative", "Built-in catalog must include the Stable Diffusion WebUI positive/negative prompt template.");

    var optimizationTargetSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OptimizationTargetService.cs"));
    AssertContains(optimizationTargetSource, "builtin-jimeng-seedance-director", "Built-in optimization targets must include the Jimeng / Seedance director Skill.");
    AssertContains(optimizationTargetSource, "文生视频、图生视频、首尾帧过渡、产品宣发、短剧对白、Seedream 图片和视频编辑", "Jimeng director target must cover the reviewed video and image generation scenarios.");
    AssertContains(optimizationTargetSource, "builtin-comfyui-stable-diffusion", "Built-in optimization targets must include the ComfyUI / Stable Diffusion adapter.");
    AssertContains(optimizationTargetSource, "Positive CLIP Text Encode", "ComfyUI adapter must output positive CLIP text fields.");
    AssertContains(optimizationTargetSource, "Negative prompt", "Stable Diffusion adapter must output WebUI negative prompt fields.");

    var skillCandidatesDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "skill-source-candidates.md"));
    AssertContains(skillCandidatesDoc, "原创干净房间实现", "Jimeng Skill candidate notes must document the clean-room implementation boundary.");
    AssertContains(skillCandidatesDoc, "高星但没有明确许可证的仓库按黑箱处理", "Jimeng Skill candidate notes must keep no-license high-star repositories black-box only.");

    var comfyDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "comfyui-stable-diffusion-prompt-adapter.md"));
    AssertContains(comfyDoc, "ComfyUI 节点字段", "ComfyUI / Stable Diffusion research doc must define the node-field output strategy.");
    AssertContains(comfyDoc, "workflow JSON", "ComfyUI / Stable Diffusion research doc must explain why full workflow JSON is not generated by default.");
}

static void CheckProviderValidation(string repoRoot)
{
    var settings = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "appsettings.json"));
    using var settingsJson = JsonDocument.Parse(settings);
    var preferredProvider = settingsJson.RootElement.GetProperty("ocr").GetProperty("preferredProvider").GetString();
    AssertEqual("fire_eye_ocr", preferredProvider, "Fire Eye OCR must remain the default provider.");
    var enableAnimations = settingsJson.RootElement.GetProperty("ui").GetProperty("enableAnimations").GetBoolean();
    Assert(enableAnimations, "UI animations should be enabled by default.");

    var routerSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OcrProviderRouter.cs"));
    AssertContains(routerSource, "public const string Auto = \"auto_ocr\"", "Auto OCR provider id must stay stable.");
    AssertContains(routerSource, "public const string WindowsMedia = \"windows_media_ocr\"", "Windows Media fallback provider id must stay stable.");
    AssertContains(routerSource, "return string.Equals(normalized, OcrProviderIds.WindowsMedia", "Direct Windows Media selection should normalize back to Fire Eye.");

    var fireEyeSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "FireEyeOcrProvider.cs"));
    AssertContains(fireEyeSource, "Utf8JsonReader", "Fire Eye worker parsing must tolerate trailing native logs after the JSON payload.");

    var llmClientSource = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.Core", "Llm", "OpenAiCompatibleClient.cs"));
    AssertContains(llmClientSource, "_httpClient.Timeout = Timeout.InfiniteTimeSpan", "Generation client must not use HttpClient's default 100-second timeout.");
    AssertContains(llmClientSource, "SendAsync(httpRequest, cancellationToken)", "Chat completion calls should wait for the provider response instead of a fixed generation timeout.");
    AssertNotContains(llmClientSource, "options.TimeoutSeconds", "Chat completion calls must not use the endpoint-probe timeout as a generation timeout.");
    AssertContains(llmClientSource, "CompleteWithResultStreamingAsync", "Generation client should support streaming chat completions.");
    AssertContains(llmClientSource, "HttpCompletionOption.ResponseHeadersRead", "Streaming chat completions should start processing before the full body is buffered.");
    AssertContains(llmClientSource, "[\"stream\"] = stream", "Streaming support should toggle the OpenAI-compatible stream payload flag.");
    AssertContains(llmClientSource, "TryReadStreamingUpdate", "Streaming support should parse SSE delta payloads.");

    var ocrReview = File.ReadAllText(Path.Combine(repoRoot, "docs", "ocr-model-license-review.md"));
    AssertContains(ocrReview, "PP-OCRv5_mobile_det", "OCR model review must cover the detection model.");
    AssertContains(ocrReview, "PP-OCRv5_mobile_rec", "OCR model review must cover the recognition model.");
    AssertContains(ocrReview, "Apache-2.0", "OCR model review must record the Apache-2.0 license conclusion.");
}

static void CheckBilingualPromptGeneration(string repoRoot)
{
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));

    AssertContains(appCode, "<AIPIN_ENGLISH_PROMPT>", "Model protocol must ask for the English prompt in the same completion.");
    AssertContains(appCode, "同一轮必须同步生成 AIPIN_ENGLISH_PROMPT", "Protocol rules must require one-pass English prompt generation.");
    AssertContains(appCode, "var protocolEnglishPrompt = protocolResult.EnglishPrompt;", "Generation flow should read the protocol English prompt directly.");
    AssertContains(appCode, "模型未按协议返回英文提示词，已使用本地同步结构", "Missing protocol English should use local fallback instead of a second model translation.");
    AssertContains(appCode, "BuildEnglishTranslationStructureRule", "One-pass English generation should still preserve target-specific structure rules.");
    AssertContains(appCode, "LooksLikeQuotaOrBillingError", "Provider quota and billing errors should be surfaced clearly.");
    AssertNotContains(appCode, "BuildGenerationRequestOptions", "Generation flow must not hard-code longer timeouts for slow reasoning calls.");
    AssertNotContains(appCode, "BuildSynchronizedEnglishPromptForGenerationAsync(", "Generation flow must not call the model a second time for translation.");
    AssertNotContains(appCode, "BuildEnglishTranslationPrompt(", "The old second-pass translation prompt must not be kept in the app code.");
}

static void CheckConversationPendingThinkingBubble(string repoRoot)
{
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));

    AssertContains(appCode, "var pendingThinkingMessage = AddPendingThinkingMessage(deepThinkingEnabled);", "Generation should add a transient pending-thinking bubble before waiting for the model.");
    AssertContains(appCode, "RemoveTransientChatMessage(pendingThinkingMessage);", "Generation should remove the pending-thinking bubble before adding formal assistant content.");
    AssertContains(appCode, "CompleteWithResultStreamingAsync", "Generation should use streaming completions so pending UI can update while the model responds.");
    AssertContains(appCode, "new PendingThinkingProgress(pendingThinkingMessage)", "Streaming deltas should update pending-thinking counters without flooding the UI thread.");
    AssertContains(appCode, "PeriodicTimer", "Pending-thinking bubble should refresh elapsed time while the request is running.");
    AssertContains(appCode, "Stopwatch.StartNew()", "Elapsed thinking time should not depend on starved UI timer ticks.");
    AssertContains(appCode, "Interlocked.Add", "Streaming token counters should be updated off the UI thread.");
    AssertNotContains(appCode, "new Progress<LlmStreamUpdate>", "Streaming progress must not enqueue every token onto the WinUI dispatcher.");
    AssertContains(appCode, "AreAnimationsEnabled()", "Conversation and output animations must honor the user animation setting.");
    AssertContains(appCode, "已思考", "Pending-thinking bubble should show elapsed thinking seconds.");
    AssertContains(appCode, "private sealed class TransientChatMessage", "Pending-thinking UI state should be represented separately from persisted conversation messages.");
    AssertContains(appCode, "正在思考，等待模型返回", "Pending-thinking bubble should show a visible Chinese transition message.");
    AssertContains(appCode, "new TransientChatMessage(elements, elapsedTexts, detailTexts", "Pending-thinking bubble should be transient UI, not conversation history.");
}

static void CheckGitHubCommunitySyncExclusion(string repoRoot)
{
    var appSettings = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "appsettings.json"));
    var appProject = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "PromptInputMethod.App.csproj"));
    var appXaml = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml"));
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));

    AssertNotContains(appSettings, "oneDriveSync", "GitHub community settings must not expose OneDrive sync configuration.");
    AssertNotContains(appSettings, "webDavSync", "GitHub community settings must not expose WebDAV sync configuration.");
    AssertNotContains(appProject, "Microsoft.Identity", "GitHub community build must not pull Microsoft cloud auth dependencies.");
    AssertNotContains(appXaml, "OneDrive", "GitHub community settings must not show a OneDrive sync entry.");
    AssertNotContains(appXaml, "WebDAV", "GitHub community settings must not show a WebDAV sync entry.");
    AssertNotContains(appCode, "OneDrive", "GitHub community app code must not keep OneDrive sync handlers.");
    AssertNotContains(appCode, "WebDav", "GitHub community app code must not keep WebDAV sync handlers.");

    var forbiddenFiles = new[]
    {
        Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveLocalFolderService.cs"),
        Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveHistorySyncService.cs"),
        Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "WebDavHistorySyncService.cs"),
        Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "WebDavRemoteStoreService.cs"),
        Path.Combine(repoRoot, "src", "PromptInputMethod.Core", "Sync")
    };

    foreach (var path in forbiddenFiles)
    {
        Assert(!File.Exists(path) && !Directory.Exists(path), $"GitHub community release must not include sync implementation: {path}");
    }
}

static void CheckAccessibilityAndLayoutCoverage(string repoRoot)
{
    var appXaml = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml"));
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));
    var trayCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "TrayMenuWindow.xaml.cs"));

    AssertContains(appXaml, "KeyboardAccelerators", "Main window should expose keyboard shortcuts.");
    AssertContains(appXaml, "AccessKey", "Primary controls should expose access keys for keyboard navigation.");
    AssertContains(appXaml, "AutomationProperties.Name", "Important icon-only controls should have automation names.");
    AssertContains(appXaml, "OptimizationCategoryBox", "Right optimization target picker should expose a top-level category ComboBox.");
    AssertContains(appXaml, "OptimizationModeBox", "Right optimization target picker should expose a target ComboBox under the selected category.");
    AssertContains(appXaml, "CompactOptimizationCategoryBox", "Compact optimization picker should expose the same top-level category ComboBox.");
    AssertContains(appXaml, "CompactModeBox", "Compact optimization picker should expose a target ComboBox under the selected category.");
    AssertContains(appCode, "RefreshVisibleOptimizationModeChoices", "Optimization target selection should filter target choices by the selected top-level category.");
    AssertContains(appCode, "OptimizationCategoryChoice", "Optimization target selection should model categories separately from target choices.");
    AssertNotContains(appXaml, "ModeGenericRadio", "Optimization targets should not be flattened into right-panel radio buttons.");
    AssertContains(appXaml, "AnimationEnabledBox", "Settings page should expose an animation enable/disable toggle.");
    AssertContains(appCode, "_settings.Ui.EnableAnimations = AnimationEnabledBox.IsChecked == true", "Animation setting must be persisted from the settings page.");
    AssertContains(appCode, "AnimationEnabledBox.IsChecked = _settings.Ui.EnableAnimations", "Animation setting must be loaded into the settings page.");
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

static void AssertNotContains(string text, string unexpected, string message)
{
    Assert(!text.Contains(unexpected, StringComparison.Ordinal), message);
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
