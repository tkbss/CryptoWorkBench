using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using CryptoScript;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.SecretSharing;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class VariableSnapshotFileStoreTests
{
    private string directory = null!;
    private readonly VariableSnapshotFileStore store = new();
    private readonly VariableSnapshotJsonSerializer serializer = new();
    private VariableDeclaration[] globals = null!;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "CryptoScript-S02-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        globals = VariableDictionary.Instance().GetVariables().ToArray();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            Assert.That(VariableDictionary.Instance().GetVariables().ToArray(), Is.EqualTo(globals));
        }
        finally
        {
            // Only this fixture's unique root; .NET does not recurse through directory links.
            Directory.Delete(directory, recursive: true);
        }
    }

    private PathVariableDeclaration Target(string name = "snapshot.json") =>
        new() { Id = "destination", Value = Path.Combine(directory, name) };

    private static VariableDeclaration[] Data(string value = "Grüezi 世界 🔐") =>
        [new StringVariableDeclaration { Id = "data", Value = value }];

    private static void Error(VariableSnapshotFileError expected, Action action)
    {
        var error = Assert.Throws<VariableSnapshotFileException>(action)!;
        Assert.That(error.Error, Is.EqualTo(expected));
        Assert.That(error.Message, Is.EqualTo($"Snapshot file operation failed: {expected}."));
        Assert.That(error.InnerException, Is.Null);
    }

    private void NoTemporaryFiles() =>
        Assert.That(Directory.GetFiles(directory, ".cryptoscript-*.tmp", SearchOption.AllDirectories), Is.Empty);

    private static ParameterVariableDeclaration Parameter(string id, BlockHeaderVariableDeclaration header)
    {
        var parameter = new ParameterVariableDeclaration { Mechanism = "WRAP-AES-TR31" };
        parameter.SetParameter("#BLKH", header.Id);
        var result = ParameterVariableDeclaration.FromSnapshot(parameter.Mechanism,
            parameter.GetParameters(), parameter.ExplicitParameterNames, parameter.Value, header);
        result.Id = id;
        return result;
    }

    private static BlockHeaderVariableDeclaration Header() => new()
    {
        Id = "header",
        Definition = Tr31HeaderDefinition.Create(new Dictionary<string, string>
        {
            ["KBVID"] = "D", ["KEYU"] = "D0", ["ALGO"] = "A", ["MODEU"] = "B", ["EXP"] = "E"
        })
    };

    [Test]
    public void AllSixTypesRoundTripWithExactS01BytesAndNoMutation()
    {
        var header = Header();
        VariableDeclaration[] source =
        [
            Data()[0],
            new KeyVariableDeclaration
            {
                Id = "key", ValueFormat = FormatConversions.HEX,
                Value = "0x(000102030405060708090a0b0c0d0e0f)",
                KeySizeInBits = new KeySize(128), KeyType = KeyType.Secret(KeyAlgorithm.Aes)
            },
            Parameter("paramdata", header), new PathVariableDeclaration { Id = "path", Value = "/ä/世界" },
            header,
            new SecretVariableDeclaration(new(new(SecretDescriptor.Split, 2, 2, 8),
                [new(1, new byte[] { 42 })])) { Id = "secret" }
        ];
        string before = serializer.Serialize(source);
        var target = Target("Schnappschuss-世界-🔐.json");
        store.Write(target, source);
        var bytes = File.ReadAllBytes(target.Value);
        Assert.That(bytes, Is.EqualTo(new UTF8Encoding(false, true).GetBytes(before)));
        var restored = store.Read(target);
        Assert.That(restored, Has.Count.EqualTo(6));
        for (int i = 0; i < source.Length; i++)
        {
            Assert.That(restored[i], Is.Not.SameAs(source[i]));
            Assert.That(restored[i].GetType(), Is.EqualTo(source[i].GetType()));
        }
        Assert.That(serializer.Serialize(restored), Is.EqualTo(before));
        Assert.That(serializer.Serialize(source), Is.EqualTo(before));
        Assert.That(((ParameterVariableDeclaration)restored[2]).HeaderReference, Is.SameAs(restored[4]));
        Assert.That(target.Value, Is.EqualTo(Path.Combine(directory, "Schnappschuss-世界-🔐.json")));
        NoTemporaryFiles();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SharedHeaderIdentitySurvivesFileRoundTrip(bool includeHeader)
    {
        var header = Header();
        VariableDeclaration[] source = includeHeader
            ? [Parameter("first", header), Parameter("second", header), header]
            : [Parameter("first", header), Parameter("second", header)];
        store.Write(Target(), source);
        var restored = store.Read(Target());
        var first = (ParameterVariableDeclaration)restored[0];
        var second = (ParameterVariableDeclaration)restored[1];
        Assert.That(first.HeaderReference, Is.SameAs(second.HeaderReference));
        Assert.That(first.HeaderReference, Is.Not.SameAs(header));
        if (includeHeader) Assert.That(first.HeaderReference, Is.SameAs(restored[2]));
    }

    [Test]
    public void ReadsS01FileAndOptionalUtf8Bom()
    {
        var json = serializer.Serialize(Data());
        foreach (bool bom in new[] { false, true })
        {
            File.WriteAllText(Target().Value, json, new UTF8Encoding(bom, true));
            Assert.That(serializer.Serialize(store.Read(Target())), Is.EqualTo(json));
        }
    }

    [Test]
    public void CreateNewPreservesExistingBytesAndExplicitReplaceChangesThem()
    {
        var target = Target();
        File.WriteAllText(target.Value, "original");
        Error(VariableSnapshotFileError.AlreadyExists, () => store.Write(target, Data()));
        Assert.That(File.ReadAllText(target.Value), Is.EqualTo("original"));
        store.Write(target, Data(), VariableSnapshotWriteMode.ReplaceExisting);
        Assert.That(store.Read(target)[0].Value, Is.EqualTo(Data()[0].Value));
        store.Write(Target("new.json"), [], VariableSnapshotWriteMode.ReplaceExisting);
        Assert.That(store.Read(Target("new.json")), Is.Empty);
        NoTemporaryFiles();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SerializationFailureNeverTouchesDestination(bool existing)
    {
        var target = Target();
        if (existing) File.WriteAllText(target.Value, "original");
        var invalid = Data();
        invalid[0].Id = "not a valid identifier";
        string originalValue = invalid[0].Value;
        Error(VariableSnapshotFileError.InvalidSnapshot,
            () => store.Write(target, invalid, VariableSnapshotWriteMode.ReplaceExisting));
        Assert.That(File.Exists(target.Value), Is.EqualTo(existing));
        if (existing) Assert.That(File.ReadAllText(target.Value), Is.EqualTo("original"));
        Assert.That(invalid[0].Value, Is.EqualTo(originalValue));
        NoTemporaryFiles();
    }

    [Test]
    public void NullDuplicateRootsAndInvalidArgumentsDoNotCreateFiles()
    {
        Error(VariableSnapshotFileError.InvalidArgument, () => store.Write(Target(), null!));
        Error(VariableSnapshotFileError.InvalidArgument,
            () => store.Write(Target(), Data(), (VariableSnapshotWriteMode)99));
        Error(VariableSnapshotFileError.InvalidSnapshot, () => store.Write(Target(), [null!]));
        var data = Data()[0];
        Error(VariableSnapshotFileError.InvalidSnapshot, () => store.Write(Target(), [data, data]));
        Error(VariableSnapshotFileError.InvalidArgument, () => new VariableSnapshotFileStore(0));
        Error(VariableSnapshotFileError.InvalidArgument, () => new VariableSnapshotFileStore(-1));
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("\0")]
    public void InvalidPathsAreControlled(string value)
    {
        var target = new PathVariableDeclaration { Value = value };
        Error(VariableSnapshotFileError.InvalidPath, () => store.Write(target, Data()));
        Error(VariableSnapshotFileError.InvalidPath, () => store.Read(target));
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }

    [Test]
    public void UnpairedSurrogateInPathIsRejectedBeforeNativeMarshalling()
    {
        // Construct at runtime: attribute strings can replace invalid Unicode in metadata.
        var target = Target(new string((char)0xd800, 1));
        Error(VariableSnapshotFileError.InvalidPath, () => store.Write(target, Data()));
        Error(VariableSnapshotFileError.InvalidPath, () => store.Read(target));
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }

    [Test]
    public void NullPathsDirectoriesAndTrailingSeparatorsAreRejected()
    {
        foreach (var target in new PathVariableDeclaration?[]
        {
            null, new() { Value = null! }, new() { Value = directory },
            new() { Value = directory + Path.DirectorySeparatorChar }, Target("."), Target("..")
        })
        {
            Error(VariableSnapshotFileError.InvalidPath, () => store.Write(target!, Data()));
            Error(VariableSnapshotFileError.InvalidPath, () => store.Read(target!));
        }
    }

    [Test]
    public void MissingFileAndDirectoryAreControlledWithoutCreatingDirectories()
    {
        Error(VariableSnapshotFileError.NotFound, () => store.Read(Target()));
        var target = Target(Path.Combine("missing", "snapshot.json"));
        Error(VariableSnapshotFileError.NotFound, () => store.Read(target));
        Error(VariableSnapshotFileError.NotFound, () => store.Write(target, Data()));
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }

    [Test]
    public void RelativePathsDotSegmentsSpacesAndPlatformSeparators()
    {
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        var target = Target(Path.Combine("nested", "..", "with spaces.json"));
        target.Value = Path.GetRelativePath(Environment.CurrentDirectory, target.Value);
        store.Write(target, Data());
        Assert.That(store.Read(Target("with spaces.json"))[0].Value, Is.EqualTo(Data()[0].Value));
        if (OperatingSystem.IsWindows())
        {
            var forwardSlashes = Target("forward.json");
            forwardSlashes.Value = forwardSlashes.Value.Replace('\\', '/');
            store.Write(forwardSlashes, Data());
            Assert.That(store.Read(forwardSlashes), Has.Count.EqualTo(1));
        }
        else
        {
            // Colon and backslash are ordinary filename characters on Linux/macOS.
            var unix = Target(@"unix:back\slash.json");
            store.Write(unix, Data());
            Assert.That(store.Read(unix), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void WindowsDeviceDriveRelativeAndAlternateStreamPathsAreRejected()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows path namespace rules.");
        foreach (var value in new[]
        {
            "C:relative.json", @"\root-relative.json", @"\\?\C:\file", @"\\.\NUL",
            Target("NUL.json").Value, Target("COM1").Value, Target("LPT¹.txt").Value,
            Target("data.json:stream").Value, Target("trailing.").Value, Target("trailing ").Value,
            Target("bad?.json").Value
        })
        {
            var target = new PathVariableDeclaration { Value = value };
            Error(VariableSnapshotFileError.InvalidPath, () => store.Write(target, Data()));
            Error(VariableSnapshotFileError.InvalidPath, () => store.Read(target));
        }
        Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
    }

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("{\"format\":\"CryptoScriptVariables\",\"version\":1}")]
    [TestCase("Print(\"do not execute\");")]
    public void InvalidJsonReturnsNothingAndDoesNotAlterFile(string contents)
    {
        File.WriteAllText(Target().Value, contents);
        IReadOnlyList<VariableDeclaration>? result = null;
        Error(VariableSnapshotFileError.InvalidSnapshot, () => result = store.Read(Target()));
        Assert.That(result, Is.Null);
        Assert.That(File.ReadAllText(Target().Value), Is.EqualTo(contents));
    }

    [Test]
    public void InvalidLaterVariableReturnsNoPartialList()
    {
        var json = JObject.Parse(serializer.Serialize([Data()[0],
            new StringVariableDeclaration { Id = "second", Value = "secret marker" }]));
        json["variables"]![1]!["metadata"]!["type"] = "UNKNOWN";
        File.WriteAllText(Target().Value, json.ToString());
        IReadOnlyList<VariableDeclaration>? result = null;
        Error(VariableSnapshotFileError.InvalidSnapshot, () => result = store.Read(Target()));
        Assert.That(result, Is.Null);
    }

    [TestCase("version")]
    [TestCase("format")]
    public void UnsupportedEnvelopeIsDistinctFromInvalidJson(string property)
    {
        var json = JObject.Parse(serializer.Serialize(Data()));
        json[property] = property == "version" ? new JValue(2) : new JValue("OtherFormat");
        File.WriteAllText(Target().Value, json.ToString());
        Error(VariableSnapshotFileError.UnsupportedFormat, () => store.Read(Target()));
    }

    [TestCase("{\"format\":\"CryptoScriptVariables\",\"version\":2,\"newField\":[]}")]
    [TestCase("{\"format\":\"OtherFormat\",\"version\":1,\"differentBody\":true}")]
    public void FutureFormatsNeedNotConformToVersionOneSchema(string json)
    {
        File.WriteAllText(Target().Value, json);
        Error(VariableSnapshotFileError.UnsupportedFormat, () => store.Read(Target()));
    }

    [Test]
    public void InvalidUtf8AndUtf16AreNotSilentlyReplacedOrDetected()
    {
        foreach (var bytes in new[]
        {
            new byte[] { 0xff, 0xfe, 0x7b, 0 },
            Encoding.UTF8.GetBytes(serializer.Serialize(Data())).Concat(new byte[] { 0xc3, 0x28 }).ToArray()
        })
        {
            File.WriteAllBytes(Target().Value, bytes);
            Error(VariableSnapshotFileError.InvalidSnapshot, () => store.Read(Target()));
        }
    }

    [Test]
    public void ByteLimitHasExactBoundaryAndRejectsOversizedWriteBeforeChanges()
    {
        string json = serializer.Serialize(Data());
        int length = Encoding.UTF8.GetByteCount(json);
        var exact = new VariableSnapshotFileStore(length);
        exact.Write(Target(), Data());
        Assert.That(exact.Read(Target()), Has.Count.EqualTo(1));
        var small = new VariableSnapshotFileStore(length - 1);
        Error(VariableSnapshotFileError.SizeLimitExceeded, () => small.Read(Target()));
        Error(VariableSnapshotFileError.SizeLimitExceeded,
            () => small.Write(Target(), Data(), VariableSnapshotWriteMode.ReplaceExisting));
        Assert.That(File.ReadAllText(Target().Value), Is.EqualTo(json));
        NoTemporaryFiles();
    }

    [Test]
    public void PublishedPermissionsAreRestricted()
    {
        store.Write(Target(), Data());
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var acl = new FileInfo(Target().Value).GetAccessControl();
            Assert.That(acl.AreAccessRulesProtected, Is.True);
            var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>().ToArray();
            Assert.That(rules, Has.Length.EqualTo(1));
            Assert.That(rules[0].IdentityReference, Is.EqualTo(identity.User));
            Assert.That(rules[0].AccessControlType, Is.EqualTo(AccessControlType.Allow));
        }
        else
        {
            var mode = File.GetUnixFileMode(Target().Value);
            Assert.That(mode & ~(UnixFileMode.UserRead | UnixFileMode.UserWrite), Is.EqualTo((UnixFileMode)0));
        }
    }

    [Test]
    public void MacOsInheritedExtendedAclIsRemovedBeforePublication()
    {
        if (!OperatingSystem.IsMacOS()) Assert.Ignore("Darwin extended ACLs.");
        Run("/bin/chmod", "+a", "everyone allow read,write,file_inherit", directory);
        // Verify the fixture really creates inherited permissions that mode 0600 alone
        // cannot remove on Darwin.
        var probe = Target("probe");
        File.WriteAllText(probe.Value, "");
        Assert.That(File.Exists(probe.Value), Is.True);
        var inheritedEntries = MacAclEntries(Run("/bin/ls", "-le", probe.Value));
        Assert.That(inheritedEntries.Any(IsInheritedEveryoneAllow), Is.True,
            "The probe must actually inherit an allow ACE for group:everyone.");
        var source = Data();
        store.Write(Target(), source);
        Assert.That(MacAclEntries(Run("/bin/ls", "-le", Target().Value)), Is.Empty,
            "The published file must have no extended ACL entries for any principal.");
        Assert.That(serializer.Serialize(store.Read(Target())), Is.EqualTo(serializer.Serialize(source)));
        NoTemporaryFiles();
    }

    private static string[] MacAclEntries(string listing) =>
        listing.Split('\n')
            .Select(line => Regex.Match(line.TrimEnd('\r'), @"^[ \t]*[0-9]+[ \t]*:[ \t]*(.*)$",
                RegexOptions.CultureInvariant))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToArray();

    private static bool IsInheritedEveryoneAllow(string entry) =>
        Regex.IsMatch(entry, @"\Agroup:everyone[ \t]+inherited[ \t]+allow(?:[ \t]+|\z)",
            RegexOptions.CultureInvariant);

    // Run on every platform: a retained ACE must never disappear from the parsed list,
    // even when its principal, flags or permissions differ from the fixture's ACE.
    [TestCase("-rw-r--r-- 1 owner staff 0 Oct 20 file\n", 0, false)]
    [TestCase("-rw-r--r-- 1 owner staff 0 Oct 20 everyone inherited allow\n", 0, false)]
    [TestCase(" 0: group:everyone inherited allow read,write\n", 1, true)]
    [TestCase("\t12 \t: \tgroup:everyone\tinherited\tallow\tread,write\r\n", 1, true)]
    [TestCase(" 0: group:everyone allow read,write\n", 1, false)]
    [TestCase(" 0: group:everyone inherited deny read,write\n", 1, false)]
    [TestCase(" 0: group:everyoneElse inherited allow read\n", 1, false)]
    [TestCase(" 0: user:everyone inherited allow read\n", 1, false)]
    [TestCase(" 0: group:everyone inherited allowSomething read\n", 1, false)]
    [TestCase(" 0: user:other allow read\n 1: group:everyone inherited allow read,write\n", 2, true)]
    [TestCase(" 0: \n 1: unknown future ACE syntax\n", 2, false)]
    public void MacAclListingRecognizesNumberedEntriesAndExactInheritedGrant(
        string listing, int expectedCount, bool expectedInheritedGrant)
    {
        var entries = MacAclEntries(listing);
        Assert.That(entries, Has.Length.EqualTo(expectedCount));
        Assert.That(entries.Any(IsInheritedEveryoneAllow), Is.EqualTo(expectedInheritedGrant));
    }

    private static string Run(string executable, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo(executable)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = System.Diagnostics.Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        string errors = process.StandardError.ReadToEnd();
        Assert.That(process.WaitForExit(10000), Is.True);
        Assert.That(process.ExitCode, Is.EqualTo(0), errors);
        return output;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TargetSymlinksIncludingDanglingLinksAreRejected(bool dangling)
    {
        var actual = Target("actual.json");
        if (!dangling) File.WriteAllText(actual.Value, "unchanged");
        try { File.CreateSymbolicLink(Target().Value, actual.Value); }
        catch (IOException error) when (OperatingSystem.IsWindows() && (error.HResult & 0xffff) == 1314)
        { Assert.Ignore("Symbolic-link creation privilege is unavailable."); }
        foreach (var mode in Enum.GetValues<VariableSnapshotWriteMode>())
            Error(VariableSnapshotFileError.InvalidPath, () => store.Write(Target(), Data(), mode));
        Error(VariableSnapshotFileError.InvalidPath, () => store.Read(Target()));
        Assert.That(File.Exists(actual.Value), Is.EqualTo(!dangling));
        if (!dangling) Assert.That(File.ReadAllText(actual.Value), Is.EqualTo("unchanged"));
        Assert.That(new FileInfo(Target().Value).LinkTarget, Is.Not.Null);
        NoTemporaryFiles();
    }

    [Test]
    public void WindowsSharingFailuresPreserveOldFileAndCleanPreparedTemporary()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows sharing and rename semantics.");
        var target = Target();
        File.WriteAllText(target.Value, "original");
        using (var locked = new FileStream(target.Value, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Error(VariableSnapshotFileError.ReadError, () => store.Read(target));
            // The temp is fully prepared before MoveFileEx fails on the locked destination.
            Error(VariableSnapshotFileError.AccessDenied,
                () => store.Write(target, Data(), VariableSnapshotWriteMode.ReplaceExisting));
            NoTemporaryFiles();
        }
        Assert.That(File.ReadAllText(target.Value), Is.EqualTo("original"));
    }

    [Test]
    public void AccessDeniedIsControlled()
    {
        var target = Target();
        File.WriteAllText(target.Value, "original");
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var info = new FileInfo(target.Value);
            var original = info.GetAccessControl();
            var denied = info.GetAccessControl();
            denied.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.ReadData,
                AccessControlType.Deny));
            try
            {
                info.SetAccessControl(denied);
                Error(VariableSnapshotFileError.AccessDenied, () => store.Read(target));
            }
            finally
            {
                original.SetSecurityDescriptorSddlForm(
                    original.GetSecurityDescriptorSddlForm(AccessControlSections.Access), AccessControlSections.Access);
                info.SetAccessControl(original);
            }
        }
        else
        {
            var original = File.GetUnixFileMode(target.Value);
            try
            {
                File.SetUnixFileMode(target.Value, UnixFileMode.None);
                // Root can bypass Unix mode checks; report this explicitly.
                try { using var probe = File.OpenRead(target.Value); Assert.Ignore("User bypasses Unix permissions."); }
                catch (UnauthorizedAccessException) { }
                Error(VariableSnapshotFileError.AccessDenied, () => store.Read(target));
            }
            finally { File.SetUnixFileMode(target.Value, original); }
        }
        Assert.That(File.ReadAllText(target.Value), Is.EqualTo("original"));
    }

    [Test]
    public void DirectoryWriteDenialDoesNotChangeExistingTarget()
    {
        var target = Target();
        File.WriteAllText(target.Value, "original");
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var info = new DirectoryInfo(directory);
            var original = info.GetAccessControl();
            var denied = info.GetAccessControl();
            denied.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.CreateFiles,
                AccessControlType.Deny));
            try
            {
                info.SetAccessControl(denied);
                Error(VariableSnapshotFileError.AccessDenied,
                    () => store.Write(target, Data(), VariableSnapshotWriteMode.ReplaceExisting));
            }
            finally
            {
                original.SetSecurityDescriptorSddlForm(
                    original.GetSecurityDescriptorSddlForm(AccessControlSections.Access), AccessControlSections.Access);
                info.SetAccessControl(original);
            }
        }
        else
        {
            var original = File.GetUnixFileMode(directory);
            try
            {
                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                try
                {
                    using var probe = File.Create(Target("probe").Value);
                    Assert.Ignore("User bypasses Unix permissions.");
                }
                catch (UnauthorizedAccessException) { }
                Error(VariableSnapshotFileError.AccessDenied,
                    () => store.Write(target, Data(), VariableSnapshotWriteMode.ReplaceExisting));
            }
            finally { File.SetUnixFileMode(directory, original); }
        }
        Assert.That(File.ReadAllText(target.Value), Is.EqualTo("original"));
        NoTemporaryFiles();
    }

    [Test]
    public void ConcurrentCreateNewHasExactlyOneWinnerAndNoPartialFile()
    {
        var errors = new System.Collections.Concurrent.ConcurrentBag<VariableSnapshotFileError>();
        int successes = 0;
        Parallel.For(0, 24, i =>
        {
            try { store.Write(Target(), Data(i.ToString())); Interlocked.Increment(ref successes); }
            catch (VariableSnapshotFileException error) { errors.Add(error.Error); }
        });
        Assert.That(successes, Is.EqualTo(1));
        Assert.That(errors, Has.Count.EqualTo(23));
        Assert.That(errors, Is.All.EqualTo(VariableSnapshotFileError.AlreadyExists));
        Assert.That(int.Parse(store.Read(Target())[0].Value), Is.InRange(0, 23));
        NoTemporaryFiles();
    }

    [Test]
    public void ConcurrentReplacementOnlyPublishesCompleteSnapshots()
    {
        store.Write(Target(), Data("initial"));
        int successes = 0;
        Parallel.For(0, 16, i =>
        {
            try
            {
                store.Write(Target(), Data(new string((char)('a' + i), 10000)),
                    VariableSnapshotWriteMode.ReplaceExisting);
                Interlocked.Increment(ref successes);
            }
            catch (VariableSnapshotFileException error) when (OperatingSystem.IsWindows() &&
                error.Error == VariableSnapshotFileError.AccessDenied)
            {
                // Windows may reject competing rename/delete operations or open handles.
            }
            string value;
            try { value = store.Read(Target())[0].Value; }
            catch (VariableSnapshotFileException error) when (OperatingSystem.IsWindows() &&
                error.Error is VariableSnapshotFileError.AccessDenied or VariableSnapshotFileError.ReadError)
            { return; }
            if (value == "initial") return;
            Assert.That(value, Has.Length.EqualTo(10000));
            Assert.That(value.All(c => c == value[0]), Is.True);
        });
        Assert.That(successes, Is.GreaterThan(0));
        string final = store.Read(Target())[0].Value;
        Assert.That(final, Has.Length.EqualTo(10000));
        Assert.That(final.All(c => c == final[0]), Is.True);
        NoTemporaryFiles();
    }

    [Test]
    public void NativePublicationDoesNotOverwriteLateCompetitor()
    {
        // Deterministically exercise the publication primitive after a competitor wins.
        var prepared = Target("prepared.tmp");
        File.WriteAllText(prepared.Value, "prepared");
        File.WriteAllText(Target().Value, "competitor");
        Error(VariableSnapshotFileError.AlreadyExists,
            () => SnapshotFilePublication.Publish(prepared.Value, Target().Value, VariableSnapshotWriteMode.CreateNew));
        Assert.That(File.ReadAllText(Target().Value), Is.EqualTo("competitor"));
        Assert.That(File.ReadAllText(prepared.Value), Is.EqualTo("prepared"));
    }

    [Test]
    public void NativePublicationDoesNotFollowLateSymlink()
    {
        var prepared = Target("prepared.tmp");
        File.WriteAllText(prepared.Value, "prepared");
        var other = Target("other.json");
        File.WriteAllText(other.Value, "other");
        try { File.CreateSymbolicLink(Target().Value, other.Value); }
        catch (IOException error) when (OperatingSystem.IsWindows() && (error.HResult & 0xffff) == 1314)
        { Assert.Ignore("Symbolic-link creation privilege is unavailable."); }
        Error(VariableSnapshotFileError.AlreadyExists,
            () => SnapshotFilePublication.Publish(prepared.Value, Target().Value, VariableSnapshotWriteMode.CreateNew));
        SnapshotFilePublication.Publish(prepared.Value, Target().Value, VariableSnapshotWriteMode.ReplaceExisting);
        Assert.That(File.ReadAllText(other.Value), Is.EqualTo("other"));
        Assert.That(File.ReadAllText(Target().Value), Is.EqualTo("prepared"));
        Assert.That(new FileInfo(Target().Value).LinkTarget, Is.Null);
    }
}
