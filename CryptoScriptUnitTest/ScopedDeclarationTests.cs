using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using static CryptoScriptUnitTest.StoreAdmissionTests;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class ScopedDeclarationTests
{
    [TestCase("VAR item=Print(item)")]
    [TestCase("KEY item=GenerateKey(AES-CBC,128)")]
    [TestCase("PARAM item=#MECH:AES-CBC #IV:unknown")]
    [TestCase("PATH item=SecureSave(path,item)")]
    [TestCase("PATH item=Save(path,item)")]
    [TestCase("TR31H item={KBVID:D;KEYU:D0;ALGO:A;MODEU:B;EXP:E;}")]
    [TestCase("TR31H item=CreateHeader(KBVID:D,KEYU:D0,ALGO:A,MODEU:B,EXP:E)")]
    [TestCase("SECRET item=Restore(unknown)")]
    [TestCase("VAL item=MacVerify(param,key,path)")]
    [TestCase("VAR item=0x(FF)")]
    public async Task DuplicatePrecedesEveryInitializer(string script)
    {
        var store = new VariableDictionary();
        int calls = 0, passwords = 0, outputs = 0;
        var runner = new CryptoScriptRunner(store, (_, _) => calls++);
        string filename = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        store.Add(new PathVariableDeclaration { Id = "path", Type = new CryptoTypePath(), Value = filename });
        runner.Execute(Parse("VAR item=0x(01) VAR iTem=0x(02)"));
        TraceStore(store, "before duplicate");
        var before = store.Get("item");
        long revision = store.Revision;
        runner.PasswordRequested += (_, _) => { passwords++; throw new AssertionException("Unexpected password"); };
        void Output(string _) => outputs++;
        OutputOperations.PrintEvent += Output;
        OutputOperations.InfoEvent += Output;
        try
        {
            try { await runner.ExecuteAsync(Parse(script)); Assert.Fail("Expected duplicate"); }
            catch (SemanticErrorException e) { Assert.That(e.SemanticError, Is.SameAs(runner.Diagnostics.Single())); }
            Assert.That(runner.Diagnostics.Single().Identifier, Is.EqualTo("item"));
            Assert.That(runner.Diagnostics.Single().Message, Does.Contain("already declared"));
            Assert.That((calls, passwords, outputs), Is.EqualTo((0, 0, 0)));
            Assert.That(File.Exists(filename), Is.False);
            Assert.That(store.Get("item"), Is.SameAs(before));
            Assert.That(before.Value, Is.EqualTo("0x(01)"));
            Assert.That(store.Revision, Is.EqualTo(revision));
            TraceStore(store, "after duplicate");
            TestContext.Out.WriteLine($"dispatch={calls} passwords={passwords} output={outputs} diagnostics={runner.Diagnostics.Count()} file={File.Exists(filename)}");
        }
        finally { OutputOperations.PrintEvent -= Output; OutputOperations.InfoEvent -= Output; File.Delete(filename); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReservationSurvivesNestedWorkAndReleasesOnInitializerFailure(bool fail)
    {
        var store = new VariableDictionary();
        TraceStore(store, "reservation before");
        var child = new CryptoScriptRunner(store);
        var runner = new CryptoScriptRunner(store, (_, _) =>
        {
            Assert.That(store.Contains("outer"), Is.False);
            Assert.Throws<SemanticErrorException>(() => child.Execute(Parse("VAR outer=Print(unknown)")));
            Assert.That(child.Diagnostics.Single().Identifier, Is.EqualTo("outer"));
            child.Execute(Parse("VAR child=0x(02)"));
            if (fail) throw new ArgumentException("initializer failure");
        });
        var input = Parse("VAR outer=Compare(1,1)");
        if (fail)
        {
            Assert.Throws<SemanticErrorException>(() => runner.Execute(input));
            Assert.That(store.Contains("outer"), Is.False);
            child.Execute(Parse("VAR outer=2"));
        }
        else runner.Execute(input);
        TraceStore(store, "reservation after");
        Assert.That(store.Get("child").Value, Is.EqualTo("0x(02)"));
        Assert.That(store.Revision, Is.EqualTo(2));
    }

    [Test]
    public async Task CancelledAndNoBindingDeclarationsReleaseTheirName()
    {
        var store = new VariableDictionary();
        TraceStore(store, "cancellation before");
        using var cancel = new CancellationTokenSource();
        var runner = new CryptoScriptRunner(store, (_, _) => cancel.Cancel());
        try { await runner.ExecuteAsync(Parse("VAR prior=1 VAR reused=Compare(1,1)"), cancel.Token); Assert.Fail(); }
        catch (OperationCanceledException) { }
        Assert.That(store.Revision, Is.EqualTo(1));
        var next = new CryptoScriptRunner(store);
        next.Execute(Parse("VAR reused=Check(0x(AABB)) VAR reused=0x(02)"));
        TraceStore(store, "no-binding retry after");
        Assert.That(next.Diagnostics.Single().DiagnosticCode, Is.EqualTo(SemanticDiagnosticCode.KeyCheckValueNotFound));
        Assert.That(store.Revision, Is.EqualTo(2));
    }

    [Test]
    public void AssignmentRevalidatesRootAfterReentry()
    {
        var store = new VariableDictionary();
        var child = new CryptoScriptRunner(store);
        child.Execute(Parse("VAR a=1"));
        TraceStore(store, "assignment before");
        var parent = new CryptoScriptRunner(store, (_, _) => child.Execute(Parse("a=2")));
        var changed = Assert.Throws<SemanticErrorException>(() => parent.Execute(Parse("a=Compare(1,1)")));
        Assert.That(changed!.SemanticError!.Message, Is.EqualTo("Assignment target changed during evaluation."));
        Assert.That(store.Get("a").Value, Is.EqualTo("2"));
        Assert.That(store.Revision, Is.EqualTo(2));
        Assert.Throws<SemanticErrorException>(() => child.Execute(Parse("a=GenerateKey(AES-CBC,128)")));
        Assert.Throws<SemanticErrorException>(() => child.Execute(Parse("undeclared=0x(01)")));
        Assert.That(store.Revision, Is.EqualTo(2));
        child.Execute(Parse("Print(a) Delete(a)"));
        Assert.That(store.Revision, Is.EqualTo(3));
        store.Clear();
        TraceStore(store, "assignment and delete after");
        Assert.That(store.Revision, Is.EqualTo(3));
    }
}
