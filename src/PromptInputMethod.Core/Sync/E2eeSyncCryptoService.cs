using System.Security.Cryptography;
using System.Text;

namespace PromptInputMethod.Core.Sync;

public sealed class E2eeSyncCryptoService
{
    public const string VaultSchema = "aipin.sync.vault.v1";
    public const string HistorySchema = "aipin.history.encrypted.v1";
    public const string VaultKdfName = "PBKDF2-HMAC-SHA256";
    public const string AesGcmAlgorithm = "AES-256-GCM";
    public const int DefaultPbkdf2Iterations = 600_000;

    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    public (EncryptedSyncVault Vault, byte[] VaultKey) CreateVault(
        string passphrase,
        string? keyId = null,
        DateTimeOffset? createdAt = null,
        int iterations = DefaultPbkdf2Iterations)
    {
        ValidatePassphrase(passphrase);
        if (iterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(iterations), "PBKDF2 iterations must be positive.");
        }

        var vaultKey = RandomNumberGenerator.GetBytes(KeySize);
        var dataSalt = RandomNumberGenerator.GetBytes(KeySize);
        var kdfSalt = RandomNumberGenerator.GetBytes(KeySize);
        var wrapNonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[KeySize];
        var tag = new byte[TagSize];
        var resolvedKeyId = string.IsNullOrWhiteSpace(keyId)
            ? DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss")
            : keyId.Trim();
        var kek = DeriveKeyEncryptionKey(passphrase, kdfSalt, iterations);

