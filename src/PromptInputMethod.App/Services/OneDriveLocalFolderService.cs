using System.Text.Json;
using System.Text.Json.Serialization;

namespace PromptInputMethod.App.Services;

public sealed class OneDriveLocalFolderService
{
    public const string DefaultAppFolderName = "啊拼";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly string[] OneDriveEnvironmentVariables =
    [
        "OneDrive",
        "OneDriveConsumer",
        "OneDriveCommercial"
    ];

    public string? DetectDefaultSyncFolder()
    {
        foreach (var variable in OneDriveEnvironmentVariables)
        {
            var root = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
            {
                return Path.Combine(root, DefaultAppFolderName);
            }
        }

        return null;
    }

    public void EnsureSyncRootReady(string syncRootPath)
    {
        if (string.IsNullOrWhiteSpace(syncRootPath))
        {
            throw new InvalidOperationException("请先选择 OneDrive 本地同步文件夹。");
        }

        Directory.CreateDirectory(syncRootPath);
    }

    public IReadOnlyList<string> FindConflictFiles(string syncRootPath)
    {
        if (string.IsNullOrWhiteSpace(syncRootPath) || !Directory.Exists(syncRootPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(syncRootPath, "*", SearchOption.AllDirectories)
            .Where(path => !IsBackupPath(syncRootPath, path))
            .Where(IsOneDriveConflictFile)
            .Take(12)
            .ToArray();
    }

    public async Task<T?> ReadJsonAsync<T>(
        string syncRootPath,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSyncPath(syncRootPath, relativePath);
        if (!File.Exists(fullPath))
        {
            return default;
        }

        return await RunFileIoWithRetryAsync(async () =>
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteJsonAsync<T>(
        string syncRootPath,
        string relativePath,
        T value,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveSyncPath(syncRootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await RunFileIoWithRetryAsync(async () =>
        {
            if (File.Exists(fullPath))
            {
                await BackupExistingFileAsync(syncRootPath, relativePath, fullPath, cancellationToken).ConfigureAwait(false);
            }

            var tempPath = $"{fullPath}.tmp-{Guid.NewGuid():N}";
            try
            {
                await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
                }

                File.Move(tempPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    public static string ResolveSyncPath(string syncRootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(syncRootPath))
        {
            throw new InvalidOperationException("请先选择 OneDrive 本地同步文件夹。");
        }

        var root = Path.GetFullPath(syncRootPath);
        var combined = Path.GetFullPath(Path.Combine(root, NormalizeRelativePath(relativePath)));
        var rootWithSeparator = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        if (!combined.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("同步文件路径越界。");
        }

        return combined;
    }

    private static async Task BackupExistingFileAsync(
        string syncRootPath,
        string relativePath,
        string fullPath,
        CancellationToken cancellationToken)
    {
        var backupPath = ResolveSyncPath(
            syncRootPath,
            Path.Combine("sync", "backups", DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff"), NormalizeRelativePath(relativePath)));
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        await using var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        await using var target = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return (relativePath ?? string.Empty)
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
    }

    private static bool IsOneDriveConflictFile(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        return name.Contains("conflict", StringComparison.OrdinalIgnoreCase)
            || name.Contains("conflicted copy", StringComparison.OrdinalIgnoreCase)
            || name.Contains("冲突", StringComparison.OrdinalIgnoreCase)
            || name.Contains("冲突副本", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBackupPath(string syncRootPath, string fullPath)
    {
        var backupRoot = ResolveSyncPath(syncRootPath, "sync/backups");
        var backupRootWithSeparator = Path.EndsInDirectorySeparator(backupRoot) ? backupRoot : backupRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(backupRootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<T> RunFileIoWithRetryAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransientFileIo(ex) && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(120 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientFileIo(Exception ex)
    {
        return ex is IOException or UnauthorizedAccessException;
    }
}
