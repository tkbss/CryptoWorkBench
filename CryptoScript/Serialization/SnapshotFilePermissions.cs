using System.Runtime.InteropServices;

namespace CryptoScript.Serialization;

internal static class SnapshotFilePermissions
{
    internal static void RemoveInheritedMacAcl(FileStream stream)
    {
        // Darwin extended ACLs can grant access beyond 0600. Clear them through the
        // already-open descriptor while the file is still empty, before writing secrets.
        IntPtr acl = AclInit(0);
        if (acl == IntPtr.Zero)
            throw new VariableSnapshotFileException(VariableSnapshotFileError.WriteError);
        try
        {
            if (AclSetFd(stream.SafeFileHandle.DangerousGetHandle().ToInt32(), acl) != 0)
                throw new VariableSnapshotFileException(Marshal.GetLastPInvokeError() is 1 or 13
                    ? VariableSnapshotFileError.AccessDenied : VariableSnapshotFileError.WriteError);
        }
        finally
        {
            _ = AclFree(acl);
            GC.KeepAlive(stream); // The descriptor must remain alive through acl_set_fd.
        }
    }

    [DllImport("libc", EntryPoint = "acl_init", SetLastError = true)]
    private static extern IntPtr AclInit(int count);

    [DllImport("libc", EntryPoint = "acl_set_fd", SetLastError = true)]
    private static extern int AclSetFd(int fd, IntPtr acl);

    [DllImport("libc", EntryPoint = "acl_free")]
    private static extern int AclFree(IntPtr acl);
}
