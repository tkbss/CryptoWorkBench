using System.Reflection;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B4F1LifecycleTests
{
    [TestCase(false)]
    [TestCase(true)]
    public Task DisconnectObservesOriginalTicketAndDrainFault(bool fault) => Ui(async () =>
    {
        await using var h = new Host("original");
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("f1", WorkbenchAutomationService.LocalCaller.Permissions);
        var held = h.Owner.TryBegin()!;
        var states = new List<McpDisconnectState>();
        bool revoked = false;
        // A real service epoch close remains responsible for the actual editor.
        // Inject failure only after that original close has quiesced.
        async Task Close()
        {
            await h.Service.DisableMcpAndDrainAsync();
            if (fault) throw new IOException("PRIVATE_DRAIN_CANARY");
        }
        var disconnect = new McpDisconnect(() => revoked = true, Close, () => Task.CompletedTask, states.Add);
        var completion = disconnect.RunAsync();
        Assert.That(disconnect.RunAsync(), Is.SameAs(completion));
        Assert.That(revoked, Is.True);
        Assert.That((await h.Service.GetStatusAsync(caller, default)).Error!.Code, Is.EqualTo(AutomationErrorCode.Forbidden));
        Assert.That(completion.IsCompleted, Is.False);
        Assert.That(states.Last(), Is.EqualTo(McpDisconnectState.WaitingForDrain));
        Assert.That(h.Owner.CanStartGui, Is.False);
        held.Dispose();
        if (fault)
        {
            await Assert.ThatAsync(async () => await completion.WaitAsync(TimeSpan.FromSeconds(5)),
                Throws.TypeOf<InvalidOperationException>().With.Message.Not.Contains("PRIVATE_DRAIN_CANARY"));
            Assert.That(states.Last(), Is.EqualTo(McpDisconnectState.SafeFault));
            Assert.That(states, Does.Not.Contain(McpDisconnectState.Disconnected));
        }
        else
        {
            await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(states.Last(), Is.EqualTo(McpDisconnectState.Disconnected));
            Assert.That(h.Owner.CanStartGui, Is.True);
        }
    });

    [Test]
    public Task FailedOriginalDrainLeavesEditorProtected() => Ui(async () =>
    {
        var h = new Host("original");
        h.Service.ActivateMcp();
        var held = h.Owner.TryBegin()!;
        // Inject the registry's real resource-cleanup failure, after original work was captured.
        var states = new List<McpDisconnectState>();
        var disconnect = new McpDisconnect(() => { }, h.Service.DisableMcpAndDrainAsync,
            () => Task.CompletedTask, states.Add);
        var completion = disconnect.RunAsync();
        typeof(WorkbenchAutomationService).GetField("leaseCleanupFailed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(h.Service, true);
        held.Dispose();
        await Assert.ThatAsync(async () => await completion.WaitAsync(TimeSpan.FromSeconds(5)), Throws.TypeOf<InvalidOperationException>());
        Assert.That(states.Last(), Is.EqualTo(McpDisconnectState.SafeFault));
        Assert.That(h.Owner.CanStartGui, Is.False);
        await Assert.ThatAsync(async () => await h.DisposeAsync(), Throws.TypeOf<InvalidOperationException>());
    });

    [Test]
    public Task CompletedEpochDoesNotHideAnExistingNativeFault() => Ui(async () =>
    {
        await using var h = new Host("original");
        h.Service.ActivateMcp();
        typeof(EditorCoordinator).GetProperty("Phase", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(h.Owner, EditorControlPhase.Faulted);
        var states = new List<McpDisconnectState>();
        var disconnect = new McpDisconnect(() => { }, h.Service.DisableMcpAndDrainAsync,
            () => Task.CompletedTask, states.Add);
        await Assert.ThatAsync(async () => await disconnect.RunAsync().WaitAsync(TimeSpan.FromSeconds(5)),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(states.Last(), Is.EqualTo(McpDisconnectState.SafeFault));
        Assert.That(h.Owner.CanStartGui, Is.False);
    });

    [Test]
    public Task ShutdownShowsWaitingAfterFiveSecondsAndThenCloses() => Ui(async () =>
    {
        await using var h = new Host("original");
        var held = h.Owner.TryBegin()!;
        var messages = new List<string>();
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Window.Closed += (_, _) => closed.TrySetResult();
        App.CoordinateClosing(h.Window, h.Service, reportShutdown: messages.Add);
        try
        {
            h.Window.Close();
            await Task.Delay(TimeSpan.FromSeconds(5.2));
            Assert.That(messages.Single(), Does.Contain("waiting for pending work"));
            Assert.That(closed.Task.IsCompleted, Is.False);
            Assert.That(h.Owner.CanStartGui, Is.False);
            held.Dispose();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally { held.Dispose(); }
    });

    [Test]
    public Task ShutdownFailureIsVisibleAndDoesNotClose() => Ui(async () =>
    {
        await using var h = new Host("original");
        var messages = new List<string>();
        bool fail = true;
        App.CoordinateClosing(h.Window, h.Service,
            () => fail ? Task.FromException(new IOException("PRIVATE_SHUTDOWN_CANARY")) : Task.CompletedTask, messages.Add);
        h.Window.Close();
        await Task.Yield();
        Assert.That(messages.Last(), Does.Contain("safe shutdown failed").And.Not.Contain("PRIVATE_SHUTDOWN_CANARY"));
        Assert.That(h.Window.IsVisible, Is.True);
        Assert.That(h.Owner.CanStartGui, Is.False);
        fail = false;
        h.Window.Close();
    });
}
