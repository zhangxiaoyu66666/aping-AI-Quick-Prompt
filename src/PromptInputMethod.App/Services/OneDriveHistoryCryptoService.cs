using System.Text.Json;
using System.Text.Json.Serialization;
using PromptInputMethod.Core.Sync;

namespace PromptInputMethod.App.Services;

public sealed class OneDriveHistoryCryptoService
{
    public const string PlainHistorySchema = "aipin.history.plain.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly E2eeSyncCryptoService _crypto = new();

    public EncryptedSyncDocument EncryptHistory(
        PromptHistoryItem item,
        byte[] vaultKey,
        EncryptedSyncVault vault)
    {
        var document = ToPlainDocument(item);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        return _crypto.EncryptHistoryJson(item.Id, json, vaultKey, vault, item.EffectiveUpdatedAt);
    }

    public PromptHistoryItem DecryptHistory(
        EncryptedSyncDocument document,
        byte[] vaultKey,
        EncryptedSyncVault vault)
    {
        var json = _crypto.DecryptHistoryJson(document, vaultKey, vault);
        var plain = JsonSerializer.Deserialize<OneDrivePlainHistoryDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("Encrypted history payload did not deserialize.");

        if (!string.Equals(plain.Schema, PlainHistorySchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported plain history schema: {plain.Schema}");
        }

        if (!string.Equals(plain.Id, document.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Encrypted history id does not match its plaintext payload.");
        }

        return new PromptHistoryItem(
            plain.Id,
            plain.Title,
            plain.UserRequest,
            plain.ChinesePrompt,
            plain.EnglishPrompt,
            plain.Scene,
            plain.Mode,
            plain.CreatedAt,
            plain.Messages,
            plain.UpdatedAt);
    }

    private static OneDrivePlainHistoryDocument ToPlainDocument(PromptHistoryItem item)
    {
        return new OneDrivePlainHistoryDocument(
            PlainHistorySchema,
            item.Id,
            item.Title,
            item.UserRequest,
            item.ChinesePrompt,
            item.EnglishPrompt,
            item.Scene,
            item.Mode,
            item.CreatedAt,
            item.EffectiveUpdatedAt,
            item.Messages?.ToArray() ?? []);
    }
}

public sealed record OneDrivePlainHistoryDocument(
    string Schema,
    string Id,
    string Title,
    string UserRequest,
    string ChinesePrompt,
    string EnglishPrompt,
    string Scene,
    string Mode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PromptConversationMessage> Messages);
