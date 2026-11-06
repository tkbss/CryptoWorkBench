using CryptoScript;
using CryptoScript.ErrorListner;
using System.Security.Cryptography;
using CryptoScript.Operations;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.SecretSharing;
using System.Security.AccessControl;
using System.Security.Principal;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

public partial class SecureSaveTests
{
    [Test] public void CancellationAfterFlushRemovesPrivateTemporaryWithoutPublishing()
    {
        var destination = SnapshotByteFilePublisher.Prepare(path,VariableSnapshotWriteMode.CreateNew);
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => SnapshotByteFilePublisher.Write(destination,new byte[] { 1,2,3 },3,
            VariableSnapshotWriteMode.CreateNew,cts.Token,afterFlush: () =>
            {
                Assert.That(Directory.GetFiles(directory,".cryptoscript-*.tmp"),Has.Length.EqualTo(1));
                cts.Cancel();
            }));
        Assert.That(Directory.GetFiles(directory),Is.Empty);
    }

    [Test] public void CancellationAfterKdfClearsPasswordAndStopsBeforeEncryptionOrPublication()
    {
        using var prepared = SecureSaveOperations.Prepare(Call("SecureSave(path,data,#SECUREKDF:PBKDF2)"));
        using var cts = new CancellationTokenSource(); byte[] password = "canary"u8.ToArray();
        Assert.Throws<OperationCanceledException>(() => SecureContainerCryptography.Protect(prepared.Options,prepared.Payload,
            prepared.ManifestJson,prepared.ManifestFrame,password,new byte[16],new byte[12],cts.Token,afterDerivation: () =>
            { Assert.That(password,Is.All.Zero); cts.Cancel(); }));
        Assert.That(Directory.GetFiles(directory),Is.Empty);
    }

    [Test] public async Task WrongPasswordCannotAuthenticate()
    {
        var runner = new CryptoScriptRunner(); runner.PasswordRequested += (_,r) => Task.FromResult(Supply(r));
        await runner.ExecuteAsync(Parse("SecureSave(path,data,#SECUREKDF:PBKDF2)"));
        Assert.Throws<AuthenticationTagMismatchException>(() => SecureContainerTestReader.Read(File.ReadAllText(path.Value),"wrong"));
    }

    [Test] public void CancellationImmediatelyAfterPasswordRequestDoesNotPublishOrExecuteFollowingStatement()
    {
        using var cts = new CancellationTokenSource(); var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_,r) => { var answer = Supply(r); cts.Cancel(); return Task.FromResult(answer); };
        Assert.CatchAsync<OperationCanceledException>(async () => await runner.ExecuteAsync(Parse("SecureSave(path,data) VAR later=0x(01)"),cts.Token));
        Assert.That(VariableDictionary.Instance().Contains("later"),Is.False);
        Assert.That(Directory.GetFiles(directory),Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public async Task AllTypesManifestAndHeaderIdentityMatchExactPayload(bool includeHeader)
    {
        var header = new BlockHeaderVariableDeclaration { Id = "header", Definition = Tr31HeaderDefinition.Create(
            new Dictionary<string,string> { ["KBVID"]="D", ["KEYU"]="D0", ["ALGO"]="A", ["MODEU"]="B", ["EXP"]="E" }) };
        var parameter = new ParameterVariableDeclaration { Mechanism = "WRAP-AES-TR31" };
        parameter.SetParameter("#BLKH",header.Id);
        var linked = ParameterVariableDeclaration.FromSnapshot(parameter.Mechanism,parameter.GetParameters(),
            parameter.ExplicitParameterNames,parameter.Value,header); linked.Id = "params";
        var key = new KeyVariableDeclaration { Id = "key", ValueFormat = FormatConversions.HEX,
            Value = "0x(000102030405060708090a0b0c0d0e0f)", KeyType = KeyType.Secret(KeyAlgorithm.Aes) };
        var unknown = new KeyVariableDeclaration { Id = "unknown", ValueFormat = FormatConversions.HEX, Value = "0x(010203)" };
        var secret = new SecretVariableDeclaration(new(new(SecretDescriptor.Split,2,2,8),[new(1,new byte[] { 42 })])) { Id = "secret" };
        var dictionary = VariableDictionary.Instance();
        foreach (var v in new VariableDeclaration[] { linked,key,unknown,secret }) dictionary.Add(v);
        if (includeHeader) dictionary.Add(header);
        data.Type = null;
        string expected = new VariableSnapshotJsonSerializer().Serialize(dictionary.GetVariables());
        var runner = new CryptoScriptRunner(); runner.PasswordRequested += (_,r) => Task.FromResult(Supply(r));
        await runner.ExecuteAsync(Parse("SecureSave(path, ALL, #SECUREKDF:PBKDF2)"));
        string envelope = File.ReadAllText(path.Value);
        Assert.That(SecureContainerTestReader.Read(envelope,"päss🔐\0"), Is.EqualTo(expected));
        var roots = new VariableSnapshotJsonSerializer().Deserialize(expected);
        var manifest = (JArray)JObject.Parse(envelope)["manifest"]!;
        Assert.That(manifest.Select(x => (string)x["identifier"]!),Is.EqualTo(roots.Select(r => r.Id)));
        Assert.That((string?)manifest.Single(x => (string)x["identifier"]! == "key")["key"]!["kcv"],Is.EqualTo(key.Kcv));
        Assert.That((int)manifest.Single(x => (string)x["identifier"]! == "key")["key"]!["bitLength"]!,Is.EqualTo(128));
        Assert.That(manifest.Single(x => (string)x["identifier"]! == "unknown")["key"]!["kcv"]!.Type,Is.EqualTo(JTokenType.Null));
        if (includeHeader) Assert.That(roots.OfType<ParameterVariableDeclaration>().Single().HeaderReference,Is.SameAs(roots.OfType<BlockHeaderVariableDeclaration>().Single()));
        else Assert.That(manifest.Any(x => (string)x["type"]! == "TR31H"),Is.False);
        Assert.That(envelope,Does.Not.Contain(directory).And.Not.Contain("0001020304050607").And.Not.Contain("canary"));
        if (OperatingSystem.IsWindows())
        {
            var acl = new FileInfo(path.Value).GetAccessControl();
            Assert.That(acl.AreAccessRulesProtected,Is.True);
            using var identity = WindowsIdentity.GetCurrent();
            var rules = acl.GetAccessRules(true,true,typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            Assert.That(rules,Has.Length.EqualTo(1)); Assert.That(rules[0].IdentityReference,Is.EqualTo(identity.User));
        }
        else Assert.That(File.GetUnixFileMode(path.Value),Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
    }

    [Test] public void SecureOptionsAreNotParameterRegistryEntries()
    {
        Assert.That(AntlrLanguageMetadata.GetParameters(),Does.Not.Contain("#SECUREMODE").And.Not.Contain("#SECUREKDF").And.Not.Contain("#ARGONMEM"));
        foreach (string name in new[] { "SECUREMODE", "SECUREKDF", "ARGONMEM", "ARGONITER", "ARGONLANES", "PBKDFITER" })
        {
            Assert.That(typeof(CryptoScriptLexer).GetField("SOPT_" + name),Is.Not.Null);
            Assert.That(typeof(CryptoScriptLexer).GetField("P_" + name),Is.Null);
        }
    }

    [TestCase("identifier")] [TestCase("roots")] [TestCase("path")]
    public void LimitsAndInvalidPathRejectBeforePassword(string kind)
    {
        var dictionary = VariableDictionary.Instance();
        if (kind == "identifier") { dictionary.Clear(); dictionary.Add(path); data.Id = new string('a',257); dictionary.Add(data); }
        else if (kind == "roots") for (int i=0;i<4096;i++) dictionary.Add(new StringVariableDeclaration { Id="a"+i,Value="v" });
        else path.Value = "\0bad";
        int requests = 0; var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_,r) => { requests++; return Task.FromResult(Supply(r)); };
        Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(Parse("SecureSave(path,ALL)")));
        Assert.That(requests,Is.Zero); Assert.That(Directory.GetFiles(directory),Is.Empty);
    }

    [Test] public void IndependentManifestWireAndFrameLimitsAreEnforced()
    {
        var entries = Enumerable.Range(0,4096).Select(i => new SecureManifestEntry(new string('a',252)+i.ToString("D4"),"KEY",
            new SecureKeyManifest("Aes",128,"0x("+new string('0',32)+")"))).ToArray();
        Assert.Throws<SecureSaveException>(() => SecureManifest.Json(entries));
        Assert.Throws<SecureSaveException>(() => SecureManifest.Frame(entries));
        Assert.Throws<SecureSaveException>(() => SecureContainerCryptography.EnvelopeSize(new(Gmac:true),
            new byte[SecureSaveOptions.PayloadLimit],"[]"u8.ToArray()));
    }

    [Test] public async Task RelativeDestinationSurvivesWorkingDirectoryChangeDuringDialog()
    {
        string cwd = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory; path.Value = "relative.json";
            var runner = new CryptoScriptRunner();
            runner.PasswordRequested += (_,r) => { Environment.CurrentDirectory=cwd; return Task.FromResult(Supply(r)); };
            await runner.ExecuteAsync(Parse("SecureSave(path,data,#SECUREKDF:PBKDF2)"));
            Assert.That(File.Exists(Path.Combine(directory,"relative.json")),Is.True);
        }
        finally { Environment.CurrentDirectory = cwd; }
    }
}
