using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PromptInputMethod.Core.Prompt;
using PromptInputMethod.Core.Sync;

var repoRoot = FindRepositoryRoot();
var checks = new ReleaseCheck[]
{
    new("Prompt routing", CheckPromptRouting),
    new("Template data import/export", () => CheckTemplateData(repoRoot)),
    new("Skill matching inputs", () => CheckSkillMatchingInputs(repoRoot)),
    new("Provider validation", () => CheckProviderValidation(repoRoot)),
    new("Bilingual prompt generation protocol", () => CheckBilingualPromptGeneration(repoRoot)),
    new("Conversation pending thinking bubble", () => CheckConversationPendingThinkingBubble(repoRoot)),
    new("OneDrive E2EE history crypto", CheckOneDriveE2eeHistoryCrypto),
    new("OneDrive sync implementation", () => CheckOneDriveSyncImplementation(repoRoot)),
    new("WebDAV sync implementation", () => CheckWebDavSyncImplementation(repoRoot)),
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
    AssertContains(optimizationTargetSource, "正向 CLIP 文本编码（Positive CLIP Text Encode）", "ComfyUI adapter must keep Chinese primary-output labels for positive CLIP fields.");
    AssertContains(optimizationTargetSource, "反向提示词（Negative prompt）", "Stable Diffusion adapter must keep Chinese primary-output labels for WebUI negative prompt fields.");
    AssertContains(optimizationTargetSource, "英文提示词由 AIPIN_ENGLISH_PROMPT 单独输出", "ComfyUI adapter must not push English output into the Chinese prompt pane.");

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
    var appXaml = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml"));
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));

    AssertContains(appCode, "<AIPIN_ENGLISH_PROMPT>", "Model protocol must ask for the English prompt in the same completion.");
    AssertContains(appCode, "同一轮必须同步生成 AIPIN_ENGLISH_PROMPT", "Protocol rules must require one-pass English prompt generation.");
    AssertContains(appCode, "var protocolEnglishPrompt = protocolResult.EnglishPrompt;", "Generation flow should read the protocol English prompt directly.");
    AssertContains(appCode, "模型未按协议返回英文提示词，已使用本地同步结构", "Missing protocol English should use local fallback instead of a second model translation.");
    AssertContains(appCode, "BuildEnglishTranslationStructureRule", "One-pass English generation should still preserve target-specific structure rules.");
    AssertContains(appXaml, "CopyComfyPositiveButton_Click", "ComfyUI / Stable Diffusion outputs should expose one-click positive prompt copy.");
    AssertContains(appXaml, "CopyComfyNegativeButton_Click", "ComfyUI / Stable Diffusion outputs should expose one-click negative prompt copy.");
    AssertContains(appXaml, "CopyComfyParametersButton_Click", "ComfyUI / Stable Diffusion outputs should expose one-click parameter copy.");
    AssertContains(appCode, "BuildPromptFieldCopyPlan", "Target outputs should expose target-specific field copy plans instead of copying the whole prompt.");
    AssertContains(appCode, "PromptFieldCopyKind.Primary", "Field copy should support the current target's core field.");
    AssertContains(appCode, "PromptFieldCopyKind.Constraint", "Field copy should support the current target's constraint field.");
    AssertContains(appCode, "PromptFieldCopyKind.Parameter", "Field copy should support the current target's parameter or verification field.");
    AssertContains(appCode, "ExtractPromptField", "One-click field copy should parse fields instead of copying the whole prompt.");
    AssertContains(appCode, "SuggestOptimizationTarget", "Generation should recommend a target from obvious user intent when the user is still on the general LLM target.");
    AssertContains(appCode, "BuildOptimizationModeCapabilityText", "Optimization target picker should surface target capability tags.");
    AssertContains(appCode, "图片输入：可能支持", "Model capability tags should show likely multimodal support.");
    AssertContains(appCode, "流式输出", "Model capability tags should show streaming support.");
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

