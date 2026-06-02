using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PromptInputMethod.Core.Prompt;

namespace PromptInputMethod.App.Services;

public sealed class ForegroundWindowService
{
    public WindowContext GetCurrentWindowContext()
    {
        var hwnd = GetForegroundWindow();
        return GetWindowContext(hwnd);
    }

    public nint GetForegroundWindowHandle() => GetForegroundWindow();

    public WindowContext GetWindowContext(nint hwnd) => ReadWindowContext(hwnd);

    public void ActivateWindow(nint hwnd)
    {
        if (hwnd != 0)
        {
            SetForegroundWindow(hwnd);
        }
    }

    private static WindowContext ReadWindowContext(nint hwnd)
    {
        if (hwnd == 0)
        {
            return WindowContext.Unknown;
        }

        var title = GetWindowTitle(hwnd);
        _ = GetWindowThreadProcessId(hwnd, out var processId);
        var processName = string.Empty;

        try
        {
            processName = Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            processName = string.Empty;
        }

        var className = GetClassNameText(hwnd);
        return new WindowContext(processName, title, className);
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(length + 1);
        GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string GetClassNameText(nint hwnd)
    {
        var builder = new StringBuilder(256);
        GetClassName(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
}
