using System.Collections;
using CryptoScript;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class StoreAdmissionTests
{
    internal static CryptoScriptParser.ProgramContext Parse(string script)
    {
        var parser = ParserBuilder.StringBuild(script);
        var program = parser.program();
        Assert.That(parser.NumberOfSyntaxErrors, Is.Zero, script);
        return program;
    }

    internal static void TraceStore(VariableDictionary store, string phase)
    {
        string roots = string.Join(", ", store.GetVariables().Select(v => v.Id + "=" + v.Type?.Name));
        TestContext.Out.WriteLine($"{phase}: process={VariableDictionary.ProcessInstanceId} book={store.BookId} session={store.SessionGeneration} state={store.VariableStateId} instance={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(store)} revision={store.Revision} roots=[{roots}]");
    }

    private sealed class ProbeSequence : IEnumerable<StatementNode?>
    {
        internal int Enumerations, Advances;
        public IEnumerator<StatementNode?> GetEnumerator()
        {
            Enumerations++;
            return Probe().GetEnumerator();
        }
        private IEnumerable<StatementNode?> Probe()
        {
            Advances++;
            yield break;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ProbeProgram() : CryptoScriptParser.ProgramContext(null!, -1)
    {
        internal int Visits;
        public override int ChildCount { get { Visits++; return 0; } }
    }

    [Test]
    public void PrintChildInheritsOriginAndRejectsEveryWrongStoreEntranceBeforeEffects()
    {
        var a = new VariableDictionary();
        var b = new VariableDictionary(a.BookId, a.VariableStateId);
        new CryptoScriptRunner(a).Execute(Parse("VAR shared=1"));
        new CryptoScriptRunner(b).Execute(Parse("VAR shared=2"));
        TraceStore(a, "A before");
        TraceStore(b, "B before");
        var outer = new CryptoScriptRunner(a);
        var wrong = new CryptoScriptRunner(b);
        var sequence = new ProbeSequence();
        var mapped = new ProbeProgram();
        int callbacks = 0;
        void Reenter(string output)
        {
            callbacks++;
            var parent = outer.ExecutionId;
            Assert.Throws<ExecutionBusyException>(() => wrong.Execute(sequence));
            Assert.Throws<ExecutionBusyException>(() => wrong.Execute(mapped));
            Assert.ThrowsAsync<ExecutionBusyException>(async () => await wrong.ExecuteAsync(sequence));
            Assert.ThrowsAsync<ExecutionBusyException>(async () => await wrong.ExecuteAsync(mapped));
            Assert.Throws<ExecutionBusyException>(() => CryptoType.Lexer = CryptoType.Lexer);
            Assert.Throws<ExecutionBusyException>(() => b.Get("value"));
            Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().Get("value"));
            Assert.That(a.Get("shared").Value, Is.EqualTo("1"));
            TraceStore(a, "A after wrong entrances, before admitted children");
            new CryptoScriptRunner().Execute(Parse("VAR inherited=0x(01)"));
            outer.Execute(Parse("VAR explicitChild=0x(02)"));
            Assert.That(outer.ExecutionId, Is.EqualTo(parent));
        }
        OutputOperations.PrintEvent += Reenter;
        try { outer.Execute(Parse("VAR value=0x(01) Print(value)")); }
        finally { OutputOperations.PrintEvent -= Reenter; }
        Assert.That((sequence.Enumerations, sequence.Advances, mapped.Visits, callbacks), Is.EqualTo((0, 0, 0, 1)));
        TraceStore(a, "A after child");
        TraceStore(b, "B after rejection");
        TestContext.Out.WriteLine($"GetEnumerator={sequence.Enumerations} MoveNext={sequence.Advances} mapping={mapped.Visits} parentPrint={callbacks}");
        Assert.That(b.Revision, Is.EqualTo(1));
        Assert.That(b.Get("shared").Value, Is.EqualTo("2"));
        wrong.Execute(Parse("VAR value=0x(02)"));
        Assert.That(b.Get("value").Value, Is.EqualTo("0x(02)"));
        Assert.That(a.Get("value").Value, Is.EqualTo("0x(01)"));
        Assert.That(a.Revision, Is.EqualTo(4));
        TraceStore(b, "B after independent run");
        Assert.That(a.SessionGeneration, Is.Not.EqualTo(b.SessionGeneration));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NestedFaultRestoresOuterAndInvalidatesCopiedBorrow(bool disposal)
    {
        var a = new VariableDictionary();
        var b = new CryptoScriptRunner(new VariableDictionary());
        TraceStore(a, "parent before"); TraceStore(b.Store, "independent store before");
        ExecutionContext? copied = null;
        IEnumerable<StatementNode?> Fail()
        {
            copied = ExecutionContext.Capture();
            try
            {
                if (!disposal) throw new OperationCanceledException();
                yield return null;
            }
            finally { if (disposal) throw new IOException("dispose"); }
        }
        void Child(string output)
        {
            Assert.Catch(() => new CryptoScriptRunner(a).Execute(Fail()));
            ExecutionContext.Run(copied!, _ =>
            {
                Assert.Throws<ExecutionBusyException>(() => a.Clear());
                Assert.Throws<ExecutionBusyException>(() => new CryptoScriptRunner().Execute(new ProbeSequence()));
            }, null);
            Assert.That(a.Contains("prior"), Is.True);
        }
        OutputOperations.PrintEvent += Child;
        try { new CryptoScriptRunner(a).Execute(Parse("VAR prior=1 Print(prior) VAR after=2")); }
        finally { OutputOperations.PrintEvent -= Child; }
        b.Execute(Parse("VAR independent=3"));
        TraceStore(a, "parent after unwind"); TraceStore(b.Store, "independent store after");
        Assert.That(a.Revision, Is.EqualTo(2));
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [Test]
    public async Task ExplicitHostDesignatesRunnerAndRejectsFlowedAndExpiredAuthorization()
    {
        var a = new VariableDictionary();
        var runner = new CryptoScriptRunner(a);
        var probe = new ProbeSequence();
        InterpreterExecution.HostAdmission? saved = null;
        TraceStore(a, "host before");
        var program = Parse("VAR data=0x(11)");
        await InterpreterExecution.RunAsync(a, async host =>
        {
            saved = host;
            await Task.Run(async () =>
            {
                Assert.Throws<ExecutionBusyException>(() => InterpreterExecution.AccessState(() => a.GetVariables()));
                Assert.ThrowsAsync<ExecutionBusyException>(async () => await runner.ExecuteAsync(probe));
                await Task.Yield();
            });
            await runner.ExecuteAsync(host, program);
            return true;
        });
        Assert.ThrowsAsync<ExecutionBusyException>(async () => await runner.ExecuteAsync(saved!, probe));
        Assert.That(probe.Enumerations, Is.Zero);
        TraceStore(a, "host after");
        Assert.That(a.Get("data").Value, Is.EqualTo("0x(11)"));
    }

    [Test]
    public async Task UnqualifiedHostPermitsDirectRunnerEntryAndRejectsAnInheritedTask()
    {
        var runner = new CryptoScriptRunner();
        var probe = new ProbeSequence();
        await InterpreterExecution.RunAsync(async () =>
        {
            await runner.ExecuteAsync(probe);
            await Task.Run(async () =>
            {
                Assert.ThrowsAsync<ExecutionBusyException>(async () => await runner.ExecuteAsync(probe));
                await Task.Yield();
            });
            return true;
        });
        Assert.That((probe.Enumerations, probe.Advances), Is.EqualTo((1, 1)));
    }

    [Test]
    public async Task LatePasswordCallbackCannotReadOrStartTheNextBook()
    {
        var a = new VariableDictionary();
        var b = new VariableDictionary();
        var runner = new CryptoScriptRunner(a);
        var wrong = new CryptoScriptRunner(b);
        var probe = new ProbeSequence();
        TraceStore(a, "dialog A before"); TraceStore(b, "dialog B before");
        var dialog = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        runner.PasswordRequested += async (_, request) =>
        {
            dialog.SetResult();
            await release.Task;
            try
            {
                Assert.Throws<ExecutionBusyException>(() => wrong.Execute(probe));
                Assert.Throws<ExecutionBusyException>(() => b.Get("other"));
                observed.SetResult();
            }
            catch (Exception error) { observed.SetException(error); }
            return PasswordResponse.UserCancelled(request.RequestId);
        };
        var running = runner.ExecuteAsync([new AsyncInfrastructureStatement(async context =>
        {
            using var response = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad);
        })], cancellation.Token);
        await dialog.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        try { await running; Assert.Fail("Expected cancellation"); }
        catch (OperationCanceledException) { }
        await InterpreterExecution.RunAsync(b, async host =>
        {
            release.SetResult();
            await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await wrong.ExecuteAsync(host, probe);
            return true;
        });
        Assert.That(probe.Enumerations, Is.EqualTo(1));
        TraceStore(a, "dialog A after"); TraceStore(b, "dialog B after");
        Assert.That((a.Revision, b.Revision), Is.EqualTo((0L, 0L)));
    }

    [Test]
    public async Task HostReleaseDrainsAnAlreadyStartedRunner()
    {
        var store = new VariableDictionary();
        var runner = new CryptoScriptRunner(store);
        TraceStore(store, "host before drain");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? child = null;
        var hostTask = InterpreterExecution.RunAsync(store, host =>
        {
            child = runner.ExecuteAsync(host, [new AsyncInfrastructureStatement(async _ =>
            {
                started.SetResult();
                await finish.Task;
            })]);
            return Task.FromResult(true);
        });
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(hostTask.IsCompleted, Is.False);
            Assert.Throws<ExecutionBusyException>(() => new CryptoScriptRunner().Execute(new ProbeSequence()));
        }
        finally { finish.TrySetResult(); }
        await hostTask;
        await child!;
        TraceStore(store, "host after release");
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    [Test]
    public void RevocationSurvivesReopenWithMatchingPersistentIds()
    {
        var old = new VariableDictionary();
        var runner = new CryptoScriptRunner(old);
        var fresh = new VariableDictionary(old.BookId, old.VariableStateId);
        TraceStore(old, "old session before retirement"); TraceStore(fresh, "new session before");
        old.Retire();
        var probe = new ProbeSequence();
        Assert.Throws<ExecutionBusyException>(() => runner.Execute(probe));
        Assert.ThrowsAsync<ExecutionBusyException>(async () => await runner.ExecuteAsync(probe));
        Assert.Throws<ExecutionBusyException>(() => old.GetVariables());
        Assert.That(probe.Enumerations, Is.Zero);
        new CryptoScriptRunner(fresh).Execute(Parse("VAR reopened=1"));
        TraceStore(fresh, "new session after");
    }

    [Test]
    public void TransferPinsBothEndsAndCannotExecuteScriptOrOutliveTheAdmission()
    {
        var a = new VariableDictionary();
        var b = new VariableDictionary();
        TraceStore(a, "transfer source before");
        TraceStore(b, "transfer destination before");
        var child = new ProbeSequence();
        void Attempt(string output) =>
            Assert.Throws<ExecutionBusyException>(() => InterpreterExecution.RunTransfer(a, b, 1, 0, _ => Assert.Fail()));
        OutputOperations.PrintEvent += Attempt;
        try { new CryptoScriptRunner(a).Execute(Parse("VAR data=0x(01) Print(data)")); }
        finally { OutputOperations.PrintEvent -= Attempt; }
        InterpreterExecution.Transfer? saved = null;
        InterpreterExecution.RunTransfer(a, b, a.Revision, b.Revision, transfer =>
        {
            saved = transfer;
            Assert.Throws<ExecutionBusyException>(() => new CryptoScriptRunner(a).Execute(child));
            Assert.Throws<ExecutionBusyException>(() => new CryptoScriptRunner(b).Execute(child));
            Assert.Throws<ExecutionBusyException>(() => a.Clear());
            Assert.Throws<ExecutionBusyException>(() => b.Get("data"));
            var snapshot = transfer.Export();
            Assert.Throws<InvalidOperationException>(() => transfer.Import(new InterpreterExecution.Transfer.ExportedGraph(snapshot.Json)));
            transfer.Import(snapshot);
            Assert.Catch(() => transfer.Import(snapshot));
        });
        Assert.Throws<ExecutionBusyException>(() => saved!.Export());
        Assert.That((child.Enumerations, child.Advances), Is.EqualTo((0, 0)));
        TraceStore(a, "transfer source after");
        TraceStore(b, "transfer destination after");
        Assert.That(b.Get("data"), Is.Not.SameAs(a.Get("data")));
        Assert.That((a.Revision, b.Revision), Is.EqualTo((1L, 1L)));
    }
}
