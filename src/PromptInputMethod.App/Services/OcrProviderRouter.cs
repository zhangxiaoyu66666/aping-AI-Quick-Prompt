using System.Diagnostics;
using PromptOcrResult = PromptInputMethod.Core.Ocr.OcrResult;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace PromptInputMethod.App.Services;

public interface IOcrProvider
{
    string Id { get; }
    string DisplayName { get; }
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<PromptOcrResult> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken cancellationToken = default);
}

public sealed class OcrProviderRouter
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    private readonly IReadOnlyList<IOcrProvider> _providers;
    private readonly OcrSchedulerStore _schedulerStore;

    public OcrProviderRouter()
        : this(
            new IOcrProvider[]
            {
                new FireEyeOcrProvider(),
                new WindowsMediaOcrProvider()
            },
            new OcrSchedulerStore())
    {
    }

    public OcrProviderRouter(IReadOnlyList<IOcrProvider> providers, OcrSchedulerStore schedulerStore)
    {
        _providers = providers;
        _schedulerStore = schedulerStore;
    }

    public async Task<OcrRouteResult> RecognizeImageFileAsync(StorageFile file, OcrSettings? settings)
    {
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        return await RecognizeSoftwareBitmapAsync(bitmap, settings);
    }

    public async Task<OcrRouteResult> RecognizeSoftwareBitmapAsync(SoftwareBitmap bitmap, OcrSettings? settings)
    {
        var preferredProviderId = NormalizeProviderId(settings?.PreferredProvider);
        var timeout = GetTimeout(settings);
        var candidates = BuildCandidates(preferredProviderId);
        var attempts = new List<OcrProviderAttempt>();

        foreach (var provider in candidates)
        {
            var stopwatch = Stopwatch.StartNew();
            var isAvailable = false;
            try
            {
                isAvailable = await WithTimeout(token => provider.IsAvailableAsync(token), timeout, provider.DisplayName);
                if (!isAvailable)
                {
                    stopwatch.Stop();
                    var unavailableAttempt = new OcrProviderAttempt(provider.Id, provider.DisplayName, false, false, false, stopwatch.ElapsedMilliseconds, "Provider unavailable");
                    attempts.Add(unavailableAttempt);
                    await _schedulerStore.AppendAsync(preferredProviderId, unavailableAttempt);
                    continue;
                }

                var result = await WithTimeout(token => provider.RecognizeAsync(bitmap, token), timeout, provider.DisplayName);
                stopwatch.Stop();

                var successAttempt = new OcrProviderAttempt(provider.Id, provider.DisplayName, true, true, false, stopwatch.ElapsedMilliseconds, null);
                attempts.Add(successAttempt);
                await _schedulerStore.AppendAsync(preferredProviderId, successAttempt);
                return new OcrRouteResult(result, provider.Id, provider.DisplayName, stopwatch.Elapsed, attempts.Count > 1, attempts);
            }
            catch (TimeoutException ex)
            {
                stopwatch.Stop();
                var timeoutAttempt = new OcrProviderAttempt(provider.Id, provider.DisplayName, false, isAvailable, true, stopwatch.ElapsedMilliseconds, ex.Message);
                attempts.Add(timeoutAttempt);
                await _schedulerStore.AppendAsync(preferredProviderId, timeoutAttempt);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var failureAttempt = new OcrProviderAttempt(provider.Id, provider.DisplayName, false, isAvailable, false, stopwatch.ElapsedMilliseconds, ex.Message);
                attempts.Add(failureAttempt);
                await _schedulerStore.AppendAsync(preferredProviderId, failureAttempt);
            }
        }

        var errors = attempts.Count == 0
            ? "没有可用的 OCR provider"
            : string.Join("；", attempts.Select(attempt => $"{attempt.ProviderDisplayName}: {attempt.ErrorMessage ?? "失败"}"));
        throw new InvalidOperationException($"OCR 失败：{errors}");
    }

    private IReadOnlyList<IOcrProvider> BuildCandidates(string preferredProviderId)
    {
        if (string.Equals(preferredProviderId, OcrProviderIds.Auto, StringComparison.OrdinalIgnoreCase))
        {
            return _providers;
        }

        var preferred = _providers.FirstOrDefault(provider => string.Equals(provider.Id, preferredProviderId, StringComparison.OrdinalIgnoreCase));
        if (preferred is null)
        {
            return _providers;
        }

        return _providers
            .OrderBy(provider => string.Equals(provider.Id, preferred.Id, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToArray();
    }

    private static TimeSpan GetTimeout(OcrSettings? settings)
    {
        var seconds = settings is { TimeoutSeconds: > 0 } ? settings.TimeoutSeconds : (int)DefaultTimeout.TotalSeconds;
        return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, 120));
    }

    private static string NormalizeProviderId(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return OcrProviderIds.FireEye;
        }

        var normalized = providerId.Trim();
        return string.Equals(normalized, OcrProviderIds.WindowsMedia, StringComparison.OrdinalIgnoreCase)
            ? OcrProviderIds.FireEye
            : normalized;
    }

    private static async Task<T> WithTimeout<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout, string providerName)
    {
        using var timeoutCts = new CancellationTokenSource();
        var task = operation(timeoutCts.Token);
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed == task)
        {
            return await task;
        }

        await timeoutCts.CancelAsync();
        ObserveFault(task);
        throw new TimeoutException($"{providerName} 超过 {timeout.TotalSeconds:0} 秒未返回。");
    }

    private static void ObserveFault<T>(Task<T> task)
    {
        _ = task.ContinueWith(
            completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}

public static class OcrProviderIds
{
    public const string Auto = "auto_ocr";
    public const string WindowsMedia = "windows_media_ocr";
    public const string FireEye = "fire_eye_ocr";
}

public sealed record OcrRouteResult(
    PromptOcrResult Result,
    string ProviderId,
    string ProviderDisplayName,
    TimeSpan Duration,
    bool UsedFallback,
    IReadOnlyList<OcrProviderAttempt> Attempts);

public sealed record OcrProviderAttempt(
    string ProviderId,
    string ProviderDisplayName,
    bool Success,
    bool IsAvailable,
    bool TimedOut,
    long DurationMilliseconds,
    string? ErrorMessage);

public sealed class OcrSchedulerStore
{
    private const string SchedulerSchema = "prompt_input_method.ocr.scheduler.v1";
    private const string SchedulerStateKey = "ocr.scheduler";
    private const int MaxRecords = 100;
    private readonly AppDatabaseService _database = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AppendAsync(string preferredProviderId, OcrProviderAttempt attempt)
    {
        try
        {
            await _gate.WaitAsync();
            var log = Load();
            log.Records.Add(new OcrSchedulerRecord
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                PreferredProviderId = preferredProviderId,
                ProviderId = attempt.ProviderId,
                ProviderDisplayName = attempt.ProviderDisplayName,
                Success = attempt.Success,
                IsAvailable = attempt.IsAvailable,
                TimedOut = attempt.TimedOut,
                DurationMilliseconds = attempt.DurationMilliseconds,
                ErrorMessage = attempt.ErrorMessage
            });

            if (log.Records.Count > MaxRecords)
            {
                log.Records = log.Records.Skip(log.Records.Count - MaxRecords).ToList();
            }

            _database.SaveState(SchedulerStateKey, log);
        }
        catch
        {
            // OCR should not fail only because scheduler diagnostics cannot be written.
        }
        finally
        {
            if (_gate.CurrentCount == 0)
            {
                _gate.Release();
            }
        }
    }

    private OcrSchedulerLog Load()
    {
        var databaseLog = _database.LoadState<OcrSchedulerLog>(SchedulerStateKey);
        if (databaseLog is not null)
        {
            databaseLog.Schema = SchedulerSchema;
            databaseLog.Records ??= new List<OcrSchedulerRecord>();
            return databaseLog;
        }

        return new OcrSchedulerLog();
    }

    private sealed class OcrSchedulerLog
    {
        public string Schema { get; set; } = SchedulerSchema;
        public List<OcrSchedulerRecord> Records { get; set; } = new();
    }

    private sealed class OcrSchedulerRecord
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public string PreferredProviderId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderDisplayName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public bool IsAvailable { get; set; }
        public bool TimedOut { get; set; }
        public long DurationMilliseconds { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

internal sealed class WindowsMediaOcrProvider : IOcrProvider
{
    private readonly WindowsMediaOcrService _service = new();

    public string Id => OcrProviderIds.WindowsMedia;
    public string DisplayName => "Windows Media OCR";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages()
            ?? Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        return Task.FromResult(engine is not null);
    }

    public async Task<PromptOcrResult> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _service.RecognizeSoftwareBitmapAsync(bitmap);
    }
}
