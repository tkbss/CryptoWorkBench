using System.Collections;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;
using static CryptoScriptUnitTest.StoreAdmissionTests;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class P1CorrectionTests
{
    private VariableDictionary store = null!;
    private static VariableDictionary Compatibility => VariableDictionary.Instance();
    private VariableDeclaration[] saved = [];

    [SetUp]
    public void SetUp()
    {
        store = new();
        saved = Compatibility.GetVariables().ToArray();
        Compatibility.Clear();
    }

    [TearDown]
    public void Restore()
    {
        Compatibility.Clear();
        foreach (var root in saved) Compatibility.Add(root);
    }

    private static StatementNode Node(string script) => AntlrToStatement.Map(Parse(script).statement(0))!;

    [Test]
    public async Task StateReadsCannotBecomeScriptExecutionOrWrites()
    {
        var node = Node("VAR unexpected=1");
        var errors = new List<SemanticError>();
        var original = new StringVariableDeclaration { Id = "first", Value = "saved" };
        Compatibility.Add(original);
        long before = Compatibility.Revision;
        await InterpreterExecution.RunAsync(async () =>
        {
            InterpreterExecution.AccessState(() =>
            {
                Assert.That(Compatibility.Get("first"), Is.SameAs(original));
                Assert.Throws<ExecutionBusyException>(() => StatementEvaluator.Evaluate(node, errors));
                Assert.Throws<ExecutionBusyException>(() => Compatibility.Clear());
                Assert.Throws<ExecutionBusyException>(() => original.Value = "changed");
                Assert.Throws<ExecutionBusyException>(() => new OutputOperations().Print(["other"]));
                return true;
            });
            await new CryptoScriptRunner().ExecuteAsync(new StatementNode?[] { null });
            await Task.Run(() =>
            {
                Assert.Throws<ExecutionBusyException>(() => InterpreterExecution.AccessState(() => true));
                Assert.Throws<ExecutionBusyException>(() => StatementEvaluator.Evaluate(node, errors));
            });
            return true;
        });
        Assert.That((Compatibility.Revision, original.Value, errors.Count), Is.EqualTo((before, "saved", 0)));
        Assert.That(Compatibility.Contains("unexpected"), Is.False);
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [Test]
    public void ActiveCompatibilityAddPreservesKeyAndReservationThenIdleRestoreSucceeds()
    {
        var key = new KeyVariableDeclaration { Id = "old", Type = new CryptoTypeKey(), Value = "0x(01)", KeySize = "008" };
        Compatibility.Add(key);
        var before = Compatibility.Revision;
        var runner = new CryptoScriptRunner(Compatibility, (_, _) =>
        {
            Assert.Throws<InvalidOperationException>(() => Compatibility.Add(new StringVariableDeclaration { Id = "old" }));
            Assert.Throws<InvalidOperationException>(() => Compatibility.Add(new StringVariableDeclaration { Id = "pending" }));
            Assert.That(Compatibility.Revision, Is.EqualTo(before));
            Assert.That(Compatibility.Get("old"), Is.SameAs(key));
            Assert.That((key.Type!.Name, key.Value, key.KeySize, key.KeySizeInBits.Bits),
                Is.EqualTo(("KEY", "0x(01)", "008", 8UL)));
        });
        runner.Execute(Parse("VAR pending=Compare(1,1)"));
        Compatibility.Add(new StringVariableDeclaration { Id = "old" });
        Compatibility.Add(key);
        Assert.That(Compatibility.Get("old"), Is.SameAs(key));
        Assert.That(Compatibility.Revision, Is.EqualTo(before + 3));
    }

    [Test]
    public void ParameterRevisionUsesMapAndPublishesOnce()
    {
        var p = new ParameterVariableDeclaration { Id = "p", Mechanism = "AES-CBC" };
        p.SetParameter("IV", "0x(01)");
        store.Add(p);
        var map = p.GetParameters();
        map["#IV"] = "0x(02)";
        p.ReplaceParameters(map);
        long before = store.Revision;
        string text = p.Value;
        p.SetParameter("IV", "0x(01)");
        Assert.That(store.Get("p"), Is.SameAs(p));
        Assert.That((p.GetParameter("IV"), p.Value, store.Revision), Is.EqualTo(("0x(01)", text, before + 1)));
        p.SetParameter("IV", "0x(01)");
        Assert.Throws<ArgumentException>(() => p.SetInstance("#IV:0x(03)#UNKNOWN:value"));
        Assert.That((p.GetParameter("IV"), p.Value, store.Revision), Is.EqualTo(("0x(01)", text, before + 1)));
    }

    [Test]
    public void PinPairComparesBothMapsAndValidatesBeforeEitherPublication()
    {
        var source = new ParameterVariableDeclaration { Id = "s" };
        var target = new ParameterVariableDeclaration { Id = "t" };
        source.SetParameter("FILL", "good"); target.SetParameter("RANDOM", "good");
        store.Add(source); store.Add(target);
        var s = source.StagePinTranslation(); var t = target.StagePinTranslation();
        source.ReplaceParameters(new Dictionary<string,string>(source.GetParameters()) { ["#FILL"] = "old" });
        target.ReplaceParameters(new Dictionary<string,string>(target.GetParameters()) { ["#RANDOM"] = "old" });
        long before = store.Revision;
        var foreign = new VariableDictionary();
        var wrong = new ParameterVariableDeclaration { Id = "wrong" }; foreign.Add(wrong);
        Assert.Throws<InvalidOperationException>(() => ParameterVariableDeclaration.CommitPinTranslation(source, s, wrong, t));
        Assert.That((source.GetParameter("FILL"), target.GetParameter("RANDOM"), store.Revision), Is.EqualTo(("old", "old", before)));
        ParameterVariableDeclaration.CommitPinTranslation(source, s, target, t);
        ParameterVariableDeclaration.CommitPinTranslation(source, s, target, t);
        Assert.That((source.GetParameter("FILL"), target.GetParameter("RANDOM"), store.Revision), Is.EqualTo(("good", "good", before + 1)));
    }

    [Test]
    public void KeyTextAndNumericSizeCommitAsOneMutation()
    {
        var key = new KeyVariableDeclaration { Id = "k", Value = "0x(01)" };
        store.Add(key);
        var before = store.Revision;
        key.KeySize = "0128";
        Assert.That((key.KeySize, key.KeySizeInBits.Bits, store.Revision), Is.EqualTo(("0128", 128UL, before + 1)));
        key.KeySizeInBits = key.KeySizeInBits;
        key.KeySizeInBits = key.KeySizeInBits;
        key.KeySize = "128";
        Assert.Throws<ArgumentException>(() => key.KeySize = "256");
        Assert.That((key.KeySize, key.KeySizeInBits.Bits, key.Value, store.Revision), Is.EqualTo(("128", 128UL, "0x(01)", before + 2)));
    }

    [TestCase(false, false), TestCase(false, true), TestCase(true, false), TestCase(true, true)]
    public void TransferKeepsNamedHeaderIdentityInEitherRootOrder(bool compatibility, bool parameterFirst)
    {
        var builder = new VariableDictionary();
        new CryptoScriptRunner(builder).Execute(Parse("TR31H h=CreateHeader(KBVID:D,KEYU:D0,ALGO:A,MODEU:B,EXP:E) PARAM p=#MECH:WRAP-AES-TR31 #BLKH:h"));
        var codec = new CryptoScript.Serialization.VariableSnapshotJsonSerializer();
        store.PublishBatch(codec.Deserialize(codec.Serialize(parameterFirst ? [builder.Get("p"), builder.Get("h")] : [builder.Get("h"), builder.Get("p")])));
        Assert.That(store.GetVariables().First().Id, Is.EqualTo(parameterFirst ? "p" : "h"));
        var dest = compatibility ? Compatibility : new VariableDictionary();
        new CryptoScriptRunner(dest).Execute(Parse("VAR prior=1"));
        var prior = dest.Get("prior");
        var before = dest.Revision;
        void Transfer() => InterpreterExecution.RunTransfer(store, dest, store.Revision, dest.Revision, permit => permit.Import(permit.Export()));
        Transfer();
        Assert.That(((ParameterVariableDeclaration)dest.Get("p")).HeaderReference, Is.SameAs(dest.Get("h")).And.Not.SameAs(store.Get("h")));
        var conflicts = new VariableDictionary();
        new CryptoScriptRunner(conflicts).Execute(Parse("VAR extra=1 VAR prior=2"));
        Assert.Throws<ArgumentException>(() => InterpreterExecution.RunTransfer(conflicts, dest, conflicts.Revision,
            dest.Revision, permit => permit.Import(permit.Export())));
        Assert.That(dest.Contains("extra"), Is.False);
        Assert.That(dest.Get("prior"), Is.SameAs(prior));
        Assert.That(dest.Revision, Is.EqualTo(before + 1));
    }

    [Test]
    public void FreshCompareIsDispatchedButItsDuplicateHasNoEffect()
    {
        int dispatch = 0;
        var runner = new CryptoScriptRunner(store, (call, _) =>
        {
            Assert.That(call.Name, Is.EqualTo("Compare"));
            dispatch++;
        });
        const string script = "VAR item=Compare(1,1)";
        runner.Execute(Parse(script));
        var original = store.Get("item");
        Assert.That(original.Value, Is.EqualTo("Values are equal"));
        Assert.That(dispatch, Is.EqualTo(1));
        dispatch = 0;
        var revision = store.Revision;
        var error = Assert.Throws<SemanticErrorException>(() => runner.Execute(Parse(script)));
        Assert.That(error!.SemanticError!.Message, Is.EqualTo("Variable 'item' is already declared."));
        Assert.That((dispatch, store.Revision), Is.EqualTo((0, revision)));
        Assert.That(store.Get("item"), Is.SameAs(original));
    }

    [TestCase(false), TestCase(true)]
    public void SynchronousOutputSubscriberCanReenterItsOwnStore(bool info)
    {
        int calls = 0;
        void Reenter(string _)
        {
            calls++;
            new CryptoScriptRunner().Execute(Parse("VAR nested=1"));
        }
        if (info) OutputOperations.InfoEvent += Reenter; else OutputOperations.PrintEvent += Reenter;
        try { new CryptoScriptRunner(store).Execute(Parse(info ? "Info(keymap)" : "Print(1)")); }
        finally { if (info) OutputOperations.InfoEvent -= Reenter; else OutputOperations.PrintEvent -= Reenter; }
        Assert.That((calls, store.Get("nested").Value, store.Revision), Is.EqualTo((1, "1", 1L)));
        Assert.That(Compatibility.Contains("nested"), Is.False);
    }

    [Test]
    public async Task DuplicateSecureSaveStopsBeforePreflightAndPassword()
    {
        string destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        store.Add(new PathVariableDeclaration { Id = "path", Type = new CryptoTypePath(), Value = destination });
        var boundaries = new List<OperationBoundary>();
        var runner = new CryptoScriptRunner(store) { ObserveBoundary = boundaries.Add };
        runner.Execute(Parse("VAR item=1"));
        var original = store.Get("item");
        long revision = store.Revision;
        int prompts = 0;
        runner.PasswordRequested += (_, r) =>
        {
            prompts++;
            return Task.FromResult(PasswordResponse.UserCancelled(r.RequestId));
        };
        try
        {
            Assert.ThrowsAsync<SecurePasswordCancelledException>(async () => await runner.ExecuteAsync(Parse("SecureSave(path,item)")));
            Assert.That(boundaries.Count(b => b == OperationBoundary.SecureSavePreflight), Is.EqualTo(1));
            Assert.That(prompts, Is.EqualTo(1));
            boundaries.Clear(); prompts = 0;
            try { await runner.ExecuteAsync(Parse("PATH item=SecureSave(path,item)")); Assert.Fail(); }
            catch (SemanticErrorException e) { Assert.That(e.SemanticError!.Message, Is.EqualTo("Variable 'item' is already declared.")); }
            Assert.That((boundaries.Count, prompts), Is.EqualTo((0, 0)));
            Assert.That(store.Get("item"), Is.SameAs(original));
            Assert.That(store.Revision, Is.EqualTo(revision));
            Assert.That(File.Exists(destination), Is.False);
        }
        finally { File.Delete(destination); }
    }

    [TestCase(false), TestCase(true)]
    public void TransferInvokesNoOutputSubscribersAndIdleOutputStillPublishes(bool info)
    {
        new CryptoScriptRunner(store).Execute(Parse("VAR payload=1"));
        int events = 0;
        var raw = new OutputOperations();
        void Publish() { if (info) raw.Info(["keymap"]); else raw.Print(["standalone"]); }
        void Observe(string _) => events++;
        if (info) OutputOperations.InfoEvent += Observe; else OutputOperations.PrintEvent += Observe;
        try
        {
            InterpreterExecution.RunTransfer(store, Compatibility, store.Revision, Compatibility.Revision, transfer =>
            {
                Assert.Throws<ExecutionBusyException>(Publish);
                transfer.Import(transfer.Export());
            });
            Assert.That(events, Is.Zero);
            Publish();
            Assert.That(events, Is.EqualTo(1));
        }
        finally { if (info) OutputOperations.InfoEvent -= Observe; else OutputOperations.PrintEvent -= Observe; }
    }

    private sealed class DisposeFault(StatementNode node) : IEnumerable<StatementNode?>, IEnumerator<StatementNode?>
    {
        internal int Open, Move, Read, Close;
        internal readonly IOException Failure = new("fault from IEnumerator.Dispose");
        public IEnumerator<StatementNode?> GetEnumerator() { Open++; return this; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool MoveNext() => ++Move == 1;
        public StatementNode Current { get { Read++; return node; } }
        object IEnumerator.Current => Current;
        public void Dispose() { Close++; throw Failure; }
        public void Reset() => throw new NotSupportedException();
    }

    [TestCase(false), TestCase(true)]
    public async Task DisposeFailureRestoresParentAndReleasesNameAndGate(bool asynchronous)
    {
        var sequence = new DisposeFault(Node("VAR reusable=Compare(1,1)"));
        int reservations = 0;
        var child = new CryptoScriptRunner(store, (_, _) =>
        {
            Assert.That(store.IsReserved("reusable"), Is.True);
            reservations++;
            throw new ArgumentException("initializer failed");
        });
        if (asynchronous)
        {
            await InterpreterExecution.RunAsync(store, async host =>
            {
                try { await child.ExecuteAsync(host, sequence); Assert.Fail(); }
                catch (IOException e) { Assert.That(e, Is.SameAs(sequence.Failure)); }
                Assert.That(InterpreterExecution.AccessState(host, () => store.IsReserved("reusable")), Is.False);
                await child.ExecuteAsync(host, InterpreterExecution.AccessState(host, () => Parse("VAR reusable=1")));
                return true;
            });
        }
        else
        {
            void Reenter(string _)
            {
                Assert.That(Assert.Throws<IOException>(() => child.Execute(sequence)), Is.SameAs(sequence.Failure));
                Assert.That(store.IsReserved("reusable"), Is.False);
                child.Execute(Parse("VAR reusable=1"));
            }
            OutputOperations.PrintEvent += Reenter;
            try { new CryptoScriptRunner(store).Execute(Parse("Print(1)")); }
            finally { OutputOperations.PrintEvent -= Reenter; }
        }
        Assert.That((sequence.Open, sequence.Move, sequence.Read, sequence.Close, reservations), Is.EqualTo((1, 1, 1, 1, 1)));
        Assert.That(store.Get("reusable").Value, Is.EqualTo("1"));
        var b = new VariableDictionary();
        new CryptoScriptRunner(b).Execute(Parse("VAR independent=2"));
        Assert.That(b.Revision, Is.EqualTo(1));
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }
}
