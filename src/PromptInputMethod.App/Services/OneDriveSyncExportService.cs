using PromptInputMethod.Core.Sync;

namespace PromptInputMethod.App.Services;

public sealed class OneDriveSyncExportService
{
    private readonly PromptHistoryService _historyService;
    private readonly OneDriveHistoryCryptoService _historyCrypto;

    public OneDriveSyncExportService()
        : this(new PromptHistoryService(), new OneDriveHistoryCryptoService())
    {
    }

    public OneDriveSyncExportService(PromptHistoryService historyService, OneDriveHistoryCryptoService historyCrypto)
    {
        _historyService = historyService;
        _historyCrypto = historyCrypto;
    }

    public IReadOnlyList<EncryptedSyncDocument> ExportEncryptedHistory(
        byte[] vaultKey,
        EncryptedSyncVault vault,
        int limit = int.MaxValue)
    {
        return _historyService.LoadForSync(limit)
            .OrderBy(item => item.EffectiveUpdatedAt)
            .Select(item => _historyCrypto.EncryptHistory(item, vaultKey, vault))
            .ToArray();
    }

    public int ImportEncryptedHistory(
        IEnumerable<EncryptedSyncDocument> documents,
        byte[] vaultKey,
        EncryptedSyncVault vault)
    {
        var items = documents
            .Select(document => _historyCrypto.DecryptHistory(document, vaultKey, vault))
            .ToArray();
        return _historyService.ImportForSync(items);
    }
}
