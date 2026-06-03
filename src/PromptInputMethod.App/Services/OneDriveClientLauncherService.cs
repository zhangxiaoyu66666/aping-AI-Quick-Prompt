using System.Diagnostics;
using Microsoft.Win32;

namespace PromptInputMethod.App.Services;

public sealed class OneDriveClientLauncherService
{
    public OneDriveClientLaunchResult TryLaunchClient()
    {
        if (IsOneDriveRunning())
        {
            return new OneDriveClientLaunchResult(true, "OneDrive 客户端已在运行。");
        }

        var executable = FindOneDriveExecutable();
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new OneDriveClientLaunchResult(false, "未找到 OneDrive 客户端，请确认已安装并登录 OneDrive。");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "/background",
                UseShellExecute = true
            });
            return new OneDriveClientLaunchResult(true, "已唤起 OneDrive 客户端，上传由 OneDrive 后台同步接管。");
        }
        catch (Exception ex)
        {
            return new OneDriveClientLaunchResult(false, $"唤起 OneDrive 客户端失败：{ex.Message}");
        }
    }

    private static bool IsOneDriveRunning()
    {
        try
        {
            return Process.GetProcessesByName("OneDrive").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindOneDriveExecutable()
    {
        foreach (var path in EnumerateCandidatePaths())
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string?> EnumerateCandidatePaths()
    {
        yield return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\App Paths\OneDrive.exe", string.Empty, null)?.ToString();
        yield return Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\App Paths\OneDrive.exe", string.Empty, null)?.ToString();
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "Microsoft OneDrive", "OneDrive.exe");
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "Microsoft OneDrive", "OneDrive.exe");
        }
    }
}

public sealed record OneDriveClientLaunchResult(bool LaunchedOrAlreadyRunning, string Message);