static void CheckOneDriveE2eeHistoryCrypto()
{
    const string passphrase = "correct horse battery staple";
    var crypto = new E2eeSyncCryptoService();
    var (vault, vaultKey) = crypto.CreateVault(passphrase, "release-check-key", new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero), iterations: 20_000);
    var unlocked = crypto.UnlockVault(vault, passphrase);
    Assert(vaultKey.SequenceEqual(unlocked), "Unlocking the sync vault should recover the same vault key.");

    const string historyJson = """
        {"schema":"aipin.history.plain.v1","id":"h1","title":"同步测试","userRequest":"请优化这段需求","messages":[{"role":"user","text":"不要明文上传历史","createdAt":"2026-06-02T00:00:00Z"}]}
        """;
    var encrypted = crypto.EncryptHistoryJson("h1", historyJson, unlocked, vault, new DateTimeOffset(2026, 6, 2, 0, 0, 1, TimeSpan.Zero));
    Assert(!encrypted.Ciphertext.Contains("同步测试", StringComparison.Ordinal), "Encrypted history ciphertext must not contain plaintext Chinese history text.");
    AssertEqual(historyJson, crypto.DecryptHistoryJson(encrypted, unlocked, vault), "Encrypted history should decrypt back to the original JSON payload.");

    AssertThrows<CryptographicException>(() => crypto.UnlockVault(vault, "wrong passphrase"), "Wrong OneDrive sync passphrase must not unlock the vault.");

    var tampered = encrypted with
    {
        Ciphertext = encrypted.Ciphertext[..^1] + (encrypted.Ciphertext[^1] == 'A' ? "B" : "A")
    };
    AssertThrows<CryptographicException>(() => crypto.DecryptHistoryJson(tampered, unlocked, vault), "Tampered encrypted history must fail authentication.");
}

