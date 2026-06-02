using System.Runtime.InteropServices;

namespace PromptInputMethod.App.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint TrayIconId = 0x50494d;
    private const uint TrayCallbackMessage = 0x8000 + 0x504;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NimSetVersion = 0x00000004;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NotifyIconVersion4 = 4;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x00000010;
    private const uint LrDefaultSize = 0x00000040;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;
    private const int NinSelect = 0x0400;
    private const int NinKeySelect = 0x0401;
    private const int WmNcDestroy = 0x0082;

    private readonly nint _hwnd;
    private readonly SUBCLASSPROC _subclassProc;
    private readonly nint _icon;
    private bool _subclassed;
    private bool _added;
    private bool _disposed;

    public event EventHandler? ShowRequested;
    public event EventHandler<TrayMenuRequestedEventArgs>? MenuRequested;

    public TrayIconService(nint hwnd, string iconPath, string tooltip)
    {
        _hwnd = hwnd;
        _subclassProc = WindowSubclassProc;
        _subclassed = SetWindowSubclass(_hwnd, _subclassProc, 2, 0);
        _icon = File.Exists(iconPath)
            ? LoadImage(0, iconPath, ImageIcon, 0, 0, LrLoadFromFile | LrDefaultSize)
            : 0;

        AddTrayIcon(tooltip);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_added)
        {
            Shell_NotifyIcon(NimDelete, CreateNotifyIconData(string.Empty));
            _added = false;
        }

        if (_subclassed)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, 2);
            _subclassed = false;
        }

        if (_icon != 0)
        {
            DestroyIcon(_icon);
        }

        _disposed = true;
    }

    private void AddTrayIcon(string tooltip)
    {
        var data = CreateNotifyIconData(tooltip);
        data.uFlags = NifMessage | NifIcon | NifTip;
        _added = Shell_NotifyIcon(NimAdd, data);
        if (_added)
        {
            data.uTimeoutOrVersion = NotifyIconVersion4;
            Shell_NotifyIcon(NimSetVersion, data);
        }
    }

    private NOTIFYICONDATA CreateNotifyIconData(string tooltip)
    {
        return new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = TrayIconId,
            uCallbackMessage = TrayCallbackMessage,
            hIcon = _icon,
            szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private nint WindowSubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint refData)
    {
        try
        {
            if (message == TrayCallbackMessage)
            {
                var trayMessage = (int)((uint)lParam & 0xFFFF);
                if (trayMessage is WmLButtonUp or WmLButtonDblClk or NinSelect or NinKeySelect)
                {
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                    return 0;
                }

                if (trayMessage is WmRButtonUp or WmContextMenu)
                {
                    ShowContextMenu();
                    return 0;
                }
            }

            if (message == WmNcDestroy && _subclassed)
            {
                RemoveWindowSubclass(hwnd, _subclassProc, subclassId);
                _subclassed = false;
            }
        }
        catch
        {
            if (message == TrayCallbackMessage)
            {
                return 0;
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        if (!GetCursorPos(out var point))
        {
            return;
        }

        MenuRequested?.Invoke(this, new TrayMenuRequestedEventArgs(point.X, point.Y));
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SUBCLASSPROC(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint refData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, in NOTIFYICONDATA lpData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);
}

public sealed record TrayMenuRequestedEventArgs(int X, int Y);
