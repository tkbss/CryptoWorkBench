using System.Runtime.InteropServices;

namespace CryptoScript.Serialization;

/// <summary>Same-directory namespace operations only; never falls back to copying.</summary>
internal static class SnapshotFilePublication
{
    internal static void Publish(string temporaryPath, string destination, VariableSnapshotWriteMode mode)
    {
        if (OperatingSystem.IsWindows())
        {
            // Deliberately omit MOVEFILE_COPY_ALLOWED. Keep the temporary file's private DACL.
            if (!MoveFileEx(temporaryPath, destination,
                    mode == VariableSnapshotWriteMode.ReplaceExisting ? 1u : 0u))
                throw WindowsError(Marshal.GetLastPInvokeError());
        }
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // link() atomically fails with EEXIST, even for a dangling destination symlink.
            // rename() replaces the directory entry, not the object behind a destination link.
            int result = mode == VariableSnapshotWriteMode.CreateNew
                ? Link(temporaryPath, destination)
                : Rename(temporaryPath, destination);
            if (result != 0) throw UnixError(Marshal.GetLastPInvokeError());
            // For link(), the caller's finally removes the temporary name. A failure there
            // must not turn an already successful publication into a reported failure.
        }
        else
        {
            throw new VariableSnapshotFileException(VariableSnapshotFileError.WriteError);
        }
    }

    private static VariableSnapshotFileException WindowsError(int code) => new(code switch
    {
        80 or 183 => VariableSnapshotFileError.AlreadyExists,
        2 or 3 => VariableSnapshotFileError.NotFound,
        5 or 32 or 33 => VariableSnapshotFileError.AccessDenied,
        123 or 206 or 267 => VariableSnapshotFileError.InvalidPath,
        _ => VariableSnapshotFileError.WriteError
    });

    private static VariableSnapshotFileException UnixError(int code) => new(code switch
    {
        17 => VariableSnapshotFileError.AlreadyExists,
        2 => VariableSnapshotFileError.NotFound,
        1 or 13 or 30 => VariableSnapshotFileError.AccessDenied,
        20 or 22 => VariableSnapshotFileError.InvalidPath,
        _ => VariableSnapshotFileError.WriteError
    });

    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingName, string newName, uint flags);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link([MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);

    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename([MarshalAs(UnmanagedType.LPUTF8Str)] string source,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);
}
