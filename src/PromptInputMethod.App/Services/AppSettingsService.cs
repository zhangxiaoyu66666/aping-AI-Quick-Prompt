using System.Text.Json;
using PromptInputMethod.Core.Llm;

namespace PromptInputMethod.App.Services;

public sealed class AppSettingsService
{
    private readonly CredentialService _credentialService = new();
    private readonly AppDatabaseService _database = new();
    private const string SettingsStateKey = "appsettings";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public AppSettings Load()
    {
        var databaseSettings = _database.LoadState<AppSettings>(SettingsStateKey);
        if (databaseSettings is not null)
        {
            return EnsureDefaults(databaseSettings);
        }

        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        return EnsureDefaults(LoadFromPath(path) ?? new AppSettings());
    }

    public void SaveUserSettings(AppSettings settings, string? apiKey)
    {
        _database.SaveState(SettingsStateKey, EnsureDefaults(settings));

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _credentialService.WriteSecret(settings.Model.CredentialTargetName, apiKey);
        }
    }

    private static AppSettings? LoadFromPath(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public LlmRequestOptions ToLlmOptions(AppSettings settings)
    {
        var model = settings.Model;
        var apiKey = _credentialService.ReadSecret(model.CredentialTargetName);

        return new LlmRequestOptions(
            model.Enabled,
            model.ProviderId,
            model.BaseUrl,
            model.Model,
            apiKey,
            model.TimeoutSeconds <= 0 ? 30 : model.TimeoutSeconds);
    }

    private static AppSettings EnsureDefaults(AppSettings settings)
    {
        settings.Model ??= new ModelSettings();
        settings.Ocr ??= new OcrSettings();
        settings.Privacy ??= new PrivacySettings();
        settings.Hotkey ??= new HotkeySettings();
        settings.Ui ??= new UiSettings();
        if (string.IsNullOrWhiteSpace(settings.Ocr.PreferredProvider))
        {
            settings.Ocr.PreferredProvider = OcrProviderIds.FireEye;
        }
        else if (string.Equals(settings.Ocr.PreferredProvider, OcrProviderIds.WindowsMedia, StringComparison.OrdinalIgnoreCase))
        {
            settings.Ocr.PreferredProvider = OcrProviderIds.FireEye;
        }

        if (settings.Ocr.TimeoutSeconds <= 0)
        {
            settings.Ocr.TimeoutSeconds = 15;
        }

        if (string.IsNullOrWhiteSpace(settings.Hotkey.Key))
        {
            settings.Hotkey.Key = "Space";
        }
        else
        {
            settings.Hotkey.Key = GlobalHotkeyService.NormalizeMainKey(settings.Hotkey.Key);
        }

        if (string.IsNullOrWhiteSpace(settings.Ui.SelectedMode))
        {
            settings.Ui.SelectedMode = "通用 LLM";
        }

        if (string.IsNullOrWhiteSpace(settings.Model.ProviderId))
        {
            settings.Model.ProviderId = "custom";
        }

        if (string.IsNullOrWhiteSpace(settings.Model.CredentialTargetName))
        {
            settings.Model.CredentialTargetName = "PromptInputMethod/OpenAICompatibleApiKey";
        }

        settings.Ui.CustomMode ??= string.Empty;

        if (string.IsNullOrWhiteSpace(settings.Ui.LanguageCode))
        {
            settings.Ui.LanguageCode = "auto";
        }

        return settings;
    }
}
