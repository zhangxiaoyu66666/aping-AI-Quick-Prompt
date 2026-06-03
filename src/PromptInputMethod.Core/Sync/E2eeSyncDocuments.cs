namespace PromptInputMethod.Core.Sync;

public sealed record EncryptedSyncVault(
    string Schema,
    string KeyId,
    DateTimeOffset CreatedAt,
    string DataSalt,
    E2eeVaultKdf Kdf,
    E2eeVaultWrap Wrap);

public sealed record E2eeVaultKdf(
    string Name,
    int Iterations,
    string Salt,
    int OutputBytes);

public sealed record E2eeVaultWrap(
    string Alg,
    string Nonce,
    string Tag,
    string Ciphertext);

public sealed record EncryptedSyncDocument(
    string Schema,
    string Kind,
    string Id,
    string KeyId,
    string Alg,
    string Nonce,
    string Tag,
    string Aad,
    string Ciphertext,
    string CiphertextSha256,
    DateTimeOffset EncryptedAt);
