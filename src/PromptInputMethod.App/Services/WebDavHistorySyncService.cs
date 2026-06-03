using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PromptInputMethod.Core.Sync;

namespace PromptInputMethod.App.Services;

public sealed class WebDavHistorySyncService
{
    private const string HistoryKind = "history";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly WebDavRemoteStoreService _remoteStore;
    private readonly OneDriveSyncExportService _historySync;
    private readonly OneDriveVaultCacheService _vaultCache;
    private readonly E2eeSyncCryptoService _crypto;

    public WebDavHistorySyncService()
        : this(new WebDavRemoteStoreService(), new OneDriveSyncExportService(), new OneDriveVaultCacheService(), new E2eeSyncCryptoService())
    {
    }

    public WebDavHistorySyncService(
        WebDavRemoteStoreService remoteStore,
        OneDriveSyncExportService historySync,
        OneDriveVaultCacheService vaultCache,
        E2eeSyncCryptoService crypto)
    {
        _remoteStore = remoteStore;
        _historySync = historySync;
        _vaultCache = vaultCache;
        _crypto = crypto;
    }

    public Task TestConnectionAsync(
        WebDavConnectionSettings connection,
        CancellationToken cancellationToken = default)
    {
        EnsureConnectionConfigured(connection);
        return _remoteStore.TestConnectionAsync(connection, cancellationToken);
    }

    public async Task<WebDavHistorySyncResult> PushEncryptedHistoryAsync(
        WebDavConnectionSettings connection,
        string passphrase,
        string appVersion,
        string deviceId,
        bool rememberVaultOnThisDevice,
        CancellationToken cancellationToken = default)
    {
        EnsureConnectionConfigured(connection);
        await EnsureNoConflictFilesAsync(connection, cancellationToken).ConfigureAwait(false);

        var cacheScope = BuildVaultCacheScope(connection);
        var (vault, vaultKey, createdVault) = await ResolveVaultAsync(
            connection,
            cacheScope,
            passphrase,
            createIfMissing: true,
            allowCachedVaultKey: true,
            rememberVaultOnThisDevice,
            cancellationToken).ConfigureAwait(false);
        if (vault is null || vaultKey is null)
        {
            throw new InvalidOperationException("WebDAV 同步 vault 未准备好。");
        }

        try
        {
            if (createdVault)
            {
                await _remoteStore.WriteJsonAsync(connection, OneDriveSyncPaths.Vault, vault, cancellationToken).ConfigureAwait(false);
            }

            var existingManifest = await _remoteStore.ReadJsonAsync<OneDriveSyncManifest>(
                    connection,
                    OneDriveSyncPaths.Manifest,
                    cancellationToken).ConfigureAwait(false)
                ?? OneDriveSyncManifest.Empty(appVersion, deviceId);

            var documents = _historySync.ExportEncryptedHistory(vaultKey, vault);
            var updatedItems = new Dictionary<string, OneDriveSyncManifestItem>(StringComparer.Ordinal);
            foreach (var item in existingManifest.Items)
            {
                updatedItems[BuildManifestKey(item.Kind, item.Id)] = item;
            }

            foreach (var document in documents)
            {
                var path = OneDriveSyncPaths.History(document.Id);
                await _remoteStore.WriteJsonAsync(connection, path, document, cancellationToken).ConfigureAwait(false);
                updatedItems[BuildManifestKey(HistoryKind, document.Id)] = new OneDriveSyncManifestItem(
                    HistoryKind,
                    document.Id,
                    path,
                    document.EncryptedAt,
                    ComputeJsonSha256(document),
                    document.KeyId,
                    document.CiphertextSha256);
            }

            var manifest = existingManifest with
            {
                Schema = OneDriveSyncManifest.CurrentSchema,
                AppVersion = appVersion,
                DeviceId = deviceId,
                UpdatedAt = DateTimeOffset.UtcNow,
                Items = updatedItems.Values
                    .OrderBy(item => item.Kind, StringComparer.Ordinal)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray(),
                Tombstones = existingManifest.Tombstones
                    .OrderBy(item => item.Kind, StringComparer.Ordinal)
                    .ThenBy(item => item.Id, StringComparer.Ordinal)
                    .ToArray()
            };

            await _remoteStore.WriteJsonAsync(connection, OneDriveSyncPaths.Manifest, manifest, cancellationToken).ConfigureAwait(false);
            return new WebDavHistorySyncResult(BuildRemoteScope(connection), vault.KeyId, createdVault, documents.Count, 0, DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultKey);
        }
    }

