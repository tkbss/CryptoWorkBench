using Avalonia.Headless;
using Avalonia.Threading;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Services;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class Avalonia12DispatcherTests
{
    private static async Task Ui(Func<Task> action)
    {
        var session = GuiTestSession.Session;
        await session.Dispatch(async () => { await action(); return true; }, CancellationToken.None);
    }

    [Test]
    public Task RawDispatcherCapturesWorkerContextButHostHandoffsDoNot() => Ui(async () =>
    {
        var ambient = new AsyncLocal<object?>();
        var sentinel = new object();
        await Task.Run(async () =>
        {
            ambient.Value = sentinel;
            Assert.That(await Dispatcher.UIThread.InvokeAsync(() => ambient.Value), Is.SameAs(sentinel),
                "positive control: Avalonia 12 captures ExecutionContext");
            Assert.That(await UiDispatch.InvokeAsync(() => ambient.Value), Is.Null);
            object? actionValue = sentinel;
            await UiDispatch.InvokeAsync(() => { actionValue = ambient.Value; });
            Assert.That(actionValue, Is.Null);
            Assert.That(await UiDispatch.InvokeAsync(async () => { await Task.Yield(); return ambient.Value; }), Is.Null);
            var posted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            UiDispatch.Post(() => posted.SetResult(ambient.Value));
            Assert.That(await posted.Task.WaitAsync(TimeSpan.FromSeconds(5)), Is.Null);
            Assert.That(ambient.Value, Is.SameAs(sentinel));
            Assert.That(ExecutionContext.IsFlowSuppressed(), Is.False);
        });
    });

    [Test]
    public Task HandoffDoesNotGrantWorkerInterpreterCapabilityToUi() => Ui(async () =>
    {
        await Task.Run(() => InterpreterExecution.RunAsync(async () =>
        {
            Assert.That(await Dispatcher.UIThread.InvokeAsync(() => InterpreterExecution.AccessState(() => true)), Is.True,
                "positive control: raw capture transports the ambient Core scope");
            bool denied = await UiDispatch.InvokeAsync(() =>
            {
                try { InterpreterExecution.AccessState(() => true); return false; }
                catch (ExecutionBusyException) { return true; }
            });
            Assert.That(denied, Is.True);
            Assert.That(InterpreterExecution.AccessState(() => true), Is.True,
                "the worker keeps its original capability");
            return true;
        }));
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    });

    [Test]
    public Task AlreadySuppressedFlowAndUiOriginKeepQueueAndExceptionSemantics() => Ui(async () =>
    {
        var ambient = new AsyncLocal<string?> { Value = "ui-origin" };
        DispatcherOperation<string?> operation;
        using (ExecutionContext.SuppressFlow())
        {
            operation = UiDispatch.InvokeAsync(() => ambient.Value);
            Assert.That(ExecutionContext.IsFlowSuppressed(), Is.True);
        }
        Assert.That(await operation, Is.Null);
        Assert.That(ambient.Value, Is.EqualTo("ui-origin"));
        Assert.That(ExecutionContext.IsFlowSuppressed(), Is.False);
        var failure = new InvalidOperationException("migration-probe");
        Exception? thrown = null;
        try { await UiDispatch.InvokeAsync((Action)(() => throw failure)); }
        catch (InvalidOperationException error) { thrown = error; }
        Assert.That(thrown, Is.SameAs(failure));
        Assert.That(ExecutionContext.IsFlowSuppressed(), Is.False);
    });
}
