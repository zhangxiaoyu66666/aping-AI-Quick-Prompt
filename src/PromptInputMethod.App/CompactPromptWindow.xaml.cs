using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PromptInputMethod.App.Services;
using PromptInputMethod.Core.Llm;
using PromptInputMethod.Core.Ocr;
using PromptInputMethod.Core.Prompt;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PromptInputMethod.App;

public sealed partial class CompactPromptWindow : Window
{
    private const int SwHide = 0;
    private const int SwShow = 5;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const double SidebarCollapseWidth = 1180;
    private const double SidebarExpandWidth = 1260;
    private const int PageTransitionDurationMs = 120;
    private const int TabTransitionDurationMs = 100;
    private const int ScreenCaptureSettleDelayMs = 420;
    private const double SidebarFullCollapseWidth = 720;
    private const double SidebarFullExpandWidth = 820;
    private const double RightPanelCollapseWidth = 760;
    private const double RightPanelExpandWidth = 840;
    private const double OutputStackWidth = 940;
    private const double OutputUnstackWidth = 1020;
    private const int TypewriterFrameDelayMs = 12;
    private const int TypewriterMaxFrames = 140;
    private const string SkillTemplateSource = "Skill";
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNotTopmost = new(-2);
    private static readonly Regex TemplateVariableRegex = new(@"\{\{\s*(?<name>[\p{L}\p{N}_ \-]{1,48})\s*\}\}|\{\s*(?<name>[\p{L}\p{N}_ \-]{1,48})\s*\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly IReadOnlyList<TemplateSourceDefinition> TemplateSourceDefinitions =
    [
        new("我的模板", "我的模板", "用户导入、自建分类"),
        new("ChatGPT-Shortcut", "ChatGPT-Shortcut", "通用、写作、提示词工程"),
        new("prompts.chat", "prompts.chat", "开源角色提示词库"),
        new("SD-Anima-Prompt-Studio", "SD-Anima-Prompt-Studio", "文生图、角色、构图"),
        new("Veo 3", "Veo 3", "电影镜头、对白、分镜"),
        new("即梦 / Seedance", "即梦 / Seedance", "短视频、产品、首尾帧"),
        new("AI编程", "AI编程", "Codex、Claude Code、反重力")
    ];
    private static readonly IReadOnlyList<ModelProviderPreset> ModelProviderPresets =
    [
        new("custom", "自定义 / OpenAI-compatible", string.Empty, string.Empty, [], "手动填写 OpenAI-compatible endpoint。"),
        new("openai", "OpenAI", "https://api.openai.com/v1", "gpt-5.5", ["gpt-5.5", "gpt-5.4", "gpt-4.1-mini"], "OpenAI 官方 API。"),
        new("deepseek", "DeepSeek", "https://api.deepseek.com", "deepseek-v4-flash", ["deepseek-v4-flash", "deepseek-v4-pro", "deepseek-chat", "deepseek-reasoner"], "DeepSeek OpenAI-compatible API。"),
        new("glm", "GLM / 智谱", "https://open.bigmodel.cn/api/paas/v4", "glm-4.7", ["glm-4.7", "glm-4-plus", "glm-4-flash"], "智谱 GLM OpenAI-compatible API。"),
        new("claude", "Claude / Anthropic", "https://api.anthropic.com/v1", "claude-sonnet-4-5", ["claude-sonnet-4-5", "claude-opus-4-1"], "Anthropic OpenAI SDK compatibility endpoint。"),
        new("gemini", "Gemini / Google", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-2.5-flash", ["gemini-2.5-pro", "gemini-2.5-flash"], "Google Gemini OpenAI compatibility endpoint。"),
        new("minimax", "MiniMax", "https://api.minimax.io/v1", "MiniMax-M2.7", ["MiniMax-M2.7", "abab6.5s-chat"], "MiniMax OpenAI-compatible API。"),
        new("doubao", "豆包 / 火山方舟", "https://ark.cn-beijing.volces.com/api/v3", "ep-你的方舟EndpointId", ["ep-你的方舟EndpointId"], "火山方舟 OpenAI-compatible API，模型名通常填写 endpoint ID。"),
        new("kimi", "Kimi / Moonshot", "https://api.moonshot.cn/v1", "kimi-k2.6", ["kimi-k2.6", "moonshot-v1-128k"], "Moonshot Kimi OpenAI-compatible API。")
    ];

    private readonly ForegroundWindowService _foregroundWindowService = new();
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly SceneDetector _sceneDetector = new();
    private readonly PromptStructuringService _promptStructuringService = new();
    private readonly AppSettingsService _settingsService = new();
    private readonly CredentialService _credentialService = new();
    private readonly ClipboardContextService _clipboardContextService = new();
    private readonly OcrProviderRouter _ocrRouter = new();
    private readonly OcrContextBuilder _ocrContextBuilder = new();
    private readonly WindowCaptureService _windowCaptureService = new();
    private readonly SensitiveTextRedactor _sensitiveTextRedactor = new();
    private readonly PrivacyDataService _privacyDataService = new();
    private readonly PromptFavoriteService _promptFavoriteService = new();
    private readonly CommonPromptService _commonPromptService = new();
    private readonly PromptHistoryService _historyService = new();
    private readonly PromptVersionService _promptVersionService = new();
    private readonly ModelSendAuditService _modelSendAuditService = new();
    private readonly PromptTemplateCatalogService _templateCatalogService = new();
    private readonly OptimizationTargetService _optimizationTargetService = new();
    private readonly LocalizationService _localizationService = new();
    private readonly AppDatabaseService _databaseService = new();
    private readonly OpenAiCompatibleClient _llmClient = new();
    private readonly Dictionary<string, string> _originalLocalizedValues = new();
    private readonly DispatcherTimer _modelProbeTimer = new();
    private TrayIconService? _trayIconService;
    private TrayMenuWindow? _trayMenuWindow;
    private AppSettings _settings = new();
    private LocalizationPack _languagePack = LocalizationPack.English;
    private nint _lastForegroundWindow;
    private string? _contextSource;
    private string? _contextFieldName;
    private string _selectedMode = "通用 LLM";
    private string _selectedTemplateSource = "ChatGPT-Shortcut";
    private string _selectedTemplateCategory = "全部";
    private string _selectedCommonPromptCategory = "全部";
    private string _commonPromptSearchText = string.Empty;
    private string? _conversationLockedMode;
    private string? _conversationLockedTargetLabel;
    private string? _conversationLockedCustomModeText;
    private string? _selectedMountedSkillId;
    private bool _syncingTemplateSelection;
    private bool _syncingModeSelection;
    private bool _syncingCommonPromptSelection;
    private bool _syncingCommonPromptSearch;
    private bool _syncingOptimizationTargetSelection;
    private bool _syncingDeepThinkingSelection;
    private bool _loadingSettings;
    private bool _sidebarCollapsed;
    private bool _sidebarAutoCollapsed;
    private bool _sidebarManuallyExpanded;
    private bool _sidebarFullyCollapsed;
    private bool _uiReady;
    private bool _syncingText;
    private bool _syncingModelSelection;
    private bool _syncingWorkflowModelSelection;
    private bool _syncingProviderPresetSelection;
    private bool _rightPanelManuallyCollapsed;
    private bool _rightPanelManuallyExpanded;
    private bool _rightPanelCollapsed;
    private bool _outputsStacked;
    private bool _responsiveLayoutQueued;
    private bool _applyingResponsiveLayout;
    private bool _modelProbeDialogOpen;
    private bool _isGeneratingPrompt;
    private bool _isExiting;
    private CancellationTokenSource? _outputTypewriterCts;
    private string? _pendingDiffBasePrompt;
    private string? _pendingUserReply;
    private string? _currentConversationHistoryId;
    private readonly List<PromptConversationMessage> _conversationMessages = new();
    private int _modelProbeVersion;
    private string? _lastModelProbeFailureSignature;
    private LlmImageAttachment? _modelImageAttachment;

    private sealed record ModelProviderPreset(
        string Id,
        string DisplayName,
        string BaseUrl,
        string RecommendedModel,
        IReadOnlyList<string> DefaultModels,
        string Description);

    private sealed record TemplateSourceDefinition(string Source, string Title, string Description);

    private sealed record TemplateSourceChoice(string Value, string DisplayTitle, string DisplaySubtitle)
    {
        public override string ToString()
        {
            return DisplayTitle;
        }
    }

    private sealed record CompactSkillChoice(string Id, string Title, string Description, PromptTemplateCatalogItem? Template)
    {
        public override string ToString()
        {
            return Title;
        }
    }

    private sealed record LocalizedChoice(string Value, string DisplayText)
    {
        public override string ToString()
        {
            return DisplayText;
        }
    }

    private sealed record ModelReasoningCapability(
        string ProviderId,
        string ProviderTag,
        string ModelTag,
        string RequestKind,
        bool SupportsNativeRequest,
        bool ReturnsReasoningContent,
        string Description)
    {
        public bool HasNativeSupport => SupportsNativeRequest || ReturnsReasoningContent;
    }

    public CompactPromptWindow()
    {
        InitializeComponent();
        InitializeHotkeyKeyOptions();
        _modelProbeTimer.Interval = TimeSpan.FromMilliseconds(900);
        _modelProbeTimer.Tick += ModelProbeTimer_Tick;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SystemBackdrop = new MicaBackdrop
        {
            Kind = MicaKind.BaseAlt
        };

        var hwnd = WindowNative.GetWindowHandle(this);
        var iconPath = GetAppIconPath();
        SetWindowIcon(hwnd, iconPath);
        RegisterCloseToTray(hwnd);
        _trayIconService = new TrayIconService(hwnd, iconPath, "啊拼");
        _trayIconService.ShowRequested += (_, _) => ShowFromTray();
        _trayIconService.MenuRequested += (_, args) => ShowTrayMenu(args.X, args.Y);
        _hotkeyService = new GlobalHotkeyService(hwnd);
        _hotkeyService.HotkeyPressed += (_, _) => ShowForHotkey();
        _settings = _settingsService.Load();
        if (!_hotkeyService.RegisterHotkey(_settings.Hotkey))
        {
            _hotkeyService.RegisterDefaultHotkey();
        }

        Closed += CompactPromptWindow_Closed;
        Content.KeyDown += Content_KeyDown;

        LoadSettingsIntoUi();
        ApplyLocalization();
        RefreshFavoritesUi();
        RefreshOptimizationTargetPickers();
        RefreshHistoryUi();
        RefreshCommonPromptsUi();
        RefreshTemplateViews();
        RefreshAboutUi();
        _uiReady = true;
        QueueRefreshSearchIndex();
        RefreshScene();
        RefreshModelDisplayText();
        RefreshWorkflowModelBox();
        UpdateInputBoxHeights(GetUserInput());
        SetWindowMode(expanded: true);
        ShowPage("Home");
        QueueResponsiveHomeLayout();
        ScheduleModelProbe();
    }

    private void ShowForHotkey()
    {
        _lastForegroundWindow = _foregroundWindowService.GetForegroundWindowHandle();
        RefreshScene();
        SetWindowMode(expanded: false);
        ShowMainWindow();
        InputBox.Focus(FocusState.Programmatic);
    }

    private void ShowFromTray()
    {
        HideTrayMenu();
        ShowMainWindow();
    }

    private void ShowTrayMenu(int x, int y)
    {
        if (_trayMenuWindow is null)
        {
            var menu = new TrayMenuWindow();
            menu.ShowRequested += (_, _) => ShowFromTray();
            menu.ExitRequested += (_, _) => ExitFromTray();
            menu.Closed += (_, _) =>
            {
                if (ReferenceEquals(_trayMenuWindow, menu))
                {
                    _trayMenuWindow = null;
                }
            };

            _trayMenuWindow = menu;
        }

        _trayMenuWindow.ShowAt(x, y);
    }

    private void HideTrayMenu()
    {
        if (_trayMenuWindow is null)
        {
            return;
        }

        _trayMenuWindow.HideMenu();
    }

    private void CloseTrayMenuForExit()
    {
        var menu = _trayMenuWindow;
        if (menu is null)
        {
            return;
        }

        _trayMenuWindow = null;
        menu.CloseForAppExit();
    }

    private void ShowMainWindow()
    {
        ShowWindow(WindowNative.GetWindowHandle(this), SwShow);
        Activate();
    }

    private void ExitFromTray()
    {
        HardExitApplication();
    }

    private void HardExitApplication()
    {
        _isExiting = true;
        try
        {
            _outputTypewriterCts?.Cancel();
        }
        catch
        {
        }

        CloseTrayMenuForExit();
        _trayIconService?.Dispose();
        _trayIconService = null;
        _hotkeyService.Dispose();
        KillRelatedAppProcesses();
        Environment.Exit(0);
    }

    private static void KillRelatedAppProcesses()
    {
        var current = System.Diagnostics.Process.GetCurrentProcess();
        KillProcessesByName(current.ProcessName, current.Id);
        KillProcessesByName("fire-eye-ocr-worker", current.Id);
    }

    private static void KillProcessesByName(string processName, int currentProcessId)
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName(processName))
        {
            using (process)
            {
                if (process.Id == currentProcessId)
                {
                    continue;
                }

                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(1500);
                }
                catch
                {
                }
            }
        }
    }

    private void RegisterCloseToTray(nint hwnd)
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow.GetFromWindowId(windowId).Closing += AppWindow_Closing;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        args.Cancel = true;
        HideWindow();
        SetStatus("啊拼已最小化到系统托盘，可用快捷键或托盘图标呼出。");
    }

    private void CompactPromptWindow_Closed(object sender, WindowEventArgs args)
    {
        CloseTrayMenuForExit();
        _trayIconService?.Dispose();
        _trayIconService = null;
        _hotkeyService.Dispose();
    }

    private static string GetAppIconPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
    }

    private static void SetWindowIcon(nint hwnd, string iconPath)
    {
        if (!File.Exists(iconPath))
        {
            return;
        }

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow.GetFromWindowId(windowId).SetIcon(iconPath);
    }

    private void SetWindowMode(bool expanded)
    {
        if (expanded)
        {
            CompactShell.Visibility = Visibility.Collapsed;
            ExpandedShell.Visibility = Visibility.Visible;
            ResizeWindow(1480, 820);
            SetTopmost(false);
            CenterWindow();
            ExpandedInputBox.Focus(FocusState.Programmatic);
            return;
        }

        ExpandedShell.Visibility = Visibility.Collapsed;
        CompactShell.Visibility = Visibility.Visible;
        HideSidebarOverlay();
        TopSidebarMenuButton.Visibility = Visibility.Collapsed;
        ResizeWindow(1040, 720);
        SetTopmost(true);
        CenterWindow();
    }

    private void ExpandButton_Click(object sender, RoutedEventArgs e)
    {
        SetWindowMode(expanded: true);
        ShowPage("Home");
    }

    private void CollapseToCompactButton_Click(object sender, RoutedEventArgs e)
    {
        SetWindowMode(expanded: false);
    }

    private void CompactTabButton_Click(object sender, RoutedEventArgs e)
    {
        var pageName = (sender as FrameworkElement)?.Tag?.ToString() ?? "Workbench";
        ShowCompactPage(pageName);
    }

    private void ShowCompactPage(string pageName)
    {
        var showCommonPrompts = string.Equals(pageName, "CommonPrompts", StringComparison.OrdinalIgnoreCase);
        var targetPage = showCommonPrompts ? CompactCommonPromptPage : CompactWorkbenchPage;
        var wasVisible = targetPage.Visibility == Visibility.Visible;

        CompactWorkbenchPage.Visibility = showCommonPrompts ? Visibility.Collapsed : Visibility.Visible;
        CompactCommonPromptPage.Visibility = showCommonPrompts ? Visibility.Visible : Visibility.Collapsed;
        CompactWorkbenchTab.IsChecked = !showCommonPrompts;
        CompactCommonPromptTab.IsChecked = showCommonPrompts;

        if (!wasVisible)
        {
            AnimateElementIn(targetPage, 3, TabTransitionDurationMs);
        }

        if (showCommonPrompts)
        {
            RefreshCommonPromptsUi();
        }
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string pageName })
        {
            HideSidebarOverlay();
            ShowPage(pageName);
        }
    }

    private void ShowPage(string pageName)
    {
        var targetPage = GetExpandedPage(pageName) ?? HomePage;
        var wasVisible = targetPage.Visibility == Visibility.Visible;

        foreach (var page in GetExpandedPages())
        {
            page.Visibility = page == targetPage ? Visibility.Visible : Visibility.Collapsed;
        }

        if (!wasVisible)
        {
            AnimateElementIn(targetPage, 4, PageTransitionDurationMs);
        }

        UpdateNavigationSelection(pageName);

        if (pageName == "History")
        {
            RefreshHistoryUi();
        }
        else if (pageName == "Templates")
        {
            RefreshFavoritesUi();
        }
        else if (pageName == "Skills")
        {
            RefreshSkillManagementUi();
        }
        else if (pageName == "OptimizationTargets")
        {
            RefreshOptimizationTargetPickers();
        }
        else if (pageName == "Snippets")
        {
            RefreshCommonPromptsUi();
        }
        else if (pageName is "Models" or "Settings")
        {
            LoadSettingsIntoUi();
        }
    }

    private FrameworkElement? GetExpandedPage(string pageName)
    {
        return pageName switch
        {
            "Home" => HomePage,
            "History" => HistoryPage,
            "Templates" => TemplatesPage,
            "Skills" => SkillPage,
            "OptimizationTargets" => OptimizationTargetsPage,
            "Snippets" => SnippetsPage,
            "Models" => ModelsPage,
            "Settings" => SettingsPage,
            "Help" => HelpPage,
            _ => null
        };
    }

    private IEnumerable<FrameworkElement> GetExpandedPages()
    {
        yield return HomePage;
        yield return HistoryPage;
        yield return TemplatesPage;
        yield return SkillPage;
        yield return OptimizationTargetsPage;
        yield return SnippetsPage;
        yield return ModelsPage;
        yield return SettingsPage;
        yield return HelpPage;
    }

    private static void AnimateElementIn(FrameworkElement? element, double verticalOffset, int durationMs)
    {
        if (element is null)
        {
            return;
        }

        if (element.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            element.RenderTransform = transform;
        }

        transform.Y = verticalOffset;

        var duration = new Duration(TimeSpan.FromMilliseconds(durationMs));
        var translateAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = duration,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(translateAnimation, transform);
        Storyboard.SetTargetProperty(translateAnimation, nameof(TranslateTransform.Y));

        var storyboard = new Storyboard();
        storyboard.Children.Add(translateAnimation);
        storyboard.Completed += (_, _) =>
        {
            transform.Y = 0;
        };
        storyboard.Begin();
    }

    private void UpdateNavigationSelection(string pageName)
    {
        ApplyNavigationSelection(SidebarHost, pageName);
        ApplyNavigationSelection(NarrowSidebarOverlay, pageName);
    }

    private void ApplyNavigationSelection(DependencyObject root, string pageName)
    {
        if (root is Button button && button.Tag is string tag)
        {
            var selected = string.Equals(tag, pageName, StringComparison.OrdinalIgnoreCase);
            button.Background = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(0x14, 0, 0, 0) : Colors.Transparent);
            button.BorderBrush = new SolidColorBrush(selected ? Windows.UI.Color.FromArgb(0xFF, 0, 0x67, 0xC0) : Colors.Transparent);
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            ApplyNavigationSelection(VisualTreeHelper.GetChild(root, index), pageName);
        }
    }

    private void RefreshScene()
    {
        var target = L(FormatSelectedScenes());
        SceneText.Text = $"{L("优化目标：")}{target}";
        BottomSceneText.Text = $"{L("优化目标：")}{target}";
        BottomLanguageText.Text = L("输出语言：中英双语");
    }

    private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        await GenerateOptimizedPromptAsync(null);
    }

    private async Task GenerateOptimizedPromptAsync(string? overrideUserRequest)
    {
        if (_isGeneratingPrompt)
        {
            SetStatus("正在生成中，请稍等");
            return;
        }

        _isGeneratingPrompt = true;
        try
        {
            await GenerateOptimizedPromptCoreAsync(overrideUserRequest);
        }
        finally
        {
            _isGeneratingPrompt = false;
        }
    }

    private async Task GenerateOptimizedPromptCoreAsync(string? overrideUserRequest)
    {
        CancelOutputTypewriter();
        SetStatus("正在生成...");

        var settingsLoadTask = Task.Run(() =>
        {
            var loadedSettings = _settingsService.Load();
            var loadedOptions = _settingsService.ToLlmOptions(loadedSettings);
            return (Settings: loadedSettings, Options: loadedOptions);
        });
        var context = GetTargetWindowContext();
        var scene = _sceneDetector.Detect(context);
        var userRequest = string.IsNullOrWhiteSpace(overrideUserRequest) ? GetUserInput() : overrideUserRequest.Trim();
        LockConversationTargetIfNeeded();
        if (overrideUserRequest is null)
        {
            EnsureUserRequestInConversation(userRequest);
        }
        var diffBasePrompt = _pendingDiffBasePrompt;
        var userReply = _pendingUserReply;
        _pendingDiffBasePrompt = null;
        _pendingUserReply = null;
        SetCurrentPrompt(string.IsNullOrWhiteSpace(diffBasePrompt) ? userRequest : diffBasePrompt);
        var request = new PromptStructuringRequest(userRequest, context, scene, ContextBox.Text, _contextSource, _contextFieldName);
        var selectedMountedSkill = GetSelectedMountedSkill();
        var isAiCodingMode = IsAiCodingMode();
        var isTextToImageMode = IsTextToImageMode();
        var isVideoMode = IsVideoMode();
        var isAcademicHumanizeMode = IsAcademicHumanizeMode();
        var shouldAutoMatchMountedSkill = selectedMountedSkill is null
            && (isAiCodingMode || isTextToImageMode || isVideoMode);
        var matchedSkillTask = selectedMountedSkill is null
            ? shouldAutoMatchMountedSkill
                ? Task.Run(() => FindMatchingMountedSkill(userRequest, isAiCodingMode ? 4 : 6))
                : Task.FromResult<SkillMatch?>(null)
            : Task.FromResult<SkillMatch?>(new SkillMatch(selectedMountedSkill, int.MaxValue, ExtractSkillDescription(selectedMountedSkill.Text)));
        var structuredPromptTask = Task.Run(() => _promptStructuringService.Structure(request).Text);
        var baseLocalPrompt = BuildModeAwarePrompt(userRequest, await structuredPromptTask);
        var matchedSkill = await matchedSkillTask;
        var localPrompt = matchedSkill is null
            ? baseLocalPrompt
            : BuildMountedSkillExecutionPrompt(userRequest, matchedSkill.Value.Template, baseLocalPrompt);
        var selectedOptimizationTarget = GetSelectedOptimizationTarget();
        var finalPrompt = localPrompt;
        var fallbackQuestionSource = matchedSkill is null ? localPrompt : baseLocalPrompt;
        PromptProtocolResult protocolResult = new(localPrompt, BuildLocalFollowUpQuestion(fallbackQuestionSource), BuildLocalMissingItems(fallbackQuestionSource), false, null);
        LlmRequestOptions? llmOptions = null;
        var canSyncEnglishWithModel = false;
        var deepThinkingEnabled = IsDeepThinkingEnabled();

        SceneText.Text = FormatSceneText(scene);

        try
        {
            var loaded = await settingsLoadTask;
            _settings = loaded.Settings;
            llmOptions = loaded.Options;
            if (llmOptions.IsConfigured && _settings.Privacy.ModelExternalRequestsEnabled)
            {
                var reasoningCapability = ResolveReasoningCapability(llmOptions.ProviderId, llmOptions.BaseUrl, llmOptions.Model);
                var reasoningOptions = BuildReasoningOptions(deepThinkingEnabled, reasoningCapability);
                var modelInstruction = BuildProtocolModelPrompt(userRequest, userReply, diffBasePrompt, localPrompt, isTextToImageMode, isVideoMode, matchedSkill?.Template, deepThinkingEnabled, reasoningCapability);
                var redactBeforeSend = _settings.Privacy.RedactBeforeModelSend;
                var promptToSend = redactBeforeSend
                    ? _sensitiveTextRedactor.Redact(modelInstruction)
                    : modelInstruction;
                var imagesToSend = GetModelImagesToSend(_settings);
                var imageFileNames = imagesToSend.Select(image => image.FileName).ToArray();
                var providerName = DetectProviderName(llmOptions.BaseUrl, llmOptions.Model);
                try
                {
                    var modelResult = await _llmClient.CompleteWithResultAsync(new LlmRequest(promptToSend, imagesToSend, reasoningOptions), llmOptions);
                    await Task.Run(() => _modelSendAuditService.Save(
                        providerName,
                        llmOptions.Model,
                        llmOptions.BaseUrl,
                        promptToSend,
                        redactBeforeSend,
                        imageFileNames,
                        succeeded: true,
                        error: null));
                    var normalized = await Task.Run(() =>
                    {
                        var parsed = ParsePromptProtocol(modelResult.Content, localPrompt);
                        if (deepThinkingEnabled)
                        {
                            var thinkingText = BuildThinkingBubbleText(modelResult.ReasoningContent, parsed.Thinking, reasoningCapability)
                                ?? BuildDerivedThinkingBubbleText(parsed, userRequest, reasoningCapability);
                            parsed = parsed with
                            {
                                Thinking = thinkingText
                            };
                        }

                        var cleanedPrompt = StripPromptMarkdown(parsed.Prompt);
                        return (Protocol: parsed with { Prompt = cleanedPrompt }, Prompt: cleanedPrompt);
                    });

                    protocolResult = normalized.Protocol;
                    finalPrompt = normalized.Prompt;
                    canSyncEnglishWithModel = true;
                    SetStatus(matchedSkill is null
                        ? BuildModelCallStatus(redactBeforeSend, deepThinkingEnabled ? reasoningCapability.ModelTag : null, imagesToSend.Count)
                        : $"{L("已按挂载 Skill 生成提示词：")}{matchedSkill.Value.Template.Title}");
                }
                catch (Exception ex)
                {
                    await Task.Run(() => _modelSendAuditService.Save(
                        providerName,
                        llmOptions.Model,
                        llmOptions.BaseUrl,
                        promptToSend,
                        redactBeforeSend,
                        imageFileNames,
                        succeeded: false,
                        error: ex.Message));
                    throw;
                }
            }
            else
            {
                SetStatus(matchedSkill is null
                    ? (llmOptions.Enabled ? "模型配置不完整，已使用本地结构化" : "模型未启用，已使用本地结构化")
                    : "模型不可用，已输出 Skill 生成草稿");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"{L("模型调用失败，已使用本地结构化：")}{ex.Message}");
        }

        var cleaned = await Task.Run(() =>
        {
            var cleanedPrompt = StripPromptMarkdown(finalPrompt);
            return (Protocol: protocolResult with { Prompt = cleanedPrompt }, Prompt: cleanedPrompt);
        });
        protocolResult = cleaned.Protocol;
        finalPrompt = cleaned.Prompt;

        AddProtocolAssistantMessage(protocolResult, animate: true);
        var outputToken = ResetOutputTypewriter();
        var chineseAnimation = SetChineseOutputWithTypewriterAsync(finalPrompt, outputToken);
        var englishResultTask = BuildSynchronizedEnglishPromptForGenerationAsync(finalPrompt, llmOptions, canSyncEnglishWithModel, isTextToImageMode, isVideoMode, isAiCodingMode, isAcademicHumanizeMode, selectedOptimizationTarget, _settings);
        await UpdatePromptDiffAsync(diffBasePrompt, finalPrompt);
        var englishResult = await englishResultTask;
        var englishPrompt = await Task.Run(() => StripPromptMarkdown(englishResult.Text));
        if (!string.IsNullOrWhiteSpace(englishResult.Status))
        {
            SetStatus(englishResult.Status);
        }

        await chineseAnimation;
        await SetEnglishOutputWithTypewriterAsync(englishPrompt, outputToken);

        var historyUserRequest = BuildHistoryUserRequest(userRequest);
        var conversationSnapshot = _conversationMessages.ToArray();
        var historyId = _currentConversationHistoryId;
        var selectedScenes = FormatSelectedScenes();
        var effectiveMode = GetEffectiveMode();
        try
        {
            var historyItem = await Task.Run(() =>
            {
                _promptVersionService.Save(userRequest, diffBasePrompt, finalPrompt, englishPrompt, selectedScenes, effectiveMode);
                return _historyService.Save(
                    historyUserRequest,
                    finalPrompt,
                    englishPrompt,
                    selectedScenes,
                    effectiveMode,
                    conversationSnapshot,
                    historyId);
            });
            _currentConversationHistoryId = historyItem.Id;
            RefreshHistoryUi();
        }
        catch (Exception ex)
        {
            SetStatus($"{L("保存历史失败：")}{ex.Message}");
        }
    }

    private async void ChatSendButton_Click(object sender, RoutedEventArgs e)
    {
        var addition = GetUserInput().Trim();
        var current = GetChineseOutput().Trim();
        if (string.IsNullOrWhiteSpace(current))
        {
            current = CurrentPromptBox.Text.Trim();
        }

        if (string.IsNullOrWhiteSpace(addition) && string.IsNullOrWhiteSpace(current))
        {
            SetStatus("先输入要修改的需求");
            return;
        }

        var mergedRequest = string.IsNullOrWhiteSpace(current)
            ? addition
            : $"上一版提示词：{current}{Environment.NewLine}{Environment.NewLine}用户补充：{FallbackText(addition, "请继续完善并追问缺失需求。")}";
        _pendingDiffBasePrompt = string.IsNullOrWhiteSpace(current) ? null : current;
        _pendingUserReply = addition;

        if (!string.IsNullOrWhiteSpace(addition))
        {
            AddChatMessage(addition, isUser: true);
        }

        SetUserInput(string.Empty);
        await GenerateOptimizedPromptAsync(mergedRequest);
    }

    private void SendKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ChatSendButton_Click(sender, new RoutedEventArgs());
        args.Handled = true;
    }

    private void NewSessionKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        NewSessionButton_Click(sender, new RoutedEventArgs());
        args.Handled = true;
    }

    private async void CommandPaletteKeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        await ShowCommandPaletteAsync();
        args.Handled = true;
    }

    private void DeepThinkingToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _loadingSettings || _syncingDeepThinkingSelection)
        {
            return;
        }

        var enabled = sender is ToggleButton toggle
            ? toggle.IsChecked == true
            : IsDeepThinkingEnabled();
        SyncDeepThinkingToggles(enabled, sender as ToggleButton);
        SaveUiSettings();
        RefreshModelCapabilityText();
        var capability = ResolveCurrentReasoningCapability();
        SetStatus(enabled
            ? $"{L("深度思考已开启：")}{L(capability.Description)}"
            : "已关闭深度思考");
    }

    private bool IsDeepThinkingEnabled()
    {
        return DeepThinkingToggle?.IsChecked == true || CompactDeepThinkingToggle?.IsChecked == true;
    }

    private void SyncDeepThinkingToggles(bool enabled, ToggleButton? source = null)
    {
        _syncingDeepThinkingSelection = true;
        try
        {
            if (DeepThinkingToggle is not null && !ReferenceEquals(DeepThinkingToggle, source))
            {
                DeepThinkingToggle.IsChecked = enabled;
            }

            if (CompactDeepThinkingToggle is not null && !ReferenceEquals(CompactDeepThinkingToggle, source))
            {
                CompactDeepThinkingToggle.IsChecked = enabled;
            }
        }
        finally
        {
            _syncingDeepThinkingSelection = false;
        }
    }

    private static LlmReasoningOptions? BuildReasoningOptions(bool enabled, ModelReasoningCapability capability)
    {
        return enabled && capability.SupportsNativeRequest
            ? new LlmReasoningOptions(capability.ProviderId, capability.RequestKind, "high", IncludeThoughts: true)
            : null;
    }

    private string? BuildThinkingBubbleText(string? nativeReasoning, string? protocolThinking, ModelReasoningCapability capability)
    {
        var nativeBody = NormalizeThinkingText(nativeReasoning ?? string.Empty);
        var protocolBody = NormalizeThinkingText(protocolThinking ?? string.Empty);
        var useNative = HasVisibleThinkingText(nativeBody);
        var body = useNative ? nativeBody : protocolBody;
        if (!HasVisibleThinkingText(body))
        {
            return null;
        }

        var source = useNative && capability.HasNativeSupport
            ? $"{L(capability.ProviderTag)} / {L(capability.ModelTag)}"
            : L("提示词式思考摘要");
        return $"{L("来源：")}{source}{Environment.NewLine}{Environment.NewLine}{body}";
    }

    private string? BuildDerivedThinkingBubbleText(PromptProtocolResult result, string userRequest, ModelReasoningCapability capability)
    {
        var parts = new List<string>
        {
            $"{L("模型未返回可见原生思考正文，以下为客户端基于可见结果整理的处理摘要。")}",
            $"{L("当前标签：")}{L(capability.ProviderTag)} / {L(capability.ModelTag)}",
            $"{L("需求摘要：")}{OneLinePreview(userRequest, 120)}"
        };

        if (!string.IsNullOrWhiteSpace(result.Question))
        {
            parts.Add($"{L("下一步追问：")}{OneLinePreview(result.Question, 160)}");
        }

        if (!string.IsNullOrWhiteSpace(result.Missing)
            && !result.Missing.Contains("暂无明显缺失", StringComparison.Ordinal))
        {
            parts.Add($"{L("可能缺失：")}{OneLinePreview(result.Missing, 180)}");
        }

        parts.Add(L("处理方式：按当前优化目标、挂载 Skill 和模型协议生成最终提示词，并移除 Markdown 外壳。"));
        var body = string.Join(Environment.NewLine, parts.Where(HasVisibleThinkingText));
        return HasVisibleThinkingText(body)
            ? $"{L("来源：")}{L("客户端可见处理摘要")}{Environment.NewLine}{Environment.NewLine}{body}"
            : null;
    }

    private static string OneLinePreview(string? text, int maxLength)
    {
        var normalized = NormalizeThinkingText(text ?? string.Empty)
            .Replace(Environment.NewLine, " ", StringComparison.Ordinal)
            .Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    private string BuildModelCallStatus(bool redacted, string? modelTag, int imageCount)
    {
        var parts = new List<string>
        {
            L(redacted ? "已脱敏并调用模型优化" : "已调用模型优化")
        };

        if (!string.IsNullOrWhiteSpace(modelTag))
        {
            parts.Add(L(modelTag));
        }

        if (imageCount > 0)
        {
            parts.Add(L("已发送附图"));
        }

        return string.Join(L("，"), parts);
    }

    private async void RefineFromOutputButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPrompt = GetChineseOutput();
        if (string.IsNullOrWhiteSpace(currentPrompt))
        {
            SetStatus("没有可补充的优化结果");
            return;
        }

        var refinedRequest = await BuildRefinedUserRequestAsync(currentPrompt);
        _pendingDiffBasePrompt = currentPrompt;
        SetUserInput(refinedRequest);
        await GenerateOptimizedPromptAsync(refinedRequest);
    }

    private void NewSessionButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingDiffBasePrompt = null;
        _pendingUserReply = null;
        _conversationLockedMode = null;
        _conversationLockedTargetLabel = null;
        _conversationLockedCustomModeText = null;
        _currentConversationHistoryId = null;
        _conversationMessages.Clear();
        SetUserInput(string.Empty);
        SetCurrentPrompt(string.Empty);
        SetChineseOutput(string.Empty);
        SetEnglishOutput(string.Empty);
        PromptDiffPanel.Visibility = Visibility.Collapsed;
        PromptDiffBlock.Blocks.Clear();
        ClearModelImageAttachment();
        if (CompactChatStatusText is not null)
        {
            CompactChatStatusText.Text = L("新会话已开始，可以切换优化目标或 Skill。");
        }

        ChatMessagesPanel.Children.Clear();
        CompactChatMessagesPanel.Children.Clear();
        AddChatMessage(L("新的会话已开始。把你想优化的需求发给我，我会边聊天边更新当前提示词和优化后提示词。"), isUser: false);
        SetStatus("已开始新会话");
    }

    private void LockConversationTargetIfNeeded()
    {
        if (_conversationLockedMode is not null)
        {
            if (!string.Equals(_conversationLockedMode, _selectedMode, StringComparison.OrdinalIgnoreCase))
            {
                RestoreLockedConversationMode();
            }

            return;
        }

        _conversationLockedMode = _selectedMode;
        _conversationLockedTargetLabel = GetEffectiveMode();
        _conversationLockedCustomModeText = string.Equals(_selectedMode, "自定义", StringComparison.OrdinalIgnoreCase)
            ? CustomModeBox.Text.Trim()
            : null;
    }

    private bool CanUseConversationMode(string requestedMode)
    {
        return _conversationLockedMode is null
            || string.Equals(_conversationLockedMode, requestedMode, StringComparison.OrdinalIgnoreCase);
    }

    private void RejectConversationModeChange()
    {
        RestoreLockedConversationMode();
        SetStatus($"本段对话已锁定优化目标：{FallbackText(_conversationLockedTargetLabel, GetEffectiveMode())}。要切换目标请先点“新会话”。");
    }

    private void RestoreLockedConversationMode()
    {
        if (string.IsNullOrWhiteSpace(_conversationLockedMode))
        {
            return;
        }

        _selectedMode = _conversationLockedMode;
        _syncingModeSelection = true;
        try
        {
            if (string.Equals(_selectedMode, "自定义", StringComparison.OrdinalIgnoreCase)
                && _conversationLockedCustomModeText is not null
                && CustomModeBox.Text != _conversationLockedCustomModeText)
            {
                CustomModeBox.Text = _conversationLockedCustomModeText;
            }

            SetModeRadioState(_selectedMode);
            SyncCompactModeBox();
        }
        finally
        {
            _syncingModeSelection = false;
        }

        SelectTemplateSourceForMode(_selectedMode);
        RefreshModelDisplayText();
        RefreshScene();
        RefreshTemplateViews();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetChineseOutput();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("没有可复制的结果");
            return;
        }

        SetStatus(_clipboardContextService.TrySetClipboardText(text) ? "已复制中文提示词" : "剪贴板暂时被占用，请再点一次复制");
    }

    private void CopyEnglishButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EnglishOutputBox.Text))
        {
            SetStatus("没有可复制的英文提示词");
            return;
        }

        SetStatus(_clipboardContextService.TrySetClipboardText(EnglishOutputBox.Text) ? "已复制英文提示词" : "剪贴板暂时被占用，请再点一次复制");
    }

    private void FavoriteChinesePromptButton_Click(object sender, RoutedEventArgs e)
    {
        SaveOutputAsCommonPrompt("中文提示词", GetChineseOutput());
    }

    private void FavoriteEnglishPromptButton_Click(object sender, RoutedEventArgs e)
    {
        SaveOutputAsCommonPrompt("英语提示词", EnglishOutputBox.Text);
    }

    private void SaveOutputAsCommonPrompt(string label, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus($"{label}为空，无法收藏");
            return;
        }

        try
        {
            var title = $"{label} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            var item = _commonPromptService.SaveOrUpdate(null, title, text, "收藏提示词");
            RefreshCommonPromptsUi(item.Id);
            SetStatus($"已收藏到常用提示词：{item.Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"收藏失败：{ex.Message}");
        }
    }

    private async void ExpandChineseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowLargeOutputDialogAsync("中文优化提示词", GetChineseOutput(), SetChineseOutput);
    }

    private async void ExpandEnglishOutputButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowLargeOutputDialogAsync("英文翻译提示词", EnglishOutputBox.Text, SetEnglishOutput);
    }

    private async Task ShowLargeOutputDialogAsync(string title, string text, Action<string> applyText)
    {
        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Text = text,
            MinWidth = 760,
            MinHeight = 520,
            MaxHeight = 660,
            Padding = new Thickness(14)
        };
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);

        var dialog = new ContentDialog
        {
            Title = L(title),
            Content = editor,
            PrimaryButtonText = L("应用并关闭"),
            SecondaryButtonText = L("复制"),
            CloseButtonText = L("取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        dialog.SecondaryButtonClick += (_, args) =>
        {
            SetStatus(_clipboardContextService.TrySetClipboardText(editor.Text)
                ? "已复制优化后的提示词"
                : "剪贴板暂时被占用，请再点一次复制");
            args.Cancel = true;
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            applyText(editor.Text);
            SetStatus("已应用放大编辑内容");
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetChineseOutput();
        if (!string.IsNullOrWhiteSpace(EnglishOutputBox.Text))
        {
            text = $"{text}{Environment.NewLine}{Environment.NewLine}--- English Prompt ---{Environment.NewLine}{EnglishOutputBox.Text}";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("没有可导出的结果");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = "aipin-prompt";
            picker.FileTypeChoices.Add("文本文件", [".txt"]);

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                SetStatus("已取消导出");
                return;
            }

            await Windows.Storage.FileIO.WriteTextAsync(file, text);
            SetStatus($"已导出：{file.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"导出失败：{ex.Message}");
        }
    }

    private async Task<string?> PickJsonOpenPathAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickJsonSavePathAsync(string suggestedName)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = suggestedName;
        picker.FileTypeChoices.Add("JSON", [".json"]);
        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickFolderPathAsync()
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private async void NewFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveTemplateWithDialogAsync(string.Empty, null);
    }

    private async void SaveFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        var templateText = string.IsNullOrWhiteSpace(GetChineseOutput())
            ? GetUserInput()
            : GetChineseOutput();
        await SaveTemplateWithDialogAsync(templateText, null);
    }

    private async void MountSkillButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is null)
        {
            SetStatus("已取消挂载 Skill");
            return;
        }

        var skillPath = Path.Combine(folderPath, "SKILL.md");
        if (!File.Exists(skillPath))
        {
            SetStatus("未找到 SKILL.md，请选择 Skill 根目录");
            return;
        }

        try
        {
            var skillText = await File.ReadAllTextAsync(skillPath);
            if (string.IsNullOrWhiteSpace(skillText))
            {
                SetStatus("SKILL.md 为空，无法挂载");
                return;
            }

            var title = ExtractSkillTitle(skillText, Path.GetFileName(folderPath));
            var mountedText = $"""
【挂载 Skill】
来源目录：{folderPath}
Skill 文件：{skillPath}

{skillText.Trim()}
""";
            var favorite = _promptFavoriteService.SaveOrUpdate(
                null,
                mountedText,
                SkillTemplateSource,
                SkillTemplateSource,
                DetectSkillCategory(skillText),
                title);
            _selectedMountedSkillId = favorite.Id;
            SetTemplateTabState(skillSelected: true);
            RefreshFavoritesUi(favorite.Id);
            RefreshSkillManagementUi(favorite.Id);
            RefreshMountedSkillQuickList();
            RefreshSearchIndex();
            SetStatus($"已挂载 Skill：{favorite.Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"挂载 Skill 失败：{ex.Message}");
        }
    }

    private async void ImportSkillPackageButton_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = await PickFolderPathAsync();
        if (folderPath is null)
        {
            SetStatus("已取消导入 Skill 包");
            return;
        }

        try
        {
            var skillPath = FindSkillMarkdownFile(folderPath);
            if (skillPath is null)
            {
                SetStatus("未找到 SKILL.md，无法导入 Skill 包");
                return;
            }

            var skillText = await File.ReadAllTextAsync(skillPath);
            if (string.IsNullOrWhiteSpace(skillText))
            {
                SetStatus("SKILL.md 为空，无法导入 Skill 包");
                return;
            }

            var title = ExtractSkillTitle(skillText, Path.GetFileName(folderPath));
            var packageText = $"""
【Skill 包】
来源目录：{folderPath}
Skill 文件：{skillPath}
{await ReadSkillPackageManifestSummaryAsync(folderPath)}
{await ReadSkillPackageExamplesSummaryAsync(folderPath)}

{skillText.Trim()}
""";
            var favorite = _promptFavoriteService.SaveOrUpdate(
                null,
                packageText,
                SkillTemplateSource,
                SkillTemplateSource,
                "Skill 包",
                title);
            _selectedMountedSkillId = favorite.Id;
            SetTemplateTabState(skillSelected: true);
            RefreshFavoritesUi(favorite.Id);
            RefreshSkillManagementUi(favorite.Id);
            RefreshMountedSkillQuickList();
            RefreshSearchIndex();
            SetStatus($"已导入 Skill 包：{favorite.Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"导入 Skill 包失败：{ex.Message}");
        }
    }

    private static string? FindSkillMarkdownFile(string folderPath)
    {
        var direct = Path.Combine(folderPath, "SKILL.md");
        if (File.Exists(direct))
        {
            return direct;
        }

        return Directory.EnumerateFiles(folderPath, "SKILL.md", SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    private static async Task<string> ReadSkillPackageManifestSummaryAsync(string folderPath)
    {
        var manifestPath = Path.Combine(folderPath, "skill.json");
        if (!File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(folderPath, "aipin-skill.json");
        }

        if (!File.Exists(manifestPath))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var root = document.RootElement;
            var builder = new StringBuilder();
            builder.AppendLine("包清单：");
            foreach (var name in new[] { "name", "description", "version", "author", "license" })
            {
                if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
                {
                    builder.AppendLine($"{name}: {property.GetString()}");
                }
            }

            return builder.ToString().TrimEnd();
        }
        catch
        {
            return "包清单：skill.json 存在，但解析失败。";
        }
    }

    private static async Task<string> ReadSkillPackageExamplesSummaryAsync(string folderPath)
    {
        var examplesDirectory = Path.Combine(folderPath, "examples");
        if (!Directory.Exists(examplesDirectory))
        {
            return string.Empty;
        }

        var files = Directory.EnumerateFiles(examplesDirectory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetExtension(path) is ".md" or ".txt")
            .Take(3)
            .ToArray();
        if (files.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("示例文件：");
        foreach (var file in files)
        {
            var text = await File.ReadAllTextAsync(file);
            var preview = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (preview.Length > 220)
            {
                preview = $"{preview[..220]}...";
            }

            builder.AppendLine($"- {Path.GetFileName(file)}：{preview}");
        }

        return builder.ToString().TrimEnd();
    }

    private async Task SaveTemplateWithDialogAsync(string initialText, PromptTemplateCatalogItem? existing)
    {
        var selectedCategory = GetChoiceValue(TemplateCategoryFilterBox.SelectedItem);
        if (string.IsNullOrWhiteSpace(selectedCategory) || selectedCategory == "全部")
        {
            selectedCategory = "未分类";
        }

        if (existing is { IsUserTemplate: false })
        {
            SetStatus("内置模板不能编辑");
            return;
        }

        var titleBox = new TextBox
        {
            Header = L("标题"),
            PlaceholderText = L("例如：角色定位"),
            Text = existing?.Title ?? string.Empty
        };
        var categoryBox = new TextBox
        {
            Header = L("分类"),
            PlaceholderText = L("例如：UI 设计"),
            Text = existing?.Category ?? selectedCategory
        };
        var contentBox = new TextBox
        {
            Header = L("需求 / 模板内容"),
            PlaceholderText = L("例如：流畅设计"),
            Text = existing?.Text ?? initialText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 180,
            MaxHeight = 320
        };
        var dialog = new ContentDialog
        {
            Title = L(existing is null ? "保存到我的模板" : "编辑模板"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    titleBox,
                    categoryBox,
                    contentBox
                }
            },
            PrimaryButtonText = L("保存"),
            CloseButtonText = L("取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(contentBox.Text))
            {
                args.Cancel = true;
                SetStatus("请先填写模板内容");
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            SetStatus(existing is null ? "已取消保存模板" : "已取消编辑模板");
            return;
        }

        try
        {
            var favorite = _promptFavoriteService.SaveOrUpdate(
                existing?.Id,
                contentBox.Text,
                FormatSelectedScenes(),
                "我的模板",
                categoryBox.Text,
                string.IsNullOrWhiteSpace(titleBox.Text) ? BuildFavoriteTitle(contentBox.Text, categoryBox.Text) : titleBox.Text);
            RefreshFavoritesUi(favorite.Id);
            RefreshSearchIndex();
            SetStatus(existing is null ? $"已保存为模板：{favorite.Title}" : $"已更新模板：{favorite.Title}");
        }
        catch (Exception ex)
        {
            SetStatus($"保存模板失败：{ex.Message}");
        }
    }

    private async void FavoritesBox_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_syncingTemplateSelection || e.ClickedItem is not PromptTemplateCatalogItem template)
        {
            return;
        }

        FavoritesBox.SelectedItem = template;
        await InsertFavoriteTemplateAsync(template);
    }

    private void FavoritesBox_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var template = FindDataContext<PromptTemplateCatalogItem>(e.OriginalSource as DependencyObject);
        if (template is null)
        {
            return;
        }

        FavoritesBox.SelectedItem = template;

        var flyout = new MenuFlyout();
        var editItem = new MenuFlyoutItem
        {
            Text = L("编辑"),
            IsEnabled = template.IsUserTemplate
        };
        editItem.Click += async (_, _) => await SaveTemplateWithDialogAsync(template.Text, template);
        var deleteItem = new MenuFlyoutItem
        {
            Text = L("删除"),
            IsEnabled = template.IsUserTemplate
        };
        deleteItem.Click += (_, _) => DeleteFavoriteTemplate(template);
        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        flyout.ShowAt(FavoritesBox, e.GetPosition(FavoritesBox));
        e.Handled = true;
    }

    private async void LoadFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesBox.SelectedItem is not PromptTemplateCatalogItem template)
        {
            SetStatus("请选择要载入的模板");
            return;
        }

        await InsertFavoriteTemplateAsync(template);
    }

    private async Task InsertFavoriteTemplateAsync(PromptTemplateCatalogItem template)
    {
        var text = await ResolveTemplateVariablesAsync(template);
        if (text is null)
        {
            SetStatus("已取消插入模板");
            return;
        }

        SetUserInput(AppendLine(GetUserInput(), text));
        ShowPage("Home");
        SetStatus($"已插入模板：{template.Title}");
    }

    private async Task<string?> ResolveTemplateVariablesAsync(PromptTemplateCatalogItem template)
    {
        var variables = ExtractTemplateVariables(template.Text);
        if (variables.Count == 0)
        {
            return template.Text.Trim();
        }

        var inputBoxes = variables
            .Select(variable => new TextBox
            {
                Header = variable,
                PlaceholderText = $"填写 {variable}",
                MinWidth = 480
            })
            .ToArray();
        var stack = new StackPanel
        {
            Spacing = 10
        };
        stack.Children.Add(new TextBlock
        {
            Text = $"模板包含 {variables.Count} 个变量，填写后会插入到用户需求。",
            TextWrapping = TextWrapping.Wrap
        });
        foreach (var box in inputBoxes)
        {
            stack.Children.Add(box);
        }

        var dialog = new ContentDialog
        {
            Title = $"填写模板变量：{template.Title}",
            Content = new ScrollViewer
            {
                Content = stack,
                MaxHeight = 560,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            PrimaryButtonText = L("插入"),
            CloseButtonText = L("取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var values = variables.Zip(inputBoxes, (variable, box) => (variable, value: box.Text.Trim()))
            .ToDictionary(pair => pair.variable, pair => pair.value, StringComparer.OrdinalIgnoreCase);
        var resolved = TemplateVariableRegex.Replace(template.Text, match =>
        {
            var name = match.Groups["name"].Value.Trim();
            return values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : match.Value;
        });

        return resolved.Trim();
    }

    private static IReadOnlyList<string> ExtractTemplateVariables(string text)
    {
        return TemplateVariableRegex.Matches(text)
            .Select(match => match.Groups["name"].Value.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
    }

    private void DeleteFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (FavoritesBox.SelectedItem is not PromptTemplateCatalogItem template)
        {
            SetStatus("请选择要删除的模板");
            return;
        }

        DeleteFavoriteTemplate(template);
    }

    private void DeleteFavoriteTemplate(PromptTemplateCatalogItem template)
    {
        if (!template.IsUserTemplate)
        {
            SetStatus("内置模板不能删除");
            return;
        }

        if (_promptFavoriteService.Delete(template.Id))
        {
            RefreshFavoritesUi();
            RefreshSearchIndex();
            SetStatus("已删除模板");
            return;
        }

        SetStatus("删除模板失败");
    }

    private static string BuildFavoriteTitle(string text, string category)
    {
        var normalizedText = text.Replace("\r", " ").Replace("\n", " ").Trim();
        while (normalizedText.Contains("  ", StringComparison.Ordinal))
        {
            normalizedText = normalizedText.Replace("  ", " ");
        }

        var normalizedCategory = string.IsNullOrWhiteSpace(category) ? "模板" : category.Trim();
        if (normalizedText.Length == 0)
        {
            return normalizedCategory;
        }

        var summary = normalizedText.Length <= 18 ? normalizedText : $"{normalizedText[..18]}...";
        return $"{normalizedCategory} · {summary}";
    }

    private static string ExtractSkillTitle(string skillText, string fallback)
    {
        var lines = skillText.Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return NormalizeShortTitle(line[2..], fallback);
            }

            if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeShortTitle(line[5..].Trim().Trim('"', '\''), fallback);
            }
        }

        return NormalizeShortTitle(fallback, "Skill");
    }

    private static string DetectSkillCategory(string skillText)
    {
        if (skillText.Contains("Claude", StringComparison.OrdinalIgnoreCase))
        {
            return "Claude Skill";
        }

        if (skillText.Contains("Antigravity", StringComparison.OrdinalIgnoreCase)
            || skillText.Contains("反重力", StringComparison.Ordinal))
        {
            return "Antigravity Rules";
        }

        if (skillText.Contains("Codex", StringComparison.OrdinalIgnoreCase)
            || skillText.Contains("SKILL.md", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex Skill";
        }

        return "挂载 Skill";
    }

    private static string NormalizeShortTitle(string? value, string fallback)
    {
        var title = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return title.Length <= 32 ? title : $"{title[..32]}...";
    }

    private SkillMatch? FindMatchingMountedSkill(string userRequest, int minimumScore)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return null;
        }

        var candidates = GetTemplateCatalogForSource(SkillTemplateSource)
            .Where(IsMountedSkillTemplate)
            .Select(template => new SkillMatch(template, ScoreMountedSkill(template, userRequest), ExtractSkillDescription(template.Text)))
            .Where(match => match.Score >= minimumScore)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Template.Title)
            .ToArray();

        return candidates.FirstOrDefault() is { Score: > 0 } match ? match : null;
    }

    private static int ScoreMountedSkill(PromptTemplateCatalogItem template, string userRequest)
    {
        var normalizedRequest = NormalizeForSkillMatch(userRequest);
        var score = 0;
        foreach (var term in ExtractSkillTerms(template).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var normalizedTerm = NormalizeForSkillMatch(term);
            if (normalizedTerm.Length < 2)
            {
                continue;
            }

            if (normalizedRequest.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase))
            {
                score += normalizedTerm.Length >= 4 ? 4 : 2;
            }

            score += ScoreSkillTermFragments(normalizedRequest, normalizedTerm);
        }

        if (normalizedRequest.Contains(NormalizeForSkillMatch(template.Title), StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(template.Category)
            && normalizedRequest.Contains(NormalizeForSkillMatch(template.Category), StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }

        return score;
    }

    private static int ScoreSkillTermFragments(string normalizedRequest, string normalizedTerm)
    {
        if (normalizedTerm.Length < 4)
        {
            return 0;
        }

        var score = 0;
        for (var length = 4; length >= 2; length--)
        {
            for (var i = 0; i <= normalizedTerm.Length - length; i++)
            {
                var fragment = normalizedTerm.Substring(i, length);
                if (IsGenericSkillFragment(fragment))
                {
                    continue;
                }

                if (normalizedRequest.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    score += length == 4 ? 3 : length == 3 ? 2 : 1;
                }
            }
        }

        return Math.Min(score, 10);
    }

    private static bool IsGenericSkillFragment(string fragment)
    {
        return fragment is "用户" or "任务" or "使用" or "输出" or "生成" or "提示" or "需要" or "处理" or "内容" or "格式" or "工作" or "流程" or "要求" or "目标" or "场景" or "文件" or "目录" or "Skill";
    }

    private static IEnumerable<string> ExtractSkillTerms(PromptTemplateCatalogItem template)
    {
        yield return template.Title;
        yield return template.Category;

        foreach (var line in template.Text.Replace("\r", string.Empty).Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal)
                || line.StartsWith("name:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("description:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("触发", StringComparison.Ordinal)
                || line.Contains("关键词", StringComparison.Ordinal)
                || line.Contains("适用", StringComparison.Ordinal)
                || line.Contains("场景", StringComparison.Ordinal))
            {
                foreach (var token in SplitSkillTerms(line))
                {
                    yield return token;
                }
            }
        }
    }

    private static IEnumerable<string> SplitSkillTerms(string text)
    {
        var normalized = text
            .Replace("【", " ", StringComparison.Ordinal)
            .Replace("】", " ", StringComparison.Ordinal)
            .Replace("：", " ", StringComparison.Ordinal)
            .Replace(":", " ", StringComparison.Ordinal)
            .Replace("，", " ", StringComparison.Ordinal)
            .Replace(",", " ", StringComparison.Ordinal)
            .Replace("/", " ", StringComparison.Ordinal)
            .Replace("|", " ", StringComparison.Ordinal)
            .Replace("、", " ", StringComparison.Ordinal)
            .Replace("[", " ", StringComparison.Ordinal)
            .Replace("]", " ", StringComparison.Ordinal)
            .Replace("{", " ", StringComparison.Ordinal)
            .Replace("}", " ", StringComparison.Ordinal);

        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2
                && !string.Equals(token, "Skill", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(token, "SKILL.md", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(token, "description", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(token, "name", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractSkillDescription(string skillText)
    {
        foreach (var line in skillText.Replace("\r", string.Empty).Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                return line[12..].Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeForSkillMatch(string text)
    {
        return text.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private void ContinueHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not PromptHistoryItem item)
        {
            SetStatus("请选择要继续编辑的历史记录");
            return;
        }

        ApplyHistoryItemToWorkbench(item, lockConversation: true);
        SetStatus($"已进入历史继续编辑：{item.Title}");
    }

    private void ApplyHistoryItemToWorkbench(PromptHistoryItem item, bool lockConversation)
    {
        ApplyHistoryMode(item, lockConversation);
        _pendingDiffBasePrompt = item.ChinesePrompt;
        _pendingUserReply = null;
        _currentConversationHistoryId = item.Id;
        SetUserInput(string.Empty);
        SetCurrentPrompt(item.ChinesePrompt);
        SetChineseOutput(item.ChinesePrompt);
        SetEnglishOutput(item.EnglishPrompt);
        PromptDiffPanel.Visibility = Visibility.Collapsed;
        PromptDiffBlock.Blocks.Clear();
        ChatMessagesPanel.Children.Clear();
        CompactChatMessagesPanel.Children.Clear();
        _conversationMessages.Clear();
        var messages = item.Messages?.Where(message => !string.IsNullOrWhiteSpace(message.Text)).ToArray() ?? [];
        if (messages.Length > 0)
        {
            foreach (var message in messages)
            {
                AddChatMessage(message.Text, message.IsUser, record: false, createdAt: message.CreatedAt, messageKind: message.Role);
                _conversationMessages.Add(message);
            }
        }

        ShowPage("Home");
    }

    private void ApplyHistoryMode(PromptHistoryItem item, bool lockConversation)
    {
        var mode = ResolveModeFromHistory(item.Mode);
        _conversationLockedMode = null;
        _conversationLockedTargetLabel = null;
        _conversationLockedCustomModeText = null;
        ApplySelectedMode(mode, save: false);
        if (lockConversation)
        {
            LockConversationTargetIfNeeded();
        }
    }

    private string ResolveModeFromHistory(string mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "通用 LLM" : mode.Trim();
        var builtInModes = new[] { "通用 LLM", "论文去AI味", "文生图", "即梦", "Veo 3", "AI编程" };
        var builtIn = builtInModes.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        if (builtIn is not null)
        {
            return builtIn;
        }

        var target = _optimizationTargetService.Load()
            .FirstOrDefault(item => string.Equals(item.Title, normalized, StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.Id, normalized, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
        {
            return MakeOptimizationTargetMode(target.Id);
        }

        _syncingModeSelection = true;
        try
        {
            CustomModeBox.Text = normalized;
        }
        finally
        {
            _syncingModeSelection = false;
        }

        return "自定义";
    }

    private void ApplySelectedMode(string mode, bool save)
    {
        _selectedMode = mode;
        _syncingModeSelection = true;
        try
        {
            SetModeRadioState(_selectedMode);
            SyncCompactModeBox();
        }
        finally
        {
            _syncingModeSelection = false;
        }

        SelectTemplateSourceForMode(_selectedMode);
        RefreshModelDisplayText();
        RefreshScene();
        RefreshTemplateViews();
        if (save)
        {
            SaveUiSettings();
        }
    }

    private void DeleteHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is not PromptHistoryItem item)
        {
            SetStatus("请选择要删除的历史记录");
            return;
        }

        if (_historyService.Delete(item.Id))
        {
            if (string.Equals(_currentConversationHistoryId, item.Id, StringComparison.OrdinalIgnoreCase))
            {
                _currentConversationHistoryId = null;
            }

            RefreshHistoryUi();
            SetStatus("已删除历史记录");
        }
    }

    private async void ShowSendAuditButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowSendAuditDialogAsync();
    }

    private async Task ShowSendAuditDialogAsync()
    {
        var audits = _modelSendAuditService.Load();
        if (audits.Count == 0)
        {
            SetStatus("还没有模型发送记录");
            return;
        }

        var list = new ListView
        {
            ItemsSource = audits,
            SelectionMode = ListViewSelectionMode.Single,
            MinWidth = 320,
            MaxHeight = 480
        };
        var detailBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 660,
            MinHeight = 420,
            MaxHeight = 520
        };
        ScrollViewer.SetVerticalScrollBarVisibility(detailBox, ScrollBarVisibility.Auto);
        list.SelectionChanged += (_, _) =>
        {
            detailBox.Text = list.SelectedItem is ModelSendAuditItem item ? BuildModelSendAuditDetail(item) : string.Empty;
        };
        list.SelectedIndex = 0;

        var dialog = new ContentDialog
        {
            Title = L("发送审计"),
            Content = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(340) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                Children =
                {
                    list,
                    detailBox
                }
            },
            PrimaryButtonText = L("复制发送文本"),
            SecondaryButtonText = L("清空记录"),
            CloseButtonText = L("关闭"),
            XamlRoot = Content.XamlRoot
        };
        Grid.SetColumn(detailBox, 1);
        dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            if (list.SelectedItem is not ModelSendAuditItem item)
            {
                SetStatus("请选择发送记录");
                return;
            }

            SetStatus(_clipboardContextService.TrySetClipboardText(item.TextSent)
                ? "已复制发送文本"
                : "剪贴板暂时被占用，请再点一次复制");
        };
        dialog.SecondaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            _modelSendAuditService.Clear();
            audits = _modelSendAuditService.Load();
            list.ItemsSource = audits;
            detailBox.Text = string.Empty;
            SetStatus("已清空发送审计记录");
        };

        await dialog.ShowAsync();
    }

    private static string BuildModelSendAuditDetail(ModelSendAuditItem item)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{item.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"状态：{(item.Succeeded ? "成功" : "失败")}");
        builder.AppendLine($"Provider：{item.Provider}");
        builder.AppendLine($"模型：{item.Model}");
        builder.AppendLine($"Base URL：{item.BaseUrl}");
        builder.AppendLine($"脱敏：{(item.Redacted ? "是" : "否")}");
        builder.AppendLine($"附图：{item.ImageCount}");
        if (item.ImageFileNames.Count > 0)
        {
            builder.AppendLine($"附件名：{string.Join(", ", item.ImageFileNames)}");
        }

        if (!string.IsNullOrWhiteSpace(item.Error))
        {
            builder.AppendLine($"错误：{item.Error}");
        }

        builder.AppendLine();
        builder.AppendLine("发送文本：");
        builder.AppendLine(item.TextSent);
        return builder.ToString();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SetUserInput(string.Empty);
        SetChineseOutput(string.Empty);
        SetEnglishOutput(string.Empty);
        ContextBox.Text = string.Empty;
        _contextSource = null;
        _contextFieldName = null;
        ClearModelImageAttachment();
        SetStatus(string.Empty);
        InputBox.Focus(FocusState.Programmatic);
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady || _syncingModeSelection)
        {
            return;
        }

        InputCountText.Text = $"{InputBox.Text.Length} / 1000";
        UpdateInputBoxHeights(InputBox.Text);
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        ExpandedInputBox.Text = InputBox.Text;
        ExpandedInputCountText.Text = $"{ExpandedInputBox.Text.Length}/1000";
        _syncingText = false;
    }

    private void ExpandedInputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        ExpandedInputCountText.Text = $"{ExpandedInputBox.Text.Length}/1000";
        UpdateInputBoxHeights(ExpandedInputBox.Text);
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        InputBox.Text = ExpandedInputBox.Text;
        InputCountText.Text = $"{InputBox.Text.Length} / 1000";
        _syncingText = false;
    }

    private void OutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        OutputCountText.Text = $"{OutputBox.Text.Length} / 12000";
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        ExpandedOutputBox.Text = OutputBox.Text;
        UpdateChineseOutputCounts();
        _syncingText = false;
    }

    private void ExpandedOutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        UpdateChineseOutputCounts();
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        OutputBox.Text = ExpandedOutputBox.Text;
        OutputCountText.Text = $"{OutputBox.Text.Length} / 12000";
        _syncingText = false;
    }

    private void EnglishOutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        UpdateEnglishCounts(EnglishOutputBox.Text);
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        CompactEnglishOutputBox.Text = EnglishOutputBox.Text;
        _syncingText = false;
    }

    private void CompactEnglishOutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        UpdateEnglishCounts(CompactEnglishOutputBox.Text);
        if (_syncingText)
        {
            return;
        }

        _syncingText = true;
        EnglishOutputBox.Text = CompactEnglishOutputBox.Text;
        _syncingText = false;
    }

    private async void ReadClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        SetStatus("正在导入剪贴板...");
        try
        {
            var text = await _clipboardContextService.ReadClipboardTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                SetStatus("没有读取到剪贴板文本");
                return;
            }

            SetUserInput(string.IsNullOrWhiteSpace(GetUserInput()) ? text.Trim() : GetUserInput());
            SetContextText(text, "剪贴板", "已导入剪贴板文本");
        }
        catch (Exception ex)
        {
            SetStatus($"导入剪贴板失败：{ex.Message}");
        }
    }

    private async void InputTextBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Bitmap))
        {
            return;
        }

        e.Handled = true;
        _settings = _settingsService.Load();
        try
        {
            var bitmapReference = await content.GetBitmapAsync();
            using var stream = await bitmapReference.OpenReadAsync();
            var bytes = await ReadAllBytesAsync(stream);
            _modelImageAttachment = CreateModelImageAttachment(bytes, "pasted-image.png", "image/png");
            await SetImagePreviewAsync(bytes, "已粘贴图片：pasted-image.png");
            SetStatus(_settings.Privacy.ModelImageExternalRequestsEnabled
                ? "已从剪贴板粘贴图片并作为模型附图"
                : "已粘贴图片；图片外发当前关闭，优化时不会发送图片");
        }
        catch (Exception ex)
        {
            SetStatus($"粘贴图片失败：{ex.Message}");
        }
    }

    private async void ReadImageOcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOcrEnabled())
        {
            return;
        }

        SetStatus("正在选择图片...");
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" })
            {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                SetStatus("已取消图片 OCR");
                return;
            }

            SetStatus("正在识别图片文字...");
            var routeResult = await RecognizeImageFileAsync(file);
            await SetImagePreviewAsync(file, $"已 OCR 图片：{file.Name}");
            SetOcrContextText(routeResult, "OCR 图片", "已识别图片文字", appendToUserInput: true);
        }
        catch (Exception ex)
        {
            SetStatus($"图片 OCR 失败：{ex.Message}");
        }
    }

    private async void ReadWindowOcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOcrEnabled())
        {
            return;
        }

        if (_lastForegroundWindow == 0)
        {
            SetStatus("没有记录到目标窗口，请用快捷键呼出后再试");
            return;
        }

        SetStatus("正在截取目标窗口...");
        try
        {
            using var bitmap = _windowCaptureService.CaptureWindow(_lastForegroundWindow);
            SetStatus("正在识别窗口文字...");
            var routeResult = await RecognizeSoftwareBitmapAsync(bitmap);
            SetOcrContextText(routeResult, "OCR 当前窗口", "已识别窗口文字");
        }
        catch (Exception ex)
        {
            SetStatus($"窗口 OCR 失败：{ex.Message}");
        }
    }

    private async void ReadRegionOcrButton_Click(object sender, RoutedEventArgs e)
    {
        if (!EnsureOcrEnabled())
        {
            return;
        }

        SetStatus("正在准备区域 OCR...");
        try
        {
            HideWindow();
            await WaitForScreenCaptureSettleAsync();

            using var capture = _windowCaptureService.CaptureVirtualScreen();
            var selector = new RegionSelectionWindow(capture.Bitmap, capture.Bounds);
            selector.Activate();
            var selection = await selector.SelectionTask;
            if (selection is null)
            {
                Activate();
                SetStatus("已取消区域 OCR");
                return;
            }

            using var cropped = _windowCaptureService.Crop(capture.Bitmap, selection.Value);
            Activate();
            SetStatus("正在识别区域文字...");
            var routeResult = await RecognizeSoftwareBitmapAsync(cropped);
            SetOcrContextText(routeResult, "OCR 区域截图", "已识别区域文字", appendToUserInput: true);
        }
        catch (Exception ex)
        {
            Activate();
            SetStatus($"区域 OCR 失败：{ex.Message}");
        }
    }

    private async void AttachModelImageButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = _settingsService.Load();

        SetStatus("正在选择图片...");
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                picker.FileTypeFilter.Add(extension);
            }

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                SetStatus("已取消附图");
                return;
            }

            _modelImageAttachment = await LoadModelImageAttachmentAsync(file);
            await SetImagePreviewAsync(file, $"已选择图片：{file.Name}");
            SetStatus(_settings.Privacy.ModelImageExternalRequestsEnabled
                ? $"已附图给模型：{_modelImageAttachment.FileName}（优化时会外发）"
                : $"已选择图片：{_modelImageAttachment.FileName}；图片外发当前关闭，优化时不会发送图片");
        }
        catch (Exception ex)
        {
            SetStatus($"附图失败：{ex.Message}");
        }
    }

    private async void AttachScreenshotToModelButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = _settingsService.Load();
        SetStatus("正在选择截图区域...");
        try
        {
            HideWindow();
            await WaitForScreenCaptureSettleAsync();

            using var capture = _windowCaptureService.CaptureVirtualScreen();
            var selector = new RegionSelectionWindow(capture.Bitmap, capture.Bounds);
            selector.Activate();
            var selection = await selector.SelectionTask;
            if (selection is null)
            {
                Activate();
                SetStatus("已取消截图附图");
                return;
            }

            using var cropped = _windowCaptureService.Crop(capture.Bitmap, selection.Value);
            Activate();
            var bytes = await EncodeSoftwareBitmapToPngAsync(cropped);
            _modelImageAttachment = CreateModelImageAttachment(bytes, "screenshot.png", "image/png");
            await SetImagePreviewAsync(bytes, "已选择截图：screenshot.png");
            SetStatus(_settings.Privacy.ModelImageExternalRequestsEnabled
                ? "已将截图作为模型附图（优化时会外发）"
                : "已选择截图；图片外发当前关闭，优化时不会发送图片");
        }
        catch (Exception ex)
        {
            Activate();
            SetStatus($"截图附图失败：{ex.Message}");
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SetWindowMode(expanded: true);
        ShowPage("Settings");
    }

    private async void CommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowCommandPaletteAsync();
    }

    private async Task ShowCommandPaletteAsync()
    {
        var searchBox = new TextBox
        {
            PlaceholderText = L("搜索命令、模板、Skill 或页面"),
            MinWidth = 560
        };
        var list = new ListView
        {
            MaxHeight = 420,
            SelectionMode = ListViewSelectionMode.Single
        };
        var entries = BuildCommandPaletteEntries();

        void ApplyFilter()
        {
            var query = searchBox.Text.Trim();
            var filtered = string.IsNullOrWhiteSpace(query)
                ? entries.Take(60).ToArray()
                : entries
                    .Where(entry => entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                        || entry.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Concat(BuildCommandPaletteSearchEntries(query))
                    .GroupBy(entry => $"{entry.Title}\u001f{entry.Subtitle}", StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Take(60)
                    .ToArray();
            list.ItemsSource = filtered;
            list.SelectedIndex = filtered.Length > 0 ? 0 : -1;
        }

        searchBox.TextChanged += (_, _) => ApplyFilter();
        ApplyFilter();

        var dialog = new ContentDialog
        {
            Title = L("命令面板"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    searchBox,
                    list
                }
            },
            PrimaryButtonText = L("执行"),
            CloseButtonText = L("取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (list.SelectedItem is not CommandPaletteItem)
            {
                args.Cancel = true;
                SetStatus("请选择要执行的命令");
            }
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary
            && list.SelectedItem is CommandPaletteItem selected)
        {
            await selected.Execute();
        }
    }

    private IReadOnlyList<CommandPaletteItem> BuildCommandPaletteEntries()
    {
        var entries = new List<CommandPaletteItem>
        {
            new("首页", "切换到主工作台", () => { ShowPage("Home"); return Task.CompletedTask; }),
            new("历史记录", "恢复历史会话并继续编辑", () => { ShowPage("History"); return Task.CompletedTask; }),
            new("我的模板", "管理用户模板", () => { ShowPage("Templates"); return Task.CompletedTask; }),
            new("Skill 管理", "挂载、导入、编辑和删除 Skill", () => { ShowPage("Skills"); return Task.CompletedTask; }),
            new("常用提示词", "管理常用提示词", () => { ShowPage("Snippets"); return Task.CompletedTask; }),
            new("模型管理", "配置模型 Provider", () => { ShowPage("Models"); return Task.CompletedTask; }),
            new("新会话", "清空当前会话和输出", () => { NewSessionButton_Click(this, new RoutedEventArgs()); return Task.CompletedTask; }),
            new("生成优化提示词", "使用当前需求生成提示词", async () => await GenerateOptimizedPromptAsync(null)),
            new("挂载 Skill", "选择包含 SKILL.md 的目录", () => { MountSkillButton_Click(this, new RoutedEventArgs()); return Task.CompletedTask; }),
            new("导入 Skill 包", "导入包含 SKILL.md 和可选清单/示例的包", () => { ImportSkillPackageButton_Click(this, new RoutedEventArgs()); return Task.CompletedTask; }),
            new("发送审计", "查看最近发给模型的文本和附件记录", async () => await ShowSendAuditDialogAsync())
        };

        entries.AddRange(GetTemplateCatalogForSource(_selectedTemplateSource)
            .Take(80)
            .Select(template => new CommandPaletteItem(
                $"模板：{template.Title}",
                $"{template.Source} / {template.Category}",
                async () => await InsertFavoriteTemplateAsync(template))));

        entries.AddRange(_commonPromptService.Load()
            .Take(60)
            .Select(item => new CommandPaletteItem(
                $"常用提示词：{item.Title}",
                item.Category,
                () =>
                {
                    SetUserInput(AppendLine(GetUserInput(), item.Text));
                    ShowPage("Home");
                    SetStatus($"已插入常用提示词：{item.Title}");
                    return Task.CompletedTask;
                })));

        return entries;
    }

    private IEnumerable<CommandPaletteItem> BuildCommandPaletteSearchEntries(string query)
    {
        foreach (var result in _databaseService.Search(query, 40))
        {
            if (result.Kind == AppDatabaseService.KindHistory)
            {
                var item = DeserializeSearchPayload<PromptHistoryItem>(result.Payload);
                if (item is not null)
                {
                    yield return new CommandPaletteItem(
                        $"历史：{item.Title}",
                        $"{item.Mode} / {item.Scene}",
                        () =>
                        {
                            ApplyHistoryItemToWorkbench(item, lockConversation: true);
                            return Task.CompletedTask;
                        });
                }
            }
            else if (result.Kind == AppDatabaseService.KindCommonPrompt)
            {
                var item = DeserializeSearchPayload<CommonPromptItem>(result.Payload);
                if (item is not null)
                {
                    yield return new CommandPaletteItem(
                        $"常用提示词：{item.Title}",
                        item.Category,
                        () =>
                        {
                            SetUserInput(AppendLine(GetUserInput(), item.Text));
                            ShowPage("Home");
                            SetStatus($"已插入常用提示词：{item.Title}");
                            return Task.CompletedTask;
                        });
                }
            }
            else if (result.Kind == AppDatabaseService.KindTemplate)
            {
                var item = DeserializeSearchPayload<PromptTemplateCatalogItem>(result.Payload);
                if (item is not null)
                {
                    yield return new CommandPaletteItem(
                        $"模板：{item.Title}",
                        $"{item.Source} / {item.Category}",
                        async () => await InsertFavoriteTemplateAsync(item));
                }
            }
            else if (result.Kind == AppDatabaseService.KindOptimizationTarget)
            {
                var item = DeserializeSearchPayload<OptimizationTargetItem>(result.Payload);
                if (item is not null)
                {
                    yield return new CommandPaletteItem(
                        $"优化目标：{item.Title}",
                        $"{item.Category} / {item.TemplateSource}",
                        () =>
                        {
                            var mode = MakeOptimizationTargetMode(item.Id);
                            if (!CanUseConversationMode(mode))
                            {
                                RejectConversationModeChange();
                                return Task.CompletedTask;
                            }

                            ApplySelectedMode(mode, save: true);
                            SetStatus($"已切换优化目标：{item.Title}");
                            return Task.CompletedTask;
                        });
                }
            }
        }
    }

    private static T? DeserializeSearchPayload<T>(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch
        {
            return default;
        }
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        PersistSettingsFromUi(showStatus: true);
    }

    private void RestoreDefaultSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = new AppSettings();
        _settingsService.SaveUserSettings(_settings, null);
        _hotkeyService.RegisterHotkey(_settings.Hotkey);
        LoadSettingsIntoUi();
        ApplyLocalization();
        RefreshModelDisplayText();
        SetStatus(L("已恢复默认设置"));
    }

    private void SettingsControl_Changed(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _loadingSettings)
        {
            return;
        }

        PersistSettingsFromUi(showStatus: false);
    }

    private void SettingsControl_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _loadingSettings)
        {
            return;
        }

        PersistSettingsFromUi(showStatus: false);
    }

    private void SettingsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _loadingSettings)
        {
            return;
        }

        PersistSettingsFromUi(showStatus: false);
    }

    private void ProviderPresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _loadingSettings || _syncingProviderPresetSelection)
        {
            return;
        }

        var presetId = (ProviderPresetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var preset = FindProviderPreset(presetId);
        if (preset is null || string.Equals(preset.Id, "custom", StringComparison.OrdinalIgnoreCase))
        {
            PersistSettingsFromUi(showStatus: false);
            RefreshModelDisplayText();
            return;
        }

        _syncingProviderPresetSelection = true;
        try
        {
            BaseUrlBox.Text = preset.BaseUrl;
            ModelNameBox.Text = preset.RecommendedModel;
            ModelEnabledBox.IsChecked = true;
            ApiKeyBox.Password = ReadProviderApiKey(preset.Id);
        }
        finally
        {
            _syncingProviderPresetSelection = false;
        }

        PersistSettingsFromUi(showStatus: false);
        RefreshModelDisplayText();
        RefreshWorkflowModelBox();
        SetModelProbeStatus($"{L("已套用提供商预设")}：{preset.DisplayName} · {preset.RecommendedModel}");
        ScheduleModelProbe();
    }

    private void ModelMenuButton_Click(object sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();
        var currentProviderId = GetCurrentProviderId();
        var currentModel = ModelNameBox.Text.Trim();

        foreach (var preset in ModelProviderPresets.Where(preset => !string.Equals(preset.Id, "custom", StringComparison.OrdinalIgnoreCase)))
        {
            var providerItem = new MenuFlyoutSubItem
            {
                Text = GetProviderMenuLabel(preset)
            };

            foreach (var modelId in GetModelMenuCandidates(preset))
            {
                var modelItem = new MenuFlyoutItem
                {
                    Text = FormatModelLabelWithTags(preset.Id, modelId)
                };
                if (string.Equals(currentProviderId, preset.Id, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(currentModel, modelId, StringComparison.OrdinalIgnoreCase))
                {
                    modelItem.Icon = new FontIcon
                    {
                        FontFamily = new FontFamily("Segoe Fluent Icons"),
                        Glyph = "\uE73E",
                        FontSize = 14
                    };
                }

                modelItem.Click += (_, _) => ApplyModelMenuSelection(preset, modelId);
                providerItem.Items.Add(modelItem);
            }

            flyout.Items.Add(providerItem);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        var manageItem = new MenuFlyoutItem { Text = L("模型管理") };
        manageItem.Click += (_, _) =>
        {
            SetWindowMode(expanded: true);
            ShowPage("Models");
        };
        flyout.Items.Add(manageItem);
        flyout.ShowAt(TopModelButton);
    }

    private IReadOnlyList<string> GetModelMenuCandidates(ModelProviderPreset preset)
    {
        var detected = string.Equals(GetCurrentProviderId(), preset.Id, StringComparison.OrdinalIgnoreCase)
            ? EnumerateDetectedModelIds()
            : [];
        return preset.DefaultModels
            .Concat(detected)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void ApplyModelMenuSelection(ModelProviderPreset preset, string modelId)
    {
        var storedApiKey = ReadProviderApiKey(preset.Id);
        _loadingSettings = true;
        try
        {
            ModelEnabledBox.IsChecked = true;
            BaseUrlBox.Text = preset.BaseUrl;
            ModelNameBox.Text = modelId;
            ApiKeyBox.Password = storedApiKey;
            SelectProviderPresetForBaseUrl(preset.BaseUrl);
            SelectDetectedModel(modelId);
        }
        finally
        {
            _loadingSettings = false;
        }

        _settings = _settingsService.Load();
        _settings.Model.Enabled = true;
        _settings.Model.ProviderId = preset.Id;
        _settings.Model.BaseUrl = preset.BaseUrl;
        _settings.Model.Model = modelId;
        _settings.Model.CredentialTargetName = BuildProviderCredentialTargetName(preset.Id);
        _settings.Model.TimeoutSeconds = 30;
        _settingsService.SaveUserSettings(_settings, string.IsNullOrWhiteSpace(storedApiKey) ? null : storedApiKey);
        RefreshModelDisplayText();
        RefreshWorkflowModelBox();
        RefreshModelCapabilityText();
        SetStatus($"已切换模型：{GetProviderMenuLabel(preset)} {modelId}");
        ScheduleModelProbe();
    }

    private void ModelEndpointInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady || _loadingSettings || _syncingProviderPresetSelection)
        {
            return;
        }

        SelectProviderPresetForBaseUrl(BaseUrlBox.Text);
        PersistSettingsFromUi(showStatus: false);
        ScheduleModelProbe();
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || _loadingSettings || _syncingProviderPresetSelection)
        {
            return;
        }

        PersistSettingsFromUi(showStatus: false);
        ScheduleModelProbe();
    }

    private void DetectedModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _loadingSettings || _syncingModelSelection)
        {
            return;
        }

        var modelId = (DetectedModelBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        ModelNameBox.Text = modelId;
        PersistSettingsFromUi(showStatus: false);
        RefreshWorkflowModelBox();
        RefreshModelCapabilityText();
        SetModelProbeStatus($"{L("已选择模型")}：{modelId}");
    }

    private void WorkflowModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _loadingSettings || _syncingWorkflowModelSelection)
        {
            return;
        }

        var modelId = (WorkflowModelBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return;
        }

        ModelNameBox.Text = modelId;
        PersistSettingsFromUi(showStatus: false);
        SelectDetectedModel(modelId);
        RefreshModelDisplayText();
        RefreshModelCapabilityText();
        SetStatus($"{L("已切换模型")}：{modelId}");
    }

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _loadingSettings)
        {
            return;
        }

        PersistSettingsFromUi(showStatus: false);
        ApplyLocalization();
        RefreshTemplateViews();
        RefreshUserTemplateList();
        RefreshSkillManagementUi();
        RefreshCommonPromptsUi();
        RefreshRuntimeLocalizedText();
    }

    private async void MountLanguagePackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".resw");
            picker.FileTypeFilter.Add(".pri");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                SetStatus(L("已取消挂载语言包"));
                return;
            }

            var pack = _localizationService.Mount(file.Path);
            if (pack is null)
            {
                SetStatus(L("语言包无效"));
                return;
            }

            _settings = _settingsService.Load();
            _settings.Ui.LanguageCode = pack.Code;
            _settings.Ui.MountedLanguagePackPath = pack.SourcePath;
            _settingsService.SaveUserSettings(_settings, null);
            LoadSettingsIntoUi();
            ApplyLocalization();
            RefreshScene();
            RefreshModelDisplayText();
            RefreshAboutUi();
            SetStatus($"{L("语言包已挂载")}：{pack.DisplayName}");
        }
        catch (Exception ex)
        {
            SetStatus($"{L("语言包无效")}：{ex.Message}");
        }
    }

    private void ScheduleModelProbe()
    {
        var baseUrl = BaseUrlBox.Text.Trim();
        var apiKey = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _modelProbeTimer.Stop();
            ClearDetectedModels();
            SetModelProbeStatus(L("填写 Base URL 和 API Key 后会自动检测可用性并刷新模型列表。"));
            return;
        }

        SetModelProbeStatus(L("等待检测模型 endpoint..."));
        _modelProbeTimer.Stop();
        _modelProbeTimer.Start();
    }

    private async void ModelProbeTimer_Tick(object? sender, object e)
    {
        _modelProbeTimer.Stop();
        await RefreshModelsFromEndpointAsync(showErrors: true);
    }

    private async Task RefreshModelsFromEndpointAsync(bool showErrors)
    {
        var baseUrl = BaseUrlBox.Text.Trim();
        var apiKey = ApiKeyBox.Password;
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            ClearDetectedModels();
            SetModelProbeStatus(L("Base URL 格式不正确"));
            await ShowModelProbeErrorOnceAsync(BuildModelProbeSignature(baseUrl, apiKey), "Base URL 格式不正确，请填写 http 或 https 开头的 OpenAI-compatible 地址。", showErrors);
            return;
        }

        var probeVersion = ++_modelProbeVersion;
        var signature = BuildModelProbeSignature(baseUrl, apiKey);
        SetModelProbeStatus(L("正在检测模型 endpoint..."));

        try
        {
            var models = await _llmClient.ListModelsAsync(baseUrl, apiKey, _settings.Model.TimeoutSeconds, CancellationToken.None);
            if (probeVersion != _modelProbeVersion)
            {
                return;
            }

            _lastModelProbeFailureSignature = null;
            RefreshDetectedModelBox(models);

            if (string.IsNullOrWhiteSpace(ModelNameBox.Text) && models.Count > 0)
            {
                ModelNameBox.Text = models[0].Id;
            }

            PersistSettingsFromUi(showStatus: false);
            RefreshModelDisplayText();
            RefreshWorkflowModelBox();
            var selectedModel = ModelNameBox.Text.Trim();
            SetModelProbeStatus(string.IsNullOrWhiteSpace(selectedModel)
                ? $"{L("已连接，发现模型")} {models.Count}"
                : $"{L("已连接，发现模型")} {models.Count} · {selectedModel}");
        }
        catch (Exception ex) when (probeVersion == _modelProbeVersion)
        {
            ClearDetectedModels();
            var message = NormalizeModelProbeError(ex);
            SetModelProbeStatus($"{L("模型 endpoint 不可用")}：{message}");
            await ShowModelProbeErrorOnceAsync(signature, message, showErrors);
        }
    }

    private void RefreshDetectedModelBox(IReadOnlyList<LlmModelInfo> models)
    {
        _syncingModelSelection = true;
        try
        {
            DetectedModelBox.Items.Clear();
            var currentModel = ModelNameBox.Text.Trim();
            var providerId = GetCurrentProviderId();
            var selectedIndex = -1;
            for (var i = 0; i < models.Count; i++)
            {
                var model = models[i];
                var item = new ComboBoxItem
                {
                    Content = FormatModelLabelWithTags(providerId, model.Id),
                    Tag = model.Id
                };
                ToolTipService.SetToolTip(item, BuildModelCapabilityTooltip(providerId, model.Id, model.OwnedBy));

                DetectedModelBox.Items.Add(item);
                if (string.Equals(model.Id, currentModel, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            DetectedModelBox.SelectedIndex = selectedIndex;
            RefreshWorkflowModelBox(models.Select(model => model.Id));
        }
        finally
        {
            _syncingModelSelection = false;
        }

        RefreshModelCapabilityText();
    }

    private void ClearDetectedModels()
    {
        _syncingModelSelection = true;
        try
        {
            DetectedModelBox.Items.Clear();
            DetectedModelBox.SelectedIndex = -1;
            RefreshWorkflowModelBox();
        }
        finally
        {
            _syncingModelSelection = false;
        }
    }

    private void RefreshWorkflowModelBox(IEnumerable<string>? modelIds = null)
    {
        var currentModel = ModelNameBox.Text.Trim();
        var providerId = GetCurrentProviderIdFromSettingsOrUi();
        var candidates = (modelIds ?? EnumerateDetectedModelIds())
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(currentModel)
            && !candidates.Contains(currentModel, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Insert(0, currentModel);
        }

        _syncingWorkflowModelSelection = true;
        try
        {
            WorkflowModelBox.Items.Clear();
            var selectedIndex = -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                var modelId = candidates[i];
                WorkflowModelBox.Items.Add(new ComboBoxItem
                {
                    Content = FormatModelLabelWithTags(providerId, modelId),
                    Tag = modelId
                });
                if (string.Equals(modelId, currentModel, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            WorkflowModelBox.SelectedIndex = selectedIndex;
        }
        finally
        {
            _syncingWorkflowModelSelection = false;
        }
    }

    private IEnumerable<string> EnumerateDetectedModelIds()
    {
        foreach (var item in DetectedModelBox.Items.OfType<ComboBoxItem>())
        {
            var modelId = item.Tag?.ToString();
            if (!string.IsNullOrWhiteSpace(modelId))
            {
                yield return modelId;
            }
        }
    }

    private void SelectDetectedModel(string modelId)
    {
        _syncingModelSelection = true;
        try
        {
            for (var i = 0; i < DetectedModelBox.Items.Count; i++)
            {
                if (DetectedModelBox.Items[i] is ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), modelId, StringComparison.OrdinalIgnoreCase))
                {
                    DetectedModelBox.SelectedIndex = i;
                    return;
                }
            }

            DetectedModelBox.SelectedIndex = -1;
        }
        finally
        {
            _syncingModelSelection = false;
        }
    }

    private static ModelProviderPreset? FindProviderPreset(string? presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            return null;
        }

        return ModelProviderPresets.FirstOrDefault(preset =>
            string.Equals(preset.Id, presetId, StringComparison.OrdinalIgnoreCase));
    }

    private string GetCurrentProviderId()
    {
        var selectedProviderId = (ProviderPresetBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(selectedProviderId)
            && !string.Equals(selectedProviderId, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return selectedProviderId;
        }

        var detectedProviderId = DetectProviderIdFromBaseUrl(BaseUrlBox.Text);
        if (!string.Equals(detectedProviderId, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return detectedProviderId;
        }

        return "custom";
    }

    private static string DetectProviderIdFromBaseUrl(string? baseUrl)
    {
        var preset = ModelProviderPresets
            .Where(item => !string.Equals(item.Id, "custom", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(item => IsSameBaseUrl(item.BaseUrl, baseUrl));
        return preset?.Id ?? "custom";
    }

    private static string BuildProviderCredentialTargetName(string providerId)
    {
        var safeProvider = string.IsNullOrWhiteSpace(providerId) ? "custom" : providerId.Trim().ToLowerInvariant();
        return $"PromptInputMethod/OpenAICompatibleApiKey/{safeProvider}";
    }

    private string ReadProviderApiKey(string providerId)
    {
        var providerTargetName = BuildProviderCredentialTargetName(providerId);
        var key = _credentialService.ReadSecret(providerTargetName);
        return key ?? string.Empty;
    }

    private static string GetProviderMenuLabel(ModelProviderPreset preset)
    {
        return preset.Id switch
        {
            "deepseek" => "DeepSeek",
            "openai" => "OpenAI",
            "glm" => "GLM",
            "claude" => "Claude",
            "gemini" => "Gemini",
            "minimax" => "MiniMax",
            "doubao" => "豆包",
            "kimi" => "Kimi",
            _ => preset.DisplayName
        };
    }

    private void SelectProviderPresetForBaseUrl(string? baseUrl)
    {
        _syncingProviderPresetSelection = true;
        try
        {
            var preset = ModelProviderPresets
                .Where(item => !string.Equals(item.Id, "custom", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(item => IsSameBaseUrl(item.BaseUrl, baseUrl));
            var presetId = preset?.Id ?? "custom";

            for (var i = 0; i < ProviderPresetBox.Items.Count; i++)
            {
                if (ProviderPresetBox.Items[i] is ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), presetId, StringComparison.OrdinalIgnoreCase))
                {
                    ProviderPresetBox.SelectedIndex = i;
                    return;
                }
            }

            ProviderPresetBox.SelectedIndex = -1;
        }
        finally
        {
            _syncingProviderPresetSelection = false;
        }
    }

    private static bool IsSameBaseUrl(string presetBaseUrl, string? currentBaseUrl)
    {
        var preset = NormalizeBaseUrlForPreset(presetBaseUrl);
        var current = NormalizeBaseUrlForPreset(currentBaseUrl);
        return !string.IsNullOrWhiteSpace(preset)
            && string.Equals(preset, current, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBaseUrlForPreset(string? baseUrl)
    {
        return string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : baseUrl.Trim().TrimEnd('/');
    }

    private async Task ShowModelProbeErrorOnceAsync(string signature, string message, bool showErrors)
    {
        if (!showErrors || _modelProbeDialogOpen || _lastModelProbeFailureSignature == signature)
        {
            return;
        }

        _lastModelProbeFailureSignature = signature;
        _modelProbeDialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                Title = L("模型连接不可用"),
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                CloseButtonText = L("知道了"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        finally
        {
            _modelProbeDialogOpen = false;
        }
    }

    private void SetModelProbeStatus(string text)
    {
        ModelProbeStatusText.Text = L(text);
    }

    private static string BuildModelProbeSignature(string baseUrl, string apiKey)
    {
        return $"{baseUrl.Trim()}|{apiKey.Length}|{StringComparer.Ordinal.GetHashCode(apiKey)}";
    }

    private static string NormalizeModelProbeError(Exception ex)
    {
        var message = ex is OperationCanceledException
            ? "请求超时，请检查网络、Base URL 或代理。"
            : ex.Message;
        return message.Length > 900 ? $"{message[..900]}..." : message;
    }

    private void PersistSettingsFromUi(bool showStatus)
    {
        _settings.Model.Enabled = ModelEnabledBox.IsChecked == true;
        _settings.Model.ProviderId = GetCurrentProviderId();
        _settings.Model.BaseUrl = BaseUrlBox.Text.Trim();
        _settings.Model.Model = ModelNameBox.Text.Trim();
        _settings.Model.CredentialTargetName = BuildProviderCredentialTargetName(_settings.Model.ProviderId);
        _settings.Model.TimeoutSeconds = 30;
        _settings.Ocr.PreferredProvider = (OcrProviderBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? OcrProviderIds.FireEye;
        _settings.Ocr.TimeoutSeconds = 15;
        _settings.Ui.DeepThinking = IsDeepThinkingEnabled();
        var selectedLanguageItem = LanguageBox.SelectedItem as ComboBoxItem;
        _settings.Ui.LanguageCode = selectedLanguageItem?.Tag?.ToString() ?? "auto";
        _settings.Ui.MountedLanguagePackPath = selectedLanguageItem?.DataContext as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_settings.Ui.MountedLanguagePackPath)
            && _settings.Ui.LanguageCode is "auto" or "zh-CN" or "en-US")
        {
            _settings.Ui.MountedLanguagePackPath = string.Empty;
        }

        _settings.Hotkey.Enabled = HotkeyEnabledBox.IsChecked == true;
        _settings.Hotkey.Control = HotkeyCtrlBox.IsChecked == true;
        _settings.Hotkey.Shift = HotkeyShiftBox.IsChecked == true;
        _settings.Hotkey.Alt = HotkeyAltBox.IsChecked == true;
        _settings.Hotkey.Win = HotkeyWinBox.IsChecked == true;
        _settings.Hotkey.Key = GetSelectedHotkeyMainKey();
        _settings.Privacy.OcrEnabled = OcrEnabledBox.IsChecked == true;
        _settings.Privacy.ModelExternalRequestsEnabled = ModelExternalRequestsEnabledBox.IsChecked == true;
        _settings.Privacy.ModelImageExternalRequestsEnabled = ModelImageExternalRequestsEnabledBox.IsChecked == true;
        _settings.Privacy.RedactBeforeModelSend = RedactBeforeModelSendBox.IsChecked == true;

        if (!_hotkeyService.RegisterHotkey(_settings.Hotkey))
        {
            if (showStatus)
            {
                SetStatus("快捷键无效或已被其他程序占用，请换一个组合");
            }

            return;
        }

        _settingsService.SaveUserSettings(_settings, ApiKeyBox.Password);
        RefreshModelDisplayText();
        if (showStatus)
        {
            SetStatus(_settings.Model.Enabled ? "设置已保存" : "设置已保存，模型关闭时使用本地结构化");
        }
    }

    private void ToggleSidebarButton_Click(object sender, RoutedEventArgs e)
    {
        var nextCollapsed = !_sidebarCollapsed;
        _sidebarManuallyExpanded = !nextCollapsed;
        SetSidebarCollapsed(nextCollapsed, autoCollapsed: false);
    }

    private void TopSidebarMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_sidebarFullyCollapsed)
        {
            return;
        }

        NarrowSidebarOverlay.Visibility = NarrowSidebarOverlay.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void SidebarOverlayScrim_Tapped(object sender, TappedRoutedEventArgs e)
    {
        HideSidebarOverlay();
    }

    private void HideSidebarOverlay()
    {
        NarrowSidebarOverlay.Visibility = Visibility.Collapsed;
    }

    private void SetSidebarFullyCollapsed(bool collapsed)
    {
        if (_sidebarFullyCollapsed == collapsed)
        {
            return;
        }

        _sidebarFullyCollapsed = collapsed;
        TopSidebarMenuButton.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        SidebarHost.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = collapsed ? new GridLength(0) : new GridLength(_sidebarCollapsed ? 72 : 248);

        if (!collapsed)
        {
            HideSidebarOverlay();
        }
    }

    private void SetSidebarCollapsed(bool collapsed, bool autoCollapsed)
    {
        _sidebarCollapsed = collapsed;
        _sidebarAutoCollapsed = autoCollapsed;
        if (_sidebarFullyCollapsed)
        {
            return;
        }

        SidebarColumn.Width = new GridLength(collapsed ? 72 : 248);
        var textVisibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarHeader.Visibility = Visibility.Collapsed;
        SidebarToggleText.Visibility = textVisibility;
        HomeNavText.Visibility = textVisibility;
        HistoryNavText.Visibility = textVisibility;
        TemplatesNavText.Visibility = textVisibility;
        SkillsNavText.Visibility = textVisibility;
        OptimizationTargetsNavText.Visibility = textVisibility;
        SnippetsNavText.Visibility = textVisibility;
        ModelsNavText.Visibility = textVisibility;
        SettingsNavText.Visibility = textVisibility;
        HelpNavText.Visibility = textVisibility;
        SidebarToggleIcon.Glyph = _sidebarCollapsed ? "\uE8A0" : "\uE700";
        ApplySidebarNavLayout(SidebarHost, collapsed);
    }

    private void ApplySidebarNavLayout(DependencyObject root, bool collapsed)
    {
        if (root is Button button)
        {
            button.Padding = collapsed ? new Thickness(0) : new Thickness(14, 0, 14, 0);
            button.HorizontalContentAlignment = collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            ApplySidebarNavLayout(VisualTreeHelper.GetChild(root, index), collapsed);
        }
    }

    private void ToggleRightPanelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_rightPanelCollapsed)
        {
            _rightPanelManuallyCollapsed = false;
            _rightPanelManuallyExpanded = true;
        }
        else
        {
            _rightPanelManuallyCollapsed = true;
            _rightPanelManuallyExpanded = false;
        }

        ApplyResponsiveHomeLayout();
    }

    private void HomePage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueResponsiveHomeLayout();
    }

    private void QueueResponsiveHomeLayout()
    {
        if (!_uiReady || _responsiveLayoutQueued)
        {
            return;
        }

        _responsiveLayoutQueued = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            _responsiveLayoutQueued = false;
            ApplyResponsiveHomeLayout();
        });
    }

    private void ApplyResponsiveHomeLayout()
    {
        if (!_uiReady || _applyingResponsiveLayout)
        {
            return;
        }

        _applyingResponsiveLayout = true;
        try
        {
            var width = ExpandedShell.ActualWidth;
            if (double.IsNaN(width) || width <= 0)
            {
                return;
            }

            if (width < SidebarFullCollapseWidth)
            {
                _sidebarManuallyExpanded = false;
                if (!_sidebarFullyCollapsed)
                {
                    SetSidebarCollapsed(true, autoCollapsed: true);
                    SetSidebarFullyCollapsed(true);
                }
            }
            else if (_sidebarFullyCollapsed && width > SidebarFullExpandWidth)
            {
                SetSidebarFullyCollapsed(false);
                SetSidebarCollapsed(true, autoCollapsed: true);
            }

            if (!_sidebarFullyCollapsed && !_sidebarCollapsed && !_sidebarManuallyExpanded && width < SidebarCollapseWidth)
            {
                SetSidebarCollapsed(true, autoCollapsed: true);
            }
            else if (!_sidebarFullyCollapsed && _sidebarCollapsed && _sidebarAutoCollapsed && width > SidebarExpandWidth)
            {
                _sidebarManuallyExpanded = false;
                SetSidebarCollapsed(false, autoCollapsed: false);
            }

            var forceCollapseRight = _rightPanelCollapsed
                ? width < RightPanelExpandWidth
                : width < RightPanelCollapseWidth;
            var stackOutputs = _outputsStacked
                ? width < OutputUnstackWidth
                : width < OutputStackWidth;
            SetRightPanelCollapsed(_rightPanelManuallyCollapsed || (forceCollapseRight && !_rightPanelManuallyExpanded));
            SetOutputsStacked(stackOutputs);
        }
        catch
        {
            // WinUI can report transient layout states during interactive resizing.
            // Keep the resize loop alive and apply the next stable layout pass.
        }
        finally
        {
            _applyingResponsiveLayout = false;
        }
    }

    private void SetRightPanelCollapsed(bool collapsed)
    {
        if (_rightPanelCollapsed == collapsed)
        {
            return;
        }

        _rightPanelCollapsed = collapsed;
        HomeRightColumn.Width = collapsed ? new GridLength(0) : new GridLength(320);
        RightTemplatePanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
    }

    private void SetOutputsStacked(bool stacked)
    {
        if (_outputsStacked == stacked)
        {
            return;
        }

        _outputsStacked = stacked;
        EnglishOutputColumn.Width = stacked ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        EnglishOutputRow.Height = stacked ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(EnglishOutputPanel, stacked ? 0 : 1);
        Grid.SetRow(EnglishOutputPanel, stacked ? 1 : 0);
    }

    private void UpdateInputBoxHeights(string text)
    {
        var expandedHeight = EstimateInputHeight(text, 52, 128, 82);
        var compactHeight = EstimateInputHeight(text, 72, 150, 82);
        ExpandedInputBox.Height = expandedHeight;
        InputBox.Height = compactHeight;
    }

    private static double EstimateInputHeight(string text, double minHeight, double maxHeight, int approximateCharsPerLine)
    {
        var lineCount = string.IsNullOrEmpty(text)
            ? 1
            : text.Split(["\r\n", "\n"], StringSplitOptions.None)
                .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / (double)approximateCharsPerLine)));
        var target = 72 + lineCount * 22;
        return Math.Clamp(target, minHeight, maxHeight);
    }

    private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var deleted = _privacyDataService.ClearHistory() + _historyService.Clear();
        RefreshHistoryUi();
        RefreshSearchIndex();
        SetStatus(deleted == 0 ? "没有找到可清除的历史记录" : $"已清除 {deleted} 个历史/临时文件");
    }

    private void ClearFavoritesButton_Click(object sender, RoutedEventArgs e)
    {
        var deleted = _privacyDataService.ClearFavorites();
        RefreshFavoritesUi();
        RefreshSearchIndex();
        SetStatus(deleted == 0 ? "当前没有本地收藏文件" : $"已清除 {deleted} 个收藏文件");
    }

    private void InsertCurrentWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetChineseOutput();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("没有可插入的提示词");
            return;
        }

        if (!_clipboardContextService.TrySetClipboardText(text))
        {
            SetStatus("剪贴板暂时被占用，无法插入");
            return;
        }

        if (_lastForegroundWindow != 0)
        {
            _foregroundWindowService.ActivateWindow(_lastForegroundWindow);
            Task.Delay(80).ContinueWith(_ => SendCtrlV());
            SetStatus("已插入到当前窗口");
            return;
        }

        SetStatus("已复制提示词；没有记录到目标窗口，需手动粘贴");
    }

    private void InsertEnglishCurrentWindowButton_Click(object sender, RoutedEventArgs e)
    {
        var text = EnglishOutputBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("没有可插入的英文提示词");
            return;
        }

        if (!_clipboardContextService.TrySetClipboardText(text))
        {
            SetStatus("剪贴板暂时被占用，无法插入英文提示词");
            return;
        }

        if (_lastForegroundWindow != 0)
        {
            _foregroundWindowService.ActivateWindow(_lastForegroundWindow);
            Task.Delay(80).ContinueWith(_ => SendCtrlV());
            SetStatus("已插入英文提示词到当前窗口");
            return;
        }

        SetStatus("已复制英文提示词；没有记录到目标窗口，需手动粘贴");
    }

    private void CopyCurrentPromptButton_Click(object sender, RoutedEventArgs e)
    {
        var text = CurrentPromptBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("当前提示词为空");
            return;
        }

        SetStatus(_clipboardContextService.TrySetClipboardText(text) ? "已复制当前提示词" : "剪贴板暂时被占用，请再点一次复制");
    }

    private void InsertCurrentPromptButton_Click(object sender, RoutedEventArgs e)
    {
        InsertTextIntoForegroundWindow(CurrentPromptBox.Text, "当前提示词");
    }

    private void QuoteCurrentPromptButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetChineseOutput();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("中文提示词为空，无法引用");
            return;
        }

        ExpandedInputBox.Text = AppendLine(ExpandedInputBox.Text, $"引用中文提示词：{text}");
        ExpandedInputBox.Focus(FocusState.Programmatic);
        SetStatus("已引用中文提示词，可继续补充修改意见");
    }

    private void ReplaceCurrentPromptButton_Click(object sender, RoutedEventArgs e)
    {
        var text = GetChineseOutput();
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("优化后提示词为空，无法替换");
            return;
        }

        SetCurrentPrompt(text);
        AddChatMessage("已将优化后提示词替换为当前提示词，可继续在此基础上修改。", isUser: false);
        SetStatus("已替换当前提示词");
    }

    private void InsertTextIntoForegroundWindow(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus($"{label}为空，无法插入");
            return;
        }

        if (!_clipboardContextService.TrySetClipboardText(text))
        {
            SetStatus("剪贴板暂时被占用，无法插入");
            return;
        }

        if (_lastForegroundWindow != 0)
        {
            _foregroundWindowService.ActivateWindow(_lastForegroundWindow);
            Task.Delay(80).ContinueWith(_ => SendCtrlV());
            SetStatus($"已插入{label}到当前窗口");
            return;
        }

        SetStatus($"已复制{label}；没有记录到目标窗口，需手动粘贴");
    }

    private void SnippetButton_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag?.ToString() ?? string.Empty;
        var snippet = tag switch
        {
            "镜头语言" => "请补充镜头景别、机位、镜头运动、构图重心、光线和画面节奏。",
            "动作描述" => "请明确主体动作、动作起因、连续行为、情绪变化和环境互动。",
            "视频模型" => "请按时间顺序拆成 3-5 个镜头，包含每个镜头的时长、主体、动作、景别和转场。",
            "英文" => "请同时输出自然、准确、适合英文模型理解的 English prompt。",
            "角色定位" => "角色定位：你是一个专业提示词优化器，需要保留用户原意并提升可执行性。",
            "输出格式" => "输出格式：目标、背景、约束、步骤、交付物、验收标准。",
            "隐私约束" => "约束：不要编造事实，不要泄露隐私，不要输出无关寒暄。",
            _ => "请进一步结构化需求，补全目标、约束和输出格式。"
        };

        SetUserInput(AppendLine(GetUserInput(), snippet));
        SetStatus("已插入常用提示词");
    }

    private async void SaveCommonPromptButton_Click(object sender, RoutedEventArgs e)
    {
        await SaveCommonPromptWithDialogAsync(null);
    }

    private async Task SaveCommonPromptWithDialogAsync(CommonPromptItem? existing)
    {
        var selectedCategory = _selectedCommonPromptCategory;
        if (string.IsNullOrWhiteSpace(selectedCategory) || selectedCategory == "全部")
        {
            selectedCategory = "未分类";
        }

        var titleBox = new TextBox
        {
            Header = L("标题"),
            PlaceholderText = L("例如：角色定位"),
            Text = existing?.Title ?? string.Empty
        };
        var categoryBox = new TextBox
        {
            Header = L("分类"),
            PlaceholderText = L("例如：UI 设计"),
            Text = existing?.Category ?? selectedCategory
        };
        var contentBox = new TextBox
        {
            Header = L("提示词内容"),
            PlaceholderText = L("输入一段常用提示词，保存后点击即可复制"),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 200,
            MaxHeight = 360,
            Text = existing?.Text ?? string.Empty
        };
        var dialog = new ContentDialog
        {
            Title = L(existing is null ? "新建常用提示词" : "编辑常用提示词"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    titleBox,
                    categoryBox,
                    contentBox
                }
            },
            PrimaryButtonText = L("保存"),
            CloseButtonText = L("取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (string.IsNullOrWhiteSpace(contentBox.Text))
            {
                args.Cancel = true;
                SetStatus("请输入常用提示词内容。");
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            SetStatus(existing is null ? "已取消新建常用提示词" : "已取消编辑常用提示词");
            return;
        }

        try
        {
            var item = _commonPromptService.SaveOrUpdate(existing?.Id, titleBox.Text, contentBox.Text, categoryBox.Text);
            RefreshCommonPromptsUi(item.Id);
            SetStatus(existing is null ? $"已新建常用提示词：{item.Title}" : $"已更新常用提示词：{item.Title}");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private void CommonPromptList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_syncingCommonPromptSelection || e.ClickedItem is not CommonPromptItem item)
        {
            return;
        }

        CopyCommonPrompt(item, CommonPromptList);
    }

    private void CommonPromptList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        ShowCommonPromptContextMenu(CommonPromptList, e);
    }

    private void CompactCommonPromptList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_syncingCommonPromptSelection || e.ClickedItem is not CommonPromptItem item)
        {
            return;
        }

        CopyCommonPrompt(item, CompactCommonPromptList);
    }

    private void CompactCommonPromptList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        ShowCommonPromptContextMenu(CompactCommonPromptList, e);
    }

    private void CopyCommonPrompt(CommonPromptItem item, ListView owner)
    {
        owner.SelectedItem = item;
        SetStatus(_clipboardContextService.TrySetClipboardText(item.Text)
            ? $"已复制常用提示词：{item.Title}"
            : "剪贴板暂时被占用，请再点一次复制");
    }

    private void ShowCommonPromptContextMenu(ListView owner, RightTappedRoutedEventArgs e)
    {
        var item = FindDataContext<CommonPromptItem>(e.OriginalSource as DependencyObject);
        if (item is null)
        {
            return;
        }

        owner.SelectedItem = item;

        var flyout = new MenuFlyout();
        var editItem = new MenuFlyoutItem { Text = L("编辑") };
        editItem.Click += async (_, _) => await SaveCommonPromptWithDialogAsync(item);
        var deleteItem = new MenuFlyoutItem { Text = L("删除") };
        deleteItem.Click += (_, _) => DeleteCommonPrompt(item);
        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        flyout.ShowAt(owner, e.GetPosition(owner));
        e.Handled = true;
    }

    private void InsertCommonPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (CommonPromptList.SelectedItem is not CommonPromptItem item)
        {
            SetStatus("请选择要插入的常用提示词");
            return;
        }

        SetUserInput(AppendLine(GetUserInput(), item.Text));
        SetStatus($"已插入常用提示词：{item.Title}");
        ShowPage("Home");
    }

    private void CompactInsertCommonPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (CompactCommonPromptList.SelectedItem is not CommonPromptItem item)
        {
            SetStatus("请选择要插入的常用提示词");
            return;
        }

        SetUserInput(AppendLine(GetUserInput(), item.Text));
        SetStatus($"已插入常用提示词：{item.Title}");
        ShowCompactPage("Workbench");
    }

    private void DeleteCommonPromptButton_Click(object sender, RoutedEventArgs e)
    {
        if (CommonPromptList.SelectedItem is not CommonPromptItem item)
        {
            SetStatus("请选择要删除的常用提示词");
            return;
        }

        DeleteCommonPrompt(item);
    }

    private void DeleteCommonPrompt(CommonPromptItem item)
    {
        var deleted = _commonPromptService.Delete(item.Id);
        RefreshCommonPromptsUi();
        SetStatus(deleted ? $"已删除常用提示词：{item.Title}" : "没有找到要删除的常用提示词");
    }

    private void CommonPromptCategoryFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCommonPromptSelection)
        {
            return;
        }

        _selectedCommonPromptCategory = GetChoiceValue((sender as ComboBox)?.SelectedItem);
        if (string.IsNullOrWhiteSpace(_selectedCommonPromptCategory))
        {
            _selectedCommonPromptCategory = "全部";
        }

        RefreshCommonPromptsUi();
    }

    private void CompactCommonPromptCategoryFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCommonPromptSelection)
        {
            return;
        }

        _selectedCommonPromptCategory = GetChoiceValue((sender as ComboBox)?.SelectedItem);
        if (string.IsNullOrWhiteSpace(_selectedCommonPromptCategory))
        {
            _selectedCommonPromptCategory = "全部";
        }

        RefreshCommonPromptsUi();
    }

    private void CommonPromptSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady || _syncingCommonPromptSearch)
        {
            return;
        }

        _commonPromptSearchText = (sender as TextBox)?.Text.Trim() ?? string.Empty;
        SyncCommonPromptSearchBoxes(sender as TextBox);
        RefreshCommonPromptsUi();
    }

    private void SyncCommonPromptSearchBoxes(TextBox? source)
    {
        _syncingCommonPromptSearch = true;
        try
        {
            if (!ReferenceEquals(source, CommonPromptSearchBox)
                && CommonPromptSearchBox is not null
                && CommonPromptSearchBox.Text != _commonPromptSearchText)
            {
                CommonPromptSearchBox.Text = _commonPromptSearchText;
            }

            if (!ReferenceEquals(source, CompactCommonPromptSearchBox)
                && CompactCommonPromptSearchBox is not null
                && CompactCommonPromptSearchBox.Text != _commonPromptSearchText)
            {
                CompactCommonPromptSearchBox.Text = _commonPromptSearchText;
            }
        }
        finally
        {
            _syncingCommonPromptSearch = false;
        }
    }

    private async void ImportCommonPromptsJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickJsonOpenPathAsync();
        if (path is null)
        {
            SetStatus(L("已取消导入"));
            return;
        }

        var count = _commonPromptService.ImportFromFile(path);
        RefreshCommonPromptsUi();
        SetStatus(count == 0 ? L("JSON 中没有可导入的项目") : $"{L("已导入")} {count} {L("条常用提示词")}");
    }

    private async void ExportCommonPromptsJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickJsonSavePathAsync("aipin-common-prompts");
        if (path is null)
        {
            SetStatus(L("已取消导出"));
            return;
        }

        _commonPromptService.ExportToFile(path);
        SetStatus($"{L("已导出")}：{Path.GetFileName(path)}");
    }

    private async void ImportTemplatesJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickJsonOpenPathAsync();
        if (path is null)
        {
            SetStatus(L("已取消导入"));
            return;
        }

        var count = _promptFavoriteService.ImportFromFile(path);
        RefreshFavoritesUi();
        RefreshSearchIndex();
        SetStatus(count == 0 ? L("JSON 中没有可导入的项目") : $"{L("已导入")} {count} {L("条提示词模板")}");
    }

    private async void ExportTemplatesJsonButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickJsonSavePathAsync("aipin-user-templates");
        if (path is null)
        {
            SetStatus(L("已取消导出"));
            return;
        }

        var templates = GetTemplateCatalogForSource(_selectedTemplateSource).ToArray();
        if (templates.Length == 0)
        {
            templates = GetUserTemplateCatalog().ToArray();
        }

        _promptFavoriteService.ExportCatalogToFile(path, templates);
        SetStatus($"{L("已导出")}：{Path.GetFileName(path)}");
    }

    private async void ImportOptimizationTargetButton_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickJsonOpenPathAsync();
        if (path is null)
        {
            SetStatus(L("已取消导入"));
            return;
        }

        var imported = _optimizationTargetService.ImportFromFile(path);
        RefreshOptimizationTargetPickers(imported.FirstOrDefault()?.Id);
        RefreshSearchIndex();
        if (imported.Count > 0)
        {
            var requestedMode = MakeOptimizationTargetMode(imported[0].Id);
            if (CanUseConversationMode(requestedMode))
            {
                ApplySelectedMode(requestedMode, save: true);
            }
            else
            {
                RejectConversationModeChange();
            }
        }

        SetStatus(imported.Count == 0 ? L("JSON 中没有可导入的项目") : $"{L("已导入")} {imported.Count} {L("个优化目标")}");
    }

    private async void ExportOptimizationTargetButton_Click(object sender, RoutedEventArgs e)
    {
        var target = GetExportableOptimizationTarget();
        if (target is null)
        {
            SetStatus(L("请选择要导出的优化目标"));
            return;
        }

        var path = await PickJsonSavePathAsync($"aipin-target-{target.Id}");
        if (path is null)
        {
            SetStatus(L("已取消导出"));
            return;
        }

        _optimizationTargetService.ExportToFile(path, target);
        SetStatus($"{L("已导出")}：{Path.GetFileName(path)}");
    }

    private void DeleteOptimizationTargetButton_Click(object sender, RoutedEventArgs e)
    {
        if (OptimizationTargetManagementList.SelectedItem is not OptimizationTargetItem target)
        {
            SetStatus(L("请选择要删除的优化目标"));
            return;
        }

        if (_optimizationTargetService.Delete(target.Id))
        {
            if (string.Equals(GetOptimizationTargetId(_selectedMode), target.Id, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(GetOptimizationTargetId(_conversationLockedMode), target.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _conversationLockedMode = null;
                    _conversationLockedTargetLabel = null;
                    _conversationLockedCustomModeText = null;
                }

                _selectedMode = "通用 LLM";
                SetModeRadioState(_selectedMode);
                SaveUiSettings();
            }

            RefreshOptimizationTargetPickers();
            RefreshSearchIndex();
            RefreshScene();
            SetStatus($"{L("已删除优化目标")}：{target.Title}");
            return;
        }

        SetStatus(L("删除优化目标失败"));
    }

    private void OpenOptimizationTargetFormatDocButton_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "optimization-target-format.md");
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "optimization-target-format.md"));
        }

        if (!File.Exists(path))
        {
            SetStatus(L("没有找到优化目标格式文档"));
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    private async void TemplateSourceBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTemplateSelection)
        {
            return;
        }

        var source = TemplateSourceBox.SelectedItem is TemplateSourceChoice choice
            ? choice.Value
            : "ChatGPT-Shortcut";
        await SelectTemplateSourceAsync(source);
    }

    private async Task SelectTemplateSourceAsync(string source)
    {
        source = NormalizeTemplateSource(source);
        _selectedTemplateSource = source;
        _selectedTemplateCategory = "全部";
        SetTemplateTabState(skillSelected: false);
        if (string.Equals(source, "prompts.chat", StringComparison.OrdinalIgnoreCase))
        {
            QuickTemplateList.ItemsSource = Array.Empty<PromptTemplateCatalogItem>();
            SetStatus("正在加载 prompts.chat 模板...");
            await Task.Yield();
            await Task.Run(() => _templateCatalogService.PreloadSource(source));
            if (!string.Equals(_selectedTemplateSource, source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        RefreshTemplateViews();
    }

    private void TemplateTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateTabState(skillSelected: false);
        RefreshTemplateViews();
    }

    private void SkillTemplateTabButton_Click(object sender, RoutedEventArgs e)
    {
        SetTemplateTabState(skillSelected: true);
    }

    private void SetTemplateTabState(bool skillSelected)
    {
        var templateWasVisible = TemplateBrowserGrid is not null && TemplateBrowserGrid.Visibility == Visibility.Visible;
        var skillWasVisible = MountedSkillPanel is not null && MountedSkillPanel.Visibility == Visibility.Visible;

        if (TemplateTabButton is not null)
        {
            TemplateTabButton.IsChecked = !skillSelected;
            TemplateTabButton.IsEnabled = true;
        }

        if (SkillTemplateTabButton is not null)
        {
            SkillTemplateTabButton.IsChecked = skillSelected;
        }

        var templateVisibility = skillSelected ? Visibility.Collapsed : Visibility.Visible;
        var skillVisibility = skillSelected ? Visibility.Visible : Visibility.Collapsed;
        if (MountedSkillPanel is not null)
        {
            MountedSkillPanel.Visibility = skillVisibility;
        }

        if (TemplateSourcePickerPanel is not null)
        {
            TemplateSourcePickerPanel.Visibility = templateVisibility;
        }

        if (TemplateSearchBox is not null)
        {
            TemplateSearchBox.Visibility = templateVisibility;
        }

        if (TemplateBrowserGrid is not null)
        {
            TemplateBrowserGrid.Visibility = templateVisibility;
        }

        if (TemplateHintText is not null)
        {
            TemplateHintText.Visibility = templateVisibility;
        }

        if (TemplateAssistButtonsPanel is not null)
        {
            TemplateAssistButtonsPanel.Visibility = templateVisibility;
        }

        if (skillSelected)
        {
            if (!skillWasVisible)
            {
                AnimateElementIn(MountedSkillPanel, 3, TabTransitionDurationMs);
            }

            RefreshMountedSkillQuickList();
        }
        else if (!templateWasVisible)
        {
            AnimateElementIn(TemplateSourcePickerPanel, 3, TabTransitionDurationMs);
            AnimateElementIn(TemplateSearchBox, 3, TabTransitionDurationMs);
            AnimateElementIn(TemplateBrowserGrid, 3, TabTransitionDurationMs);
        }
    }

    private void TemplateCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTemplateSelection)
        {
            return;
        }

        _selectedTemplateCategory = GetChoiceValue(TemplateCategoryList.SelectedItem);
        if (string.IsNullOrWhiteSpace(_selectedTemplateCategory))
        {
            _selectedTemplateCategory = "全部";
        }
        RefreshTemplateViews();
    }

    private void TemplateSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        RefreshTemplateViews();
    }

    private async void QuickTemplateList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PromptTemplateCatalogItem template)
        {
            return;
        }

        await InsertFavoriteTemplateAsync(template);
    }

    private void MountedSkillQuickList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PromptTemplateCatalogItem template)
        {
            return;
        }

        _selectedMountedSkillId = template.Id;
        MountedSkillQuickList.SelectedItem = template;
        SelectCompactSkill(template.Id);

        RefreshMountedSkillStatus(MountedSkillQuickList.ItemsSource as IReadOnlyList<PromptTemplateCatalogItem> ?? GetMountedSkillTemplates());
        SetStatus($"已挂载到当前优化目标：{template.Title}");
    }

    private void TemplateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTemplateSelection)
        {
            return;
        }

        RefreshUserTemplateList();
    }

    private void SkillCategoryFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingTemplateSelection)
        {
            return;
        }

        RefreshSkillManagementUi();
    }

    private async void SkillList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (_syncingTemplateSelection || e.ClickedItem is not PromptTemplateCatalogItem template)
        {
            return;
        }

        SkillList.SelectedItem = template;
        await InsertFavoriteTemplateAsync(template);
    }

    private void SkillList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var template = FindDataContext<PromptTemplateCatalogItem>(e.OriginalSource as DependencyObject);
        if (template is null)
        {
            return;
        }

        SkillList.SelectedItem = template;

        var flyout = new MenuFlyout();
        var insertItem = new MenuFlyoutItem { Text = L("插入 Skill") };
        insertItem.Click += async (_, _) => await InsertFavoriteTemplateAsync(template);
        var editItem = new MenuFlyoutItem
        {
            Text = L("编辑"),
            IsEnabled = template.IsUserTemplate
        };
        editItem.Click += async (_, _) => await SaveTemplateWithDialogAsync(template.Text, template);
        var deleteItem = new MenuFlyoutItem
        {
            Text = L("删除"),
            IsEnabled = template.IsUserTemplate
        };
        deleteItem.Click += (_, _) => DeleteSkillTemplate(template);
        flyout.Items.Add(insertItem);
        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        flyout.ShowAt(SkillList, e.GetPosition(SkillList));
        e.Handled = true;
    }

    private async void InsertSkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (SkillList.SelectedItem is not PromptTemplateCatalogItem template)
        {
            SetStatus("请选择要插入的 Skill");
            return;
        }

        await InsertFavoriteTemplateAsync(template);
    }

    private async void EditSkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (SkillList.SelectedItem is not PromptTemplateCatalogItem template)
        {
            SetStatus("请选择要编辑的 Skill");
            return;
        }

        await SaveTemplateWithDialogAsync(template.Text, template);
        RefreshSkillManagementUi(template.Id);
    }

    private void DeleteSkillButton_Click(object sender, RoutedEventArgs e)
    {
        if (SkillList.SelectedItem is not PromptTemplateCatalogItem template)
        {
            SetStatus("请选择要删除的 Skill");
            return;
        }

        DeleteSkillTemplate(template);
    }

    private void DeleteSkillTemplate(PromptTemplateCatalogItem template)
    {
        if (!template.IsUserTemplate)
        {
            SetStatus("内置 Skill 不能删除");
            return;
        }

        if (_promptFavoriteService.Delete(template.Id))
        {
            RefreshFavoritesUi();
            RefreshSkillManagementUi();
            RefreshTemplateViews();
            RefreshSearchIndex();
            SetStatus("已删除 Skill");
            return;
        }

        SetStatus("删除 Skill 失败");
    }

    private void ModeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (!_syncingModeSelection)
        {
            var requestedMode = (sender as FrameworkElement)?.Tag?.ToString() ?? "通用 LLM";
            if (!CanUseConversationMode(requestedMode))
            {
                RejectConversationModeChange();
                return;
            }

            _selectedMode = requestedMode;
        }

        if (_selectedMode == "自定义" && string.IsNullOrWhiteSpace(CustomModeBox.Text))
        {
            CustomModeBox.Focus(FocusState.Programmatic);
        }

        if (!_uiReady || _syncingModeSelection)
        {
            return;
        }

        SyncCompactModeBox();
        SelectTemplateSourceForMode(_selectedMode);
        RefreshModelDisplayText();
        RefreshScene();
        RefreshTemplateViews();
        SaveUiSettings();
    }

    private void OptimizationTargetChoiceList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (!_uiReady || _syncingOptimizationTargetSelection || e.ClickedItem is not OptimizationTargetItem target)
        {
            return;
        }

        var requestedMode = MakeOptimizationTargetMode(target.Id);
        if (!CanUseConversationMode(requestedMode))
        {
            RejectConversationModeChange();
            return;
        }

        _selectedMode = requestedMode;
        _syncingModeSelection = true;
        SetModeRadioState(_selectedMode);
        _syncingModeSelection = false;

        SyncCompactModeBox();
        SelectTemplateSourceForMode(_selectedMode);
        RefreshModelDisplayText();
        RefreshScene();
        RefreshTemplateViews();
        SaveUiSettings();
        SetStatus($"{L("已切换优化目标")}：{target.Title}");
    }

    private void OptimizationTargetManagementList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OptimizationTargetItem target)
        {
            OptimizationTargetManagementList.SelectedItem = target;
        }
    }

    private void CompactModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _syncingModeSelection || CompactModeBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var requestedMode = item.Tag?.ToString() ?? "通用 LLM";
        if (!CanUseConversationMode(requestedMode))
        {
            RejectConversationModeChange();
            return;
        }

        _selectedMode = requestedMode;
        _syncingModeSelection = true;
        SetModeRadioState(_selectedMode);
        _syncingModeSelection = false;

        SelectTemplateSourceForMode(_selectedMode);
        RefreshModelDisplayText();
        RefreshScene();
        RefreshTemplateViews();
        SaveUiSettings();
    }

    private async void CompactTemplateBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _syncingTemplateSelection || CompactTemplateBox.SelectedItem is not PromptTemplateCatalogItem template)
        {
            return;
        }

        _syncingTemplateSelection = true;
        CompactTemplateBox.SelectedItem = null;
        _syncingTemplateSelection = false;
        await InsertFavoriteTemplateAsync(template);
    }

    private void CompactSkillBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _syncingTemplateSelection || CompactSkillBox.SelectedItem is not CompactSkillChoice choice)
        {
            return;
        }

        _selectedMountedSkillId = choice.Template?.Id;
        SyncMountedSkillQuickSelection(_selectedMountedSkillId);
        RefreshMountedSkillStatus(GetMountedSkillTemplates());
        SetStatus(choice.Template is null ? "小窗口已取消 Skill" : $"小窗口已选择 Skill：{choice.Title}");
    }

    private void CustomModeBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady || _syncingModeSelection)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(CustomModeBox.Text))
        {
            if (!CanUseConversationMode("自定义")
                || (_conversationLockedCustomModeText is not null
                    && !string.Equals(CustomModeBox.Text.Trim(), _conversationLockedCustomModeText, StringComparison.Ordinal)))
            {
                RejectConversationModeChange();
                return;
            }

            ModeCustomRadio.IsChecked = true;
            _selectedMode = "自定义";
        }

        SelectTemplateSourceForMode(GetEffectiveMode());
        RefreshModelDisplayText();
        RefreshScene();
        RefreshTemplateViews();
        SaveUiSettings();
    }

    private void SceneChip_Click(object sender, RoutedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        RefreshScene();
        SaveUiSettings();
    }

    private void CustomSceneBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        RefreshScene();
        SaveUiSettings();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideWindow();
    }

    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            HideWindow();
            if (_lastForegroundWindow != 0)
            {
                _foregroundWindowService.ActivateWindow(_lastForegroundWindow);
            }

            e.Handled = true;
        }
    }

    private void ResizeWindow(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void CenterWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var x = displayArea.WorkArea.X + (displayArea.WorkArea.Width - appWindow.Size.Width) / 2;
        var y = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - appWindow.Size.Height) / 2;
        appWindow.Move(new Windows.Graphics.PointInt32(x, y));
    }

    private void HideWindow()
    {
        ShowWindow(WindowNative.GetWindowHandle(this), SwHide);
    }

    private static async Task WaitForScreenCaptureSettleAsync()
    {
        await Task.Yield();
        await Task.Delay(ScreenCaptureSettleDelayMs);
    }

    private void SetTopmost(bool topmost)
    {
        SetWindowPos(WindowNative.GetWindowHandle(this), topmost ? HwndTopmost : HwndNotTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void LoadSettingsIntoUi()
    {
        _loadingSettings = true;
        _settings = _settingsService.Load();
        ModelEnabledBox.IsChecked = _settings.Model.Enabled;
        ModelExternalRequestsEnabledBox.IsChecked = _settings.Privacy.ModelExternalRequestsEnabled;
        ModelImageExternalRequestsEnabledBox.IsChecked = _settings.Privacy.ModelImageExternalRequestsEnabled;
        RedactBeforeModelSendBox.IsChecked = _settings.Privacy.RedactBeforeModelSend;
        BaseUrlBox.Text = _settings.Model.BaseUrl;
        ModelNameBox.Text = _settings.Model.Model;
        var providerId = string.IsNullOrWhiteSpace(_settings.Model.ProviderId)
            || string.Equals(_settings.Model.ProviderId, "custom", StringComparison.OrdinalIgnoreCase)
            ? DetectProviderIdFromBaseUrl(_settings.Model.BaseUrl)
            : _settings.Model.ProviderId;
        SelectProviderPresetForBaseUrl(_settings.Model.BaseUrl);
        ApiKeyBox.Password = ReadProviderApiKey(providerId);
        HotkeyEnabledBox.IsChecked = _settings.Hotkey.Enabled;
        HotkeyCtrlBox.IsChecked = _settings.Hotkey.Control;
        HotkeyShiftBox.IsChecked = _settings.Hotkey.Shift;
        HotkeyAltBox.IsChecked = _settings.Hotkey.Alt;
        HotkeyWinBox.IsChecked = _settings.Hotkey.Win;
        SelectHotkeyMainKey(_settings.Hotkey.Key);
        OcrEnabledBox.IsChecked = _settings.Privacy.OcrEnabled;
        SelectOcrProvider(_settings.Ocr.PreferredProvider);
        var languagePack = _localizationService.Load(_settings.Ui.LanguageCode, _settings.Ui.MountedLanguagePackPath);
        SelectLanguage(_settings.Ui.LanguageCode, languagePack.DisplayName, languagePack.SourcePath);
        ApplyUiSettings();
        _loadingSettings = false;
        if (_uiReady)
        {
            ScheduleModelProbe();
        }
    }

    private void RefreshFavoritesUi(string? selectedId = null)
    {
        RefreshTemplateViews();
        RefreshUserTemplateList();
        var templates = GetUserTemplateCatalog().ToArray();

        var selectedTemplate = selectedId is null
            ? templates.FirstOrDefault()
            : templates.FirstOrDefault(template => string.Equals(template.Id, selectedId, StringComparison.OrdinalIgnoreCase));

        FavoritesBox.SelectedItem = selectedTemplate;
    }

    private IReadOnlyList<PromptTemplateCatalogItem> GetTemplateCatalog()
    {
        return _templateCatalogService
            .MergeWithUserTemplates(_promptFavoriteService.Load().Take(100))
            .Where(template => !string.Equals(template.Source, SkillTemplateSource, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private IReadOnlyList<PromptTemplateCatalogItem> GetTemplateCatalogForSource(string source)
    {
        return _templateCatalogService
            .MergeWithUserTemplatesBySource(_promptFavoriteService.Load().Take(100), source)
            .ToArray();
    }

    private IReadOnlyList<PromptTemplateCatalogItem> GetUserTemplateCatalog()
    {
        return _promptFavoriteService.Load()
            .Take(100)
            .Where(item => !string.IsNullOrWhiteSpace(item.Text)
                && !string.Equals(item.Source, SkillTemplateSource, StringComparison.OrdinalIgnoreCase))
            .Select(item => new PromptTemplateCatalogItem(
                item.Id,
                item.Title,
                string.IsNullOrWhiteSpace(item.Source) ? "我的模板" : item.Source,
                string.IsNullOrWhiteSpace(item.Category) ? "未分类" : item.Category,
                item.Text,
                true))
            .ToArray();
    }

    private void RefreshTemplateViews()
    {
        _selectedTemplateSource = NormalizeTemplateSource(_selectedTemplateSource);
        RefreshTemplateSourcePicker();
        var templates = GetTemplateCatalogForSource(_selectedTemplateSource).ToArray();
        var categories = templates
            .Select(template => template.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .Prepend("全部")
            .ToArray();

        if (!categories.Contains(_selectedTemplateCategory, StringComparer.OrdinalIgnoreCase))
        {
            _selectedTemplateCategory = "全部";
        }

        var search = TemplateSearchBox.Text.Trim();
        var visibleTemplates = templates
            .Where(template => _selectedTemplateCategory == "全部" || string.Equals(template.Category, _selectedTemplateCategory, StringComparison.OrdinalIgnoreCase))
            .Where(template => string.IsNullOrWhiteSpace(search)
                || template.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || template.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
                || template.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        _syncingTemplateSelection = true;
        SetLocalizedListItemsKeepingSelection(TemplateCategoryList, categories, _selectedTemplateCategory);
        QuickTemplateList.ItemsSource = visibleTemplates;
        _syncingTemplateSelection = false;
        RefreshMountedSkillQuickList();
        RefreshCompactTemplatePicker();
    }

    private void RefreshTemplateSourcePicker()
    {
        if (TemplateSourceBox is null)
        {
            return;
        }

        _selectedTemplateSource = NormalizeTemplateSource(_selectedTemplateSource);
        var choices = TemplateSourceDefinitions
            .Select(source => new TemplateSourceChoice(source.Source, L(source.Title), L(source.Description)))
            .ToArray();

        _syncingTemplateSelection = true;
        try
        {
            TemplateSourceBox.ItemsSource = choices;
            TemplateSourceBox.SelectedItem = choices.FirstOrDefault(choice => string.Equals(choice.Value, _selectedTemplateSource, StringComparison.OrdinalIgnoreCase))
                ?? choices.FirstOrDefault();
        }
        finally
        {
            _syncingTemplateSelection = false;
        }
    }

    private IReadOnlyList<PromptTemplateCatalogItem> GetMountedSkillTemplates()
    {
        return GetTemplateCatalogForSource(SkillTemplateSource)
            .Where(IsMountedSkillTemplate)
            .ToArray();
    }

    private static bool IsMountedSkillTemplate(PromptTemplateCatalogItem template)
    {
        if (!string.Equals(template.Source, SkillTemplateSource, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return template.Text.Contains("【挂载 Skill】", StringComparison.Ordinal)
            || template.Text.Contains("【Skill 包】", StringComparison.Ordinal)
            || template.Text.Contains("【内置 Skill 包】", StringComparison.Ordinal);
    }

    private PromptTemplateCatalogItem? GetSelectedMountedSkill()
    {
        if (MountedSkillQuickList?.SelectedItem is PromptTemplateCatalogItem selected)
        {
            return selected;
        }

        if (string.IsNullOrWhiteSpace(_selectedMountedSkillId))
        {
            return null;
        }

        return GetMountedSkillTemplates()
            .FirstOrDefault(template => string.Equals(template.Id, _selectedMountedSkillId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshMountedSkillQuickList()
    {
        if (MountedSkillQuickList is null)
        {
            return;
        }

        var mountedSkills = GetMountedSkillTemplates();
        MountedSkillQuickList.ItemsSource = mountedSkills;
        PromptTemplateCatalogItem? selected = null;
        if (!string.IsNullOrWhiteSpace(_selectedMountedSkillId))
        {
            selected = mountedSkills
                .FirstOrDefault(template => string.Equals(template.Id, _selectedMountedSkillId, StringComparison.OrdinalIgnoreCase));
            MountedSkillQuickList.SelectedItem = selected;
            if (MountedSkillQuickList.SelectedItem is null)
            {
                _selectedMountedSkillId = null;
            }
        }
        else
        {
            MountedSkillQuickList.SelectedItem = null;
        }

        RefreshCompactSkillPicker(mountedSkills);

        if (MountedSkillEmptyText is not null)
        {
            MountedSkillEmptyText.Visibility = mountedSkills.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        RefreshMountedSkillStatus(mountedSkills, selected);
    }

    private void RefreshMountedSkillStatus(
        IReadOnlyList<PromptTemplateCatalogItem> mountedSkills,
        PromptTemplateCatalogItem? selected = null)
    {
        if (MountedSkillStatusText is null)
        {
            return;
        }

        selected ??= MountedSkillQuickList?.SelectedItem as PromptTemplateCatalogItem;
        MountedSkillStatusText.Text = mountedSkills.Count == 0
            ? "当前 Skill：未挂载"
            : selected is null
                ? $"可用 Skill：{mountedSkills.Count} 个，请选择一个"
                : $"当前 Skill：{selected.Title}";
    }

    private void RefreshCompactSkillPicker(IReadOnlyList<PromptTemplateCatalogItem> mountedSkills)
    {
        if (CompactSkillBox is null)
        {
            return;
        }

        var choices = mountedSkills
            .Select(skill => new CompactSkillChoice(
                skill.Id,
                skill.Title,
                string.IsNullOrWhiteSpace(skill.Category) ? L("挂载 Skill") : skill.Category,
                skill))
            .Prepend(new CompactSkillChoice(string.Empty, L("不使用 Skill"), L("仅按当前优化目标生成"), null))
            .ToArray();

        _syncingTemplateSelection = true;
        try
        {
            CompactSkillBox.ItemsSource = choices;
            SelectCompactSkill(_selectedMountedSkillId);
        }
        finally
        {
            _syncingTemplateSelection = false;
        }
    }

    private void SelectCompactSkill(string? skillId)
    {
        if (CompactSkillBox?.ItemsSource is not IEnumerable<CompactSkillChoice> choices)
        {
            return;
        }

        CompactSkillBox.SelectedItem = string.IsNullOrWhiteSpace(skillId)
            ? choices.FirstOrDefault(choice => choice.Template is null)
            : choices.FirstOrDefault(choice => string.Equals(choice.Id, skillId, StringComparison.OrdinalIgnoreCase))
                ?? choices.FirstOrDefault(choice => choice.Template is null);
    }

    private void SyncMountedSkillQuickSelection(string? skillId)
    {
        if (MountedSkillQuickList is null)
        {
            return;
        }

        MountedSkillQuickList.SelectedItem = string.IsNullOrWhiteSpace(skillId)
            ? null
            : GetMountedSkillTemplates()
                .FirstOrDefault(template => string.Equals(template.Id, skillId, StringComparison.OrdinalIgnoreCase));
    }

    private void RefreshCompactTemplatePicker()
    {
        if (CompactTemplateBox is null)
        {
            return;
        }

        var selectedTarget = GetSelectedOptimizationTarget();
        var modeSource = NormalizeTemplateSource(selectedTarget?.TemplateSource ?? GetTemplateSourceForMode(_selectedMode));
        var templates = GetTemplateCatalogForSource(modeSource)
            .Take(80)
            .ToArray();

        _syncingTemplateSelection = true;
        CompactTemplateBox.ItemsSource = templates;
        CompactTemplateBox.SelectedItem = null;
        CompactTemplateBox.PlaceholderText = templates.Length == 0 ? L("暂无对应模板") : L("选择目标对应模板");
        _syncingTemplateSelection = false;
    }

    private void RefreshUserTemplateList()
    {
        var userTemplates = GetUserTemplateCatalog();
        var userCategories = userTemplates
            .Select(template => template.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .Prepend("全部")
            .ToArray();

        _syncingTemplateSelection = true;
        SetComboItemsKeepingSelection(TemplateCategoryFilterBox, userCategories);
        _syncingTemplateSelection = false;

        var userCategory = GetChoiceValue(TemplateCategoryFilterBox.SelectedItem);
        FavoritesBox.ItemsSource = userTemplates
            .Where(template => string.IsNullOrWhiteSpace(userCategory) || userCategory == "全部" || string.Equals(template.Category, userCategory, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void RefreshSkillManagementUi(string? selectedId = null)
    {
        var skillTemplates = GetTemplateCatalogForSource(SkillTemplateSource).ToArray();
        var categories = skillTemplates
            .Select(template => template.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .Prepend("全部")
            .ToArray();

        _syncingTemplateSelection = true;
        SetComboItemsKeepingSelection(SkillCategoryFilterBox, categories);
        _syncingTemplateSelection = false;

        var selectedCategory = GetChoiceValue(SkillCategoryFilterBox.SelectedItem);
        var visibleTemplates = skillTemplates
            .Where(template => string.IsNullOrWhiteSpace(selectedCategory)
                || selectedCategory == "全部"
                || string.Equals(template.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        SkillList.ItemsSource = visibleTemplates;
        SkillList.SelectedItem = selectedId is null
            ? visibleTemplates.FirstOrDefault()
            : visibleTemplates.FirstOrDefault(template => string.Equals(template.Id, selectedId, StringComparison.OrdinalIgnoreCase));
    }

    private void SetComboItemsKeepingSelection(ComboBox comboBox, IReadOnlyList<string> items)
    {
        var selected = GetChoiceValue(comboBox.SelectedItem);
        var choices = BuildLocalizedChoices(items);
        comboBox.ItemsSource = choices;
        comboBox.SelectedItem = choices.FirstOrDefault(item => string.Equals(item.Value, selected, StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();
    }

    private void SelectComboChoice(ComboBox comboBox, string value)
    {
        var choices = comboBox.ItemsSource as IEnumerable<LocalizedChoice> ?? comboBox.Items.OfType<LocalizedChoice>();
        comboBox.SelectedItem = choices.FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase));
    }

    private void SetLocalizedListItemsKeepingSelection(ListView listView, IReadOnlyList<string> items, string selectedValue)
    {
        var choices = BuildLocalizedChoices(items);
        listView.ItemsSource = choices;
        listView.SelectedItem = choices.FirstOrDefault(item => string.Equals(item.Value, selectedValue, StringComparison.OrdinalIgnoreCase)) ?? choices.FirstOrDefault();
    }

    private LocalizedChoice[] BuildLocalizedChoices(IEnumerable<string> items)
    {
        return items
            .Select(item => new LocalizedChoice(item, L(item)))
            .ToArray();
    }

    private static string GetChoiceValue(object? item)
    {
        return item switch
        {
            LocalizedChoice choice => choice.Value,
            string text => text,
            null => string.Empty,
            _ => item.ToString() ?? string.Empty
        };
    }

    private static T? FindDataContext<T>(DependencyObject? source) where T : class
    {
        var current = source;
        while (current is not null)
        {
            if (current is FrameworkElement element && element.DataContext is T value)
            {
                return value;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void RefreshHistoryUi()
    {
        var query = HistorySearchBox?.Text.Trim() ?? string.Empty;
        HistoryList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _historyService.Load().Take(100).ToArray()
            : _historyService.Search(query, 100).ToArray();
    }

    private void RefreshSearchIndex()
    {
        try
        {
            _databaseService.ReplaceTemplateIndex(GetTemplateCatalog());
            _databaseService.ReplaceOptimizationTargetIndex(_optimizationTargetService.Load());
        }
        catch (Exception ex)
        {
            SetStatus($"搜索索引刷新失败：{ex.Message}");
        }
    }

    private void QueueRefreshSearchIndex()
    {
        _ = Task.Run(() =>
        {
            try
            {
                var templates = GetTemplateCatalog();
                var targets = _optimizationTargetService.Load();
                _databaseService.ReplaceTemplateIndex(templates);
                _databaseService.ReplaceOptimizationTargetIndex(targets);
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => SetStatus($"搜索索引刷新失败：{ex.Message}"));
            }
        });
    }

    private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_uiReady)
        {
            return;
        }

        RefreshHistoryUi();
    }

    private void RefreshCommonPromptsUi(string? selectedId = null)
    {
        var allItems = _commonPromptService.Load().ToArray();
        var items = string.IsNullOrWhiteSpace(_commonPromptSearchText)
            ? allItems
            : _commonPromptService.Search(_commonPromptSearchText, 200).ToArray();
        var categories = allItems
            .Select(item => string.IsNullOrWhiteSpace(item.Category) ? "未分类" : item.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .Prepend("全部")
            .ToArray();

        if (!categories.Contains(_selectedCommonPromptCategory, StringComparer.OrdinalIgnoreCase))
        {
            _selectedCommonPromptCategory = "全部";
        }

        _syncingCommonPromptSelection = true;
        SetComboItemsKeepingSelection(CommonPromptCategoryFilterBox, categories);
        SelectComboChoice(CommonPromptCategoryFilterBox, _selectedCommonPromptCategory);
        SetComboItemsKeepingSelection(CompactCommonPromptCategoryFilterBox, categories);
        SelectComboChoice(CompactCommonPromptCategoryFilterBox, _selectedCommonPromptCategory);
        var selectedCategory = _selectedCommonPromptCategory;
        var visibleItems = items
            .Where(item => string.IsNullOrWhiteSpace(selectedCategory)
                || selectedCategory == "全部"
                || string.Equals(item.Category, selectedCategory, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        CommonPromptList.ItemsSource = visibleItems;
        CommonPromptList.SelectedItem = selectedId is null
            ? null
            : visibleItems.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        CompactCommonPromptList.ItemsSource = visibleItems;
        CompactCommonPromptList.SelectedItem = selectedId is null
            ? null
            : visibleItems.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        _syncingCommonPromptSelection = false;
    }

    private void InitializeHotkeyKeyOptions()
    {
        HotkeyKeyBox.Items.Clear();
        foreach (var key in GlobalHotkeyService.SupportedMainKeys)
        {
            HotkeyKeyBox.Items.Add(new ComboBoxItem
            {
                Content = key,
                Tag = key
            });
        }

        SelectHotkeyMainKey("Space");
    }

    private string GetSelectedHotkeyMainKey()
    {
        return HotkeyKeyBox.SelectedItem is ComboBoxItem item
            ? GlobalHotkeyService.NormalizeMainKey(item.Tag?.ToString() ?? item.Content?.ToString())
            : "Space";
    }

    private void SelectHotkeyMainKey(string? key)
    {
        var normalized = GlobalHotkeyService.NormalizeMainKey(key);
        for (var i = 0; i < HotkeyKeyBox.Items.Count; i++)
        {
            if (HotkeyKeyBox.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                HotkeyKeyBox.SelectedIndex = i;
                return;
            }
        }

        HotkeyKeyBox.SelectedIndex = 0;
    }

    private void SelectOcrProvider(string providerId)
    {
        for (var i = 0; i < OcrProviderBox.Items.Count; i++)
        {
            if (OcrProviderBox.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), providerId, StringComparison.OrdinalIgnoreCase))
            {
                OcrProviderBox.SelectedIndex = i;
                return;
            }
        }

        OcrProviderBox.SelectedIndex = 0;
    }

    private void SelectLanguage(string languageCode, string? displayName = null, string? sourcePath = null)
    {
        var mountedPath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;
        for (var i = 0; i < LanguageBox.Items.Count; i++)
        {
            if (LanguageBox.Items[i] is ComboBoxItem item
                && string.Equals(item.Tag?.ToString(), languageCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.DataContext as string ?? string.Empty, mountedPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                LanguageBox.SelectedIndex = i;
                return;
            }
        }

        var customItem = new ComboBoxItem
        {
            Tag = languageCode,
            Content = string.IsNullOrWhiteSpace(displayName) ? languageCode : displayName,
            DataContext = mountedPath
        };
        LanguageBox.Items.Add(customItem);
        LanguageBox.SelectedItem = customItem;
    }

    private void RefreshOptimizationTargetPickers(string? selectedTargetId = null)
    {
        var targets = _optimizationTargetService.Load();
        var selectedId = selectedTargetId ?? GetOptimizationTargetId(_selectedMode);
        _syncingOptimizationTargetSelection = true;
        try
        {
            OptimizationTargetChoiceList.Items.Clear();
            OptimizationTargetManagementList.Items.Clear();
            OptimizationTargetItem? selectedTarget = null;
            foreach (var target in targets)
            {
                OptimizationTargetChoiceList.Items.Add(target);
                OptimizationTargetManagementList.Items.Add(target);
                if (string.Equals(target.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedTarget = target;
                }
            }

            OptimizationTargetChoiceList.SelectedItem = selectedTarget;
            OptimizationTargetManagementList.SelectedItem = selectedTarget;
        }
        finally
        {
            _syncingOptimizationTargetSelection = false;
        }

        RefreshCompactOptimizationTargetItems(targets);
        SyncCompactModeBox();
    }

    private void RefreshCompactOptimizationTargetItems(IReadOnlyList<OptimizationTargetItem> targets)
    {
        if (CompactModeBox is null)
        {
            return;
        }

        for (var i = CompactModeBox.Items.Count - 1; i >= 0; i--)
        {
            if (CompactModeBox.Items[i] is ComboBoxItem item
                && IsOptimizationTargetMode(item.Tag?.ToString()))
            {
                CompactModeBox.Items.RemoveAt(i);
            }
        }

        foreach (var target in targets)
        {
            var item = new ComboBoxItem
            {
                Tag = MakeOptimizationTargetMode(target.Id),
                Content = target.Title
            };
            if (!string.IsNullOrWhiteSpace(target.Description))
            {
                ToolTipService.SetToolTip(item, target.Description);
            }

            CompactModeBox.Items.Add(item);
        }
    }

    private void SetModeRadioState(string mode)
    {
        ModeGenericRadio.IsChecked = mode == "通用 LLM";
        ModeAcademicHumanizeRadio.IsChecked = mode == "论文去AI味";
        ModeTextImageRadio.IsChecked = mode == "文生图";
        ModeJimengRadio.IsChecked = mode == "即梦";
        ModeVeo3Radio.IsChecked = mode == "Veo 3";
        ModeAiCodingRadio.IsChecked = mode == "AI编程";
        ModeCustomRadio.IsChecked = mode == "自定义";
        SelectOptimizationTargetChoice(mode);
    }

    private void SelectOptimizationTargetChoice(string mode)
    {
        var targetId = GetOptimizationTargetId(mode);
        _syncingOptimizationTargetSelection = true;
        try
        {
            var selectedTarget = string.IsNullOrWhiteSpace(targetId)
                ? null
                : OptimizationTargetChoiceList.Items
                    .OfType<OptimizationTargetItem>()
                    .FirstOrDefault(target => string.Equals(target.Id, targetId, StringComparison.OrdinalIgnoreCase));
            OptimizationTargetChoiceList.SelectedItem = selectedTarget;
            OptimizationTargetManagementList.SelectedItem = selectedTarget;
        }
        finally
        {
            _syncingOptimizationTargetSelection = false;
        }
    }

    private void SyncCompactModeBox()
    {
        if (CompactModeBox is null)
        {
            return;
        }

        _syncingModeSelection = true;
        foreach (var item in CompactModeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), _selectedMode, StringComparison.OrdinalIgnoreCase))
            {
                CompactModeBox.SelectedItem = item;
                _syncingModeSelection = false;
                return;
            }
        }

        CompactModeBox.SelectedIndex = 0;
        _syncingModeSelection = false;
    }

    private void SelectTemplateSourceForMode(string mode)
    {
        _selectedTemplateSource = NormalizeTemplateSource(GetSelectedOptimizationTarget(mode)?.TemplateSource ?? GetTemplateSourceForMode(mode));
        _selectedTemplateCategory = "全部";
        SetTemplateTabState(skillSelected: false);
    }

    private static string NormalizeTemplateSource(string? source)
    {
        if (!string.IsNullOrWhiteSpace(source))
        {
            var matched = TemplateSourceDefinitions
                .FirstOrDefault(item => string.Equals(item.Source, source.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched is not null)
            {
                return matched.Source;
            }
        }

        return "ChatGPT-Shortcut";
    }

    private static string GetTemplateSourceForMode(string mode)
    {
        if (IsOptimizationTargetMode(mode))
        {
            return "ChatGPT-Shortcut";
        }

        if (mode.Contains("文生图", StringComparison.Ordinal))
        {
            return "SD-Anima-Prompt-Studio";
        }

        if (mode.Contains("Veo", StringComparison.OrdinalIgnoreCase))
        {
            return "Veo 3";
        }

        if (mode.Contains("即梦", StringComparison.Ordinal) || mode.Contains("Seedance", StringComparison.OrdinalIgnoreCase))
        {
            return "即梦 / Seedance";
        }

        if (mode.Contains("AI编程", StringComparison.Ordinal) || mode.Contains("AI 编程", StringComparison.Ordinal))
        {
            return "AI编程";
        }

        if (mode.Contains("论文", StringComparison.Ordinal)
            || mode.Contains("去AI味", StringComparison.Ordinal)
            || mode.Contains("Academic", StringComparison.OrdinalIgnoreCase))
        {
            return "ChatGPT-Shortcut";
        }

        return "ChatGPT-Shortcut";
    }

    private void ApplyUiSettings()
    {
        _selectedMode = _settings.Ui.SelectedMode;
        RefreshOptimizationTargetPickers(GetOptimizationTargetId(_selectedMode));
        _syncingModeSelection = true;
        SetModeRadioState(_selectedMode);
        _syncingModeSelection = false;
        SyncCompactModeBox();
        CustomModeBox.Text = _settings.Ui.CustomMode;
        SelectTemplateSourceForMode(GetEffectiveMode());
        SceneTextButton.IsChecked = _settings.Ui.SceneText;
        SceneImageButton.IsChecked = _settings.Ui.SceneImage;
        SceneJimengButton.IsChecked = _settings.Ui.SceneJimeng;
        SceneVeoButton.IsChecked = _settings.Ui.SceneVeo;
        SceneUiButton.IsChecked = _settings.Ui.SceneUi;
        SceneVideoButton.IsChecked = _settings.Ui.SceneVideo;
        SyncDeepThinkingToggles(_settings.Ui.DeepThinking);
        CustomSceneBox.Text = _settings.Ui.CustomScene;
        RefreshModelDisplayText();
        RefreshScene();
        RefreshCompactTemplatePicker();
    }

    private void ApplyLocalization()
    {
        _settings = _settingsService.Load();
        _languagePack = _localizationService.Load(_settings.Ui.LanguageCode, _settings.Ui.MountedLanguagePackPath);
        Title = L("啊拼 / AI Quick Prompt");
        RefreshTemplateSourcePicker();
        LocalizeObject(Content as DependencyObject);
        RefreshRuntimeLocalizedText();
    }

    private void LocalizeObject(DependencyObject? root)
    {
        if (root is null)
        {
            return;
        }

        switch (root)
        {
            case TextBlock textBlock:
                textBlock.Text = L(GetOriginal(textBlock, nameof(TextBlock.Text), textBlock.Text));
                break;
            case ContentControl contentControl when contentControl.Content is string content:
                contentControl.Content = L(GetOriginal(contentControl, nameof(ContentControl.Content), content));
                break;
            case TextBox textBox:
                if (textBox.Header is string textHeader)
                {
                    textBox.Header = L(GetOriginal(textBox, $"{nameof(TextBox.Header)}", textHeader));
                }

                textBox.PlaceholderText = L(GetOriginal(textBox, nameof(TextBox.PlaceholderText), textBox.PlaceholderText));
                break;
            case PasswordBox passwordBox:
                if (passwordBox.Header is string passwordHeader)
                {
                    passwordBox.Header = L(GetOriginal(passwordBox, $"{nameof(PasswordBox.Header)}", passwordHeader));
                }

                passwordBox.PlaceholderText = L(GetOriginal(passwordBox, nameof(PasswordBox.PlaceholderText), passwordBox.PlaceholderText));
                break;
            case ComboBox comboBox:
                if (comboBox.Header is string comboHeader)
                {
                    comboBox.Header = L(GetOriginal(comboBox, $"{nameof(ComboBox.Header)}", comboHeader));
                }

                comboBox.PlaceholderText = L(GetOriginal(comboBox, nameof(ComboBox.PlaceholderText), comboBox.PlaceholderText));
                foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
                {
                    LocalizeComboBoxItem(item);
                }

                break;
            case ComboBoxItem comboBoxItem:
                LocalizeComboBoxItem(comboBoxItem);
                break;
        }

        if (root is Button button && button.Flyout is MenuFlyout menuFlyout)
        {
            foreach (var item in menuFlyout.Items.OfType<MenuFlyoutItem>())
            {
                item.Text = L(GetOriginal(item, nameof(MenuFlyoutItem.Text), item.Text));
            }
        }

        if (root is FrameworkElement element && ToolTipService.GetToolTip(element) is string toolTip)
        {
            ToolTipService.SetToolTip(element, L(GetOriginal(element, "ToolTip", toolTip)));
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childCount; i++)
        {
            LocalizeObject(VisualTreeHelper.GetChild(root, i));
        }
    }

    private void LocalizeComboBoxItem(ComboBoxItem item)
    {
        if (item.Content is string content)
        {
            item.Content = L(GetOriginal(item, nameof(ComboBoxItem.Content), content));
        }
    }

    private string GetOriginal(object target, string propertyName, string currentValue)
    {
        var key = $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target)}:{propertyName}";
        if (!_originalLocalizedValues.TryGetValue(key, out var original))
        {
            original = currentValue;
            _originalLocalizedValues[key] = original;
        }

        return original;
    }

    private string L(string text)
    {
        return _languagePack.Translate(text);
    }

    private void RefreshAboutUi()
    {
        var version = typeof(CompactPromptWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        VersionTextBlock.Text = $"{L("版本号：")}{version}";
    }

    private void RefreshRuntimeLocalizedText()
    {
        RefreshScene();
        RefreshModelDisplayText();
        RefreshAboutUi();
        RefreshPromptCountLabels();
    }

    private void RefreshPromptCountLabels()
    {
        if (CurrentPromptBox is not null)
        {
            CurrentPromptCountText.Text = $"{L("字数：")}{CurrentPromptBox.Text.Length}";
        }

        UpdateChineseOutputCounts();
        UpdateEnglishCounts(EnglishOutputBox?.Text ?? string.Empty);
    }

    private void RefreshModelDisplayText()
    {
        var currentModel = BuildCurrentModelDisplayText();
        BottomModelText.Text = $"{L("当前模型：")}{L(currentModel)}";
        TopModelText.Text = $"{L("当前模型：")}{L(currentModel)}";
        RefreshModelCapabilityText();
        RefreshWorkflowModelBox();
    }

    private string BuildCurrentModelDisplayText()
    {
        _settings = _settingsService.Load();
        if (!_settings.Model.Enabled)
        {
            return "本地结构化";
        }

        var model = _settings.Model.Model.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            return "未配置模型";
        }

        var provider = DetectProviderName(_settings.Model.BaseUrl, model);
        return string.IsNullOrWhiteSpace(provider) ? model : $"{provider} {model}";
    }

    private void RefreshModelCapabilityText()
    {
        var capability = ResolveCurrentReasoningCapability();
        var model = GetCurrentModelNameForTags();
        var tags = BuildModelTagList(capability.ProviderId, model);
        var text = string.IsNullOrWhiteSpace(model)
            ? $"{L("模型标签：")}{L("未配置模型")}"
            : $"{L("模型标签：")}{string.Join(" · ", tags.Select(L))}";

        if (ModelCapabilityText is not null)
        {
            ModelCapabilityText.Text = text;
        }

        if (DeepThinkingToggle is not null)
        {
            ToolTipService.SetToolTip(DeepThinkingToggle, L(capability.Description));
        }

        if (CompactDeepThinkingToggle is not null)
        {
            ToolTipService.SetToolTip(CompactDeepThinkingToggle, L(capability.Description));
        }
    }

    private ModelReasoningCapability ResolveCurrentReasoningCapability()
    {
        var providerId = GetCurrentProviderIdFromSettingsOrUi();
        var baseUrl = BaseUrlBox?.Text ?? _settings.Model.BaseUrl;
        var model = GetCurrentModelNameForTags();
        return ResolveReasoningCapability(providerId, baseUrl, model);
    }

    private string GetCurrentProviderIdFromSettingsOrUi()
    {
        if (ProviderPresetBox is not null)
        {
            var providerId = GetCurrentProviderId();
            if (!string.IsNullOrWhiteSpace(providerId))
            {
                return providerId;
            }
        }

        return string.IsNullOrWhiteSpace(_settings.Model.ProviderId)
            ? DetectProviderIdFromBaseUrl(_settings.Model.BaseUrl)
            : _settings.Model.ProviderId;
    }

    private string GetCurrentModelNameForTags()
    {
        if (ModelNameBox is not null && !string.IsNullOrWhiteSpace(ModelNameBox.Text))
        {
            return ModelNameBox.Text.Trim();
        }

        return _settings.Model.Model.Trim();
    }

    private static IReadOnlyList<string> BuildModelTagList(string providerId, string model)
    {
        var capability = ResolveReasoningCapability(providerId, string.Empty, model);
        var tags = new List<string> { GetProviderLabelById(capability.ProviderId) };
        if (capability.HasNativeSupport)
        {
            tags.Add(capability.ModelTag);
        }
        else
        {
            tags.Add("深度思考：提示词摘要");
        }

        return tags;
    }

    private static string FormatModelLabelWithTags(string providerId, string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        var tags = BuildModelTagList(providerId, model)
            .Skip(1)
            .Where(tag => !string.Equals(tag, "深度思考：提示词摘要", StringComparison.Ordinal))
            .ToArray();
        return tags.Length == 0 ? model : $"{model}  [{string.Join(" · ", tags)}]";
    }

    private string BuildModelCapabilityTooltip(string providerId, string model, string? ownedBy = null)
    {
        var capability = ResolveReasoningCapability(providerId, string.Empty, model);
        var owner = string.IsNullOrWhiteSpace(ownedBy) ? string.Empty : $"{Environment.NewLine}{L("所属：")}{ownedBy}";
        return $"{model}{Environment.NewLine}{L(capability.Description)}{owner}";
    }

    private static string GetProviderLabelById(string providerId)
    {
        return providerId switch
        {
            "openai" => "OpenAI",
            "deepseek" => "DeepSeek",
            "glm" => "GLM",
            "claude" => "Claude",
            "gemini" => "Gemini",
            "minimax" => "MiniMax",
            "doubao" => "豆包",
            "kimi" => "Kimi",
            _ => "自定义 Provider"
        };
    }

    private static ModelReasoningCapability ResolveReasoningCapability(string? providerId, string? baseUrl, string? model)
    {
        var resolvedProviderId = string.IsNullOrWhiteSpace(providerId) || string.Equals(providerId, "custom", StringComparison.OrdinalIgnoreCase)
            ? DetectProviderIdFromBaseUrl(baseUrl)
            : providerId.Trim().ToLowerInvariant();
        var modelName = model?.Trim() ?? string.Empty;
        var lowerModel = modelName.ToLowerInvariant();
        var looksLikeThinkingModel = lowerModel.Contains("reasoner", StringComparison.Ordinal)
            || lowerModel.Contains("thinking", StringComparison.Ordinal)
            || lowerModel.Contains("r1", StringComparison.Ordinal)
            || lowerModel.Contains("o1", StringComparison.Ordinal)
            || lowerModel.Contains("o3", StringComparison.Ordinal)
            || lowerModel.Contains("o4", StringComparison.Ordinal)
            || lowerModel.Contains("o5", StringComparison.Ordinal);

        return resolvedProviderId switch
        {
            "openai" when IsOpenAiReasoningModel(lowerModel) => new("openai", "OpenAI", "reasoning_effort=high", "reasoning_effort", true, false, "OpenAI 原生标签：reasoning_effort=high。"),
            "openai" => new("openai", "OpenAI", "提示词摘要", string.Empty, false, false, "当前 OpenAI 模型未标记为 reasoning/o 系列，深度思考会使用可展示的提示词式思考摘要。"),
            "deepseek" when IsDeepSeekReasoningModel(lowerModel) => new("deepseek", "DeepSeek", "thinking + reasoning_effort=high + reasoning_content", "deepseek_thinking", true, true, "DeepSeek 原生标签：thinking=enabled、reasoning_effort=high、reasoning_content。"),
            "deepseek" => new("deepseek", "DeepSeek", "提示词摘要", string.Empty, false, false, "当前 DeepSeek 模型没有命中原生思考标签，深度思考会使用提示词式思考摘要。"),
            "gemini" when IsGeminiReasoningModel(lowerModel) => new("gemini", "Gemini", "reasoning_effort=high", "reasoning_effort", true, false, "Gemini OpenAI-compatible 标签：reasoning_effort=high。"),
            "gemini" => new("gemini", "Gemini", "提示词摘要", string.Empty, false, false, "当前 Gemini 模型未标记为 2.5/3 系列，深度思考会使用提示词式思考摘要。"),
            "claude" when IsClaudeThinkingModel(lowerModel) => new("claude", "Claude", "thinking", "anthropic_thinking", true, false, "Claude 原生标签：thinking={type: enabled, budget_tokens: 4096}。"),
            "claude" => new("claude", "Claude", "提示词摘要", string.Empty, false, false, "当前 Claude 模型未标记为支持 extended thinking，深度思考会使用提示词式思考摘要。"),
            "kimi" when IsKimiThinkingModel(lowerModel) => new("kimi", "Kimi", "reasoning_content", string.Empty, false, true, "Kimi thinking 模型标签：reasoning_content。"),
            "kimi" => new("kimi", "Kimi", "提示词摘要", string.Empty, false, false, "当前 Kimi 模型未标记为 thinking，深度思考会使用提示词式思考摘要。"),
            "glm" when looksLikeThinkingModel => new("glm", "GLM", "thinking/reasoner", string.Empty, false, true, "GLM 当前模型名带 thinking/reasoner 标签，会尝试读取 reasoning_content。"),
            "doubao" when looksLikeThinkingModel => new("doubao", "豆包", "thinking/reasoner", string.Empty, false, true, "豆包当前模型名带 thinking/reasoner 标签，会尝试读取 reasoning_content。"),
            "minimax" when looksLikeThinkingModel => new("minimax", "MiniMax", "thinking/reasoner", string.Empty, false, true, "MiniMax 当前模型名带 thinking/reasoner 标签，会尝试读取 reasoning_content。"),
            _ when looksLikeThinkingModel => new(resolvedProviderId, GetProviderLabelById(resolvedProviderId), "thinking/reasoner", string.Empty, false, true, "当前模型名带 thinking/reasoner 标签，会尝试读取 reasoning_content。"),
            _ => new(resolvedProviderId, GetProviderLabelById(resolvedProviderId), "提示词摘要", string.Empty, false, false, "没有命中内置原生思考标签，深度思考会使用提示词式思考摘要。")
        };
    }

    private static bool IsOpenAiReasoningModel(string lowerModel)
    {
        return lowerModel.StartsWith("o1", StringComparison.Ordinal)
            || lowerModel.StartsWith("o3", StringComparison.Ordinal)
            || lowerModel.StartsWith("o4", StringComparison.Ordinal)
            || lowerModel.StartsWith("o5", StringComparison.Ordinal)
            || lowerModel.Contains("gpt-5", StringComparison.Ordinal);
    }

    private static bool IsDeepSeekReasoningModel(string lowerModel)
    {
        return lowerModel.Contains("reasoner", StringComparison.Ordinal)
            || lowerModel.Contains("deepseek-r1", StringComparison.Ordinal)
            || lowerModel.Contains("v4-flash", StringComparison.Ordinal)
            || lowerModel.Contains("deepseek-v4", StringComparison.Ordinal)
            || lowerModel.Contains("v4-pro", StringComparison.Ordinal);
    }

    private static bool IsGeminiReasoningModel(string lowerModel)
    {
        return lowerModel.Contains("gemini-2.5", StringComparison.Ordinal)
            || lowerModel.Contains("gemini-3", StringComparison.Ordinal);
    }

    private static bool IsClaudeThinkingModel(string lowerModel)
    {
        return lowerModel.Contains("sonnet-4", StringComparison.Ordinal)
            || lowerModel.Contains("opus-4", StringComparison.Ordinal)
            || lowerModel.Contains("claude-3-7", StringComparison.Ordinal)
            || lowerModel.Contains("3.7", StringComparison.Ordinal);
    }

    private static bool IsKimiThinkingModel(string lowerModel)
    {
        return lowerModel.Contains("thinking", StringComparison.Ordinal)
            || lowerModel.Contains("k2.5", StringComparison.Ordinal)
            || lowerModel.Contains("k2.6", StringComparison.Ordinal);
    }

    private static string DetectProviderName(string baseUrl, string model)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            var host = uri.Host.ToLowerInvariant();
            if (host.Contains("openai", StringComparison.Ordinal))
            {
                return "OpenAI";
            }

            if (host.Contains("anthropic", StringComparison.Ordinal))
            {
                return "Claude";
            }

            if (host.Contains("google", StringComparison.Ordinal)
                || host.Contains("gemini", StringComparison.Ordinal)
                || host.Contains("generativelanguage", StringComparison.Ordinal))
            {
                return "Gemini";
            }

            if (host.Contains("deepseek", StringComparison.Ordinal))
            {
                return "DeepSeek";
            }

            if (host.Contains("bigmodel", StringComparison.Ordinal)
                || host.Contains("zhipu", StringComparison.Ordinal))
            {
                return "GLM";
            }

            if (host.Contains("minimax", StringComparison.Ordinal)
                || host.Contains("minimaxi", StringComparison.Ordinal))
            {
                return "MiniMax";
            }

            if (host.Contains("volces", StringComparison.Ordinal)
                || host.Contains("volcengine", StringComparison.Ordinal)
                || host.Contains("ark.cn-", StringComparison.Ordinal))
            {
                return "豆包";
            }

            if (host.Contains("moonshot", StringComparison.Ordinal)
                || host.Contains("kimi", StringComparison.Ordinal))
            {
                return "Kimi";
            }

            if (host.Contains("openrouter", StringComparison.Ordinal))
            {
                return "OpenRouter";
            }

            if (host.Contains("siliconflow", StringComparison.Ordinal))
            {
                return "SiliconFlow";
            }

            if (host is "localhost" or "127.0.0.1")
            {
                return "Local";
            }

            var label = host.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrWhiteSpace(label) ? string.Empty : char.ToUpperInvariant(label[0]) + label[1..];
        }

        if (model.StartsWith("gpt", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenAI";
        }

        if (model.StartsWith("claude", StringComparison.OrdinalIgnoreCase))
        {
            return "Claude";
        }

        if (model.Contains("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemini";
        }

        if (model.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            return "DeepSeek";
        }

        if (model.StartsWith("glm", StringComparison.OrdinalIgnoreCase))
        {
            return "GLM";
        }

        if (model.StartsWith("minimax", StringComparison.OrdinalIgnoreCase))
        {
            return "MiniMax";
        }

        if (model.StartsWith("doubao", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("ep-", StringComparison.OrdinalIgnoreCase))
        {
            return "豆包";
        }

        if (model.StartsWith("kimi", StringComparison.OrdinalIgnoreCase))
        {
            return "Kimi";
        }

        return string.Empty;
    }

    private void OpenOpenSourceReferencesButton_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "open-source-references.md");
        if (!File.Exists(path))
        {
            SetStatus("没有找到开源项目引用文档");
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
        {
            UseShellExecute = true
        });
    }

    private void SaveUiSettings()
    {
        _settings = _settingsService.Load();
        _settings.Ui.SelectedMode = _selectedMode;
        _settings.Ui.CustomMode = CustomModeBox.Text.Trim();
        _settings.Ui.SceneText = SceneTextButton.IsChecked == true;
        _settings.Ui.SceneImage = SceneImageButton.IsChecked == true;
        _settings.Ui.SceneJimeng = SceneJimengButton.IsChecked == true;
        _settings.Ui.SceneVeo = SceneVeoButton.IsChecked == true;
        _settings.Ui.SceneUi = SceneUiButton.IsChecked == true;
        _settings.Ui.SceneVideo = SceneVideoButton.IsChecked == true;
        _settings.Ui.DeepThinking = IsDeepThinkingEnabled();
        _settings.Ui.CustomScene = CustomSceneBox.Text.Trim();
        _settingsService.SaveUserSettings(_settings, null);
    }

    private async Task<OcrRouteResult> RecognizeImageFileAsync(Windows.Storage.StorageFile file)
    {
        _settings = _settingsService.Load();
        if (!_settings.Privacy.OcrEnabled)
        {
            throw new InvalidOperationException("OCR 已在设置中关闭。");
        }

        return await _ocrRouter.RecognizeImageFileAsync(file, _settings.Ocr);
    }

    private async Task<OcrRouteResult> RecognizeSoftwareBitmapAsync(Windows.Graphics.Imaging.SoftwareBitmap bitmap)
    {
        _settings = _settingsService.Load();
        if (!_settings.Privacy.OcrEnabled)
        {
            throw new InvalidOperationException("OCR 已在设置中关闭。");
        }

        return await _ocrRouter.RecognizeSoftwareBitmapAsync(bitmap, _settings.Ocr);
    }

    private bool EnsureOcrEnabled()
    {
        _settings = _settingsService.Load();
        if (_settings.Privacy.OcrEnabled)
        {
            return true;
        }

        SetStatus("OCR 已在设置中关闭");
        return false;
    }

    private WindowContext GetTargetWindowContext()
    {
        return _lastForegroundWindow != 0
            ? _foregroundWindowService.GetWindowContext(_lastForegroundWindow)
            : _foregroundWindowService.GetCurrentWindowContext();
    }

    private void SetContextText(string? text, string source, string successMessage, string? contextFieldName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("没有读取到文本");
            return;
        }

        ContextBox.Text = text.Trim();
        _contextSource = source;
        _contextFieldName = contextFieldName;
        SetStatus(successMessage);
    }

    private void SetOcrContextText(OcrRouteResult routeResult, string source, string successMessage, bool appendToUserInput = false)
    {
        var context = GetTargetWindowContext();
        var scene = _sceneDetector.Detect(context);
        var ocrContext = _ocrContextBuilder.Build(routeResult.Result, scene);
        RefreshScene();

        var truncatedText = ocrContext.IsTruncated ? "，已截断预处理" : string.Empty;
        var fallbackText = BuildOcrFallbackStatus(routeResult);
        SetContextText(ocrContext.Text, source, $"{successMessage}：{ocrContext.LineCount} 行，{routeResult.ProviderDisplayName} {routeResult.Duration.TotalMilliseconds:0}ms{fallbackText}，已归类为{ocrContext.FieldName}{truncatedText}", ocrContext.FieldName);
        if (appendToUserInput)
        {
            SetUserInput(AppendLine(GetUserInput(), ocrContext.Text));
        }
    }

    private static string BuildOcrFallbackStatus(OcrRouteResult routeResult)
    {
        if (!routeResult.UsedFallback)
        {
            return string.Empty;
        }

        var failedAttempt = routeResult.Attempts.FirstOrDefault(attempt => !attempt.Success);
        if (failedAttempt is null)
        {
            return "，已启用兜底";
        }

        var reason = NormalizeCompactStatusText(failedAttempt.ErrorMessage ?? "未返回可用结果", 96);
        return $"，{failedAttempt.ProviderDisplayName} 失败后兜底：{reason}";
    }

    private static string NormalizeCompactStatusText(string value, int maxLength)
    {
        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return $"{normalized[..maxLength]}...";
    }

    private async Task<string> BuildRefinedUserRequestAsync(string currentPrompt)
    {
        var fallback = $"""
请基于以下已优化提示词，反向补全成更清晰的用户需求，并继续生成更具体、更可执行的提示词。

原始用户需求：
{FallbackText(GetUserInput(), "暂无")}

当前优化结果：
{currentPrompt}
""";

        try
        {
            _settings = _settingsService.Load();
            var llmOptions = _settingsService.ToLlmOptions(_settings);
            if (!llmOptions.IsConfigured || !_settings.Privacy.ModelExternalRequestsEnabled)
            {
                SetStatus("模型不可用，已用当前结果本地补充需求");
                return fallback;
            }

            var prompt = $"""
你是 啊拼 的需求补全器。请根据“原始用户需求”和“当前优化结果”，反向整理出一份更清晰、更完整、可继续生成提示词的用户需求。

要求：
1. 只输出补充后的用户需求，不要输出解释。
2. 保留用户原意，不新增未经确认的事实。
3. 把隐含的目标、平台、风格、约束、输出格式、验收标准整理清楚。
4. 如果当前结果里出现占位项，把它们转成“需要确认/待补充”的需求条目。
5. 输出语言使用：{GetPrimaryPromptLanguage()}。英文语言包除外：英文界面时仍输出中文需求。

原始用户需求：
{FallbackText(GetUserInput(), "暂无")}

当前优化结果：
{currentPrompt}
""";

            var promptToSend = _settings.Privacy.RedactBeforeModelSend
                ? _sensitiveTextRedactor.Redact(prompt)
                : prompt;
            SetStatus("正在补充需求...");
            return await _llmClient.CompleteAsync(promptToSend, llmOptions);
        }
        catch (Exception ex)
        {
            SetStatus($"补充需求失败，已使用本地补充：{ex.Message}");
            return fallback;
        }
    }

    private string BuildModeAwarePrompt(string userRequest, string fallbackPrompt)
    {
        var selectedScenes = FormatSelectedScenes();
        var effectiveMode = GetEffectiveMode();
        var primaryLanguage = GetPrimaryPromptLanguage();
        var selectedTarget = GetSelectedOptimizationTarget();
        if (selectedTarget is not null)
        {
            return BuildOptimizationTargetLocalPrompt(selectedTarget, userRequest, fallbackPrompt);
        }

        if (IsAiCodingMode(selectedScenes, effectiveMode))
        {
            return BuildAiCodingStructuredPrompt(userRequest, primaryLanguage, effectiveMode);
        }

        if (IsAcademicHumanizeMode(selectedScenes, effectiveMode))
        {
            return BuildAcademicHumanizeStructuredPrompt(userRequest);
        }

        if (IsVideoMode(selectedScenes, effectiveMode))
        {
            return BuildVideoStructuredPrompt(userRequest, primaryLanguage, selectedScenes, effectiveMode);
        }

        if (IsTextToImageMode(selectedScenes, effectiveMode))
        {
            return BuildTextToImageStructuredPrompt(userRequest, primaryLanguage, selectedScenes, effectiveMode);
        }

        var sceneLine = selectedScenes.Contains("视频", StringComparison.Ordinal) || effectiveMode == "Veo 3"
            ? "适合视频生成模型的高质量提示词，包含镜头、主体、动作、光线、节奏和画面比例。"
            : selectedScenes.Contains("文生图", StringComparison.Ordinal) || effectiveMode is "文生图"
            ? "适合图像生成模型的高质量提示词，包含主体、风格、构图、材质、光线、负面约束和画面比例。"
            : "适合通用 LLM 的高质量提示词，包含明确任务、上下文、参考依据、输出格式、评估标准和迭代要求。";

        return $"""
【Task】
请根据以下需求完成任务，并输出可直接使用的结果：
{FallbackText(userRequest, "请根据当前场景整理一个可执行的提示词。")}

【Context】
目标平台 / 优化模式：{effectiveMode}
场景选择：{selectedScenes}
主输出语言：{primaryLanguage}
英文翻译提示词区域会单独同步输出英文；如果当前界面语言是英文，主输出语言仍保持中文。
上下文来源：{FallbackText(_contextSource, "手动输入")}
额外上下文：{FallbackText(ContextBox.Text, "暂无")}
输出方向：{sceneLine}

本地参考草稿：
{fallbackPrompt}

【References】
参考用户原始需求、额外上下文、场景选择、目标平台和常见模型提示词最佳实践组织内容。
不要编造未经用户确认的品牌内部细节、人物、数据或功能。

【Evaluate】
最终结果必须可直接复制给目标模型使用，并明确标注【Task】【Context】【References】【Evaluate】【Iterate】五部分。
除 TCREI 标题外，正文必须使用主输出语言；主输出语言不是中文时，请把正文翻译/改写为该语言。
必须保留用户原意，不添加未经确认的事实或无关功能。
如果面向图片或视频模型，明确主体、环境、动作、镜头语言、风格、光线、负面约束和比例。
如果需求信息不足，在对应部分写出合理占位或需要补充的问题，不要编造具体品牌、人物或事实。

【Iterate】
生成结果前先自查是否遗漏 TCREI 任一部分、是否超出用户需求、是否缺少可执行细节。
如存在信息不足，在末尾附上“需要补充的信息：”，列出最少必要项。
""";
    }

    private string BuildModelOptimizationPrompt(string userRequest, string localPrompt)
    {
        return $"""
你是 啊拼 内部提示词优化器。你的任务是把用户需求改写成一份“最终可直接复制给目标模型使用”的提示词。

硬性规则：
1. 只输出最终提示词正文，必须从【Task】开始。
2. 必须严格包含并按顺序输出：【Task】【Context】【References】【Evaluate】【Iterate】。
3. 不要输出标题“Meta-TCREI 元提示词模板”，不要输出 Markdown 分隔线，不要输出解释、分析、推理过程。
4. 不要写“请据此生成一份可直接使用的提示词”或“生成一份用于描述某某的提示词”。你要直接把用户真正想完成的任务写进【Task】。
5. 不要编造未经用户确认的事实、品牌内部细节、具体版本或功能。信息不足时，在对应部分使用占位符或在末尾写“需要补充的信息：”。
6. 主输出语言必须是：{GetPrimaryPromptLanguage()}。英文语言包除外：如果当前语言包是英文，主输出仍保持中文，因为英文区域会单独输出英文。

用户原始需求：
{FallbackText(userRequest, "请根据当前场景整理一个可执行的提示词。")}

请基于下面的本地 TCREI 草稿继续增强，但不要照抄其中的元说明：
{localPrompt}
""";
    }

    private string BuildOptimizationTargetLocalPrompt(OptimizationTargetItem target, string userRequest, string fallbackPrompt)
    {
        var template = string.IsNullOrWhiteSpace(target.LocalPromptTemplate)
            ? """
              你正在使用 啊拼 共享优化目标：{{targetTitle}}。

              请根据用户输入生成可直接使用的提示词。

              用户输入：
              {{userRequest}}
              """
            : target.LocalPromptTemplate;

        return ApplyOptimizationTargetVariables(template, target, userRequest, fallbackPrompt);
    }

    private string BuildOptimizationTargetModelPrompt(OptimizationTargetItem target, string userRequest, string localPrompt)
    {
        var customRules = string.IsNullOrWhiteSpace(target.ModelInstruction)
            ? "请遵循该优化目标文件中的 LocalPromptTemplate，生成最终可复制使用的提示词。不要编造事实，不要输出无关解释。"
            : target.ModelInstruction;

        return $"""
你是 啊拼 内部提示词优化器。当前使用的是用户导入的共享优化目标文件。

目标文件格式：{OptimizationTargetService.Schema}
目标名称：{target.Title}
目标分类：{target.Category}
目标说明：{FallbackText(target.Description, "无")}
目标兼容：{target.Compatibility}

硬性规则：
1. 你的任务是生成一份可复制给目标模型使用的最终提示词，而不是解释这个目标文件。
2. 输出只能是最终提示词正文，不要输出 JSON、协议说明、Markdown 分隔线或额外解释。
3. 必须保留用户原意，不编造数据、案例、品牌内部细节或来源。
4. 主输出语言必须是：{GetPrimaryPromptLanguage()}。英文区域会单独同步输出英文版本。
5. 如果目标文件要求“只输出正文”或“不要解释”，必须保留这类约束。

目标自定义规则：
{customRules}

用户原始输入：
{FallbackText(userRequest, "请根据当前共享优化目标生成提示词。")}

本地模板草稿：
{localPrompt}
""";
    }

    private string ApplyOptimizationTargetVariables(OptimizationTargetItem target, string userRequest, string fallbackPrompt)
    {
        return ApplyOptimizationTargetVariables(target.LocalPromptTemplate, target, userRequest, fallbackPrompt);
    }

    private string ApplyOptimizationTargetVariables(string template, OptimizationTargetItem target, string userRequest, string fallbackPrompt)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["targetId"] = target.Id,
            ["targetTitle"] = target.Title,
            ["targetDescription"] = target.Description,
            ["targetCategory"] = target.Category,
            ["templateSource"] = target.TemplateSource,
            ["compatibility"] = target.Compatibility,
            ["keywords"] = string.Join("、", target.Keywords),
            ["userRequest"] = FallbackText(userRequest, "【粘贴文本】"),
            ["localPrompt"] = fallbackPrompt,
            ["context"] = ContextBox.Text.Trim(),
            ["contextSource"] = FallbackText(_contextSource, "手动输入"),
            ["primaryLanguage"] = GetPrimaryPromptLanguage(),
            ["effectiveMode"] = target.Title
        };

        var result = template;
        foreach (var pair in replacements)
        {
            result = result
                .Replace("{{" + pair.Key + "}}", pair.Value, StringComparison.OrdinalIgnoreCase)
                .Replace("{" + pair.Key + "}", pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result.Trim();
    }

    private static string BuildAcademicHumanizeStructuredPrompt(string userRequest)
    {
        return $"""
你是一名中文论文写作润色助手。你的任务是把我给出的内容改写成“像真实学生或研究者写出来的论文段落”，而不是 AI 生成的模板文。

请注意：我要的是自然、清楚、有学术感，不是口水话，也不是营销文，更不是机械排比句。

写作要求：
1. 不要使用明显 AI 腔。禁止频繁出现：“首先、其次、再次、最后、综上所述、总而言之、由此可见、不可否认的是、值得注意的是、在当今社会、随着时代的发展、在新时代背景下、具有重要意义、具有深远影响、提供了新思路、提供了新路径、注入新动能、赋能、助力、推动高质量发展”。
2. 不要写成机械列表。除非我明确要求列点，否则请用自然段落推进逻辑。
3. 不要堆空话。每句话都要有信息量；如果写“重要”或“有影响”，必须说明具体原因和影响位置。
4. 保留论文感，但降低八股味。语气要准确、克制、清楚，可以有自然转折和解释，但不要像聊天、新闻通稿或宣传稿。
5. 句式要自然。长句和短句混合使用，不要每句话都用同一种结构。可以适当使用“这意味着”“问题在于”“更关键的是”“换句话说”“从这个角度看”等自然连接，但不要滥用。
6. 逻辑要像人写的。每段围绕一个中心意思展开，段落之间要有自然过渡，不要把观点硬拼在一起。
7. 不要过度拔高。结论要和材料规模匹配，不要把普通现象写成时代命题。
8. 不要编造信息。原文没有数据、案例、来源或结论时，不要自行补充；可以使用“可能说明”“在一定程度上反映”“从已有材料看”等谨慎表达。
9. 尽量保留我的原意，不擅自改变观点，不把尖锐判断磨平成空话。
10. 输出只给最终改写结果。不要解释改了什么，不要写“以下是润色后的版本”，不要加标题，除非我要求。

额外禁止：
- 禁止使用“既是……也是……更是……”
- 禁止使用“它不仅……而且……更……”
- 禁止连续排比
- 禁止“第一、第二、第三”式展开
- 禁止写成申论、公众号、新闻通稿或宣传稿

需要处理的文本如下：
{FallbackText(userRequest, "【粘贴文本】")}
""";
    }

    private string BuildAcademicHumanizeModelOptimizationPrompt(string userRequest, string localPrompt)
    {
        return $"""
你是 啊拼 内部提示词优化器。当前优化目标是“论文去AI味”。

重要边界：
1. 你的任务是生成一份可复制给目标模型使用的“中文论文自然改写提示词”，不是直接执行论文改写。
2. 输出只能是最终提示词正文，不要解释、不要标题、不要 Markdown 分隔线、不要 TCREI 结构。
3. 最终提示词必须要求目标模型：保留学术表达，但模拟真实学生或研究者的自然写作习惯。
4. 必须包含反 AI 腔禁用词库、禁止机械列表、禁止连续排比、禁止空话套话、禁止过度拔高、禁止编造信息、保留原意、只输出改写正文。
5. 如果用户已经给了待处理文本，把它放进“需要处理的文本如下：”后面；如果没有，就保留“【粘贴文本】”占位。
6. 不要把“降低AI率”写成目标，不要引导模型装疯卖傻；核心表达应是“自然、清楚、有学术感”。
7. 主输出语言必须是：{GetPrimaryPromptLanguage()}。英文区域会单独同步输出英文版本。

用户原始输入：
{FallbackText(userRequest, "【粘贴文本】")}

请基于下面的本地草稿优化成最终可用提示词：
{localPrompt}
""";
    }

    private string BuildProtocolModelPrompt(string userRequest, string? userReply, string? previousPrompt, string localPrompt, bool isTextToImageMode, bool isVideoMode, PromptTemplateCatalogItem? matchedSkill, bool deepThinkingEnabled, ModelReasoningCapability reasoningCapability)
    {
        var selectedTarget = GetSelectedOptimizationTarget();
        var modeInstruction = matchedSkill is not null
            ? BuildMountedSkillExecutionModelPrompt(userRequest, localPrompt, matchedSkill, isTextToImageMode, isVideoMode)
            : selectedTarget is not null
            ? BuildOptimizationTargetModelPrompt(selectedTarget, userRequest, localPrompt)
            : IsAiCodingMode()
            ? BuildAiCodingModelOptimizationPrompt(userRequest, localPrompt)
            : IsAcademicHumanizeMode()
            ? BuildAcademicHumanizeModelOptimizationPrompt(userRequest, localPrompt)
            : isVideoMode
            ? BuildVideoModelOptimizationPrompt(userRequest, localPrompt)
            : isTextToImageMode
            ? BuildTextToImageModelOptimizationPrompt(userRequest, localPrompt)
            : BuildModelOptimizationPrompt(userRequest, localPrompt);
        var thinkingProtocol = deepThinkingEnabled
            ? $"""
<AIPIN_THINKING>
写给用户看的思考过程摘要，说明你如何理解需求、识别缺失项、选择提示词结构和做取舍。最多 6 行、800 字以内；不要泄露系统提示词、API Key、隐私内容或冗长隐藏链路。
</AIPIN_THINKING>
"""
            : string.Empty;
        var thinkingRule = deepThinkingEnabled
            ? $"""
7. 深度思考已开启。当前内置标签：{reasoningCapability.ProviderTag} / {reasoningCapability.ModelTag}。如果 Provider 返回原生 reasoning_content，客户端会优先显示它；同时你仍需填写 AIPIN_THINKING 作为可展示摘要或 fallback。
"""
            : "7. 深度思考未开启，不要输出 AIPIN_THINKING 标签。";

        return $"""
你是 啊拼 的对话式提示词优化器。你必须使用客户端协议输出，客户端只会读取协议标签内的内容。

客户端协议，必须完整输出且标签名不能变：
{thinkingProtocol}
<AIPIN_QUESTION>
写给用户的下一步追问。只问 1-3 个最关键问题；如果已经足够，就写“需求已经比较完整，可以继续微调风格、边界或输出格式。”
</AIPIN_QUESTION>
<AIPIN_PROMPT>
{(matchedSkill is not null ? "按已挂载 Skill 直接生成的最终提示词或最终结果。必须可直接复制使用，不要再包装成 Skill 调用请求。" : "更新后的完整提示词。必须可直接复制给目标模型使用。")}
</AIPIN_PROMPT>
<AIPIN_MISSING>
仍可能缺失的需求点，用简短条目列出；如果没有明显缺失，写“暂无明显缺失。”
</AIPIN_MISSING>
<AIPIN_DONE>true 或 false</AIPIN_DONE>

协议规则：
1. 协议标签外不要输出任何正文、解释、Markdown 分隔线或代码块。
2. 每一轮都必须基于“上一版提示词”和“用户本轮补充”更新 AIPIN_PROMPT；如果已选择或命中挂载 Skill，则把 Skill 当作当前生成规则直接执行，不要输出“请调用/激活 Skill”的中间提示词。
3. 不要把追问写进 AIPIN_PROMPT 正文；追问只放 AIPIN_QUESTION。
4. AIPIN_MISSING 必须指出可能还缺什么，例如技术栈、平台、画幅、风格、约束、输出格式、验收标准等。
5. 如果用户本轮只是回答某个缺失项，比如“Java”，要把它合并进提示词，并继续追问下一个最重要缺失项。
6. AIPIN_PROMPT 必须是纯文本最终提示词，不要使用 Markdown：不要用 # 标题、-/* 列表符号、**粗体**、`行内代码`、```代码围栏、Markdown 链接或 Markdown 分隔线。需要分段时使用中文括号标题如【任务】或普通换行。
{thinkingRule}

上一版提示词：
{FallbackText(previousPrompt, "暂无，这是第一轮需求。")}

用户本轮补充：
{FallbackText(userReply, userRequest)}

本轮合并后的需求上下文：
{FallbackText(userRequest, "请根据当前场景整理一个可执行的提示词。")}

底层优化规则：
{modeInstruction}
""";
    }

    private static PromptProtocolResult ParsePromptProtocol(string rawResponse, string fallbackPrompt)
    {
        var prompt = ExtractProtocolTag(rawResponse, "AIPIN_PROMPT");
        var question = ExtractProtocolTag(rawResponse, "AIPIN_QUESTION");
        var missing = ExtractProtocolTag(rawResponse, "AIPIN_MISSING");
        var doneText = ExtractProtocolTag(rawResponse, "AIPIN_DONE");
        var thinking = ExtractProtocolTag(rawResponse, "AIPIN_THINKING");

        if (string.IsNullOrWhiteSpace(prompt))
        {
            prompt = rawResponse.Contains("<AIPIN_", StringComparison.OrdinalIgnoreCase)
                ? fallbackPrompt
                : rawResponse.Trim();
        }

        var done = bool.TryParse(doneText, out var parsedDone) && parsedDone;
        return new PromptProtocolResult(prompt.Trim(), question?.Trim(), missing?.Trim(), done, thinking?.Trim());
    }

    private static string? ExtractProtocolTag(string text, string tag)
    {
        var openTag = $"<{tag}>";
        var closeTag = $"</{tag}>";
        var start = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += openTag.Length;
        var end = text.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0 || end <= start)
        {
            return null;
        }

        return text[start..end];
    }

    private static string BuildLocalFollowUpQuestion(string prompt)
    {
        if (prompt.Contains("AI 编程", StringComparison.Ordinal) || prompt.Contains("AI编程", StringComparison.Ordinal) || prompt.Contains("Codex", StringComparison.OrdinalIgnoreCase) || prompt.Contains("Claude Code", StringComparison.OrdinalIgnoreCase) || prompt.Contains("Antigravity", StringComparison.OrdinalIgnoreCase))
        {
            return "还可以补充任务类型、允许修改范围、禁止修改范围、项目技术栈、复现步骤、验证命令和验收标准。";
        }

        if (prompt.Contains("Veo", StringComparison.OrdinalIgnoreCase) || prompt.Contains("即梦", StringComparison.Ordinal) || prompt.Contains("Seedance", StringComparison.OrdinalIgnoreCase))
        {
            return "还可以补充时长、画幅、主体动作、镜头运动、对白或字幕、BGM / 音效和首尾帧要求。";
        }

        return prompt.Contains("文生图", StringComparison.Ordinal)
            ? "还可以补充画幅比例、人物风格、场景、服装、光线和负面约束。"
            : "还可以补充目标平台、技术栈或语言、输出格式、约束条件和验收标准。";
    }

    private static string BuildLocalMissingItems(string prompt)
    {
        if (prompt.Contains("AI 编程", StringComparison.Ordinal) || prompt.Contains("AI编程", StringComparison.Ordinal) || prompt.Contains("Codex", StringComparison.OrdinalIgnoreCase) || prompt.Contains("Claude Code", StringComparison.OrdinalIgnoreCase) || prompt.Contains("Antigravity", StringComparison.OrdinalIgnoreCase))
        {
            return "可能缺失：任务类型、代码范围、禁止修改项、复现步骤、技术栈、验证命令、验收标准、是否允许改文件。";
        }

        if (prompt.Contains("Veo", StringComparison.OrdinalIgnoreCase) || prompt.Contains("即梦", StringComparison.Ordinal) || prompt.Contains("Seedance", StringComparison.OrdinalIgnoreCase))
        {
            return "可能缺失：时长、画幅比例、主体动作、镜头运动、对白或字幕、BGM / 音效、首尾帧或连续性约束。";
        }

        return prompt.Contains("文生图", StringComparison.Ordinal)
            ? "可能缺失：画幅比例、具体场景、服装方向、镜头、光线、负面约束。"
            : "可能缺失：目标平台、技术栈或语言、关键功能、输出格式、边界约束、验收标准。";
    }

    private void AddProtocolAssistantMessage(PromptProtocolResult result, bool animate = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.Question))
        {
            parts.Add(result.Question.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.Missing)
            && !result.Missing.Contains("暂无明显缺失", StringComparison.Ordinal))
        {
            parts.Add($"可能还缺：{result.Missing.Trim()}");
        }

        if (parts.Count == 0)
        {
            parts.Add(result.Done ? "需求已经比较完整，可以继续微调。" : "已更新提示词，可以继续补充需求。");
        }

        if (!string.IsNullOrWhiteSpace(result.Thinking))
        {
            AddChatMessage(result.Thinking, isUser: false, messageKind: "thinking");
        }

        AddChatMessage(string.Join(Environment.NewLine, parts), isUser: false, animate: animate);
    }

    private readonly record struct PromptProtocolResult(string Prompt, string? Question, string? Missing, bool Done, string? Thinking);

    private readonly record struct SkillMatch(PromptTemplateCatalogItem Template, int Score, string Description);

    private readonly record struct EnglishPromptResult(string Text, string? Status);

    private string BuildMountedSkillExecutionPrompt(string userRequest, PromptTemplateCatalogItem skill, string baseLocalPrompt)
    {
        var description = FallbackText(ExtractSkillDescription(skill.Text), "该 Skill 已在当前工作流中挂载，请按其说明执行。");
        var triggers = BuildSkillInvocationHints(skill);
        var selectedTarget = GetSelectedOptimizationTarget();
        var targetLine = selectedTarget is null
            ? GetEffectiveMode()
            : $"{selectedTarget.Title} / {selectedTarget.Category}";

        return $"""
请按当前已挂载 Skill 的规则，直接生成最终可用提示词。

【当前优化目标】
{targetLine}

【已挂载 Skill】
- Skill 名称：{skill.Title}
- Skill 分类：{skill.Category}
- Skill 说明：{description}
- 触发线索：{triggers}

【用户需求】
{FallbackText(userRequest, "请根据该 Skill 的适用场景生成最终提示词。")}

【生成要求】
1. 直接输出当前优化目标需要的最终提示词，不要写“请调用 / 激活 Skill”。
2. 把 Skill 当作生成规则、风格导演或工作流说明来执行。
3. 当前优化目标模板优先级高于通用提示词结构；Skill 只能补充执行风格、参数和工作流，不能改掉优化目标。
4. 保留用户明确参数；缺失项可使用清晰默认值，并在对话追问里指出。
5. 如果当前目标是文生图，直接输出可复制给图像模型的正向提示词和负面提示词。
6. 如果 Skill 规定了输出结构、代码内容、负面约束、参数锁定或安全边界，必须遵守；但不要使用 Markdown 外壳，代码内容也不要包 ``` 围栏。

【当前优化目标基础草稿】
{baseLocalPrompt}
""";
    }

    private string BuildMountedSkillExecutionModelPrompt(string userRequest, string localPrompt, PromptTemplateCatalogItem skill, bool isTextToImageMode, bool isVideoMode)
    {
        var selectedTarget = GetSelectedOptimizationTarget();
        var targetRules = selectedTarget is null
            ? $"当前优化目标：{GetEffectiveMode()}"
            : $"""
当前优化目标文件：
名称：{selectedTarget.Title}
分类：{selectedTarget.Category}
说明：{FallbackText(selectedTarget.Description, "无")}
兼容：{selectedTarget.Compatibility}
模板来源：{selectedTarget.TemplateSource}
自定义模型规则：{FallbackText(selectedTarget.ModelInstruction, "遵循 LocalPromptTemplate 生成最终可复制提示词，不要输出无关解释。")}
注意：LocalPromptTemplate 已经应用到下方“当前优化目标基础草稿”中，必须把它作为主要输出结构。
""";
        return $"""
你是 啊拼 的已挂载 Skill 执行器。现在用户选中了一个本地 Skill，你要按该 Skill 的说明直接生成当前优化目标需要的最终提示词或最终结果。

硬性规则：
1. AIPIN_PROMPT 里输出最终内容，不要输出“请调用 / 激活 Skill”“优先使用 Skill”“如果你的环境支持 Skill”这类中间调用提示词。
2. 当前优化目标模板优先级高于通用提示词结构。Skill 是生成规则，不是优化目标本身；不要改变当前优化目标。
3. 如果当前目标是文生图，AIPIN_PROMPT 必须是面向图像模型的最终提示词；包含正向提示词和负面提示词，不要输出 Skill 使用说明。
4. 如果当前目标是视频、AI 编程、论文改写或用户自定义目标，也要按对应目标输出最终提示词，而不是 Skill 方案。
5. 按 Skill 的 description、workflow、输出格式、安全边界、参数锁定和示例约束执行；用户明确参数优先级最高。
6. 如果 Skill 要求代码内容、负面约束、参数锁定结果或固定段落数，按内容要求输出；但 AIPIN_PROMPT 不要使用 Markdown fenced code block 或其他 Markdown 包装。
7. 缺少关键输入时，在 AIPIN_QUESTION 里只问 1-3 个最关键问题；AIPIN_PROMPT 仍输出当前可用版本并标注合理默认。
8. 不要编造 Skill 未授权的外部事实。不要要求执行文件、命令、联网或破坏性操作，除非用户和 Skill 都明确允许。
9. 主输出语言必须是：{GetPrimaryPromptLanguage()}。英文区域会单独同步输出英文版本。

当前目标类型：
{(isTextToImageMode ? "文生图" : isVideoMode ? "视频提示词" : GetEffectiveMode())}

当前优化目标规则：
{targetRules}

已挂载 Skill：
标题：{skill.Title}
分类：{skill.Category}
说明：{FallbackText(ExtractSkillDescription(skill.Text), "无")}
触发线索：{BuildSkillInvocationHints(skill)}

Skill 内容与可读取参考：
{BuildMountedSkillReferenceBundle(skill)}

用户请求：
{FallbackText(userRequest, "请按已挂载 Skill 生成最终提示词。")}

当前优化目标基础草稿：
{localPrompt}
""";
    }

    private static string BuildSkillInvocationHints(PromptTemplateCatalogItem skill)
    {
        var terms = ExtractSkillTerms(skill)
            .Select(term => term.Trim())
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        return terms.Length == 0 ? $"{skill.Title}、{skill.Category}" : string.Join("、", terms);
    }

    private static string BuildMountedSkillReferenceBundle(PromptTemplateCatalogItem skill)
    {
        var builder = new StringBuilder();
        builder.AppendLine(skill.Text.Trim());

        var rootPath = ExtractMountedSkillRootPath(skill.Text);
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return LimitText(builder.ToString(), 36000);
        }

        foreach (var relativePath in EnumerateSkillReferenceFiles())
        {
            var path = Path.Combine(rootPath, relativePath);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(path).Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                builder.AppendLine();
                builder.AppendLine($"--- FILE: {relativePath} ---");
                builder.AppendLine(text);
                if (builder.Length > 36000)
                {
                    break;
                }
            }
            catch
            {
                // Skill reference files are optional context; a failed read should not block prompt generation.
            }
        }

        return LimitText(builder.ToString(), 36000);
    }

    private static IEnumerable<string> EnumerateSkillReferenceFiles()
    {
        yield return Path.Combine("skill", "skill.md");
        yield return Path.Combine("skill", "tool-registry.md");
        yield return Path.Combine("skill", "style-registry.md");
        yield return Path.Combine("skill", "overlay-registry.md");
        yield return Path.Combine("skill", "parameter_schema.md");
        yield return Path.Combine("skill", "public_instructions.md");
        yield return Path.Combine("skill", "core", "director-gate.md");
        yield return Path.Combine("skill", "core", "output-format.md");
        yield return Path.Combine("skill", "core", "parameter-lock.md");
        yield return Path.Combine("skill", "core", "safety-boundary.md");
        yield return Path.Combine("skill", "core", "fallback-rules.md");
        yield return Path.Combine("skill", "core", "conflict-resolution.md");
        yield return Path.Combine("skill", "core", "reference-image-lock.md");
        yield return Path.Combine("skill", "references", "director-expansion.md");
        yield return Path.Combine("skill", "references", "visual-libraries.md");
        yield return Path.Combine("skill", "usage_examples.md");
        yield return Path.Combine("docs", "prompt_safety.md");
    }

    private static string? ExtractMountedSkillRootPath(string skillText)
    {
        foreach (var line in skillText.Replace("\r", string.Empty).Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("来源目录：", StringComparison.Ordinal))
            {
                return line["来源目录：".Length..].Trim();
            }
        }

        return null;
    }

    private static string LimitText(string text, int maxLength)
    {
        return text.Length <= maxLength ? text : $"{text[..maxLength]}\n...[已截断，仅保留前 {maxLength} 字符作为 Skill 上下文]";
    }

    private static string BuildAiCodingStructuredPrompt(string userRequest, string primaryLanguage, string effectiveMode)
    {
        return $$"""
请作为代码仓库中的 AI 编程智能体完成任务，但必须遵守最小修改原则。

【我的需求】
{{FallbackText(userRequest, "请填写要分析、修复、实现或审查的编程任务。")}}

【目标平台】
{{effectiveMode}}（适用于 Codex / Claude Code / Antigravity / Cursor Agent / Windsurf / Cline 等会读仓库、改文件、跑命令的 AI 编程平台）

【请先判断任务类型】
你需要先判断这是：
- 根因分析
- Bug 修复
- 新增功能
- UI 修改
- 重构规划
- 代码审查
- 配置 / 构建问题
- 项目规则文件（AGENTS.md / CLAUDE.md / Antigravity Rules）

【任务目标】
{用一句话说明要实现、修复、分析或审查什么}

【背景】
{项目技术栈、当前问题、相关页面 / 模块、已知现象、复现步骤}

【工作范围】
允许查看和修改：
- {允许目录或文件}

禁止修改：
- package.json / pnpm-lock.yaml / Cargo.toml / tauri.conf.json / 构建脚本，除非任务明确要求
- 无关 UI、无关样式、无关业务逻辑
- 数据库 schema、生产配置、凭据文件、用户数据，除非任务必需且已确认

【执行规则】
1. 先阅读相关代码，不要凭空猜测。
2. 先说明你准备查看哪些文件，以及为什么。
3. 找出根因或现有实现方式后，再动手修改。
4. 只做最小必要修改，不要顺手重构，不要新增依赖。
5. 保持现有架构、命名风格、状态管理方式和 UI 风格。
6. 不要删除现有功能，不要改变用户已有交互逻辑，除非需求明确要求。
7. 每次修改后检查 diff，确认没有无关改动。
8. 如果任务不明确，基于现有代码做最小合理假设，不要扩大范围。

【验证规则】
修改完成后运行相关检查：
- {类型检查命令}
- {lint 命令}
- {构建命令}
- {单元测试或相关测试命令}

如果检查无法运行，说明原因，不要假装通过。

【验收标准】
- {验收标准1}
- {验收标准2}
- {验收标准3}

【最终回复格式】
1. 任务类型判断
2. 根因 / 实现思路
3. 修改文件
4. 修改摘要
5. 验证结果
6. 风险点
7. 未完成项

【输出语言】
主输出语言：{{primaryLanguage}}。如果用户明确要求英文或目标平台规则文件需要英文，可保留英文结构。
""";
    }

    private string BuildAiCodingModelOptimizationPrompt(string userRequest, string localPrompt)
    {
        return $"""
你是 啊拼 内部 AI 编程提示词优化器。你的任务是把用户需求改写成一份“可直接发给 Codex / Claude Code / Antigravity / Cursor Agent / Windsurf / Cline 这类代码智能体使用”的完整任务提示词。

硬性规则：
1. 只输出最终提示词正文，不要输出解释、分析、推理过程或 Markdown 代码块。
2. 不要输出 TCREI，不要写“你是世界顶级工程师”这种空话。
3. 必须强调：任务目标、背景、工作范围、禁止乱改、先查根因、最小修改、验证命令、最终输出格式。
4. 必须把“防乱改、防重构、防幻觉、防删库、防越权、防没测就说好了”落实成具体规则，而不是口号。
5. 如果用户需求是“只查根因 / review / 规划”，必须明确“不允许修改文件”。
6. 如果用户需求是“新增功能 / 修 bug / UI 修改”，必须保留最小改动、不要新增依赖、不要修改无关文件、跑检查这些限制。
7. 如果用户提到 Codex，优先包含 AGENTS.md / sandbox / approval / diff / review 相关约束；如果提到 Claude Code，优先包含 CLAUDE.md / Plan Mode / hooks / skills；如果提到 Antigravity，优先包含 Rules / Artifacts / Permissions / Review Policy。
8. 信息不足时用“待补充/待确认”占位，并在末尾列出需要补充的信息。
9. 主输出语言必须是：{GetPrimaryPromptLanguage()}。英文语言包除外：如果当前语言包是英文，主输出仍保持中文，因为英文区域会单独输出英文。

推荐输出结构：
【平台】
【任务类型】
【任务目标】
【背景】
【工作范围】
【禁止事项】
【执行方式】
【验证命令】
【验收标准】
【最终回复格式】
【安全提醒】
【需要补充的信息】

用户原始需求：
{FallbackText(userRequest, "请生成一份 AI 编程智能体任务提示词。")}

本地结构草稿：
{localPrompt}
""";
    }

    private string BuildVideoStructuredPrompt(string userRequest, string primaryLanguage, string selectedScenes, string effectiveMode)
    {
        return IsVeoMode(selectedScenes, effectiveMode)
            ? BuildVeoStructuredPrompt(userRequest)
            : BuildJimengStructuredPrompt(userRequest, primaryLanguage);
    }

    private static string BuildVeoStructuredPrompt(string userRequest)
    {
        return $$"""
Create a Veo 3 video prompt using a cinematic director style.

Shot / structure:
[single shot / timestamped sequence / dialogue scene / image-to-video transition]

Subject:
[main character, product, object, or environment; keep identity consistent]

Location and time:
[specific place, time of day, weather or atmosphere]

Action:
[one clear visual action or a short sequence of actions]

Camera:
[shot size, angle, lens, framing, focus behavior]

Camera movement:
[static / push in / pull back / tracking / handheld / pan / tilt / orbit]

Lighting and color:
[light source, contrast, color palette, mood]

Visual style:
[cinematic realism / documentary / commercial / stylized / other user-specified style]

Dialogue:
[optional exact dialogue and spoken language; leave empty if not needed]

Sound:
[ambient sound, music, sound effects, room tone]

Duration and aspect ratio:
[seconds; 16:9 / 9:16 / 1:1 / other]

Continuity and constraints:
Keep subject identity, outfit, props, location, lighting direction, and action continuity stable. Avoid random cuts, distorted hands or faces, unreadable text, extra characters, flicker, low-resolution artifacts, and style drift.

User requirement:
{{FallbackText(userRequest, "No user requirement yet. Keep placeholders for the user to fill in.")}}
""";
    }

    private static string BuildJimengStructuredPrompt(string userRequest, string primaryLanguage)
    {
        return $$"""
生成一段适合即梦 / Seedance 的短视频提示词。

【1. 主题】
{视频主题 / 核心意图}

【2. 风格】
{短视频风格 / 写实 / 电影感 / 商业广告 / 二次元 / 其他}

【3. 画面主体】
{人物 / 产品 / 场景 / 事件 / 关键物体}

【4. 画面细节】
{主体外观、环境、道具、色彩、构图}

【5. 动态效果】
{主体动作、镜头运动、转场方式、节奏变化}

【6. 镜头与节奏】
{景别、视角、镜头运动、节奏快慢、是否分镜}

【7. 字幕】
{是否需要字幕、字幕内容、字幕位置、字幕样式}

【8. BGM / 音效】
{音乐氛围、环境声、关键音效}

【9. 格式要求】
{时长、画幅比例、平台、清晰度、首尾帧要求}

【10. 负面约束】
避免画面闪烁、主体漂移、人物或产品变形、文字乱码、无关元素、风格跑偏、突然跳切、低清晰度。

【用户需求补充】
主输出语言：{{primaryLanguage}}
用户原始需求：{{FallbackText(userRequest, "未填写，请保留上方占位符供用户补充。")}}
""";
    }

    private string BuildVideoModelOptimizationPrompt(string userRequest, string localPrompt)
    {
        if (IsVeoMode())
        {
            return $"""
You are AI Quick Prompt's internal Veo 3 prompt optimizer. Rewrite the user requirement into one final Veo 3 video prompt.

Hard rules:
1. Output only the final prompt body. Do not output explanations, reasoning, markdown fences, or TCREI sections.
2. The main output must be English because Veo 3 prompts work best as cinematic director-style English instructions.
3. Use a film-director structure: shot / structure, subject, location and time, action, camera, camera movement, lighting and color, visual style, dialogue, sound, duration and aspect ratio, continuity and constraints.
4. If the user asks for a timestamped sequence, output timestamped beats. If the user asks for dialogue, include exact dialogue lines. If the user provides first/last frames or image-to-video context, emphasize identity consistency and transition motion.
5. Do not use Stable Diffusion tag soup, anime quality tags, or TCREI. Do not invent unconfirmed brands, characters, durations, languages, or camera specs; use clear placeholders when missing.
6. Point the content toward video generation: visible motion, camera behavior, continuity, audio, and temporal pacing.

User requirement:
{FallbackText(userRequest, "Create a Veo 3 video prompt.")}

Local structure draft:
{localPrompt}
""";
        }

        return $"""
你是 啊拼 内部即梦 / Seedance 短视频提示词优化器。你的任务是把用户需求改写成一份“最终可直接复制给即梦或 Seedance 使用”的短视频提示词。

硬性规则：
1. 只输出最终提示词正文，不要输出解释、分析、推理过程、Markdown 代码块。
2. 不要输出 TCREI，不要输出 Stable Diffusion 标签堆，不要默认写实人像模板。
3. 必须按短视频生产结构输出：主题、风格、画面主体、画面细节、动态效果、镜头与节奏、字幕、BGM / 音效、格式要求、负面约束。
4. 主输出语言必须是中文；英文区域会单独同步输出英文。
5. 保留用户原意，把主题、平台、画幅、时长、人物 / 产品 / 场景、首尾帧、动作、镜头运动、字幕、BGM 等信息填入对应部分。
6. 信息不足时使用“待补充/待确认”，不要替用户编造具体品牌、人物、时长、比例或卖点。
7. 如果是首尾帧或图生视频，必须强调主体一致性、关键特征不变、转场运动、节奏和避免漂移。

用户原始需求：
{FallbackText(userRequest, "请生成一份即梦 / Seedance 短视频提示词。")}

本地结构草稿：
{localPrompt}
""";
    }

    private string BuildTextToImageStructuredPrompt(string userRequest, string primaryLanguage, string selectedScenes, string effectiveMode)
    {
        return $$"""
生成一张 {画幅比例} 的 {图像类型 / 主题}。

【1. 主体身份】
主体：{主体类型 / 角色身份 / 物体}
年龄或阶段：{人物或角色必须为成年 / 18 岁以上；未指定时写“成年角色，18+，具体年龄待补充”}
外貌或关键识别特征：{待补充}
必须避免的主体偏移：{不得设定为未成年、18 岁以下、儿童、少年、幼态、学生化年龄暗示}

【2. 总风格】
整体风格：{写实 / 二次元 / 赛博朋克 / UI 概念图 / 产品摄影 / 其他}
质感方向：{摄影 / 插画 / 3D / 平面设计 / 动漫 / 待补充}
风格参考：{可选，待补充}

【3. 气质】
情绪或氛围：{待补充}
关键词：{待补充}
表达强度：{克制 / 强烈 / 梦幻 / 冷感 / 热烈 / 待补充}

【4. 五官】
仅当主体包含人物或角色时填写：
脸型：{待补充}
眼型与眼神：{待补充}
鼻型、唇形、发型或其他辨识点：{待补充}
非人物主体：改写为关键形体、材质、轮廓和识别特征。

【5. 身形体态】
人物或角色体态：{待补充}
比例与姿态强度：{待补充}
重点轮廓或形体语言：{待补充}
非人物主体：改写为结构比例、体积、边缘线和视觉重心。

【6. 场景】
场景位置：{室内 / 室外 / 城市 / 自然 / 虚构空间 / 待补充}
背景复杂度：{简洁 / 中等 / 丰富 / 待补充}
环境元素：{待补充}

【7. 服装】
仅当主体包含人物或角色时填写：
服装方向：{待补充}
材质、颜色、层次：{待补充}
非人物主体：改写为表面材质、配色、装饰和组件。

【8. 姿势】
动作或姿态：{待补充}
重心与构图姿势：{待补充}
互动对象或动作目的：{待补充}

【9. 镜头】
视角：{平视 / 俯视 / 仰视 / 主观视角 / 待补充}
景别：{特写 / 中近景 / 全身 / 远景 / 待补充}
镜头或构图：{35mm / 50mm / 广角 / 对称 / 三分法 / 待补充}

【10. 光线与滤镜】
光线：{自然光 / 霓虹 / 逆光 / 柔光 / 硬光 / 待补充}
色彩与滤镜：{冷色 / 暖色 / 高饱和 / 低饱和 / 胶片 / 动漫赛璐璐 / 待补充}
明暗反差：{待补充}

【11. 真实感强化】
根据目标风格填写强化项：
写实：{纹理、材质、真实光影、拍摄噪点等}
二次元 / 插画：{线稿质量、上色层次、角色一致性、背景完成度等}
3D / 产品：{材质、反射、边缘、细节精度等}

【12. 负面约束】
避免：未成年、18 岁以下、儿童、少年、幼态、娃娃脸年龄暗示、学生化年龄暗示、年龄模糊、解剖错误、结构错误、风格跑偏、年龄跑偏、情色跑偏、摄影或绘画风格跑偏、多余肢体、文字乱码、水印、低清晰度、其他待补充。

【用户需求补充】
目标平台 / 优化模式：{{effectiveMode}}
场景选择：{{selectedScenes}}
主输出语言：{{primaryLanguage}}
用户原始需求：{{FallbackText(userRequest, "未填写，请保留上方占位符供用户补充。")}}
""";
    }

    private string BuildTextToImageModelOptimizationPrompt(string userRequest, string localPrompt)
    {
        return $"""
你是 啊拼 内部文生图提示词优化器。你的任务是把用户需求改写成一份“最终可直接复制给图像生成模型使用”的提示词。

硬性规则：
1. 只输出最终文生图提示词正文，不要输出解释、分析、推理过程、Markdown 代码块。
2. 不要输出 TCREI，不要输出【Task】【Context】【References】【Evaluate】【Iterate】。
3. 必须严格按下面 12 个部分和顺序输出，标题保留为：【1. 主体身份】到【12. 负面约束】。
4. 第一行必须是“生成一张 [画幅比例] 的 [图像类型 / 主题]。”，按用户需求填充；如果没说，保留“待补充/待确认”，不要替用户默认。
5. 主输出语言必须是：{GetPrimaryPromptLanguage()}。英文语言包除外：如果当前语言包是英文，主输出仍保持中文，因为英文区域会单独输出英文。
6. 保留用户原意，把用户给出的主体、风格、画幅、场景、服装、镜头、光线等信息填进对应部分；信息不足时使用清晰占位符，不要编造具体身份、年龄、风格或品牌。
7. 绝对不要默认写实模板、默认女性人像、默认东方女性、默认 20-28 岁、默认生活照、默认摄影质感。只有用户明确要求时才写这些内容。
8. 人物或角色主体必须明确为成年 / 18 岁以上；用户未指定年龄时写“成年角色，18+，具体年龄待补充”，禁止输出未成年、18 岁以下、儿童、少年、幼态、学生化年龄暗示或年龄模糊设定。
9. 【12. 负面约束】必须固定包含：未成年、18 岁以下、儿童、少年、幼态、娃娃脸年龄暗示、学生化年龄暗示、年龄模糊；不得省略。

固定输出结构：
【1. 主体身份】主体类型；身份或角色；人物/角色必须成年 18+；关键识别特征；避免偏移。
【2. 总风格】用户要求的风格；媒介质感；参考方向；禁止默认风格。
【3. 气质】情绪、氛围、表达强度。
【4. 五官】人物/角色则写五官；非人物则改写关键形体、材质、轮廓。
【5. 身形体态】人物/角色则写体态；非人物则改写结构比例和视觉重心。
【6. 场景】位置、背景复杂度、环境元素。
【7. 服装】人物/角色则写服装；非人物则改写材质、配色、装饰和组件。
【8. 姿势】动作、姿态、互动、重心。
【9. 镜头】视角、景别、镜头、构图。
【10. 光线与滤镜】光线、色彩、滤镜、反差。
【11. 真实感强化】按目标风格改为写实/二次元/3D/产品等对应强化项。
【12. 负面约束】未成年；18 岁以下；儿童；少年；幼态；娃娃脸年龄暗示；学生化年龄暗示；年龄模糊；解剖错误；风格跑偏；年龄跑偏；情色跑偏；摄影跑偏。

用户原始需求：
{FallbackText(userRequest, "请生成一份文生图结构提示词。")}

本地结构草稿：
{localPrompt}
""";
    }

    private async Task<string> BuildSynchronizedEnglishPromptAsync(string finalPrompt, LlmRequestOptions? llmOptions, bool canUseModel, bool isTextToImageMode, bool isVideoMode, bool isAgenticMode, bool isAcademicHumanizeMode, OptimizationTargetItem? selectedTarget)
    {
        var result = await BuildSynchronizedEnglishPromptForGenerationAsync(finalPrompt, llmOptions, canUseModel, isTextToImageMode, isVideoMode, isAgenticMode, isAcademicHumanizeMode, selectedTarget, _settings);
        if (!string.IsNullOrWhiteSpace(result.Status))
        {
            SetStatus(result.Status);
        }

        return result.Text;
    }

    private async Task<EnglishPromptResult> BuildSynchronizedEnglishPromptForGenerationAsync(string finalPrompt, LlmRequestOptions? llmOptions, bool canUseModel, bool isTextToImageMode, bool isVideoMode, bool isAgenticMode, bool isAcademicHumanizeMode, OptimizationTargetItem? selectedTarget, AppSettings settings)
    {
        if (canUseModel && llmOptions?.IsConfigured == true && settings.Privacy.ModelExternalRequestsEnabled)
        {
            try
            {
                var englishPrompt = await _llmClient.CompleteAsync(BuildEnglishTranslationPrompt(finalPrompt, isTextToImageMode, isVideoMode, isAgenticMode, isAcademicHumanizeMode, selectedTarget), llmOptions).ConfigureAwait(false);
                return new EnglishPromptResult(englishPrompt, null);
            }
            catch (Exception ex)
            {
                return new EnglishPromptResult(BuildLocalEnglishMirror(finalPrompt), $"英文同步翻译失败，已使用本地同步结构：{ex.Message}");
            }
        }

        return new EnglishPromptResult(BuildLocalEnglishMirror(finalPrompt), null);
    }

    private static string BuildEnglishTranslationPrompt(string finalPrompt, bool isTextToImageMode, bool isVideoMode, bool isAgenticMode, bool isAcademicHumanizeMode, OptimizationTargetItem? selectedTarget)
    {
        var structureRule = isVideoMode
            ? "Preserve the exact video prompt structure, section order, placeholders, timeline beats, dialogue lines, audio details, and negative constraints."
            : isTextToImageMode
            ? "Preserve the exact text-to-image section order, numbered headings, placeholders, and negative constraints."
            : isAgenticMode
            ? "Preserve the exact agentic coding or skill-authoring structure, headings, placeholders, safety boundaries, verification commands, directory paths, and file names such as SKILL.md, AGENTS.md, CLAUDE.md, and Rules."
            : selectedTarget is not null && !string.IsNullOrWhiteSpace(selectedTarget.EnglishTranslationRule)
            ? selectedTarget.EnglishTranslationRule
            : isAcademicHumanizeMode
            ? "Preserve the academic rewriting prompt as an executable instruction. Keep the anti-AI-tone banned phrase list, no-listing rules, no-fabrication rules, and the final text-only output rule."
            : "Preserve the exact TCREI structure and order. Convert section labels to [Task], [Context], [References], [Evaluate], [Iterate].";

        return $"""
Translate the following finalized primary-language prompt into English.

Hard rules:
1. {structureRule}
2. Translate the content faithfully. Do not add new facts, examples, brands, features, or requirements.
3. Keep placeholders and "需要补充的信息" as "Information needed".
4. Output only the English prompt. No explanations, no markdown fences, no bilingual duplication.
5. Do not introduce Markdown formatting: no # headings, bullet markers, bold markers, inline-code backticks, Markdown links, separators, or fenced code blocks.

Final primary-language prompt:
{finalPrompt}
""";
    }

    private string BuildLocalEnglishMirror(string finalPrompt)
    {
        var text = finalPrompt
            .Replace("【Task】", "[Task]", StringComparison.Ordinal)
            .Replace("【Context】", "[Context]", StringComparison.Ordinal)
            .Replace("【References】", "[References]", StringComparison.Ordinal)
            .Replace("【Evaluate】", "[Evaluate]", StringComparison.Ordinal)
            .Replace("【Iterate】", "[Iterate]", StringComparison.Ordinal)
            .Replace("需要补充的信息：", "Information needed:", StringComparison.Ordinal);

        return $"""
The following English prompt mirrors the finalized primary-language prompt. Machine translation is unavailable, so user-specific non-English content is preserved to avoid changing meaning.

{text}
""";
    }

    private string BuildModelCompatibilityText()
    {
        var selectedTarget = GetSelectedOptimizationTarget();
        if (selectedTarget is not null)
        {
            return selectedTarget.Compatibility;
        }

        return GetEffectiveMode() switch
        {
            "Veo 3" => "ChatGPT / Claude / Midjourney / 即梦 / Veo 3",
            "即梦" => "ChatGPT / Claude / Midjourney / 即梦",
            "文生图" => "ChatGPT / Claude / Midjourney / Stable Diffusion / 即梦",
            "AI编程" => "Codex / Claude Code / Antigravity / Cursor Agent / Windsurf / Cline",
            "论文去AI味" => "ChatGPT / Claude / Gemini / DeepSeek / Kimi / 本地模型",
            _ => "ChatGPT / Claude / Gemini / 本地 OpenAI-compatible 模型"
        };
    }

    private OptimizationTargetItem? GetSelectedOptimizationTarget()
    {
        return GetSelectedOptimizationTarget(_selectedMode);
    }

    private OptimizationTargetItem? GetSelectedOptimizationTarget(string mode)
    {
        var targetId = GetOptimizationTargetId(mode);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            return null;
        }

        return _optimizationTargetService.Load()
            .FirstOrDefault(target => string.Equals(target.Id, targetId, StringComparison.OrdinalIgnoreCase));
    }

    private OptimizationTargetItem? GetExportableOptimizationTarget()
    {
        return OptimizationTargetManagementList.SelectedItem as OptimizationTargetItem;
    }

    private static string MakeOptimizationTargetMode(string targetId)
    {
        return $"target:{targetId}";
    }

    private static bool IsOptimizationTargetMode(string? mode)
    {
        return !string.IsNullOrWhiteSpace(mode)
            && mode.StartsWith("target:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetOptimizationTargetId(string? mode)
    {
        return IsOptimizationTargetMode(mode) ? mode![7..].Trim() : null;
    }

    private string GetEffectiveMode()
    {
        var target = GetSelectedOptimizationTarget();
        if (target is not null)
        {
            return target.Title;
        }

        if (_selectedMode == "自定义" && !string.IsNullOrWhiteSpace(CustomModeBox.Text))
        {
            return CustomModeBox.Text.Trim();
        }

        return _selectedMode;
    }

    private string GetPrimaryPromptLanguage()
    {
        var code = _languagePack.Code;
        if (code.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return "中文";
        }

        return string.IsNullOrWhiteSpace(_languagePack.DisplayName)
            ? $"当前语言包（{code}）"
            : $"{_languagePack.DisplayName}（{code}）";
    }

    private bool IsTextToImageMode()
    {
        return IsTextToImageMode(FormatSelectedScenes(), GetEffectiveMode());
    }

    private static bool IsTextToImageMode(string selectedScenes, string effectiveMode)
    {
        return selectedScenes.Contains("文生图", StringComparison.Ordinal)
            || string.Equals(effectiveMode, "文生图", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsVideoMode()
    {
        return IsVideoMode(FormatSelectedScenes(), GetEffectiveMode());
    }

    private static bool IsVideoMode(string selectedScenes, string effectiveMode)
    {
        return IsVeoMode(selectedScenes, effectiveMode)
            || IsJimengMode(selectedScenes, effectiveMode)
            || selectedScenes.Contains("视频", StringComparison.Ordinal);
    }

    private bool IsVeoMode()
    {
        return IsVeoMode(FormatSelectedScenes(), GetEffectiveMode());
    }

    private static bool IsVeoMode(string selectedScenes, string effectiveMode)
    {
        return selectedScenes.Contains("Veo", StringComparison.OrdinalIgnoreCase)
            || effectiveMode.Contains("Veo", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJimengMode(string selectedScenes, string effectiveMode)
    {
        return selectedScenes.Contains("即梦", StringComparison.Ordinal)
            || selectedScenes.Contains("Seedance", StringComparison.OrdinalIgnoreCase)
            || effectiveMode.Contains("即梦", StringComparison.Ordinal)
            || effectiveMode.Contains("Seedance", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAcademicHumanizeMode()
    {
        return IsAcademicHumanizeMode(FormatSelectedScenes(), GetEffectiveMode());
    }

    private static bool IsAcademicHumanizeMode(string selectedScenes, string effectiveMode)
    {
        return selectedScenes.Contains("论文去AI味", StringComparison.Ordinal)
            || selectedScenes.Contains("论文自然改写", StringComparison.Ordinal)
            || selectedScenes.Contains("论文人话", StringComparison.Ordinal)
            || effectiveMode.Contains("论文去AI味", StringComparison.Ordinal)
            || effectiveMode.Contains("论文自然改写", StringComparison.Ordinal)
            || effectiveMode.Contains("论文人话", StringComparison.Ordinal)
            || effectiveMode.Contains("Academic Humanize", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAiCodingMode()
    {
        return IsAiCodingMode(FormatSelectedScenes(), GetEffectiveMode());
    }

    private static bool IsAiCodingMode(string selectedScenes, string effectiveMode)
    {
        return selectedScenes.Contains("AI编程", StringComparison.Ordinal)
            || selectedScenes.Contains("AI 编程", StringComparison.Ordinal)
            || selectedScenes.Contains("Agentic Coding", StringComparison.OrdinalIgnoreCase)
            || effectiveMode.Contains("AI编程", StringComparison.Ordinal)
            || effectiveMode.Contains("AI 编程", StringComparison.Ordinal)
            || effectiveMode.Contains("Agentic Coding", StringComparison.OrdinalIgnoreCase)
            || effectiveMode.Contains("Codex", StringComparison.OrdinalIgnoreCase)
            || effectiveMode.Contains("Claude Code", StringComparison.OrdinalIgnoreCase)
            || effectiveMode.Contains("Antigravity", StringComparison.OrdinalIgnoreCase);
    }

    private string FormatSelectedScenes()
    {
        return GetEffectiveMode();
    }

    private static void AddScene(Microsoft.UI.Xaml.Controls.Primitives.ToggleButton button, List<string> scenes)
    {
        if (button.IsChecked == true && button.Tag is string scene)
        {
            scenes.Add(scene.Replace(" 优化", string.Empty, StringComparison.Ordinal));
        }
    }

    private static string FormatSceneText(SceneDetectionResult scene)
    {
        var route = new ScenePromptRouter().Route(scene);
        return $"场景：{route.DisplayName}";
    }

    private IReadOnlyList<LlmImageAttachment> GetModelImagesToSend()
    {
        return GetModelImagesToSend(_settingsService.Load());
    }

    private IReadOnlyList<LlmImageAttachment> GetModelImagesToSend(AppSettings settings)
    {
        if (_modelImageAttachment is null)
        {
            return Array.Empty<LlmImageAttachment>();
        }

        if (!settings.Privacy.ModelImageExternalRequestsEnabled)
        {
            return Array.Empty<LlmImageAttachment>();
        }

        return [_modelImageAttachment];
    }

    private static async Task<LlmImageAttachment> LoadModelImageAttachmentAsync(Windows.Storage.StorageFile file)
    {
        var bytes = await File.ReadAllBytesAsync(file.Path);
        var mimeType = GetImageMimeType(file.FileType);
        return CreateModelImageAttachment(bytes, file.Name, mimeType);
    }

    private static LlmImageAttachment CreateModelImageAttachment(byte[] bytes, string fileName, string mimeType)
    {
        const long maxBytes = 8L * 1024L * 1024L;
        if (bytes.Length > maxBytes)
        {
            throw new InvalidOperationException("图片超过 8 MB，请选择更小的图片。");
        }

        return new LlmImageAttachment($"data:{mimeType};base64,{Convert.ToBase64String(bytes)}", fileName, mimeType);
    }

    private async Task SetImagePreviewAsync(Windows.Storage.StorageFile file, string caption)
    {
        var bytes = await File.ReadAllBytesAsync(file.Path);
        await SetImagePreviewAsync(bytes, caption);
    }

    private async Task SetImagePreviewAsync(byte[] bytes, string caption)
    {
        var compactImage = await BuildBitmapImageAsync(bytes);
        var expandedImage = await BuildBitmapImageAsync(bytes);

        CompactImagePreviewImage.Source = compactImage;
        ExpandedImagePreviewImage.Source = expandedImage;
        CompactImagePreviewText.Text = caption;
        ExpandedImagePreviewText.Text = caption;
        CompactImagePreview.Visibility = Visibility.Visible;
        ExpandedImagePreview.Visibility = Visibility.Visible;
    }

    private static async Task<BitmapImage> BuildBitmapImageAsync(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream);
        writer.WriteBytes(bytes);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        stream.Seek(0);
        await image.SetSourceAsync(stream);
        writer.Dispose();
        return image;
    }

    private static async Task<byte[]> ReadAllBytesAsync(IRandomAccessStreamWithContentType stream)
    {
        const ulong maxBytes = 8UL * 1024UL * 1024UL;
        if (stream.Size > maxBytes)
        {
            throw new InvalidOperationException("图片超过 8 MB，请选择更小的图片。");
        }

        var bytes = new byte[(int)stream.Size];
        var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        reader.DetachStream();
        reader.Dispose();
        return bytes;
    }

    private static async Task<byte[]> EncodeSoftwareBitmapToPngAsync(Windows.Graphics.Imaging.SoftwareBitmap bitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();

        var bytes = new byte[(int)stream.Size];
        var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        reader.DetachStream();
        reader.Dispose();
        return bytes;
    }

    private static string GetImageMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png"
        };
    }

    private void ClearModelImageAttachment()
    {
        _modelImageAttachment = null;
        CompactImagePreview.Visibility = Visibility.Collapsed;
        ExpandedImagePreview.Visibility = Visibility.Collapsed;
    }

    private void ClearAttachmentButton_Click(object sender, RoutedEventArgs e)
    {
        ClearModelImageAttachment();
        SetStatus("已移除附件");
    }

    private string GetUserInput()
    {
        return ExpandedShell.Visibility == Visibility.Visible ? ExpandedInputBox.Text : InputBox.Text;
    }

    private void SetUserInput(string text)
    {
        _syncingText = true;
        InputBox.Text = text;
        ExpandedInputBox.Text = text;
        UpdateInputBoxHeights(text);
        InputCountText.Text = $"{InputBox.Text.Length} / 1000";
        ExpandedInputCountText.Text = $"{ExpandedInputBox.Text.Length}/1000";
        _syncingText = false;
    }

    private void SetCurrentPrompt(string text)
    {
        if (CurrentPromptBox is null)
        {
            return;
        }

        CurrentPromptBox.Text = text;
        CurrentPromptCountText.Text = $"{L("字数：")}{CurrentPromptBox.Text.Length}";
    }

    private string GetChineseOutput()
    {
        return ExpandedShell.Visibility == Visibility.Visible ? ExpandedOutputBox.Text : OutputBox.Text;
    }

    private void SetChineseOutput(string text)
    {
        var cleanText = StripPromptMarkdown(text);
        _syncingText = true;
        OutputBox.Text = cleanText;
        ExpandedOutputBox.Text = cleanText;
        UpdateChineseOutputCounts();
        _syncingText = false;
    }

    private void SetEnglishOutput(string text)
    {
        var cleanText = StripPromptMarkdown(text);
        _syncingText = true;
        EnglishOutputBox.Text = cleanText;
        CompactEnglishOutputBox.Text = cleanText;
        UpdateEnglishCounts(cleanText);
        _syncingText = false;
    }

    private CancellationToken ResetOutputTypewriter()
    {
        CancelOutputTypewriter();
        _outputTypewriterCts = new CancellationTokenSource();
        return _outputTypewriterCts.Token;
    }

    private void CancelOutputTypewriter()
    {
        _outputTypewriterCts?.Cancel();
        _outputTypewriterCts = null;
    }

    private Task SetChineseOutputWithTypewriterAsync(string text, CancellationToken cancellationToken)
    {
        return AnimateTextBoxesAsync(text, UpdateChineseOutputCounts, cancellationToken, OutputBox, ExpandedOutputBox);
    }

    private Task SetEnglishOutputWithTypewriterAsync(string text, CancellationToken cancellationToken)
    {
        return AnimateTextBoxesAsync(text, () => UpdateEnglishCounts(EnglishOutputBox.Text), cancellationToken, EnglishOutputBox, CompactEnglishOutputBox);
    }

    private async Task AnimateTextBoxesAsync(string text, Action updateCounts, CancellationToken cancellationToken, params TextBox[] boxes)
    {
        var cleanText = text ?? string.Empty;
        var chunkSize = CalculateTypewriterChunkSize(cleanText.Length);
        _syncingText = true;
        try
        {
            foreach (var box in boxes)
            {
                box.Text = string.Empty;
            }
            updateCounts();

            for (var length = chunkSize; length < cleanText.Length; length += chunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var partial = cleanText[..length];
                foreach (var box in boxes)
                {
                    box.Text = partial;
                    box.Select(partial.Length, 0);
                }

                updateCounts();
                await Task.Delay(TypewriterFrameDelayMs, cancellationToken);
            }

            foreach (var box in boxes)
            {
                box.Text = cleanText;
                box.Select(cleanText.Length, 0);
            }
            updateCounts();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _syncingText = false;
        }
    }

    private static int CalculateTypewriterChunkSize(int textLength)
    {
        return Math.Max(1, (int)Math.Ceiling(textLength / (double)TypewriterMaxFrames));
    }

    private static string StripPromptMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var inCodeFence = false;
        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmedStart = line.TrimStart();
            if (trimmedStart.StartsWith("```", StringComparison.Ordinal) || trimmedStart.StartsWith("~~~", StringComparison.Ordinal))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (!inCodeFence)
            {
                line = StripPromptMarkdownLine(line);
            }

            builder.AppendLine(line);
        }

        return Regex.Replace(builder.ToString().Trim(), @"\n{3,}", "\n\n");
    }

    private static string StripPromptMarkdownLine(string line)
    {
        var trimmed = line.Trim();
        if (Regex.IsMatch(trimmed, @"^([-*_]\s*){3,}$")
            || Regex.IsMatch(trimmed, @"^\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?$"))
        {
            return string.Empty;
        }

        var clean = line;
        clean = Regex.Replace(clean, @"^\s{0,3}#{1,6}\s+", string.Empty);
        clean = Regex.Replace(clean, @"^\s{0,3}>\s?", string.Empty);
        clean = Regex.Replace(clean, @"^\s*[-*+•]\s+", string.Empty);
        clean = Regex.Replace(clean, @"^\s*\d+[.)]\s+", string.Empty);
        clean = Regex.Replace(clean, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        clean = Regex.Replace(clean, @"\[([^\]]+)\]\(([^)]+)\)", "$1（$2）");
        clean = Regex.Replace(clean, @"`([^`]*)`", "$1");
        clean = Regex.Replace(clean, @"\*\*([^*]+)\*\*", "$1");
        clean = Regex.Replace(clean, @"__([^_]+)__", "$1");
        clean = Regex.Replace(clean, @"(?<!\*)\*([^*\n]+)\*(?!\*)", "$1");
        clean = Regex.Replace(clean, @"(?<!_)_([^_\n]+)_(?!_)", "$1");
        return clean.TrimEnd();
    }

    private void UpdateEnglishCounts(string text)
    {
        var count = text
            .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Length;
        EnglishCountText.Text = $"{L("Words: ")}{count}";
        CompactEnglishCountText.Text = $"{L("Words: ")}{count}";
    }

    private void UpdateChineseOutputCounts()
    {
        if (OutputBox is not null)
        {
            OutputCountText.Text = $"{OutputBox.Text.Length} / 12000";
        }

        if (ExpandedOutputBox is not null)
        {
            ExpandedOutputCountText.Text = $"{L("字数：")}{ExpandedOutputBox.Text.Length}";
        }
    }

    private void SetStatus(string text)
    {
        var localized = L(text);
        StatusText.Text = localized;
        ExpandedStatusText.Text = localized;
        CompactCommonPromptStatusText.Text = localized;
    }

    private async Task UpdatePromptDiffAsync(string? before, string after)
    {
        if (string.IsNullOrWhiteSpace(before) || string.IsNullOrWhiteSpace(after))
        {
            PromptDiffPanel.Visibility = Visibility.Collapsed;
            PromptDiffBlock.Blocks.Clear();
            return;
        }

        var segments = await Task.Run(() => BuildLineDiff(SplitDiffLines(before), SplitDiffLines(after))
            .Where(segment => segment.Kind != DiffKind.Unchanged)
            .ToArray());

        PromptDiffBlock.Blocks.Clear();
        if (segments.Length == 0)
        {
            PromptDiffPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var paragraph = new Paragraph();
        var runBuilder = new StringBuilder();
        var currentKind = segments[0].Kind;
        foreach (var segment in segments)
        {
            if (segment.Kind != currentKind && runBuilder.Length > 0)
            {
                AddDiffRun(paragraph, currentKind, runBuilder.ToString());
                runBuilder.Clear();
                currentKind = segment.Kind;
            }

            runBuilder.Append(segment.Kind == DiffKind.Added ? "+ " : "- ");
            runBuilder.AppendLine(segment.Text);
        }

        if (runBuilder.Length > 0)
        {
            AddDiffRun(paragraph, currentKind, runBuilder.ToString());
        }

        PromptDiffBlock.Blocks.Add(paragraph);
        PromptDiffPanel.Visibility = Visibility.Visible;
    }

    private static void AddDiffRun(Paragraph paragraph, DiffKind kind, string text)
    {
        paragraph.Inlines.Add(new Run
        {
            Text = text,
            Foreground = kind == DiffKind.Added
                ? new SolidColorBrush(Colors.ForestGreen)
                : new SolidColorBrush(Colors.Firebrick)
        });
    }

    private static IReadOnlyList<string> SplitDiffLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string BuildPlainDiff(string? before, string after)
    {
        if (string.IsNullOrWhiteSpace(before))
        {
            return "+ " + after.Trim();
        }

        var segments = BuildLineDiff(SplitDiffLines(before), SplitDiffLines(after))
            .Where(segment => segment.Kind != DiffKind.Unchanged)
            .Take(120)
            .ToArray();
        if (segments.Length == 0)
        {
            return "无变化";
        }

        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            builder.Append(segment.Kind == DiffKind.Added ? "+ " : "- ");
            builder.AppendLine(segment.Text);
        }

        return builder.ToString().TrimEnd();
    }

    private static IReadOnlyList<DiffSegment> BuildLineDiff(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var dp = new int[oldLines.Count + 1, newLines.Count + 1];
        for (var i = oldLines.Count - 1; i >= 0; i--)
        {
            for (var j = newLines.Count - 1; j >= 0; j--)
            {
                dp[i, j] = string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal)
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var result = new List<DiffSegment>();
        var oldIndex = 0;
        var newIndex = 0;
        while (oldIndex < oldLines.Count && newIndex < newLines.Count)
        {
            if (string.Equals(oldLines[oldIndex], newLines[newIndex], StringComparison.Ordinal))
            {
                result.Add(new DiffSegment(DiffKind.Unchanged, oldLines[oldIndex]));
                oldIndex++;
                newIndex++;
            }
            else if (dp[oldIndex + 1, newIndex] >= dp[oldIndex, newIndex + 1])
            {
                result.Add(new DiffSegment(DiffKind.Removed, oldLines[oldIndex++]));
            }
            else
            {
                result.Add(new DiffSegment(DiffKind.Added, newLines[newIndex++]));
            }
        }

        while (oldIndex < oldLines.Count)
        {
            result.Add(new DiffSegment(DiffKind.Removed, oldLines[oldIndex++]));
        }

        while (newIndex < newLines.Count)
        {
            result.Add(new DiffSegment(DiffKind.Added, newLines[newIndex++]));
        }

        return result;
    }

    private enum DiffKind
    {
        Unchanged,
        Removed,
        Added
    }

    private readonly record struct DiffSegment(DiffKind Kind, string Text);

    private sealed record CommandPaletteItem(string Title, string Subtitle, Func<Task> Execute)
    {
        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Subtitle) ? Title : $"{Title}  ·  {Subtitle}";
        }
    }

    private void EnsureUserRequestInConversation(string userRequest)
    {
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            return;
        }

        var normalized = userRequest.Trim();
        if (_conversationMessages.LastOrDefault()?.IsUser == true
            && string.Equals(_conversationMessages.Last().Text.Trim(), normalized, StringComparison.Ordinal))
        {
            return;
        }

        AddChatMessage(normalized, isUser: true);
    }

    private string BuildHistoryUserRequest(string fallback)
    {
        var userMessages = _conversationMessages
            .Where(message => message.IsUser && !string.IsNullOrWhiteSpace(message.Text))
            .Select(message => message.Text.Trim())
            .ToArray();

        return userMessages.Length == 0
            ? fallback.Trim()
            : string.Join($"{Environment.NewLine}{Environment.NewLine}", userMessages);
    }

    private void AddChatMessage(string text, bool isUser, bool record = true, DateTimeOffset? createdAt = null, string? messageKind = null, bool animate = false)
    {
        if ((ChatMessagesPanel is null && CompactChatMessagesPanel is null) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => AddChatMessage(text, isUser, record, createdAt, messageKind, animate));
            return;
        }

        var isThinking = string.Equals(messageKind, "thinking", StringComparison.OrdinalIgnoreCase);
        var displayText = isThinking ? NormalizeThinkingText(text) : text.Trim();
        if (isThinking && !HasVisibleThinkingText(SplitThinkingBubbleText(displayText).Body))
        {
            return;
        }

        if (CompactChatStatusText is not null && !isThinking)
        {
            CompactChatStatusText.Text = $"{(isUser ? L("你：") : L("啊拼："))}{OneLinePreview(displayText, 120)}";
        }

        var timestamp = createdAt ?? DateTimeOffset.Now;
        if (record)
        {
            _conversationMessages.Add(new PromptConversationMessage(isUser ? "user" : isThinking ? "thinking" : "assistant", displayText, timestamp));
        }

        var targetPanels = new List<StackPanel>(2);
        if (ChatMessagesPanel is not null)
        {
            targetPanels.Add(ChatMessagesPanel);
        }

        if (CompactChatMessagesPanel is not null)
        {
            targetPanels.Add(CompactChatMessagesPanel);
        }

        foreach (var panel in targetPanels)
        {
            var (grid, bubble, shouldAnimateAssistant) = CreateChatMessageElement(displayText, isUser, isThinking, timestamp, animate);
            panel.Children.Add(grid);
            if (shouldAnimateAssistant)
            {
                _ = AnimateAssistantBubbleAsync(bubble, displayText, ResourceBrush("TextFillColorPrimaryBrush", Colors.Black));
            }
        }
    }

    private (Grid Container, Border Bubble, bool ShouldAnimateAssistant) CreateChatMessageElement(string displayText, bool isUser, bool isThinking, DateTimeOffset timestamp, bool animate)
    {
        var grid = new Grid
        {
            ColumnSpacing = 10
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(isUser ? 42 : 42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });

        var avatar = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(17),
            Background = isUser ? ResourceBrush("AccentFillColorSecondaryBrush", Colors.Bisque) : ResourceBrush("ControlFillColorSecondaryBrush", Colors.WhiteSmoke),
            Child = new FontIcon
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Glyph = isUser ? "\uE77B" : isThinking ? "\uE82F" : "\uE99A",
                FontSize = 18
            }
        };

        var bubbleForeground = ResourceBrush("TextFillColorPrimaryBrush", Colors.Black);
        var shouldAnimateAssistant = animate && !isUser && !isThinking;
        var bubble = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 9, 12, 9),
            Background = isUser
                ? ResourceBrush("AccentFillColorDefaultBrush", Colors.RoyalBlue)
                : isThinking
                    ? ResourceBrush("LayerFillColorAltBrush", Colors.FloralWhite)
                    : ResourceBrush("CardBackgroundFillColorSecondaryBrush", Colors.White),
            BorderBrush = isThinking ? ResourceBrush("AccentFillColorSecondaryBrush", Colors.DodgerBlue) : ResourceBrush("ControlStrokeColorDefaultBrush", Colors.Gainsboro),
            BorderThickness = new Thickness(1),
            Child = isThinking
                ? BuildThinkingBubbleContent(displayText)
                : isUser
                    ? new TextBlock
                    {
                        Text = displayText,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = ResourceBrush("TextOnAccentFillColorPrimaryBrush", Colors.White)
                    }
                    : shouldAnimateAssistant
                        ? BuildPlainAssistantBubbleContent(string.Empty, bubbleForeground)
                        : BuildMarkdownBubbleContent(displayText, bubbleForeground)
        };

        var time = new TextBlock
        {
            Text = timestamp.ToLocalTime().ToString("HH:mm"),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush", Colors.DimGray)
        };

        Grid.SetColumn(avatar, 0);
        Grid.SetColumn(bubble, 1);
        Grid.SetColumn(time, 2);
        grid.Children.Add(avatar);
        grid.Children.Add(bubble);
        grid.Children.Add(time);
        return (grid, bubble, shouldAnimateAssistant);
    }

    private UIElement BuildThinkingBubbleContent(string text)
    {
        var parts = SplitThinkingBubbleText(text);
        var body = new TextBlock
        {
            Text = parts.Body,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResourceBrush("TextFillColorSecondaryBrush", Colors.DimGray),
            FontSize = 13,
            IsTextSelectionEnabled = true
        };
        var scrollHost = new ScrollViewer
        {
            Content = body,
            MaxHeight = 260,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var panel = new StackPanel
        {
            Spacing = 6
        };
        panel.Children.Add(new TextBlock
        {
            Text = L("思考过程"),
            FontWeight = FontWeights.SemiBold,
            Foreground = ResourceBrush("TextFillColorPrimaryBrush", Colors.Black)
        });
        if (!string.IsNullOrWhiteSpace(parts.Source))
        {
            panel.Children.Add(new TextBlock
            {
                Text = parts.Source,
                TextWrapping = TextWrapping.Wrap,
                Foreground = ResourceBrush("TextFillColorTertiaryBrush", Colors.Gray),
                FontSize = 12
            });
        }

        panel.Children.Add(scrollHost);
        return panel;
    }

    private static (string? Source, string Body) SplitThinkingBubbleText(string text)
    {
        var normalized = NormalizeThinkingText(text);
        var separator = $"{Environment.NewLine}{Environment.NewLine}";
        if (normalized.StartsWith("来源：", StringComparison.Ordinal)
            && normalized.IndexOf(separator, StringComparison.Ordinal) is var splitIndex
            && splitIndex > 0)
        {
            return (normalized[..splitIndex].Trim(), normalized[(splitIndex + separator.Length)..].Trim());
        }

        return (null, normalized);
    }

    private static string NormalizeThinkingText(string text)
    {
        var normalized = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim()
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.Format
                || (char.IsControl(ch) && ch is not '\r' and not '\n' and not '\t'))
            {
                continue;
            }

            builder.Append(ch);
        }

        return builder.ToString().Trim();
    }

    private static bool HasVisibleThinkingText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (!char.IsWhiteSpace(ch) && !char.IsControl(ch) && category != UnicodeCategory.Format)
            {
                return true;
            }
        }

        return false;
    }

    private static TextBlock BuildPlainAssistantBubbleContent(string text, Brush foreground)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground,
            FontSize = 14
        };
    }

    private async Task AnimateAssistantBubbleAsync(Border bubble, string text, Brush foreground)
    {
        var cleanText = text.Trim();
        try
        {
            var textBlock = bubble.Child as TextBlock ?? BuildPlainAssistantBubbleContent(string.Empty, foreground);
            bubble.Child = textBlock;
            var chunkSize = CalculateTypewriterChunkSize(cleanText.Length);
            for (var length = chunkSize; length < cleanText.Length; length += chunkSize)
            {
                textBlock.Text = cleanText[..length];
                await Task.Delay(TypewriterFrameDelayMs);
            }

            bubble.Child = BuildMarkdownBubbleContent(cleanText, foreground);
        }
        catch
        {
        }
    }

    private static RichTextBlock BuildMarkdownBubbleContent(string markdown, Brush foreground, double fontSize = 14)
    {
        var block = new RichTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = foreground,
            FontSize = fontSize,
            IsTextSelectionEnabled = true
        };

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var codeBuffer = new StringBuilder();
        var inCodeBlock = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    AddMarkdownCodeBlock(block, codeBuffer.ToString().TrimEnd());
                    codeBuffer.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeBuffer.AppendLine(rawLine);
                continue;
            }

            AddMarkdownLine(block, line, foreground);
        }

        if (inCodeBlock && codeBuffer.Length > 0)
        {
            AddMarkdownCodeBlock(block, codeBuffer.ToString().TrimEnd());
        }

        if (block.Blocks.Count == 0)
        {
            AddMarkdownLine(block, markdown, foreground);
        }

        return block;
    }

    private static void AddMarkdownLine(RichTextBlock block, string line, Brush foreground)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            block.Blocks.Add(new Paragraph());
            return;
        }

        if (trimmed is "---" or "***" or "___")
        {
            var rule = new Paragraph();
            rule.Inlines.Add(new Run
            {
                Text = "────────",
                Foreground = ResourceBrush("TextFillColorTertiaryBrush", Colors.Gray)
            });
            block.Blocks.Add(rule);
            return;
        }

        var paragraph = new Paragraph();
        var headingLevel = CountMarkdownHeadingLevel(trimmed);
        if (headingLevel > 0)
        {
            var headingText = trimmed[headingLevel..].TrimStart();
            paragraph.Inlines.Add(new Run
            {
                Text = headingText,
                FontWeight = FontWeights.SemiBold,
                FontSize = headingLevel == 1 ? 20 : headingLevel == 2 ? 17 : 15,
                Foreground = foreground
            });
            block.Blocks.Add(paragraph);
            return;
        }

        var quote = trimmed.StartsWith(">", StringComparison.Ordinal);
        if (quote)
        {
            paragraph.Inlines.Add(new Run
            {
                Text = "│ ",
                Foreground = ResourceBrush("AccentFillColorDefaultBrush", Colors.RoyalBlue)
            });
            trimmed = trimmed.TrimStart('>').TrimStart();
        }

        var bulletMatch = Regex.Match(trimmed, @"^([-*•])\s+(?<text>.+)$");
        if (bulletMatch.Success)
        {
            paragraph.Inlines.Add(new Run { Text = "• ", Foreground = foreground });
            AppendMarkdownInlines(paragraph, bulletMatch.Groups["text"].Value, foreground);
            block.Blocks.Add(paragraph);
            return;
        }

        var numberedMatch = Regex.Match(trimmed, @"^(?<number>\d+[.)])\s+(?<text>.+)$");
        if (numberedMatch.Success)
        {
            paragraph.Inlines.Add(new Run { Text = $"{numberedMatch.Groups["number"].Value} ", Foreground = foreground });
            AppendMarkdownInlines(paragraph, numberedMatch.Groups["text"].Value, foreground);
            block.Blocks.Add(paragraph);
            return;
        }

        AppendMarkdownInlines(paragraph, trimmed, quote ? ResourceBrush("TextFillColorSecondaryBrush", Colors.DimGray) : foreground);
        block.Blocks.Add(paragraph);
    }

    private static int CountMarkdownHeadingLevel(string line)
    {
        var count = 0;
        while (count < line.Length && count < 6 && line[count] == '#')
        {
            count++;
        }

        return count > 0 && count < line.Length && char.IsWhiteSpace(line[count]) ? count : 0;
    }

    private static void AddMarkdownCodeBlock(RichTextBlock block, string code)
    {
        var paragraph = new Paragraph();
        paragraph.Inlines.Add(new Run
        {
            Text = string.IsNullOrWhiteSpace(code) ? " " : code,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            Foreground = ResourceBrush("TextFillColorPrimaryBrush", Colors.Black)
        });
        block.Blocks.Add(paragraph);
    }

    private static void AppendMarkdownInlines(Paragraph paragraph, string text, Brush foreground)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (TryReadMarkdownInlineCode(text, i, foreground, out var codeRun, out var nextIndex)
                || TryReadMarkdownBold(text, i, foreground, out codeRun, out nextIndex)
                || TryReadMarkdownLink(text, i, foreground, out codeRun, out nextIndex))
            {
                paragraph.Inlines.Add(codeRun);
                i = nextIndex;
                continue;
            }

            var nextSpecial = FindNextMarkdownSpecial(text, i + 1);
            paragraph.Inlines.Add(new Run
            {
                Text = text[i..nextSpecial],
                Foreground = foreground
            });
            i = nextSpecial;
        }
    }

    private static bool TryReadMarkdownInlineCode(string text, int start, Brush foreground, out Inline inline, out int nextIndex)
    {
        inline = new Run();
        nextIndex = start;
        if (text[start] != '`')
        {
            return false;
        }

        var end = text.IndexOf('`', start + 1);
        if (end <= start + 1)
        {
            return false;
        }

        inline = new Run
        {
            Text = text[(start + 1)..end],
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            Foreground = ResourceBrush("AccentTextFillColorPrimaryBrush", Colors.RoyalBlue)
        };
        nextIndex = end + 1;
        return true;
    }

    private static bool TryReadMarkdownBold(string text, int start, Brush foreground, out Inline inline, out int nextIndex)
    {
        inline = new Run();
        nextIndex = start;
        var marker = text[start..].StartsWith("**", StringComparison.Ordinal)
            ? "**"
            : text[start..].StartsWith("__", StringComparison.Ordinal)
                ? "__"
                : null;
        if (marker is null)
        {
            return false;
        }

        var end = text.IndexOf(marker, start + marker.Length, StringComparison.Ordinal);
        if (end <= start + marker.Length)
        {
            return false;
        }

        inline = new Run
        {
            Text = text[(start + marker.Length)..end],
            FontWeight = FontWeights.SemiBold,
            Foreground = foreground
        };
        nextIndex = end + marker.Length;
        return true;
    }

    private static bool TryReadMarkdownLink(string text, int start, Brush foreground, out Inline inline, out int nextIndex)
    {
        inline = new Run();
        nextIndex = start;
        if (text[start] != '[')
        {
            return false;
        }

        var labelEnd = text.IndexOf("](", start, StringComparison.Ordinal);
        if (labelEnd <= start + 1)
        {
            return false;
        }

        var urlEnd = text.IndexOf(')', labelEnd + 2);
        if (urlEnd <= labelEnd + 2)
        {
            return false;
        }

        var label = text[(start + 1)..labelEnd];
        var url = text[(labelEnd + 2)..urlEnd];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var link = new Hyperlink
        {
            NavigateUri = uri,
            Foreground = ResourceBrush("AccentTextFillColorPrimaryBrush", Colors.RoyalBlue)
        };
        link.Inlines.Add(new Run { Text = label });
        inline = link;
        nextIndex = urlEnd + 1;
        return true;
    }

    private static int FindNextMarkdownSpecial(string text, int start)
    {
        var next = text.Length;
        foreach (var marker in new[] { "`", "**", "__", "[" })
        {
            var index = text.IndexOf(marker, start, StringComparison.Ordinal);
            if (index >= 0 && index < next)
            {
                next = index;
            }
        }

        return next;
    }

    private static Brush ResourceBrush(string key, Windows.UI.Color fallback)
    {
        return Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush
            ? brush
            : new SolidColorBrush(fallback);
    }

    private static string AppendLine(string existing, string addition)
    {
        return string.IsNullOrWhiteSpace(existing)
            ? addition
            : $"{existing.TrimEnd()}{Environment.NewLine}{addition}";
    }

    private static string FallbackText(string? text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private static void SendCtrlV()
    {
        keybd_event(0x11, 0, 0, UIntPtr.Zero);
        keybd_event(0x56, 0, 0, UIntPtr.Zero);
        keybd_event(0x56, 0, 0x0002, UIntPtr.Zero);
        keybd_event(0x11, 0, 0x0002, UIntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
