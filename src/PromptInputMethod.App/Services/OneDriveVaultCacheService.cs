using PromptInputMethod.Core.Sync;

namespace PromptInputMethod.App.Services;

public sealed class OneDriveVaultCacheService
{
    private readonly CredentialService _credentials;

    public OneDriveVaultCacheService()
        : this(new CredentialService())
    {
    }

    public OneDriveVaultCacheService(CredentialService credentials)
    {
        _credentials = credentials;
    }

    public byte[]? ReadVaultKey(string accountScope, string keyId)
    {
        var value = _credentials.ReadSecret(BuildTargetName(accountScope, keyId));
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var key = Base64Url.Decode(value);
            return key.Length == 32 ? key : null;
        }
        catch
        {
            return null;
        }
    }

    public void WriteVaultKey(string accountScope, string keyId, byte[] vaultKey)
    {
        if (vaultKey.Length != 32)
        {
            throw new ArgumentException("OneDrive vault key must be 32 bytes.", nameof(vaultKey));
        }

        _credentials.WriteSecret(BuildTargetName(accountScope, keyId), Base64Url.Encode(vaultKey));
    }

    public bool DeleteVaultKey(string accountScope, string keyId)
    {
        return _credentials.DeleteSecret(BuildTargetName(accountScope, keyId));
    }

    private static string BuildTargetName(string accountScope, string keyId)
    {
        return $"PromptInputMethod/OneDriveVaultKey/{Sanitize(accountScope)}/{Sanitize(keyId)}";
    }

    private static string Sanitize(string value)
    {
        var sanitized = string.Concat((value ?? string.Empty)
            .Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.' or '@' ? character : '_'));
        return string.IsNullOrWhiteSpace(sanitized) ? "default" : sanitized;
    }
}
