using System.Security.Cryptography;
using CryptoScript;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Operations;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public partial class SecureSaveTests
{
    private string directory = null!;
    private VariableDeclaration[] previous = null!;
    private PathVariableDeclaration path = null!;
    private StringVariableDeclaration data = null!;
    [SetUp] public void Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "S05B2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var d = VariableDictionary.Instance(); previous = d.GetVariables().ToArray(); d.Clear();
        path = new() { Id = "path", Type = new CryptoTypePath(), Value = Path.Combine(directory, "secure.json") };
        data = new() { Id = "data", Value = "canary-秘密-🔐\u0000" };
        d.Add(path); d.Add(data);
    }
    [TearDown] public void Cleanup()
    {
        var d = VariableDictionary.Instance(); d.Clear(); foreach (var v in previous) d.Add(v);
        Directory.Delete(directory, true);
    }
    internal static PasswordResponse Supply(PasswordRequestEventArgs request, string password = "päss🔐\0") =>
        PasswordResponse.Supplied(request.RequestId, PasswordSecret.TakeOwnership(password.ToCharArray()),
            PasswordSecret.TakeOwnership(password.ToCharArray()));
    private static CryptoScriptParser.ProgramContext Parse(string script)
    {
        var parser = ParserBuilder.StringBuild(script); var result = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script); return result;
    }
    private static FunctionCallExpressionNode Call(string script) =>
        ((FunctionCallStatementNode)AntlrToStatement.Map(Parse(script).statement(0))!).Call;

    [TestCase("SecureSave(path, data)")]
    [TestCase("SecureSave(path, path, data)")]
    [TestCase("SecureSave(path, ALL)")]
    [TestCase("SecureSave(path, data, #SECUREMODE:GCM)")]
    [TestCase("SecureSave(path, data, #SECUREMODE:GMAC)")]
    [TestCase("SecureSave(path, data, #SECUREKDF:ARGON2ID)")]
    [TestCase("SecureSave(path, data, #SECUREKDF:PBKDF2)")]
    [TestCase("SecureSave(path, ALL, #SECUREKDF:PBKDF2, #PBKDFITER:600000)")]
    [TestCase("SecureSave(path, data, #ARGONMEM:65536, #ARGONITER:3, #ARGONLANES:4)")]
    [TestCase("SecureSave(path, data, #PBKDFITER:600000, #SECUREKDF:PBKDF2, #SECUREMODE:GMAC)")]
    public void ValidSyntaxPreparesDetachedSnapshot(string script)
    {
        using var prepared = SecureSaveOperations.Prepare(Call(script));
        Assert.That(prepared.Payload.Length, Is.Positive);
        Assert.That(prepared.Destination.Path, Is.EqualTo(path.Value));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase("SecureSave()")]
    [TestCase("SecureSave(path)")]
    [TestCase("SecureSave(data, data)")]
    [TestCase("SecureSave(missing, data)")]
    [TestCase("SecureSave(path, missing)")]
    [TestCase("SecureSave(path, data, data)")]
    [TestCase("SecureSave(path, ALL, data)")]
    [TestCase("SecureSave(path, #SECUREMODE:GCM, data)")]
    [TestCase("SecureSave(path, data, #SECUREMODE:BAD)")]
    [TestCase("SecureSave(path, data, #SECUREMODE:GCM, #SECUREMODE:GMAC)")]
    [TestCase("SecureSave(path, data, #SECUREKDF:BAD)")]
    [TestCase("SecureSave(path, data, #PBKDFITER:600000)")]
    [TestCase("SecureSave(path, data, #SECUREKDF:PBKDF2, #ARGONMEM:65536)")]
    [TestCase("SecureSave(path, data, #ARGONMEM:65535)")]
    [TestCase("SecureSave(path, data, #ARGONMEM:262145)")]
    [TestCase("SecureSave(path, data, #ARGONMEM:262144, #ARGONITER:4)")]
    [TestCase("SecureSave(path, data, #ARGONITER:2)")]
    [TestCase("SecureSave(path, data, #ARGONITER:7)")]
    [TestCase("SecureSave(path, data, #ARGONLANES:0)")]
    [TestCase("SecureSave(path, data, #ARGONLANES:5)")]
    [TestCase("SecureSave(path, data, #ARGONLANES:3)")]
    [TestCase("SecureSave(path, data, #ARGONMEM:999999999999999999999)")]
    [TestCase("SecureSave(path, data, #ARGONMEM:\"65536\")")]
    [TestCase("SecureSave(path, data, #SECUREKDF:PBKDF2, #PBKDFITER:599999)")]
    [TestCase("SecureSave(path, data, #SECUREKDF:PBKDF2, #PBKDFITER:2000001)")]
    [TestCase("SecureSave(path, Compare(0x(01),0x(01)))")]
    [TestCase("VAR other=SecureSave(path,data)")]
    [TestCase("Compare(Print(data),SecureSave(path,data))")]
    public void InvalidCallsFailBeforePasswordOrEarlierSibling(string script)
    {
        int requests = 0, operations = 0;
        var runner = new CryptoScriptRunner((_, _) => operations++);
        runner.PasswordRequested += (_, r) => { requests++; return Task.FromResult(Supply(r)); };
        Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(Parse(script)));
        Assert.That(requests, Is.Zero); Assert.That(operations, Is.Zero);
        Assert.That(Directory.GetFiles(directory), Is.Empty);
        Assert.That(runner.Diagnostics, Is.Not.Empty);
        Assert.That(runner.Diagnostics.All(d => !d.Message.Contains(data.Value) && !d.Message.Contains(directory)), Is.True);
    }

    [Test] public void SyncRequiresAsync()
    {
        var runner = new CryptoScriptRunner();
        Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse("SecureSave(path,data)")));
        Assert.That(runner.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.AsyncExecutionRequired));
    }

    [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
    public async Task ProductiveRoundTripUsesFreshRandomnessAndDisposesPassword(bool gmac, bool pbkdf)
    {
        var runner = new CryptoScriptRunner();
        char[] password = "päss🔐\0".ToCharArray(), confirmation = "päss🔐\0".ToCharArray();
        runner.PasswordRequested += (_, request) =>
        {
            Assert.That(request.ConfirmationRequired, Is.True);
            return Task.FromResult(PasswordResponse.Supplied(request.RequestId,
                PasswordSecret.TakeOwnership(password), PasswordSecret.TakeOwnership(confirmation)));
        };
        string script = $"SecureSave(path, data, #SECUREMODE:{(gmac ? "GMAC" : "GCM")}, #SECUREKDF:{(pbkdf ? "PBKDF2" : "ARGON2ID")})";
        var program = await runner.ExecuteAsync(Parse(script));
        Assert.That(((FunctionCall)program.Statements.Single()).ReturnVariable, Is.Null);
        Assert.That(password, Is.All.EqualTo('\0')); Assert.That(confirmation, Is.All.EqualTo('\0'));
        string first = File.ReadAllText(path.Value);
        string plaintext = SecureContainerTestReader.Read(first, "päss🔐\0");
        Assert.That(new VariableSnapshotJsonSerializer().Deserialize(plaintext).Single().Value, Is.EqualTo(data.Value));
        if (!gmac) Assert.That(first, Does.Not.Contain("canary"));
        var original = JObject.Parse(first);
        path.Value = Path.Combine(directory, "second.json");
        password = "päss🔐\0".ToCharArray(); confirmation = "päss🔐\0".ToCharArray();
        await runner.ExecuteAsync(Parse(script));
        var second = JObject.Parse(File.ReadAllText(path.Value));
        Assert.That((string?)second["kdf"]!["salt"], Is.Not.EqualTo((string?)original["kdf"]!["salt"]));
        Assert.That((string?)second["cipher"]!["nonce"], Is.Not.EqualTo((string?)original["cipher"]!["nonce"]));
        Assert.That(Directory.GetFiles(directory, ".cryptoscript-*.tmp"), Is.Empty);
    }

    [Test] public async Task LiveMutationDuringDialogCannotChangePreparedPayloadOrDestination()
    {
        string originalPath = path.Value, originalValue = data.Value;
        var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_, r) =>
        {
            path.Value = Path.Combine(directory, "other.json"); data.Value = "changed";
            return Task.FromResult(Supply(r));
        };
        await runner.ExecuteAsync(Parse("SecureSave(path, data, #SECUREKDF:PBKDF2)"));
        Assert.That(File.Exists(path.Value), Is.False);
        var roots = new VariableSnapshotJsonSerializer().Deserialize(SecureContainerTestReader.Read(File.ReadAllText(originalPath), "päss🔐\0"));
        Assert.That(roots.Single().Value, Is.EqualTo(originalValue));
    }

    [TestCase("cancel")] [TestCase("host")] [TestCase("mismatch")]
    public void PasswordFailureNeverPublishes(string kind)
    {
        var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_, r) => kind switch
        {
            "cancel" => Task.FromResult(PasswordResponse.UserCancelled(r.RequestId)),
            "host" => throw new Exception("HOST-SECRET-" + directory),
            _ => Task.FromResult(PasswordResponse.Supplied(r.RequestId, PasswordSecret.TakeOwnership("a".ToCharArray()), PasswordSecret.TakeOwnership("b".ToCharArray())))
        };
        var error = Assert.CatchAsync(async () => await runner.ExecuteAsync(Parse("SecureSave(path,data)")))!;
        Assert.That(error.Message, Does.Not.Contain(directory).And.Not.Contain("HOST-SECRET"));
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [Test] public void ExistingDestinationAndPayloadLimitsFailBeforeDialog()
    {
        int requests = 0; var runner = new CryptoScriptRunner();
        runner.PasswordRequested += (_, r) => { requests++; return Task.FromResult(Supply(r)); };
        File.WriteAllText(path.Value, "existing");
        Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(Parse("SecureSave(path,data)")));
        Assert.That(File.ReadAllText(path.Value), Is.EqualTo("existing")); File.Delete(path.Value);
        data.Value = new string('a', SecureSaveOptions.PayloadLimit);
        Assert.ThrowsAsync<SemanticErrorException>(async () => await runner.ExecuteAsync(Parse("SecureSave(path,data)")));
        Assert.That(requests, Is.Zero);
    }

    [Test] public async Task CancellationWhileWaitingDisposesLateResponse()
    {
        var tcs = new TaskCompletionSource<PasswordResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        PasswordRequestEventArgs? request = null; var runner = new CryptoScriptRunner(); using var cts = new CancellationTokenSource();
        runner.PasswordRequested += (_, r) => { request = r; return tcs.Task; };
        var pending = runner.ExecuteAsync(Parse("SecureSave(path,data)"), cts.Token);
        cts.Cancel(); Assert.CatchAsync<OperationCanceledException>(async () => await pending);
        var response = Supply(request!); tcs.SetResult(response);
        for (int i = 0; i < 100; i++)
        {
            try { _ = response.Password!.Characters; await Task.Delay(10); }
            catch (ObjectDisposedException) { break; }
        }
        Assert.Throws<ObjectDisposedException>(() => { _ = response.Password!.Characters; });
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [TestCase(false)] [TestCase(true)]
    public void PublisherHonorsCommitBoundary(bool afterCommit)
    {
        var destination = SnapshotByteFilePublisher.Prepare(path, VariableSnapshotWriteMode.CreateNew);
        using var cts = new CancellationTokenSource();
        if (!afterCommit) cts.Cancel();
        void Publish() => SnapshotByteFilePublisher.Write(destination, new byte[] { 1, 2, 3 }, 3,
            VariableSnapshotWriteMode.CreateNew, cts.Token, (temp, target, mode) =>
            { SnapshotFilePublication.Publish(temp, target, mode); cts.Cancel(); });
        if (afterCommit) Assert.DoesNotThrow(Publish); else Assert.Throws<OperationCanceledException>(Publish);
        Assert.That(File.Exists(path.Value), Is.EqualTo(afterCommit));
        Assert.That(Directory.GetFiles(directory, ".cryptoscript-*.tmp"), Is.Empty);
    }

    [Test] public void PublisherRaceKeepsExistingFileAndCleansTemporary()
    {
        var destination = SnapshotByteFilePublisher.Prepare(path, VariableSnapshotWriteMode.CreateNew);
        Assert.Throws<VariableSnapshotFileException>(() => SnapshotByteFilePublisher.Write(destination, new byte[] { 1 }, 1,
            VariableSnapshotWriteMode.CreateNew, publish: (temp, target, mode) =>
            { File.WriteAllText(target, "winner"); SnapshotFilePublication.Publish(temp, target, mode); }));
        Assert.That(File.ReadAllText(path.Value), Is.EqualTo("winner"));
        Assert.That(Directory.GetFiles(directory, ".cryptoscript-*.tmp"), Is.Empty);
    }
}
