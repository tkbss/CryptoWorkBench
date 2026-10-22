using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using CryptoScript.Serialization;
using Microsoft.Win32.SafeHandles;

namespace CryptoScript.FileEncryption;

/// <summary>Bind directory traversal before opening files. Never follow a source symlink.
/// Unix namespace operations are relative to the bound directory; Windows directory leases deny rename.</summary>
internal sealed class FilePublication : IDisposable
{
    private readonly List<SafeFileHandle> directories = new();
    private readonly string directory;
    private int DirectoryFd => directories[^1].DangerousGetHandle().ToInt32();
    private readonly bool windows = OperatingSystem.IsWindows();
    internal FilePublication(string directory)
    {
        this.directory = directory;
        try
        {
            if (windows)
            {
                string current = Path.GetPathRoot(directory)!;
                OpenWindowsDirectory(current);
                foreach (string part in directory[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
                { current = Path.Combine(current, part); OpenWindowsDirectory(current); }
            }
            else
            {
                if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
                    throw new PlatformNotSupportedException("CWE publication requires Windows, Linux or macOS.");
                directories.Add(Handle(OpenAt(-100, "/", DirectoryFlags, 0)));
                foreach (string part in directory.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    directories.Add(Handle(OpenAt(DirectoryFd, part, DirectoryFlags, 0)));
            }
        }
        catch { Dispose(); throw; }
    }
    private int NoFollow => OperatingSystem.IsMacOS() ? 0x100 : 0x20000;
    private int CloseOnExec => OperatingSystem.IsMacOS() ? 0x1000000 : 0x80000;
    private int DirectoryFlags => NoFollow | CloseOnExec | (OperatingSystem.IsMacOS() ? 0x100000 : 0x10000);

    private void OpenWindowsDirectory(string path)
    {
        var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw NativeError(); }
        directories.Add(handle);
        if ((File.GetAttributes(handle) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != FileAttributes.Directory)
            throw new IOException("CWE directory must not be a reparse point.");
    }

    internal FileStream OpenSource(string name)
    {
        SafeFileHandle handle = windows
            ? CreateFile(Path.Combine(directory, name), 0x80000000, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero)
            : Handle(OpenAt(DirectoryFd, name, NoFollow | CloseOnExec | (OperatingSystem.IsMacOS() ? 4 : 0x800), 0));
        try
        {
            if (handle.IsInvalid) throw NativeError();
            if (windows)
            {
                if (GetFileType(handle) != 1 ||
                    (File.GetAttributes(handle) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    throw new IOException("CWE source must be a regular file, not a reparse point.");
            }
            else
            {
                // fstat's mode offset is ABI-specific; supported CI ABIs are explicit here.
                byte[] stat = new byte[512];
                if (FStat(handle, stat) != 0) throw NativeError();
                int modeOffset = OperatingSystem.IsMacOS() ? 4 :
                    RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 24 :
                    RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? 16 :
                    throw new PlatformNotSupportedException("Unsupported Unix stat ABI.");
                uint mode = OperatingSystem.IsMacOS() ? BitConverter.ToUInt16(stat, modeOffset) : BitConverter.ToUInt32(stat, modeOffset);
                if ((mode & 0xf000) != 0x8000) throw new IOException("CWE source must be a regular file.");
            }
            return new FileStream(handle, FileAccess.Read, 65536);
        }
        catch { handle.Dispose(); throw; }
    }

    internal FileStream CreateTemporary(string name)
    {
        if (windows) return SnapshotByteFilePublisher.CreatePrivateFile(Path.Combine(directory, name), forHandlePublication: true);
        int flags = 1 | NoFollow | CloseOnExec | (OperatingSystem.IsMacOS() ? 0xa00 : 0xc0);
        var handle = Handle(OpenAt(DirectoryFd, name, flags, 0x180)); // O_CREAT|O_EXCL, 0600
        try
        {
            var stream = new FileStream(handle, FileAccess.Write, 65536);
            try
            {
                if (OperatingSystem.IsMacOS()) SnapshotFilePermissions.RemoveInheritedMacAcl(stream);
                return stream;
            }
            catch { stream.Dispose(); DeleteTemporary(name); throw; }
        }
        catch { handle.Dispose(); throw; }
    }

    internal string CurrentDirectory()
    {
        if (windows) return directory; // Ancestor leases deny rename until disposal.
        if (OperatingSystem.IsLinux())
            return new DirectoryInfo("/proc/self/fd/" + DirectoryFd).ResolveLinkTarget(true)?.FullName
                ?? throw new IOException("CWE directory is no longer reachable.");
        byte[] path = new byte[4096];
        if (GetPath(DirectoryFd, path, path.Length) != 0) throw NativeError(); // Darwin F_GETPATH
        int end = Array.IndexOf(path, (byte)0);
        if (end < 1) throw new IOException("CWE directory is no longer reachable.");
        return new UTF8Encoding(false, true).GetString(path, 0, end);
    }

    internal bool TryPublish(FileStream output, string temporary, string name)
    {
        if (windows)
        {
            // Rename the open, privately created object itself; no source-name lookup.
            // FILE_RENAME_INFO: BOOLEAN, alignment, HANDLE, DWORD, WCHAR[].
            byte[] path = Encoding.Unicode.GetBytes(Path.Combine(directory, name));
            int rootOffset = IntPtr.Size, lengthOffset = rootOffset + IntPtr.Size;
            byte[] info = new byte[lengthOffset + 4 + path.Length + 2];
            BitConverter.GetBytes(path.Length).CopyTo(info, lengthOffset);
            path.CopyTo(info, lengthOffset + 4);
            if (SetFileInformationByHandle(output.SafeFileHandle, 3, info, (uint)info.Length)) return true;
            int error = Marshal.GetLastPInvokeError();
            if (error is 80 or 183) return false;
            throw NativeError();
        }
        if (OperatingSystem.IsLinux())
        {
            // Unlike AT_EMPTY_PATH this /proc form does not require CAP_DAC_READ_SEARCH.
            // No copy/name-based fallback when /proc is unavailable.
            int result = LinkAt(-100, "/proc/self/fd/" + output.SafeFileHandle.DangerousGetHandle().ToInt32(),
                DirectoryFd, name, 0x400); // AT_SYMLINK_FOLLOW, source is our open descriptor
            GC.KeepAlive(output);
            if (result == 0) return true;
        }
        else
        {
            // Darwin has no link-by-fd. Require a trusted namespace (the S02 boundary), and check
            // the still-open source identity. Same-user hostile code is outside this boundary.
            byte[] opened = new byte[512], named = new byte[512];
            if (FStat(output.SafeFileHandle, opened) != 0 || FStatAt(DirectoryFd, temporary, named, 0x20) != 0)
                throw NativeError();
            if (!opened.AsSpan(0, 4).SequenceEqual(named.AsSpan(0, 4)) ||
                !opened.AsSpan(8, 8).SequenceEqual(named.AsSpan(8, 8)) || (BitConverter.ToUInt16(named, 4) & 0xf000) != 0x8000)
                throw new IOException("CWE temporary file identity changed.");
            if (LinkAt(DirectoryFd, temporary, DirectoryFd, name, 0) == 0) return true;
        }
        if (Marshal.GetLastPInvokeError() == 17) return false;
        throw NativeError();
    }

    internal void DeleteTemporary(string name)
    {
        try
        {
            if (windows) File.Delete(Path.Combine(directory, name));
            else _ = UnlinkAt(DirectoryFd, name, 0);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    public void Dispose() { foreach (var handle in directories.AsEnumerable().Reverse()) handle.Dispose(); }
    private static IOException NativeError() => new("CWE filesystem operation failed.", new Win32Exception(Marshal.GetLastPInvokeError()));
    private static SafeFileHandle Handle(int fd) => fd < 0 ? throw NativeError() : new SafeFileHandle((IntPtr)fd, true);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint GetFileType(SafeFileHandle handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, byte[] info, uint size);
    [DllImport("libc", EntryPoint = "openat", SetLastError = true)]
    private static extern int OpenAtLibc(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);
    // Darwin ARM64 passes variadic arguments on the stack. Let the C compiler call
    // openat/fcntl with their real prototypes; never emulate varargs with register padding.
    private static int OpenAt(int fd, string path, int flags, uint mode) => OperatingSystem.IsMacOS()
        ? OpenAtMac(fd, path, flags, mode) : OpenAtLibc(fd, path, flags, mode);
    [DllImport("libCryptoScript.FileSystem.dylib", EntryPoint = "cwe_openat", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    private static extern int OpenAtMac(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mode);
    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStatNative(SafeFileHandle fd, [Out] byte[] stat);
    [DllImport("libc", EntryPoint = "fstatat", SetLastError = true)]
    private static extern int FStatAtNative(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [Out] byte[] stat, int flags);
    private static bool MacIntel => OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.X64;
    private static int FStat(SafeFileHandle fd, byte[] stat) => MacIntel ? FStatMacIntel(fd, stat) : FStatNative(fd, stat);
    private static int FStatAt(int fd, string name, byte[] stat, int flags) => MacIntel
        ? FStatAtMacIntel(fd, name, stat, flags) : FStatAtNative(fd, name, stat, flags);
    [DllImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static extern int FStatMacIntel(SafeFileHandle fd, [Out] byte[] stat);
    [DllImport("libc", EntryPoint = "fstatat$INODE64", SetLastError = true)]
    private static extern int FStatAtMacIntel(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [Out] byte[] stat, int flags);
    [DllImport("libCryptoScript.FileSystem.dylib", EntryPoint = "cwe_getpath", SetLastError = true, CallingConvention = CallingConvention.Cdecl)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    private static extern int GetPath(int fd, [Out] byte[] path, int capacity);
    [DllImport("libc", EntryPoint = "linkat", SetLastError = true)]
    private static extern int LinkAt(int from, [MarshalAs(UnmanagedType.LPUTF8Str)] string source, int to,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destination, int flags);
    [DllImport("libc", EntryPoint = "unlinkat", SetLastError = true)]
    private static extern int UnlinkAt(int fd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
}
