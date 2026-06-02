namespace PromptInputMethod.App.Services;

public sealed class AppSettings
{
    public ModelSettings Model { get; set; } = new();
    public OcrSettings Ocr { get; set; } = new();
    public PrivacySettings Privacy { get; set; } = new();
    public HotkeySettings Hotkey { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
}

public sealed class ModelSettings
{
    public bool Enabled { get; set; }
    public string ProviderId { get; set; } = "custom";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CredentialTargetName { get; set; } = "PromptInputMethod/OpenAICompatibleApiKey";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class OcrSettings
{
    public string PreferredProvider { get; set; } = "fire_eye_ocr";
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed class PrivacySettings
{
    public bool OcrEnabled { get; set; } = true;
    public bool ModelExternalRequestsEnabled { get; set; } = true;
    public bool ModelImageExternalRequestsEnabled { get; set; }
    public bool RedactBeforeModelSend { get; set; } = true;
}

public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = true;
    public bool Control { get; set; } = true;
    public bool Shift { get; set; } = true;
    public bool Alt { get; set; }
    public bool Win { get; set; }
    public string Key { get; set; } = "Space";
}

public sealed class UiSettings
{
    public string LanguageCode { get; set; } = "auto";
    public string MountedLanguagePackPath { get; set; } = string.Empty;
    public string SelectedMode { get; set; } = "通用 LLM";
    public string CustomMode { get; set; } = string.Empty;
    public bool SceneText { get; set; } = true;
    public bool SceneImage { get; set; }
    public bool SceneJimeng { get; set; }
    public bool SceneVeo { get; set; }
    public bool SceneUi { get; set; }
    public bool SceneVideo { get; set; }
    public bool DeepThinking { get; set; }
    public string CustomScene { get; set; } = string.Empty;
}
