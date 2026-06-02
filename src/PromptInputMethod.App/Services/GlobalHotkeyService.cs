using System.Runtime.InteropServices;

namespace PromptInputMethod.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x50494d;
    private const uint ModControl = 0x0002;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const int WmHotkey = 0x0312;

    private readonly nint _hwnd;
    private readonly SUBCLASSPROC _subclassProc;
    private bool _subclassed;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public static IReadOnlyList<string> SupportedMainKeys { get; } = BuildSupportedMainKeys();

    public GlobalHotkeyService(nint hwnd)
    {
        _hwnd = hwnd;
        _subclassProc = WindowSubclassProc;
        _subclassed = SetWindowSubclass(_hwnd, _subclassProc, 1, 0);
    }

    public bool RegisterHotkey(HotkeySettings settings)
    {
        if (_registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }

        if (!settings.Enabled)
        {
            return true;
        }

        if (!TryBuildNativeHotkey(settings, out var modifiers, out var virtualKey))
        {
            return false;
        }

        _registered = RegisterHotKey(_hwnd, HotkeyId, modifiers, virtualKey);
        return _registered;
    }

    public bool RegisterDefaultHotkey()
    {
        return RegisterHotkey(new HotkeySettings());
    }

    public static string NormalizeMainKey(string? key)
    {
        return TryNormalizeMainKey(key, out var normalized) ? normalized : "Space";
    }

    private static bool TryBuildNativeHotkey(HotkeySettings settings, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (settings.Control)
        {
            modifiers |= ModControl;
        }

        if (settings.Shift)
        {
            modifiers |= ModShift;
        }

        if (settings.Alt)
        {
            modifiers |= ModAlt;
        }

        if (settings.Win)
        {
            modifiers |= ModWin;
        }

        virtualKey = ParseVirtualKey(settings.Key);
        return modifiers != 0 && virtualKey != 0;
    }

    private static uint ParseVirtualKey(string? key)
    {
        if (!TryNormalizeMainKey(key, out var normalized))
        {
            return 0;
        }

        if (normalized.Length == 1)
        {
            return normalized[0];
        }

        if (normalized.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            return 0x20;
        }

        if (normalized.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            return 0x09;
        }

        if (normalized.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            return 0x0D;
        }

        if (normalized.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized[1..], out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            return (uint)(0x70 + functionKey - 1);
        }

        return 0;
    }

    private static bool TryNormalizeMainKey(string? key, out string normalized)
    {
        normalized = string.Empty;
        var value = (key ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.Length == 1)
        {
            var c = char.ToUpperInvariant(value[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                normalized = c.ToString();
                return true;
            }
        }

        if (value.Equals("Space", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Space";
            return true;
        }

        if (value.Equals("Tab", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Tab";
            return true;
        }

        if (value.Equals("Enter", StringComparison.OrdinalIgnoreCase))
        {
            normalized = "Enter";
            return true;
        }

        if (value.StartsWith("F", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(value[1..], out var functionKey)
            && functionKey is >= 1 and <= 24)
        {
            normalized = $"F{functionKey}";
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> BuildSupportedMainKeys()
    {
        var keys = new List<string>
        {
            "Space",
            "Tab",
            "Enter"
        };

        keys.AddRange(Enumerable.Range('A', 26).Select(code => ((char)code).ToString()));
        keys.AddRange(Enumerable.Range(0, 10).Select(number => number.ToString()));
        keys.AddRange(Enumerable.Range(1, 24).Select(number => $"F{number}"));
        return keys;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_registered)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
        }

        if (_subclassed)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, 1);
            _subclassed = false;
        }

        _disposed = true;
    }

    private nint WindowSubclassProc(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint refData)
    {
        try
        {
            if (message == WmHotkey && wParam == HotkeyId)
            {
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
                return 0;
            }

            if (message == WmNcDestroy && _subclassed)
            {
                RemoveWindowSubclass(hwnd, _subclassProc, subclassId);
                _subclassed = false;
            }
        }
        catch
        {
            if (message == WmHotkey)
            {
                return 0;
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private const int WmNcDestroy = 0x0082;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SUBCLASSPROC(nint hwnd, uint message, nint wParam, nint lParam, nuint subclassId, nuint refData);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(nint hWnd, SUBCLASSPROC pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);
}
