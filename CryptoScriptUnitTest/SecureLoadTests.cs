using System.Text;
using CryptoScript;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public partial class SecureLoadTests
{
    private string directory = null!;
    private PathVariableDeclaration path = null!;
    private VariableDeclaration[] previous = null!;
    private readonly VariableDictionary dictionary = VariableDictionary.Instance();
    private const string Password = "päss🔐\0";
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "S06-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        previous = dictionary.GetVariables().ToArray(); dictionary.Clear();
        path = new() { Id = "path", Type = new CryptoTypePath(), Value = Path.Combine(directory, "secure.json") }; dictionary.Add(path);
    }
    [TearDown] public void Cleanup()
    { dictionary.Clear(); foreach (var v in previous) dictionary.Add(v); Directory.Delete(directory, true); }
    private static CryptoScriptParser.ProgramContext Parse(string text)
    { var p = ParserBuilder.StringBuild(text); var result = p.program(); Assert.That(p.NumberOfSyntaxErrors, Is.Zero, text); return result; }
    private static PasswordResponse Supply(PasswordRequestEventArgs r, string password = Password) =>
        PasswordResponse.Supplied(r.RequestId, PasswordSecret.TakeOwnership(password.ToCharArray()),
            r.ConfirmationRequired ? PasswordSecret.TakeOwnership(password.ToCharArray()) : null);
    private static CryptoScriptRunner Runner(string password = Password)
    { var r = new CryptoScriptRunner(); r.PasswordRequested += (_, e) => Task.FromResult(Supply(e, password)); return r; }
    private JToken Fixture(int index = 2) => JArray.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory,
        "Fixtures", "SecureSave", "vectors.json")))[index];
    private JObject WriteFixture(int index = 2)
    { byte[] bytes = Convert.FromHexString((string)Fixture(index)["envelopeHex"]!); File.WriteAllBytes(path.Value, bytes); return JObject.Parse(Encoding.UTF8.GetString(bytes)); }
    private void Write(JObject e) => File.WriteAllText(path.Value, e.ToString());
    private void Failure(string category, string script = "SecureLoad(path)", CryptoScriptRunner? runner = null)
    {
        var before = dictionary.GetVariables().ToArray(); runner ??= Runner();
        var error = Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(Parse(script)));
        Assert.That(error!.InnerException, Is.Null); Assert.That(runner.Diagnostics.Last().Message, Does.Contain(category));
        var after = dictionary.GetVariables().ToArray(); Assert.That(after.Length,Is.EqualTo(before.Length));
        for (int i=0;i<before.Length;i++) Assert.That(after[i],Is.SameAs(before[i]));
        Assert.That(runner.Diagnostics.All(d => !d.Message.Contains(Password) && !d.Message.Contains(directory)), Is.True);
    }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public async Task IndependentPythonVectorsLoadThroughPublicRuntime(int index)
    {
        WriteFixture(index); var runner = new CryptoScriptRunner(); char[] secret = Password.ToCharArray();
        runner.PasswordRequested += (_, r) =>
        {
            Assert.That(r.Purpose, Is.EqualTo(PasswordPurpose.SecureLoad)); Assert.That(r.ConfirmationRequired, Is.False);
            return Task.FromResult(PasswordResponse.Supplied(r.RequestId, PasswordSecret.TakeOwnership(secret)));
        };
        var result = await runner.ExecuteAsync(Parse("SecureLoad(path)"));
        Assert.That(((FunctionCall)result.Statements.Single()).ReturnVariable, Is.Null); Assert.That(secret, Is.All.EqualTo('\0'));
        string expected = Encoding.UTF8.GetString(Convert.FromHexString((string)Fixture(index)["payloadHex"]!));
        Assert.That(new VariableSnapshotJsonSerializer().Serialize(dictionary.GetVariables().Where(v => v.Id != "path")), Is.EqualTo(new VariableSnapshotJsonSerializer().Serialize(new VariableSnapshotJsonSerializer().Deserialize(expected))));
    }
    [TestCase("SecureLoad()")][TestCase("SecureLoad(missing)")][TestCase("SecureLoad(path,ALL,data)")]
    [TestCase("SecureLoad(path,data,data)")][TestCase("SecureLoad(path,#ONCONFLICT:REJECT,data)")]
    [TestCase("SecureLoad(path,#ONCONFLICT:REPLACE,#ONCONFLICT:REPLACE)")][TestCase("SecureLoad(path,#ONCONFLICT:BAD)")]
    [TestCase("SecureLoad(path,Print(0x(01)))")][TestCase("VAR x=SecureLoad(path)")]
    [TestCase("Compare(Print(0x(01)),SecureLoad(path))")]
    public void InvalidSyntaxHasNoEffects(string script)
    {
        int requests = 0, operations = 0; var r = new CryptoScriptRunner((_,_) => operations++);
        r.PasswordRequested += (_,e) => { requests++; return Task.FromResult(Supply(e)); };
        Assert.ThrowsAsync<SemanticErrorException>(async () => await r.ExecuteAsync(Parse(script)));
        Assert.That(requests, Is.Zero); Assert.That(operations, Is.Zero); Assert.That(dictionary.GetVariables().Count(), Is.EqualTo(1));
    }
    [Test] public void SyncRequiresAsyncBeforeIo()
    {
        var r = Runner(); Assert.Throws<SemanticErrorException>(() => r.Execute(Parse("SecureLoad(path)")));
        Assert.That(r.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.AsyncExecutionRequired));
        Assert.That(r.Diagnostics.Single().FunctionName, Is.EqualTo("SecureLoad"));
    }
    [TestCase("password")][TestCase("salt")][TestCase("nonce")][TestCase("tag")][TestCase("payload")][TestCase("manifest")][TestCase("kdf")]
    public void AuthFailuresShareOnePublicError(string field)
    {
        var e = WriteFixture();
        if (field == "manifest") e["manifest"]![0]!["identifier"] = "changed";
        else if (field == "kdf") e["kdf"]!["iterations"] = 600001;
        else if (field != "password")
        {
            var parent = field == "salt" ? e["kdf"]! : field == "nonce" ? e["cipher"]! : e;
            byte[] bytes = Convert.FromBase64String((string)parent[field]!); bytes[0] ^= 1; parent[field] = Convert.ToBase64String(bytes);
        }
        Write(e); Failure("SecureAuthenticationFailed", runner: Runner(field == "password" ? "wrong" : Password));
    }
    [TestCase("unknown")][TestCase("missing")][TestCase("version")][TestCase("mode")][TestCase("kdf")]
    [TestCase("cost")][TestCase("fraction")][TestCase("exponent")][TestCase("encoding")][TestCase("base64")]
    [TestCase("saltLength")][TestCase("nonceLength")][TestCase("tagLength")][TestCase("manifest")]
    [TestCase("duplicate")][TestCase("utf8")][TestCase("surrogate")][TestCase("bom")][TestCase("trailing")]
    public void InvalidEnvelopeNeverRequestsPassword(string kind)
    {
        var e = WriteFixture(); string category = "InvalidEnvelope";
        switch (kind)
        {
            case "unknown": e["extra"] = 1; break;
            case "missing": e.Remove("tag"); break;
            case "version": e["version"] = 2; category = "UnsupportedFormat"; break;
            case "mode": e["mode"] = "AES-CBC"; category = "UnsupportedFormat"; break;
            case "kdf": e["kdf"]!["id"] = "other"; category = "UnsupportedFormat"; break;
            case "cost": e["kdf"]!["iterations"] = 2000001; category = "ResourceLimitExceeded"; break;
            case "fraction": e["version"] = 1.5; break;
            case "encoding": e["payloadEncoding"] = "utf8"; break;
            case "base64": e["tag"] = "AAAAAAAAAAAAAAAAAAAAAB=="; break;
            case "saltLength": e["kdf"]!["salt"] = "AA=="; break;
            case "nonceLength": e["cipher"]!["nonce"] = "AA=="; break;
            case "tagLength": e["cipher"]!["tagLength"] = 12; break;
            case "manifest": e["manifest"]![0]!["key"] = new JObject(); break;
        }
        Write(e); string text = File.ReadAllText(path.Value);
        if (kind == "exponent") text = text.Replace("\"version\": 1", "\"version\": 1e0");
        if (kind == "duplicate") text = text.Replace("\"version\": 1", "\"version\": 1,\"version\":1");
        if (kind == "surrogate") text = text.Replace("\"base64\"", "\"\\ud800\"");
        if (kind == "trailing") text += "{}";
        File.WriteAllBytes(path.Value, kind == "utf8" ? [0xff, 0xfe] : kind == "bom" ? [0xef,0xbb,0xbf,..Encoding.UTF8.GetBytes(text)] : Encoding.UTF8.GetBytes(text));
        int requests = 0; var r = new CryptoScriptRunner(); r.PasswordRequested += (_,p) => { requests++; return Task.FromResult(Supply(p)); };
        Failure(category, runner:r); Assert.That(requests, Is.Zero);
    }
    [TestCase("SecureLoad(path)")][TestCase("SecureLoad(path,ALL)")][TestCase("SecureLoad(path,MasterKey,SessionKey)")]
    [TestCase("SecureLoad(path,MasterKey,#ONCONFLICT:REPLACE)")]
    public async Task SelectorsNeedNotExistAndAreCaseSensitive(string call)
    {
        dictionary.Add(new StringVariableDeclaration {Id="MasterKey",Value="one"}); dictionary.Add(new StringVariableDeclaration {Id="SessionKey",Value="two"});
        await Runner().ExecuteAsync(Parse("SecureSave(path,MasterKey,SessionKey,#SECUREKDF:PBKDF2)"));
        dictionary.Clear(); dictionary.Add(path); await Runner().ExecuteAsync(Parse(call));
        Assert.That(dictionary.Get("MasterKey").Value, Is.EqualTo("one"));
        if (!call.Contains("#ONCONFLICT")) Assert.That(dictionary.Get("SessionKey").Value, Is.EqualTo("two"));
        Failure("InvalidSelection", "SecureLoad(path,masterkey)");
    }
    [Test] public async Task RejectIsAtomicAndReplacePreservesUnselectedIdentity()
    {
        var old = new StringVariableDeclaration {Id="old",Value="saved"}; var fresh = new StringVariableDeclaration {Id="fresh",Value="new"};
        dictionary.Add(old); dictionary.Add(fresh); await Runner().ExecuteAsync(Parse("SecureSave(path,ALL,#SECUREKDF:PBKDF2)"));
        dictionary.Clear(); dictionary.Add(path); old.Value="live"; dictionary.Add(old);
        Failure("NameConflict"); Failure("NameConflict", "SecureLoad(path,fresh,old)");
        await Runner().ExecuteAsync(Parse("SecureLoad(path,fresh,#ONCONFLICT:REPLACE)")); Assert.That(dictionary.Get("old"),Is.SameAs(old));
        await Runner().ExecuteAsync(Parse("SecureLoad(path,old,#ONCONFLICT:REPLACE)")); Assert.That(dictionary.Get("old"),Is.Not.SameAs(old));
        Assert.That(dictionary.Get("old").Value,Is.EqualTo("saved")); Assert.That(dictionary.Get("path"),Is.SameAs(path));
    }
    [Test] public async Task FilePathAndCwdAreFixedBeforePasswordDialog()
    {
        WriteFixture(); string original = path.Value, cwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = directory; path.Value = "secure.json";
        var r = new CryptoScriptRunner(); r.PasswordRequested += (_,e) =>
        { File.WriteAllText(original,"changed"); path.Value="missing"; Environment.CurrentDirectory=cwd; return Task.FromResult(Supply(e)); };
        try { await r.ExecuteAsync(Parse("SecureLoad(path)")); Assert.That(dictionary.GetVariables().Count(),Is.GreaterThan(1)); }
        finally { Environment.CurrentDirectory=cwd; }
    }
    [TestCase("cancel")][TestCase("token")][TestCase("host")][TestCase("id")][TestCase("confirmation")]
    public void PasswordFailuresPreserveDictionary(string kind)
    {
        WriteFixture(); using var cts = new CancellationTokenSource(); char[] secret = Password.ToCharArray(); var r = new CryptoScriptRunner();
        r.PasswordRequested += (_,e) =>
        {
            if (kind == "host") throw new InvalidOperationException("private canary");
            if (kind == "cancel") return Task.FromResult(PasswordResponse.UserCancelled(e.RequestId));
            if (kind == "token") cts.Cancel();
            return Task.FromResult(PasswordResponse.Supplied(kind == "id" ? Guid.NewGuid() : e.RequestId,
                PasswordSecret.TakeOwnership(secret), kind == "confirmation" ? PasswordSecret.TakeOwnership(Password.ToCharArray()) : null));
        };
        Assert.CatchAsync(async () => await r.ExecuteAsync(Parse("SecureLoad(path) VAR later=0x(01)"),cts.Token));
        Assert.That(dictionary.GetVariables().Single(),Is.SameAs(path));
        if (kind is "token" or "id" or "confirmation") Assert.That(secret,Is.All.EqualTo('\0'));
        Assert.That(r.Diagnostics.All(d => !d.Message.Contains("private canary")),Is.True);
    }
}