static void CheckOneDriveSyncImplementation(string repoRoot)
{
    var appProject = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "PromptInputMethod.App.csproj"));
    AssertNotContains(appProject, "Microsoft.Identity.Client", "OneDrive folder sync must not require MSAL.NET.");
    AssertNotContains(appProject, "Microsoft.Identity.Client.Broker", "OneDrive folder sync must not require the MSAL broker package.");

    var authServicePath = Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveAuthService.cs");
    var graphClientPath = Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveGraphClient.cs");
    Assert(!File.Exists(authServicePath), "OneDrive folder sync must not keep a Microsoft account auth service.");
    Assert(!File.Exists(graphClientPath), "OneDrive folder sync must not keep a Microsoft Graph client.");

    var localFolderService = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveLocalFolderService.cs"));
    AssertContains(localFolderService, "DetectDefaultSyncFolder", "Settings should support detecting the local OneDrive folder without writing files.");
    AssertContains(localFolderService, "Directory.CreateDirectory(syncRootPath)", "Manual sync must create the chosen folder only when the sync action runs.");
    AssertContains(localFolderService, "BackupExistingFileAsync", "OneDrive folder sync must back up replaced files before writing.");
    AssertContains(localFolderService, "FindConflictFiles", "OneDrive folder sync must detect conflict-copy files before merging.");
    AssertContains(localFolderService, "IsBackupPath", "Conflict scanning should not be blocked forever by old backup copies.");
    AssertContains(localFolderService, "RunFileIoWithRetryAsync", "OneDrive folder sync should retry transient local file locks.");

    var oneDriveLauncher = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveClientLauncherService.cs"));
    AssertContains(oneDriveLauncher, "OneDrive.exe", "Manual folder sync should be able to locate the installed OneDrive client.");
    AssertContains(oneDriveLauncher, "Arguments = \"/background\"", "Manual folder sync should wake the OneDrive client without opening an app login flow.");
    AssertContains(oneDriveLauncher, "Process.GetProcessesByName(\"OneDrive\")", "Manual folder sync should detect when OneDrive is already running.");

    var syncModels = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveSyncModels.cs"));
    AssertContains(syncModels, "sync/manifest.json", "OneDrive sync must keep a stable manifest path.");
    AssertContains(syncModels, "sync/crypto/vault.json", "OneDrive sync must keep the encrypted vault separate from history records.");
    AssertContains(syncModels, "sync/history-tombstones", "OneDrive sync must reserve tombstone paths for deleted encrypted history.");

    var historySync = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "OneDriveHistorySyncService.cs"));
    AssertContains(historySync, "PushEncryptedHistoryAsync", "OneDrive history sync must expose encrypted push.");
    AssertContains(historySync, "PullEncryptedHistoryAsync", "OneDrive history sync must expose encrypted pull.");
    AssertContains(historySync, "CiphertextSha256", "OneDrive history sync must verify encrypted history hashes from the manifest.");
    AssertContains(historySync, "EnsureNoConflictFiles", "OneDrive history sync must stop when conflict-copy files are present.");
    AssertContains(historySync, "createIfMissing: false", "Importing snapshots must not create sync files just by pulling.");
    var historyService = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "PromptHistoryService.cs"));
    AssertContains(historyService, "remote.EffectiveUpdatedAt > local.EffectiveUpdatedAt", "History pull must not overwrite newer local records with older cloud copies.");

    var licenseInventory = File.ReadAllText(Path.Combine(repoRoot, "docs", "license-inventory.md"));
    AssertNotContains(licenseInventory, "Microsoft.Identity.Client", "License inventory must not list removed MSAL dependencies.");

    var appSettings = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "appsettings.json"));
    AssertContains(appSettings, "\"oneDriveSync\"", "Settings must use the local-folder sync settings section.");
    AssertContains(appSettings, "\"oneDriveEnabled\": false", "OneDrive sync must be disabled by default.");
    AssertContains(appSettings, "\"historySyncEnabled\": true", "History sync can be prepared but must still require OneDrive to be enabled.");
    AssertContains(appSettings, "\"localFolderPath\": \"\"", "OneDrive sync must not preselect or write a folder by default.");

    var appXaml = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml"));
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));
    AssertContains(appXaml, "OneDriveEnabledBox", "Settings page must expose the OneDrive enable toggle.");
    AssertContains(appXaml, "OneDriveFolderPathBox", "Settings page must show the local OneDrive sync folder path.");
    AssertContains(appXaml, "OneDriveDetectFolderButton", "Settings page should let the user detect the local OneDrive path explicitly.");
    AssertContains(appXaml, "OneDriveChooseFolderButton", "Settings page should let the user choose the sync folder explicitly.");
    AssertContains(appXaml, "OneDrivePassphraseBox", "Settings page must ask for the E2EE sync passphrase without storing it.");
    AssertContains(appXaml, "OneDrivePushButton", "Settings page must expose a manual history sync action.");
    AssertContains(appCode, "OneDriveDetectFolderButton_Click", "Detected paths must be an explicit user action.");
    AssertContains(appCode, "OneDriveChooseFolderButton_Click", "Chosen paths must be an explicit user action.");
    AssertContains(appCode, "QueueOneDriveStartupSnapshotProbe", "Startup should detect newer sync snapshots without importing automatically.");
    AssertContains(appCode, "ShowOneDriveSnapshotDetectedDialogAsync", "Newer sync snapshots should ask the user before import.");
    AssertContains(appCode, "PushEncryptedHistoryAsync", "Manual sync must export encrypted history.");
    AssertContains(appCode, "PullEncryptedHistoryAsync", "Manual sync must import encrypted history before or during sync.");
    AssertContains(appCode, "_oneDriveClientLauncherService.TryLaunchClient()", "Manual sync should wake OneDrive after writing encrypted snapshots.");
    AssertContains(appCode, "OneDrivePassphraseBox.Password = string.Empty", "Sync passphrase should be cleared after folder sync operations.");
    AssertNotContains(appCode, "AcquireToken", "OneDrive folder sync must not call Microsoft token acquisition.");
    AssertNotContains(appCode, "Microsoft Entra", "OneDrive folder sync UI must not ask for Microsoft Entra configuration.");
}

