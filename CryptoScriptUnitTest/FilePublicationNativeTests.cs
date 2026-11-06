using System.ComponentModel;
using System.Runtime.InteropServices;
using CryptoScript.FileEncryption;

namespace CryptoScriptUnitTest;

// Exercise the real Darwin shim and filesystem, on both Intel and Apple Silicon.
[NonParallelizable]
public class FilePublicationNativeTests
{
    private string directory = null!;

    [SetUp] public void Setup()
    {
        if (Environment.GetEnvironmentVariable("CWE_REQUIRE_MAC_ARM64") == "1")
        {
            Assert.That(OperatingSystem.IsMacOS(), Is.True, "The gate requires a Darwin testhost.");
            Assert.That(RuntimeInformation.ProcessArchitecture, Is.EqualTo(Architecture.Arm64),
                "An x64 testhost under Rosetta does not validate the Apple ARM64 ABI.");
        }
        if (!OperatingSystem.IsMacOS()) Assert.Ignore("Darwin native ABI regression.");
        string root = Path.GetTempPath();
        if (root.StartsWith("/var/", StringComparison.Ordinal)) root = "/private" + root;
        if (root.StartsWith("/tmp/", StringComparison.Ordinal)) root = "/private" + root;
        directory = Path.Combine(root, "CWE-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown] public void Cleanup()
    {
        if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    [Test] public void OpenAtTraversesDirectoriesAndGetPathReturnsBoundDirectory()
    {
        directory = Directory.CreateDirectory(Path.Combine(directory, "nested")).FullName;
        string parent = Path.GetDirectoryName(directory)!;
        try
        {
            using var publication = new FilePublication(directory);
            Assert.That(publication.CurrentDirectory(), Is.EqualTo(directory));
        }
        finally { directory = parent; }
    }

    [Test] public void OpenAtReadsSourceThroughBoundDirectory()
    {
        File.WriteAllBytes(Path.Combine(directory, "source"), [1, 2, 3]);
        using var publication = new FilePublication(directory);
        using var source = publication.OpenSource("source");
        Assert.That(source.ReadByte(), Is.EqualTo(1));
        Assert.That(source.Length, Is.EqualTo(3));
    }

    [Test] public void OpenAtCreatesPrivateFileAndLinkAtPublishesWithoutOverwrite()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        using var publication = new FilePublication(directory);
        using var output = publication.CreateTemporary("temporary");
        Assert.That(File.GetUnixFileMode(Path.Combine(directory, "temporary")),
            Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
        output.Write([17, 18]); output.Flush(true);
        Assert.That(publication.TryPublish(output, "temporary", "published"), Is.True);
        Assert.That(publication.TryPublish(output, "temporary", "published"), Is.False);
        publication.DeleteTemporary("temporary");
        Assert.That(File.Exists(Path.Combine(directory, "temporary")), Is.False);
        Assert.That(File.ReadAllBytes(Path.Combine(directory, "published")), Is.EqualTo(new byte[] { 17, 18 }));
        Assert.That(File.GetUnixFileMode(Path.Combine(directory, "published")),
            Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }

    [Test] public void OpenAtRejectsDirectoryAndSourceSymlinks()
    {
        string actual = Directory.CreateDirectory(Path.Combine(directory, "actual")).FullName;
        Directory.CreateSymbolicLink(Path.Combine(directory, "alias"), actual);
        Assert.Throws<IOException>(() => { using var ignored = new FilePublication(Path.Combine(directory, "alias")); });
        File.WriteAllBytes(Path.Combine(directory, "source"), [99]);
        File.CreateSymbolicLink(Path.Combine(directory, "alias-source"), Path.Combine(directory, "source"));
        using var publication = new FilePublication(directory);
        Assert.Throws<IOException>(() => { using var ignored = publication.OpenSource("alias-source"); });
    }

    [Test] public void OpenAtExclusiveCreatePreservesExistingSymlinkAndTarget()
    {
        File.WriteAllBytes(Path.Combine(directory, "target"), [99]);
        File.CreateSymbolicLink(Path.Combine(directory, "temporary"), Path.Combine(directory, "target"));
        using var publication = new FilePublication(directory);
        Assert.Throws<IOException>(() => { using var ignored = publication.CreateTemporary("temporary"); });
        Assert.That(File.ReadAllBytes(Path.Combine(directory, "target")), Is.EqualTo(new byte[] { 99 }));
        Assert.That(new FileInfo(Path.Combine(directory, "temporary")).LinkTarget, Is.Not.Null);
    }

    [Test] public void NativeFailurePreservesErrnoAndAllowsCleanup()
    {
        using var publication = new FilePublication(directory);
        var error = Assert.Throws<IOException>(() => { using var ignored = publication.OpenSource("missing"); });
        Assert.That(error!.InnerException, Is.TypeOf<Win32Exception>());
        Assert.That(((Win32Exception)error.InnerException!).NativeErrorCode, Is.EqualTo(2)); // ENOENT
        using (publication.CreateTemporary("temporary")) { }
        publication.DeleteTemporary("temporary");
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }

    [Test] public void GetPathTracksRenamedOpenDirectory()
    {
        using var publication = new FilePublication(directory);
        string moved = directory + "-moved";
        Directory.Move(directory, moved); directory = moved;
        Assert.That(publication.CurrentDirectory(), Is.EqualTo(moved));
    }
}
