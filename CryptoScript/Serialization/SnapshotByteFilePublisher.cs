using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using CryptoScript.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CryptoScript.Serialization;

internal sealed record PreparedSnapshotDestination(string Path, string Directory);

internal static class SnapshotByteFilePublisher
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static PreparedSnapshotDestination Prepare(PathVariableDeclaration path, VariableSnapshotWriteMode mode)
    {
        PreparedSnapshotDestination? result = null;
        Guard(() =>
        {
            string destination = ValidatePath(path);
            ValidateTarget(destination, mode);
            string directory = Path.GetDirectoryName(destination)!;
            if ((File.GetAttributes(directory) & FileAttributes.Directory) == 0)
                throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidPath);
            result = new(destination, directory);
        }, writing: true);
        return result!;
    }

    internal static void Write(PreparedSnapshotDestination destination, ReadOnlyMemory<byte> bytes,
        int maximum, VariableSnapshotWriteMode mode, CancellationToken token = default,
        Action<string, string, VariableSnapshotWriteMode>? publish = null, Action? afterFlush = null)
    {
        if (bytes.Length > maximum)
            throw new VariableSnapshotFileException(VariableSnapshotFileError.SizeLimitExceeded);
        Guard(() =>
        {
            token.ThrowIfCancellationRequested();
            string temporaryPath = Path.Combine(destination.Directory, $".cryptoscript-{Guid.NewGuid():N}.tmp");
            bool ownsTemporary = false;
            try
            {
                using (var stream = CreatePrivateFile(temporaryPath))
                {
                    ownsTemporary = true;
                    if (OperatingSystem.IsMacOS()) SnapshotFilePermissions.RemoveInheritedMacAcl(stream);
                    stream.Write(bytes.Span);
                    stream.Flush(flushToDisk: true);
                }
                afterFlush?.Invoke();
                token.ThrowIfCancellationRequested();
                ValidateTarget(destination.Path, mode);
                token.ThrowIfCancellationRequested();
                (publish ?? SnapshotFilePublication.Publish)(temporaryPath, destination.Path, mode);
                // Successful publication wins any later cancellation.
            }
            finally
            {
                if (ownsTemporary)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException) { }
                }
            }
        }, writing: true);
    }

    internal static string ValidatePath(PathVariableDeclaration path)
    {
        string? value = path?.Value;
        if (string.IsNullOrWhiteSpace(value) || value.IndexOf('\0') >= 0 ||
            Path.EndsInDirectorySeparator(value) || Path.GetFileName(value) is "." or "..")
            throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidPath);
        // Fail on unpaired surrogates instead of allowing a native UTF-8 path substitution.
        try { _ = Utf8.GetByteCount(value); }
        catch (EncoderFallbackException)
        {
            throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidPath);
        }
        if (OperatingSystem.IsWindows())
        {
            string windows = value.Replace('/', '\\');
            if (windows.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                windows.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                Path.IsPathRooted(value) && !Path.IsPathFullyQualified(value))
                throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidPath);
            string rest = windows[Path.GetPathRoot(windows)!.Length..];
            foreach (string part in rest.Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part is "." or "..") continue;
                string stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
                if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.EndsWith(' ') ||
                    part.EndsWith('.') || stem is "CON" or "PRN" or "AUX" or "NUL" ||
                    stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) &&
                    "123456789¹²³".Contains(stem[3]))
                    throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidPath);
            }
        }
        return Path.GetFullPath(value);
    }

    internal static void ValidateTarget(string path, VariableSnapshotWriteMode? mode)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        // ReparsePoint also covers dangling symlinks and Windows junctions.
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidPath);
        if (mode == VariableSnapshotWriteMode.CreateNew)
            throw new VariableSnapshotFileException(VariableSnapshotFileError.AlreadyExists);
    }

    internal static FileStream CreatePrivateFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var security = new FileSecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(identity.User!,
                FileSystemRights.FullControl, AccessControlType.Allow));
            return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.Write,
                FileShare.None, 4096, FileOptions.None, security);
        }
        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
    }

    internal static void Guard(Action action, bool writing)
    {
        try { action(); }
        catch (VariableSnapshotFileException) { throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            SecurityException or ArgumentException or NotSupportedException)
        {
            throw new VariableSnapshotFileException(error switch
            {
                PathTooLongException or ArgumentException or NotSupportedException => VariableSnapshotFileError.InvalidPath,
                FileNotFoundException or DirectoryNotFoundException => VariableSnapshotFileError.NotFound,
                UnauthorizedAccessException or SecurityException => VariableSnapshotFileError.AccessDenied,
                _ => writing ? VariableSnapshotFileError.WriteError : VariableSnapshotFileError.ReadError
            });
        }
    }
}
