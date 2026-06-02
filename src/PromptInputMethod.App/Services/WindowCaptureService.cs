using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;

namespace PromptInputMethod.App.Services;

public sealed class WindowCaptureService
{
    public ScreenCaptureResult CaptureVirtualScreen()
    {
        var bounds = new RectInt32(
            GetSystemMetrics(SystemMetricVirtualScreenX),
            GetSystemMetrics(SystemMetricVirtualScreenY),
            GetSystemMetrics(SystemMetricVirtualScreenWidth),
            GetSystemMetrics(SystemMetricVirtualScreenHeight));
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new InvalidOperationException("当前屏幕尺寸无效，无法截图。");
        }

        var screenDc = GetDC(0);
        if (screenDc == 0)
        {
            throw new InvalidOperationException($"获取屏幕 DC 失败：{Marshal.GetLastWin32Error()}");
        }

        try
        {
            return new ScreenCaptureResult(CaptureDeviceContext(screenDc, bounds.X, bounds.Y, bounds.Width, bounds.Height), bounds);
        }
        finally
        {
            ReleaseDC(0, screenDc);
        }
    }

    public SoftwareBitmap CaptureWindow(nint hwnd)
    {
        if (hwnd == 0 || !IsWindow(hwnd))
        {
            throw new InvalidOperationException("没有可截图的目标窗口，请用快捷键呼出后再试。");
        }

        if (IsIconic(hwnd))
        {
            throw new InvalidOperationException("目标窗口已最小化，无法截取当前窗口。");
        }

        if (!GetWindowRect(hwnd, out var rect))
        {
            throw new InvalidOperationException($"读取目标窗口尺寸失败：{Marshal.GetLastWin32Error()}");
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("目标窗口尺寸无效，无法截图。");
        }

        var windowDc = GetWindowDC(hwnd);
        if (windowDc == 0)
        {
            throw new InvalidOperationException($"获取目标窗口 DC 失败：{Marshal.GetLastWin32Error()}");
        }

        try
        {
            return CaptureWindowFromDeviceContext(hwnd, windowDc, width, height);
        }
        finally
        {
            ReleaseDC(hwnd, windowDc);
        }
    }

    public SoftwareBitmap Crop(SoftwareBitmap source, Rect rect)
    {
        var x = Math.Clamp((int)Math.Floor(rect.X), 0, source.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Floor(rect.Y), 0, source.PixelHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling(rect.X + rect.Width), x + 1, source.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(rect.Y + rect.Height), y + 1, source.PixelHeight);
        var width = right - x;
        var height = bottom - y;

        var sourceBytes = new byte[checked(source.PixelWidth * source.PixelHeight * 4)];
        source.CopyToBuffer(sourceBytes.AsBuffer());

        var cropBytes = new byte[checked(width * height * 4)];
        var sourceStride = source.PixelWidth * 4;
        var cropStride = width * 4;
        for (var row = 0; row < height; row++)
        {
            Buffer.BlockCopy(sourceBytes, ((y + row) * sourceStride) + (x * 4), cropBytes, row * cropStride, cropStride);
        }

        var cropped = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        cropped.CopyFromBuffer(cropBytes.AsBuffer());
        return cropped;
    }

    private static SoftwareBitmap CaptureWindowFromDeviceContext(nint hwnd, nint windowDc, int width, int height)
    {
        var memoryDc = CreateCompatibleDC(windowDc);
        if (memoryDc == 0)
        {
            throw new InvalidOperationException($"创建截图 DC 失败：{Marshal.GetLastWin32Error()}");
        }

        nint bitmap = 0;
        nint oldObject = 0;
        try
        {
            var info = CreateBitmapInfo(width, height);
            bitmap = CreateDIBSection(windowDc, ref info, DibRgbColors, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0)
            {
                throw new InvalidOperationException($"创建截图位图失败：{Marshal.GetLastWin32Error()}");
            }

            oldObject = SelectObject(memoryDc, bitmap);
            var printed = PrintWindow(hwnd, memoryDc, PrintWindowRenderFullContent);
            if (!printed)
            {
                var copied = BitBlt(memoryDc, 0, 0, width, height, windowDc, 0, 0, RasterOperationSourceCopy | RasterOperationCaptureBlt);
                if (!copied)
                {
                    throw new InvalidOperationException($"截取当前窗口失败：{Marshal.GetLastWin32Error()}");
                }
            }

            return CopyBitsToSoftwareBitmap(bits, width, height);
        }
        finally
        {
            if (oldObject != 0)
            {
                SelectObject(memoryDc, oldObject);
            }

            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            DeleteDC(memoryDc);
        }
    }

    private static SoftwareBitmap CaptureDeviceContext(nint sourceDc, int sourceX, int sourceY, int width, int height)
    {
        var memoryDc = CreateCompatibleDC(sourceDc);
        if (memoryDc == 0)
        {
            throw new InvalidOperationException($"创建截图 DC 失败：{Marshal.GetLastWin32Error()}");
        }

        nint bitmap = 0;
        nint oldObject = 0;
        try
        {
            var info = CreateBitmapInfo(width, height);

            bitmap = CreateDIBSection(sourceDc, ref info, DibRgbColors, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0)
            {
                throw new InvalidOperationException($"创建截图位图失败：{Marshal.GetLastWin32Error()}");
            }

            oldObject = SelectObject(memoryDc, bitmap);
            var copied = BitBlt(memoryDc, 0, 0, width, height, sourceDc, sourceX, sourceY, RasterOperationSourceCopy | RasterOperationCaptureBlt);
            if (!copied)
            {
                throw new InvalidOperationException($"截取屏幕失败：{Marshal.GetLastWin32Error()}");
            }

            return CopyBitsToSoftwareBitmap(bits, width, height);
        }
        finally
        {
            if (oldObject != 0)
            {
                SelectObject(memoryDc, oldObject);
            }

            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            DeleteDC(memoryDc);
        }
    }

    private static BITMAPINFO CreateBitmapInfo(int width, int height)
    {
        return new BITMAPINFO
        {
            Header = new BITMAPINFOHEADER
            {
                Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = BitmapCompressionRgb
            }
        };
    }

    private static SoftwareBitmap CopyBitsToSoftwareBitmap(nint bits, int width, int height)
    {
        var bytes = new byte[checked(width * height * 4)];
        Marshal.Copy(bits, bytes, 0, bytes.Length);
        var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        softwareBitmap.CopyFromBuffer(bytes.AsBuffer());
        return softwareBitmap;
    }

    private const int SystemMetricVirtualScreenX = 76;
    private const int SystemMetricVirtualScreenY = 77;
    private const int SystemMetricVirtualScreenWidth = 78;
    private const int SystemMetricVirtualScreenHeight = 79;
    private const uint DibRgbColors = 0;
    private const uint BitmapCompressionRgb = 0;
    private const uint PrintWindowRenderFullContent = 0x00000002;
    private const uint RasterOperationSourceCopy = 0x00CC0020;
    private const uint RasterOperationCaptureBlt = 0x40000000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowDC(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(nint hWnd, nint hDC);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint nFlags);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(nint hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateDIBSection(nint hdc, ref BITMAPINFO pbmi, uint usage, out nint ppvBits, nint hSection, uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint SelectObject(nint hdc, nint hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(nint hdcDest, int xDest, int yDest, int width, int height, nint hdcSource, int xSource, int ySource, uint rasterOperation);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ClrUsed;
        public uint ClrImportant;
    }
}

public sealed record ScreenCaptureResult(SoftwareBitmap Bitmap, RectInt32 Bounds) : IDisposable
{
    public void Dispose() => Bitmap.Dispose();
}
