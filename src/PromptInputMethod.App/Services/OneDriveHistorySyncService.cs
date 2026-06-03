using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PromptInputMethod.Core.Sync;

namespace PromptInputMethod.App.Services;

public sealed class OneDriveHistorySyncService
{
    private const string HistoryKind = "history";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly OneDriveLocalFolderService _folder;
    private readonly OneDriveSyncExportService _historySync;
    private readonly OneDriveVaultCacheService _vaultCache;
    private readonly E2eeSyncCryptoService _crypto;

    public OneDriveHistorySyncService()
        : this(new OneDriveLocalFolderService(), new OneDriveSyncExportService(), new OneDriveVaultCacheService(), new E2eeSyncCryptoService())
    {
    }

    public OneDriveHistorySyncService(
        OneDriveLocalFolderService folder,
        OneDriveSyncExportService historySync,
        OneDriveVaultCacheService vaultCache,
        E2eeSyncCryptoService crypto)
    {
        _folder = folder;
        _historySync = historySync;
        _vaultCache = vaultCache;
        _crypto = crypto;
    }

    public async Task<OneDriveHistorySyncResult> PushEncryptedHistoryAsync(
        string syncRootPath,
        string passphrase,
        string appVersion,
        string deviceId,
        bool rememberVaultOnThisDevice,
        CancellationToken cancellationToken = default)
    {
        _folder.EnsureSyncRootReady(syncRootPath);
        EnsureNoConflictFiles(syncRootPath);

        var (vault, vaultKey, createdVault) = await ResolveVaultAsync(
            syncRootPath,
            passphrase,
            createIfMissing: true,
            allowCachedVaultKey: true,
            rememberVaultOnThisDevice,
            cancellationToken).ConfigureAwait(false);

        if (vault is null || vaultKey is null)
        {
            throw new InvalidOperationException("同步 vault 未准备好。");
        }

        try
        {
            if (createdVault)
            {
                await _folder.WriteJsonAsync(syncRootPath, OneDriveSyncPaths.Vault, vault, cancellationToken).ConfigureAwait(false);
            }

            var existingManifest = await _folder.ReadJsonAsync<OneDriveSyncManifest>(
                    syncRootPath,
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
                await _folder.WriteJsonAsync(syncRootPath, path, document, cancellationToken).ConfigureAwait(false);
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

            await _folder.WriteJsonAsync(syncRootPath, OneDriveSyncPaths.Manifest, manifest, cancellationToken).ConfigureAwait(false);
            return new OneDriveHistorySyncResult(syncRootPath, vault.KeyId, createdVault, documents.Count, 0, DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultKey);
        }
    }

    public async Task<OneDriveHistorySyncResult> PullEncryptedHistoryAsync(
        string syncRootPath,
        string passphrase,
        bool rememberVaultOnThisDevice,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(syncRootPath))
        {
            throw new InvalidOperationException("请先选择 OneDrive 本地同步文件夹。");
        }

        EnsureNoConflictFiles(syncRootPath);
        var (vault, vaultKey, createdVault) = await ResolveVaultAsync(
            syncRootPath,
            passphrase,
            createIfMissing: false,
            allowCachedVaultKey: true,
            rememberVaultOnThisDevice,
            cancellationToken).ConfigureAwait(false);
        if (vault is null || vaultKey is null)
        {
            return new OneDriveHistorySyncResult(syncRootPath, string.Empty, false, 0, 0, DateTimeOffset.UtcNow);
        }

        try
        {
            var manifest = await _folder.ReadJsonAsync<OneDriveSyncManifest>(
                syncRootPath,
                OneDriveSyncPaths.Manifest,
                cancellationToken).ConfigureAwait(false);
            if (manifest is null)
            {
                return new OneDriveHistorySyncResult(syncRootPath, vault.KeyId, createdVault, 0, 0, DateTimeOffset.UtcNow);
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

                var document = await _folder.ReadJsonAsync<EncryptedSyncDocument>(
                    syncRootPath,
                    item.Path,
                    cancellationToken).ConfigureAwait(false);
                if (document is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(item.CiphertextSha256)
                    && !string.Equals(item.CiphertextSha256, document.CiphertextSha256, StringComparison.Ordinal))
                {
                    throw new CryptographicException($"Encrypted history manifest hash mismatch for {item.Id}.");
                }

                documents.Add(document);
            }

            var imported = _historySync.ImportEncryptedHistory(documents, vaultKey, vault);
            return new OneDriveHistorySyncResult(syncRootPath, vault.KeyId, createdVault, 0, imported, DateTimeOffset.UtcNow);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(vaultKey);
        }
    }

    private async Task<(EncryptedSyncVault? Vault, byte[]? VaultKey, bool CreatedVault)> ResolveVaultAsync(
        string syncRootPath,
        string passphrase,
        bool createIfMissing,
        bool allowCachedVaultKey,
        bool rememberVaultOnThisDevice,
        CancellationToken cancellationToken)
    {
        var vault = await _folder.ReadJsonAsync<EncryptedSyncVault>(
            syncRootPath,
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
                _vaultCache.WriteVaultKey(syncRootPath, created.Vault.KeyId, created.VaultKey);
            }

            return (created.Vault, created.VaultKey, true);
        }

        if (allowCachedVaultKey)
        {
            var cached = _vaultCache.ReadVaultKey(syncRootPath, vault.KeyId);
            if (cached is not null)
            {
                return (vault, cached, false);
            }
        }

        var vaultKey = _crypto.UnlockVault(vault, passphrase);
        if (rememberVaultOnThisDevice)
        {
            _vaultCache.WriteVaultKey(syncRootPath, vault.KeyId, vaultKey);
        }

        return (vault, vaultKey, false);
    }

    private void EnsureNoConflictFiles(string syncRootPath)
    {
        var conflicts = _folder.FindConflictFiles(syncRootPath);
        if (conflicts.Count == 0)
        {
            return;
        }

        var preview = string.Join("；", conflicts.Select(Path.GetFileName));
        throw new InvalidOperationException($"检测到 OneDrive 冲突副本，请先手动合并后再同步：{preview}");
    }

    private static string ComputeJsonSha256<T>(T value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return Base64Url.Encode(SHA256.HashData(bytes));
    }

    private static string BuildManifestKey(string kind, string id)
    {
        return $"{kind}:{id}";
    }
}

public sealed record OneDriveHistorySyncResult(
    string SyncRootPath,
    string KeyId,
    bool CreatedVault,
    int UploadedHistoryCount,
    int ImportedHistoryCount,
    DateTimeOffset SyncedAt);
