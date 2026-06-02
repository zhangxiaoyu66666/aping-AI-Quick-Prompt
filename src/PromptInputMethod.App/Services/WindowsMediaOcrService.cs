using PromptOcrResult = PromptInputMethod.Core.Ocr.OcrResult;
using PromptInputMethod.Core.Ocr;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

namespace PromptInputMethod.App.Services;

public sealed class WindowsMediaOcrService
{
    public async Task<PromptOcrResult> RecognizeImageFileAsync(StorageFile file)
    {
        using var stream = await file.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var bitmap = await decoder.GetSoftwareBitmapAsync();
        return await RecognizeSoftwareBitmapAsync(bitmap);
    }

    public async Task<PromptOcrResult> RecognizeSoftwareBitmapAsync(SoftwareBitmap bitmap)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))
            ?? throw new InvalidOperationException("当前系统没有可用的 Windows OCR 语言包。");

        if (bitmap.PixelWidth > OcrEngine.MaxImageDimension || bitmap.PixelHeight > OcrEngine.MaxImageDimension)
        {
            throw new InvalidOperationException($"图片尺寸过大，Windows OCR 最大支持 {OcrEngine.MaxImageDimension}px。");
        }

        using var ocrBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var result = await engine.RecognizeAsync(ocrBitmap);

        var text = new StringBuilder();
        var lines = new List<OcrLineResult>();
        foreach (var line in result.Lines)
        {
            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.Append(line.Text);
            var words = line.Words
                .Select(word => new OcrWordResult(word.Text, ToBoundingBox(word.BoundingRect)))
                .ToArray();
            lines.Add(new OcrLineResult(line.Text, words, MergeBoundingBoxes(words)));
        }

        return new PromptOcrResult(text.ToString(), lines, null);
    }

    private static OcrBoundingBox ToBoundingBox(Windows.Foundation.Rect rect)
    {
        return new OcrBoundingBox(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static OcrBoundingBox? MergeBoundingBoxes(IReadOnlyList<OcrWordResult> words)
    {
        if (words.Count == 0)
        {
            return null;
        }

        var left = words.Min(word => word.BoundingBox.X);
        var top = words.Min(word => word.BoundingBox.Y);
        var right = words.Max(word => word.BoundingBox.X + word.BoundingBox.Width);
        var bottom = words.Max(word => word.BoundingBox.Y + word.BoundingBox.Height);
        return new OcrBoundingBox(left, top, right - left, bottom - top);
    }
}
