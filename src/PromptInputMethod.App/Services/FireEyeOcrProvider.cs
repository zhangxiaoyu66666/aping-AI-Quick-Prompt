using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using PromptInputMethod.Core.Ocr;
using PromptOcrResult = PromptInputMethod.Core.Ocr.OcrResult;
using Windows.Graphics.Imaging;

namespace PromptInputMethod.App.Services;

internal sealed class FireEyeOcrProvider : IOcrProvider
{
    private const string WorkerFileName = "fire-eye-ocr-worker.exe";
    private const string JsonMarker = "__XIAXIA_FIRE_EYE_JSON__";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Id => OcrProviderIds.FireEye;
    public string DisplayName => "火眼金睛 OCR";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var workerPath = ResolveWorkerPath();
        if (workerPath is null)
        {
            return false;
        }

        try
        {
            var response = await RunWorkerAsync(workerPath, new FireEyeWorkerRequest
            {
                Command = "staticCapabilities"
            }, cancellationToken);
            return response.Ok && response.Capabilities?.Available == true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PromptOcrResult> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken cancellationToken = default)
    {
        var workerPath = ResolveWorkerPath()
            ?? throw new FileNotFoundException("未找到 Fire Eye OCR worker，请先构建 native/fire-eye-worker。", WorkerFileName);

        var tempImagePath = await WriteTempPngAsync(bitmap, cancellationToken);
        try
        {
            var response = await RunWorkerAsync(workerPath, new FireEyeWorkerRequest
            {
                Command = "recognize",
                Input = new FireEyeWorkerInput
                {
                    ImagePath = tempImagePath,
                    ContrastEnhanced = true
                },
                Languages = ["zh-Hans", "en"]
            }, cancellationToken);

            if (!response.Ok || response.Result is null)
            {
                throw new InvalidOperationException(response.Error ?? "Fire Eye OCR worker 未返回识别结果。");
            }

            return ToPromptOcrResult(response.Result);
        }
        finally
        {
            TryDelete(tempImagePath);
        }
    }

    private static async Task<string> WriteTempPngAsync(SoftwareBitmap bitmap, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tempPath = Path.Combine(Path.GetTempPath(), $"prompt-input-method-ocr-{Guid.NewGuid():N}.png");
        await using var fileStream = File.Create(tempPath);
        using var randomAccessStream = fileStream.AsRandomAccessStream();
        using var pngBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, randomAccessStream);
        encoder.SetSoftwareBitmap(pngBitmap);
        await encoder.FlushAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return tempPath;
    }

    private static async Task<FireEyeWorkerResponse> RunWorkerAsync(string workerPath, FireEyeWorkerRequest request, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(request, JsonOptions);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = workerPath,
                WorkingDirectory = Path.GetDirectoryName(workerPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = false
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Fire Eye OCR worker 启动失败。");
        }

        try
        {
            await process.StandardInput.WriteAsync(payload.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Fire Eye OCR worker 异常退出：{FormatWorkerOutput(stdout, stderr)}");
            }

            return ParseWorkerResponse(stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
    }

    private static FireEyeWorkerResponse ParseWorkerResponse(string stdout, string stderr)
    {
        var markerIndex = stdout.LastIndexOf(JsonMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException($"Fire Eye OCR worker 未输出 JSON：{FormatWorkerOutput(stdout, stderr)}");
        }

        var json = stdout[(markerIndex + JsonMarker.Length)..].Trim();
        return JsonSerializer.Deserialize<FireEyeWorkerResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Fire Eye OCR worker JSON 响应为空。");
    }

    private static PromptOcrResult ToPromptOcrResult(FireEyeNativeResult result)
    {
        var wordsById = (result.Words ?? [])
            .Where(word => !string.IsNullOrWhiteSpace(word.Id))
            .ToDictionary(word => word.Id!, StringComparer.Ordinal);

        var lines = (result.Lines ?? [])
            .OrderBy(line => line.Order ?? 0)
            .Select(line =>
            {
                var words = (line.WordIds ?? [])
                    .Select(id => wordsById.TryGetValue(id, out var word) ? word : null)
                    .Where(word => word is not null)
                    .Select(word => ToWordResult(word!))
                    .ToArray();
                if (words.Length == 0 && !string.IsNullOrWhiteSpace(line.Text) && line.Bbox is not null)
                {
                    words = [new OcrWordResult(line.Text, ToBoundingBox(line.Bbox), line.Confidence)];
                }

                return new OcrLineResult(line.Text ?? string.Empty, words, line.Bbox is null ? null : ToBoundingBox(line.Bbox));
            })
            .ToArray();

        var text = !string.IsNullOrWhiteSpace(result.Text)
            ? result.Text
            : string.Join(Environment.NewLine, lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        return new PromptOcrResult(text, lines, result.Confidence);
    }

    private static OcrWordResult ToWordResult(FireEyeNativeWord word)
    {
        return new OcrWordResult(word.Text ?? string.Empty, ToBoundingBox(word.Bbox), word.Confidence);
    }

    private static OcrBoundingBox ToBoundingBox(FireEyeBoundingBox? bbox)
    {
        return bbox is null
            ? new OcrBoundingBox(0, 0, 0, 0)
            : new OcrBoundingBox(bbox.X, bbox.Y, bbox.Width, bbox.Height);
    }

    private static string? ResolveWorkerPath()
    {
        foreach (var path in EnumerateWorkerPathCandidates())
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateWorkerPathCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, WorkerFileName);
        yield return Path.Combine(AppContext.BaseDirectory, "native", WorkerFileName);

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            yield return Path.Combine(directory.FullName, "native", "target", "debug", WorkerFileName);
            yield return Path.Combine(directory.FullName, "native", "target", "release", WorkerFileName);
        }
    }

    private static string FormatWorkerOutput(string stdout, string stderr)
    {
        var output = string.Join("；", new[] { stdout.Trim(), stderr.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(output) ? "无输出" : output;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private sealed class FireEyeWorkerRequest
    {
        public string Command { get; init; } = string.Empty;
        public FireEyeWorkerInput? Input { get; init; }
        public IReadOnlyList<string>? Languages { get; init; }
    }

    private sealed class FireEyeWorkerInput
    {
        public string? ImagePath { get; init; }
        public bool? ContrastEnhanced { get; init; }
    }

    private sealed class FireEyeWorkerResponse
    {
        public bool Ok { get; init; }
        public FireEyeCapabilities? Capabilities { get; init; }
        public FireEyeNativeResult? Result { get; init; }
        public string? Error { get; init; }
    }

    private sealed class FireEyeCapabilities
    {
        public bool Available { get; init; }
    }

    private sealed class FireEyeNativeResult
    {
        public string? Text { get; init; }
        public double? Confidence { get; init; }
        public IReadOnlyList<FireEyeNativeWord>? Words { get; init; }
        public IReadOnlyList<FireEyeNativeLine>? Lines { get; init; }
    }

    private sealed class FireEyeNativeLine
    {
        public string? Text { get; init; }
        public double? Confidence { get; init; }
        public FireEyeBoundingBox? Bbox { get; init; }
        public IReadOnlyList<string>? WordIds { get; init; }
        public int? Order { get; init; }
    }

    private sealed class FireEyeNativeWord
    {
        public string? Id { get; init; }
        public string? Text { get; init; }
        public double? Confidence { get; init; }
        public FireEyeBoundingBox? Bbox { get; init; }
    }

    private sealed class FireEyeBoundingBox
    {
        public double X { get; init; }
        public double Y { get; init; }
        public double Width { get; init; }
        public double Height { get; init; }
    }
}
