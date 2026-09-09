using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Application.Abstractions;

namespace Infrastructure;

public sealed class WindowsCredentialConnectionSecretStore : IConnectionSecretStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 2560;

    public Task<string?> GetPasswordAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadCredential(GetCredentialTarget(profileId)));
    }

    public Task SavePasswordAsync(Guid profileId, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WriteCredential(GetCredentialTarget(profileId), password);
        return Task.CompletedTask;
    }

    public Task DeletePasswordAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteCredential(GetCredentialTarget(profileId));
        return Task.CompletedTask;
    }

    private static string GetCredentialTarget(Guid profileId) => $"Comparador:Connection:{profileId:N}";

    private static string? ReadCredential(string targetName)
    {
        if (!CredRead(targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode == ErrorNotFound)
                return null;

            throw new InvalidOperationException($"No se pudo leer la contraseña guardada (código {errorCode}).");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            return credential.CredentialBlobSize == 0
                ? string.Empty
                : Marshal.PtrToStringUni(credential.CredentialBlob, checked((int)credential.CredentialBlobSize / sizeof(char)));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    private static void WriteCredential(string targetName, string password)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(password ?? string.Empty);
        if (passwordBytes.Length > MaximumCredentialBlobBytes)
            throw new InvalidOperationException("La contraseña excede el tamaño admitido por el Administrador de credenciales de Windows.");

        var passwordPointer = Marshal.AllocCoTaskMem(passwordBytes.Length);
        try
        {
            if (passwordBytes.Length > 0)
                Marshal.Copy(passwordBytes, 0, passwordPointer, passwordBytes.Length);

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = targetName,
                CredentialBlobSize = (uint)passwordBytes.Length,
                CredentialBlob = passwordPointer,
                Persist = CredentialPersistLocalMachine
            };

            if (!CredWrite(ref credential, 0))
                throw new InvalidOperationException($"No se pudo guardar la contraseña de forma segura (código {Marshal.GetLastWin32Error()}).");
        }
        finally
        {
            Marshal.FreeCoTaskMem(passwordPointer);
        }
    }

    private static void DeleteCredential(string targetName)
    {
        if (!CredDelete(targetName, CredentialTypeGeneric, 0) && Marshal.GetLastWin32Error() != ErrorNotFound)
            throw new InvalidOperationException($"No se pudo eliminar la contraseña guardada (código {Marshal.GetLastWin32Error()}).");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}
