using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using WinRT.Interop;

namespace PromptInputMethod.App;

public sealed partial class RegionSelectionWindow : Window
{
    private readonly SoftwareBitmap _screenshot;
    private readonly RectInt32 _screenBounds;
    private readonly TaskCompletionSource<Rect?> _completion = new();
    private Point _startPoint;
    private bool _selecting;

    public RegionSelectionWindow(SoftwareBitmap screenshot, RectInt32 screenBounds)
    {
        InitializeComponent();
        _screenshot = screenshot;
        _screenBounds = screenBounds;

        ConfigureWindowBounds();
        Content.KeyDown += Content_KeyDown;
        _ = LoadScreenshotAsync();
    }

    public Task<Rect?> SelectionTask => _completion.Task;

    private async Task LoadScreenshotAsync()
    {
        var source = new SoftwareBitmapSource();
        await source.SetBitmapAsync(_screenshot);
        ScreenshotImage.Source = source;
    }

    private void ConfigureWindowBounds()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        appWindow.MoveAndResize(_screenBounds);
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _selecting = true;
        _startPoint = e.GetCurrentPoint(RootGrid).Position;
        SelectionRectangle.Visibility = Visibility.Visible;
        RootGrid.CapturePointer(e.Pointer);
        UpdateSelection(_startPoint, _startPoint);
        e.Handled = true;
    }

    private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        UpdateSelection(_startPoint, e.GetCurrentPoint(RootGrid).Position);
        e.Handled = true;
    }

    private void RootGrid_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _selecting = false;
        RootGrid.ReleasePointerCapture(e.Pointer);
        var rect = GetBitmapSelectionRect(_startPoint, e.GetCurrentPoint(RootGrid).Position);
        Complete(rect.Width < 4 || rect.Height < 4 ? null : rect);
        e.Handled = true;
    }

    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            Complete(null);
            e.Handled = true;
        }
    }

    private void UpdateSelection(Point start, Point end)
    {
        var x = Math.Min(start.X, end.X);
        var y = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private Rect GetBitmapSelectionRect(Point start, Point end)
    {
        var scaleX = _screenshot.PixelWidth / Math.Max(1, RootGrid.ActualWidth);
        var scaleY = _screenshot.PixelHeight / Math.Max(1, RootGrid.ActualHeight);
        var x = Math.Min(start.X, end.X) * scaleX;
        var y = Math.Min(start.Y, end.Y) * scaleY;
        var width = Math.Abs(end.X - start.X) * scaleX;
        var height = Math.Abs(end.Y - start.Y) * scaleY;
        return new Rect(x, y, width, height);
    }

    private void Complete(Rect? selection)
    {
        if (!_completion.Task.IsCompleted)
        {
            _completion.SetResult(selection);
        }

        Close();
    }
}
