namespace PromptInputMethod.App.Services;

public sealed record OneDriveSyncManifest(
    string Schema,
    string AppVersion,
    DateTimeOffset UpdatedAt,
    string DeviceId,
    IReadOnlyList<OneDriveSyncManifestItem> Items,
    IReadOnlyList<OneDriveSyncTombstone> Tombstones)
{
    public const string CurrentSchema = "aipin.sync.v1";

    public static OneDriveSyncManifest Empty(string appVersion, string deviceId)
    {
        return new OneDriveSyncManifest(CurrentSchema, appVersion, DateTimeOffset.UtcNow, deviceId, [], []);
    }
}

public sealed record OneDriveSyncManifestItem(
    string Kind,
    string Id,
    string Path,
    DateTimeOffset UpdatedAt,
    string Sha256,
    string KeyId = "",
    string CiphertextSha256 = "");

public sealed record OneDriveSyncTombstone(
    string Kind,
    string Id,
    DateTimeOffset DeletedAt,
    string DeviceId,
    string KeyId = "");

public static class OneDriveSyncPaths
{
    public const string Manifest = "sync/manifest.json";
    public const string Vault = "sync/crypto/vault.json";

    public static string History(string id)
    {
        return $"sync/history/{SanitizePathId(id)}.json";
    }

    public static string HistoryTombstone(string id)
    {
        return $"sync/history-tombstones/{SanitizePathId(id)}.json";
    }

    public static string Record(string kind, string id)
    {
        return $"sync/records/{SanitizePathId(kind)}/{SanitizePathId(id)}.json";
    }

    private static string SanitizePathId(string value)
    {
        return string.Concat((value ?? string.Empty)
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_'));
    }
}
