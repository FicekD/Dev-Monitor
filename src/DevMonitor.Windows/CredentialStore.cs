using System.ComponentModel;
using System.Runtime.InteropServices;
using DevMonitor.Core;

namespace DevMonitor.Windows;

public sealed class CredentialStore : ISecretStore
{
    const uint GenericType = 1;
    const uint PersistLocalMachine = 2;
    const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential)]
    struct Credential
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll")]
    static extern void CredFree(IntPtr buffer);

    public string? Get(string name)
    {
        if (!CredRead(name, GenericType, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            return error == ErrorNotFound ? null : throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / sizeof(char));
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public void Set(string name, string secret)
    {
        var target = Marshal.StringToHGlobalUni(name);
        var blob = Marshal.StringToHGlobalUni(secret);
        try
        {
            var credential = new Credential
            {
                Type = GenericType,
                TargetName = target,
                CredentialBlob = blob,
                CredentialBlobSize = (uint)(secret.Length * sizeof(char)),
                Persist = PersistLocalMachine,
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(blob);
            Marshal.FreeHGlobal(target);
        }
    }

    public bool Delete(string name)
    {
        if (CredDelete(name, GenericType, 0))
            return true;
        var error = Marshal.GetLastWin32Error();
        return error == ErrorNotFound ? false : throw new Win32Exception(error);
    }
}