        try
        {
            using var aes = new AesGcm(kek, TagSize);
            aes.Encrypt(
                wrapNonce,
                vaultKey,
                ciphertext,
                tag,
                Encoding.UTF8.GetBytes(BuildVaultAad(resolvedKeyId)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }

        var vault = new EncryptedSyncVault(
            VaultSchema,
            resolvedKeyId,
            createdAt ?? DateTimeOffset.UtcNow,
            Base64Url.Encode(dataSalt),
            new E2eeVaultKdf(VaultKdfName, iterations, Base64Url.Encode(kdfSalt), KeySize),
            new E2eeVaultWrap(
                AesGcmAlgorithm,
                Base64Url.Encode(wrapNonce),
                Base64Url.Encode(tag),
                Base64Url.Encode(ciphertext)));

        return (vault, vaultKey);
    }

    public byte[] UnlockVault(EncryptedSyncVault vault, string passphrase)
    {
        ValidateVault(vault);
        ValidatePassphrase(passphrase);

        var kdfSalt = Base64Url.Decode(vault.Kdf.Salt);
        var nonce = Base64Url.Decode(vault.Wrap.Nonce);
        var tag = Base64Url.Decode(vault.Wrap.Tag);
        var ciphertext = Base64Url.Decode(vault.Wrap.Ciphertext);
        ValidateExactLength(kdfSalt, KeySize, "vault KDF salt");
        ValidateExactLength(nonce, NonceSize, "vault wrap nonce");
        ValidateExactLength(tag, TagSize, "vault wrap tag");
        ValidateExactLength(ciphertext, KeySize, "wrapped vault key");

        var vaultKey = new byte[KeySize];
        var kek = DeriveKeyEncryptionKey(passphrase, kdfSalt, vault.Kdf.Iterations);

        try
        {
            using var aes = new AesGcm(kek, TagSize);
            aes.Decrypt(
                nonce,
                ciphertext,
                tag,
                vaultKey,
                Encoding.UTF8.GetBytes(BuildVaultAad(vault.KeyId)));
            return vaultKey;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(vaultKey);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    public EncryptedSyncDocument EncryptHistoryJson(
        string id,
        string plaintextJson,
        byte[] vaultKey,
        EncryptedSyncVault vault,
        DateTimeOffset? encryptedAt = null)
    {
        return EncryptJson(HistorySchema, "history", id, plaintextJson, vaultKey, vault, encryptedAt);
    }

    public string DecryptHistoryJson(EncryptedSyncDocument document, byte[] vaultKey, EncryptedSyncVault vault)
    {
        if (!string.Equals(document.Schema, HistorySchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported encrypted history schema: {document.Schema}");
        }

        if (!string.Equals(document.Kind, "history", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported encrypted history kind: {document.Kind}");
        }

        return DecryptJson(document, vaultKey, vault);
    }

    public byte[] DeriveHistoryKey(byte[] vaultKey, EncryptedSyncVault vault)
    {
        ValidateVault(vault);
        ValidateExactLength(vaultKey, KeySize, "vault key");
        var dataSalt = Base64Url.Decode(vault.DataSalt);
        ValidateExactLength(dataSalt, KeySize, "vault data salt");
        return HkdfSha256(vaultKey, dataSalt, "aipin/history/aes-gcm/v1", KeySize);
    }

    private EncryptedSyncDocument EncryptJson(
        string schema,
        string kind,
        string id,
        string plaintextJson,
        byte[] vaultKey,
        EncryptedSyncVault vault,
        DateTimeOffset? encryptedAt)
    {
        ValidateVault(vault);
        ValidateExactLength(vaultKey, KeySize, "vault key");
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Encrypted sync document id is required.", nameof(id));
        }

        var key = DeriveHistoryKey(vaultKey, vault);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plaintext = Encoding.UTF8.GetBytes(plaintextJson ?? string.Empty);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var aadText = BuildDocumentAad(schema, kind, id, vault.KeyId);

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(aadText));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }

        return new EncryptedSyncDocument(
            schema,
            kind,
            id,
            vault.KeyId,
            AesGcmAlgorithm,
            Base64Url.Encode(nonce),
            Base64Url.Encode(tag),
            aadText,
            Base64Url.Encode(ciphertext),
            Base64Url.Encode(SHA256.HashData(ciphertext)),
            encryptedAt ?? DateTimeOffset.UtcNow);
    }

    private string DecryptJson(EncryptedSyncDocument document, byte[] vaultKey, EncryptedSyncVault vault)
    {
        ValidateVault(vault);
        ValidateExactLength(vaultKey, KeySize, "vault key");
        if (!string.Equals(document.Alg, AesGcmAlgorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported encrypted sync algorithm: {document.Alg}");
        }

        if (!string.Equals(document.KeyId, vault.KeyId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Encrypted sync document key id does not match the unlocked vault.");
        }

        var expectedAad = BuildDocumentAad(document.Schema, document.Kind, document.Id, document.KeyId);
        if (!string.Equals(document.Aad, expectedAad, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Encrypted sync document AAD does not match its envelope.");
        }

        var key = DeriveHistoryKey(vaultKey, vault);
        var nonce = Base64Url.Decode(document.Nonce);
        var tag = Base64Url.Decode(document.Tag);
        var ciphertext = Base64Url.Decode(document.Ciphertext);
        var expectedHash = Base64Url.Encode(SHA256.HashData(ciphertext));
        if (!string.Equals(document.CiphertextSha256, expectedHash, StringComparison.Ordinal))
        {
            throw new CryptographicException("Encrypted sync document hash mismatch.");
        }

        ValidateExactLength(nonce, NonceSize, "encrypted document nonce");
        ValidateExactLength(tag, TagSize, "encrypted document tag");
        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(document.Aad));
            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] DeriveKeyEncryptionKey(string passphrase, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            passphrase,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            KeySize);
    }

    private static byte[] HkdfSha256(byte[] inputKeyMaterial, byte[] salt, string info, int length)
    {
        using var extract = new HMACSHA256(salt);
        var prk = extract.ComputeHash(inputKeyMaterial);
        var output = new byte[length];
        var previous = Array.Empty<byte>();
        var offset = 0;
        var counter = 1;

        try
        {
            while (offset < length)
            {
                using var expand = new HMACSHA256(prk);
                var infoBytes = Encoding.UTF8.GetBytes(info);
                var blockInput = new byte[previous.Length + infoBytes.Length + 1];
                Buffer.BlockCopy(previous, 0, blockInput, 0, previous.Length);
                Buffer.BlockCopy(infoBytes, 0, blockInput, previous.Length, infoBytes.Length);
                blockInput[^1] = (byte)counter;
                previous = expand.ComputeHash(blockInput);
                var copy = Math.Min(previous.Length, length - offset);
                Buffer.BlockCopy(previous, 0, output, offset, copy);
                offset += copy;
                counter++;
            }

            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(prk);
            CryptographicOperations.ZeroMemory(previous);
        }
    }

    private static string BuildVaultAad(string keyId)
    {
        return $"{VaultSchema}|{keyId}";
    }

    private static string BuildDocumentAad(string schema, string kind, string id, string keyId)
    {
        return $"{schema}|{kind}|{id}|{keyId}";
    }

    private static void ValidateVault(EncryptedSyncVault vault)
    {
        if (!string.Equals(vault.Schema, VaultSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported sync vault schema: {vault.Schema}");
        }

        if (!string.Equals(vault.Kdf.Name, VaultKdfName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported sync vault KDF: {vault.Kdf.Name}");
        }

        if (!string.Equals(vault.Wrap.Alg, AesGcmAlgorithm, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported sync vault wrap algorithm: {vault.Wrap.Alg}");
        }

        if (vault.Kdf.OutputBytes != KeySize)
        {
            throw new InvalidDataException("Unsupported sync vault key size.");
        }
    }

    private static void ValidatePassphrase(string passphrase)
    {
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("Sync passphrase is required.", nameof(passphrase));
        }
    }

    private static void ValidateExactLength(byte[] bytes, int expectedLength, string label)
    {
        if (bytes.Length != expectedLength)
        {
            throw new InvalidDataException($"{label} must be {expectedLength} bytes.");
        }
    }
}