static void CheckWebDavSyncImplementation(string repoRoot)
{
    var appSettings = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "appsettings.json"));
    AssertContains(appSettings, "\"webDavSync\"", "Settings must include a WebDAV sync section.");
    AssertContains(appSettings, "\"webDavEnabled\": false", "WebDAV sync must be disabled by default.");
    AssertContains(appSettings, "\"serverUrl\": \"\"", "WebDAV sync must not preselect a remote server by default.");
    AssertContains(appSettings, "\"credentialTargetName\": \"PromptInputMethod/WebDavPassword\"", "WebDAV app password should live in Windows Credential Manager.");

    var remoteStore = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "WebDavRemoteStoreService.cs"));
    AssertContains(remoteStore, "new(\"MKCOL\")", "WebDAV sync must create missing remote directories.");
    AssertContains(remoteStore, "new(\"PROPFIND\")", "WebDAV sync must inspect remote metadata without a provider SDK.");
    AssertContains(remoteStore, "HttpMethod.Put", "WebDAV sync must upload encrypted snapshot JSON with PUT.");
    AssertContains(remoteStore, "BackupExistingFileAsync", "WebDAV sync must back up replaced remote files before writing.");
    AssertContains(remoteStore, "FindConflictFilesAsync", "WebDAV sync must detect conflict-copy files before merging.");
    AssertContains(remoteStore, "AuthenticationHeaderValue(\"Basic\"", "WebDAV sync should support app-password based Basic authentication.");
    AssertNotContains(remoteStore, "Microsoft.Identity", "WebDAV sync must not depend on Microsoft auth packages.");

    var historySync = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "Services", "WebDavHistorySyncService.cs"));
    AssertContains(historySync, "PushEncryptedHistoryAsync", "WebDAV history sync must expose encrypted push.");
    AssertContains(historySync, "PullEncryptedHistoryAsync", "WebDAV history sync must expose encrypted pull.");
    AssertContains(historySync, "CiphertextSha256", "WebDAV history sync must verify encrypted history hashes from the manifest.");
    AssertContains(historySync, "createIfMissing: false", "Importing WebDAV snapshots must not create remote sync files just by pulling.");
    AssertContains(historySync, "BuildVaultCacheScope", "WebDAV remembered vault keys must be scoped to the configured remote.");

    var appXaml = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml"));
    var appCode = File.ReadAllText(Path.Combine(repoRoot, "src", "PromptInputMethod.App", "CompactPromptWindow.xaml.cs"));
    AssertContains(appXaml, "WebDavEnabledBox", "Settings page must expose the WebDAV enable toggle.");
    AssertContains(appXaml, "https://dav.jianguoyun.com/dav/", "Settings page should provide the Nutstore WebDAV example URL.");
    AssertContains(appXaml, "WebDavPasswordBox", "Settings page must ask for the WebDAV app password.");
    AssertContains(appXaml, "WebDavPassphraseBox", "Settings page must ask for a separate E2EE sync passphrase.");
    AssertContains(appXaml, "WebDavTestButton", "Settings page must expose a manual WebDAV connection test.");
    AssertContains(appXaml, "WebDavPushButton", "Settings page must expose a manual WebDAV sync action.");
    AssertContains(appCode, "SaveWebDavPasswordFromUi", "WebDAV app password should be saved only through Credential Manager.");
    AssertContains(appCode, "WebDavPassphraseBox.Password = string.Empty", "WebDAV sync passphrase should be cleared after sync operations.");
    AssertContains(appCode, "WebDavTestButton_Click", "Testing WebDAV should be an explicit user action.");
    AssertNotContains(appCode, "QueueWebDavStartup", "WebDAV sync must not run hidden network probes at startup.");
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

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, got {ex.GetType().Name}.");
    }

    throw new InvalidOperationException($"{message} Expected {typeof(TException).Name}, but no exception was thrown.");
}

internal sealed record ReleaseCheck(string Name, Action Run);

internal sealed record TemplateRecord(
    string Id,
    string Title,
    string Source,
    string Category,
    string Text);