    public async Task<WebDavHistorySyncResult> PullEncryptedHistoryAsync(
        WebDavConnectionSettings connection,
        string passphrase,
        bool rememberVaultOnThisDevice,
        CancellationToken cancellationToken = default)
    {
        EnsureConnectionConfigured(connection);
        await EnsureNoConflictFilesAsync(connection, cancellationToken).ConfigureAwait(false);

        var cacheScope = BuildVaultCacheScope(connection);
        var (vault, vaultKey, createdVault) = await ResolveVaultAsync(
            connection,
            cacheScope,
            passphrase,
            createIfMissing: false,
            allowCachedVaultKey: true,
            rememberVaultOnThisDevice,
            cancellationToken).ConfigureAwait(false);
        if (vault is null || vaultKey is null)
        {
            return new WebDavHistorySyncResult(BuildRemoteScope(connection), string.Empty, false, 0, 0, DateTimeOffset.UtcNow);
        }

        try
        {
            var manifest = await _remoteStore.ReadJsonAsync<OneDriveSyncManifest>(
                connection,
                OneDriveSyncPaths.Manifest,
                cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return new WebDavHistorySyncResult(BuildRemoteScope(connection), vault.KeyId, createdVault, 0, 0, DateTimeOffset.UtcNow);
            }

            var deletedHistoryIds = manifest.Tombstones
                .Where(item => string.Equals(item.Kind, HistoryKind, StringComparison.Ordinal))
                .Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var documents = new List<EncryptedSyncDocument>();
            foreach (var item in manifest.Items.Where(item => string.Equals(item.Kind, HistoryKind, StringComparison.Ordinal)))
            {
                if (deletedHistoryIds.Contains(item.Id))
                {
                    continue;
                }

                var document = await _remoteStore.ReadJsonAsync<EncryptedSyncDocument>(
                    connection,
                    item.Path,
                    cancellationToken).ConfigureAwait(false);
                if (document is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(item.CiphertextSha256)
                    && !string.Equals(item.CiphertextSha256, document.CiphertextSha256, StringComparison.Ordinal))
                {
                    throw new CryptographicException($"Encrypted WebDAV history manifest hash mismatch for {item.Id}.");
                }

                documents.Add(document);
            }

            var imported = _historySync.ImportEncryptedHistory(documents, vaultKey, vault);
            return new WebDavHistorySyncResult(BuildRemoteScope(connection), vault.KeyId, createdVault, 0, imported, DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultKey);
        }
    }

    private async Task<(EncryptedSyncVault? Vault, byte[]? VaultKey, bool CreatedVault)> ResolveVaultAsync(
        WebDavConnectionSettings connection,
        string cacheScope,
        string passphrase,
        bool createIfMissing,
        bool allowCachedVaultKey,
        bool rememberVaultOnThisDevice,
        CancellationToken cancellationToken)
    {
        var vault = await _remoteStore.ReadJsonAsync<EncryptedSyncVault>(
            connection,
            OneDriveSyncPaths.Vault,
            cancellationToken).ConfigureAwait(false);
        if (vault is null)
        {
            if (!createIfMissing)
            {
                return (null, null, false);
            }

            var created = _crypto.CreateVault(passphrase);
            if (rememberVaultOnThisDevice)
            {
                _vaultCache.WriteVaultKey(cacheScope, created.Vault.KeyId, created.VaultKey);
            }

            return (created.Vault, created.VaultKey, true);
        }

        if (allowCachedVaultKey)
        {
            var cached = _vaultCache.ReadVaultKey(cacheScope, vault.KeyId);
            if (cached is not null)
            {
                return (vault, cached, false);
            }
        }

        var vaultKey = _crypto.UnlockVault(vault, passphrase);
        if (rememberVaultOnThisDevice)
        {
            _vaultCache.WriteVaultKey(cacheScope, vault.KeyId, vaultKey);
        }

        return (vault, vaultKey, false);
    }

    private async Task EnsureNoConflictFilesAsync(
        WebDavConnectionSettings connection,
        CancellationToken cancellationToken)
    {
        var conflicts = await _remoteStore.FindConflictFilesAsync(connection, cancellationToken).ConfigureAwait(false);
        if (conflicts.Count == 0)
        {
            return;
        }

        var preview = string.Join("；", conflicts.Select(Path.GetFileName));
        throw new InvalidOperationException($"检测到 WebDAV 冲突副本，请先手动合并后再同步：{preview}");
    }

    private static void EnsureConnectionConfigured(WebDavConnectionSettings connection)
    {
        if (string.IsNullOrWhiteSpace(connection.ServerUrl))
        {
            throw new InvalidOperationException("请先填写 WebDAV 服务器地址。");
        }

        if (string.IsNullOrWhiteSpace(connection.Username))
        {
            throw new InvalidOperationException("请先填写 WebDAV 用户名。");
        }

        if (string.IsNullOrWhiteSpace(connection.Password))
        {
            throw new InvalidOperationException("请先填写 WebDAV 应用密码。");
        }
    }

    private static string ComputeJsonSha256<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return Base64Url.Encode(SHA256.HashData(bytes));
    }

    public static string BuildVaultCacheScope(WebDavConnectionSettings connection)
    {
        return $"webdav:{BuildRemoteScope(connection)}";
    }

    public static string BuildRemoteScope(WebDavConnectionSettings connection)
    {
        return $"{connection.Username}@{connection.ServerUrl.Trim().TrimEnd('/')}/{connection.RemoteRootPath.Trim().Trim('/')}";
    }

    private static string BuildManifestKey(string kind, string id)
    {
        return $"{kind}:{id}";
    }
}

public sealed record WebDavHistorySyncResult(
    string RemoteScope,
    string KeyId,
    bool CreatedVault,
    int UploadedHistoryCount,
    int ImportedHistoryCount,
    DateTimeOffset SyncedAt);
