using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace PromptInputMethod.App;

public sealed partial class TrayMenuWindow : Window
{
    private const int MenuWidth = 248;
    private const int MenuHeight = 178;
    private const int SwHide = 0;
    private const int SwShow = 5;
    private bool _handlingCommand;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public TrayMenuWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        Activated += TrayMenuWindow_Activated;
        Content.KeyDown += Content_KeyDown;
    }

    public void ShowAt(int x, int y)
    {
        _handlingCommand = false;
        MoveNearPoint(x, y);
        ShowWindow(WindowNative.GetWindowHandle(this), SwShow);
        Activate();
    }

    public void HideMenu()
    {
        ShowWindow(WindowNative.GetWindowHandle(this), SwHide);
    }

    public void CloseForAppExit()
    {
        _handlingCommand = true;
        Close();
    }

    private void ConfigureWindow()
    {
        SystemBackdrop = new DesktopAcrylicBackdrop();

        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        appWindow.Resize(new SizeInt32(MenuWidth, MenuHeight));
    }

    private void MoveNearPoint(int x, int y)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        var displayArea = DisplayArea.GetFromPoint(new PointInt32(x, y), DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;
        var menuX = x + MenuWidth > workArea.X + workArea.Width
            ? x - MenuWidth + 8
            : x - 8;
        var menuY = y + MenuHeight > workArea.Y + workArea.Height
            ? y - MenuHeight + 8
            : y - 8;

        menuX = Math.Clamp(menuX, workArea.X, workArea.X + workArea.Width - MenuWidth);
        menuY = Math.Clamp(menuY, workArea.Y, workArea.Y + workArea.Height - MenuHeight);
        appWindow.Move(new PointInt32(menuX, menuY));
    }

    private void TrayMenuWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_handlingCommand && args.WindowActivationState == WindowActivationState.Deactivated)
        {
            HideMenu();
        }
    }

    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            HideMenu();
            e.Handled = true;
        }
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        _handlingCommand = true;
        HideMenu();
        ShowRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _handlingCommand = true;
        HideMenu();
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
}
