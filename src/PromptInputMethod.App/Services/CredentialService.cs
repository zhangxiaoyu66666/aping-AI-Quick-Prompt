using System.Runtime.InteropServices;
using System.Text;

namespace PromptInputMethod.App.Services;

public sealed class CredentialService
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public string? ReadSecret(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        if (!CredRead(targetName, CredTypeGeneric, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == 0)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public void WriteSecret(string targetName, string secret)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            throw new ArgumentException("凭据名称不能为空。", nameof(targetName));
        }

        var secretBytes = Encoding.Unicode.GetBytes(secret ?? string.Empty);
        var targetNamePtr = Marshal.StringToCoTaskMemUni(targetName);
        var userNamePtr = Marshal.StringToCoTaskMemUni(Environment.UserName);
        var blobPtr = Marshal.AllocCoTaskMem(secretBytes.Length);
        try
        {
            Marshal.Copy(secretBytes, 0, blobPtr, secretBytes.Length);

            var credential = new CREDENTIAL
            {
                Type = CredTypeGeneric,
                TargetName = targetNamePtr,
                CredentialBlobSize = secretBytes.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                UserName = userNamePtr
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"写入 Windows Credential Manager 失败：{Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blobPtr);
            Marshal.FreeCoTaskMem(targetNamePtr);
            Marshal.FreeCoTaskMem(userNamePtr);
        }
    }

    public bool DeleteSecret(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        return CredDelete(targetName, CredTypeGeneric, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CREDENTIAL
    {
        public int Flags;
        public int Type;
        public nint TargetName;
        public nint Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public nint CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out nint credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, int flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(nint buffer);
}
