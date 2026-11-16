using CryptoScript.Model;
using CryptoScript.Variables;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class HeadlessAsyncLifetimeTests
{
    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Test]
    public async Task RawTaskDispatchCompletesWhileInterpreterOwnerIsStillActive()
    {
        var entered = Gate();
        var release = Gate();
        // Exact overload selected by the original ThemeTests lambda. The explicit
        // type is also a compile-time proof that Dispatch does not unwrap this task.
        Task<Task> outer = GuiTestSession.Session.Dispatch(async () =>
        {
            await Task.Run(() => InterpreterExecution.RunAsync(async () =>
            {
                entered.SetResult();
                await release.Task.ConfigureAwait(false);
                return true;
            })).ConfigureAwait(false);
        }, CancellationToken.None);
        Task? inner = null;
        try
        {
            inner = await outer.WaitAsync(TimeSpan.FromSeconds(10));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(inner.IsCompleted, Is.False);
            Assert.That(InterpreterExecution.IsBusy, Is.True);
            Assert.Throws<ExecutionBusyException>(() => VariableDictionary.Instance().GetVariables());
        }
        finally
        {
            release.TrySetResult();
            inner ??= await outer;
            await inner.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.That(InterpreterExecution.IsBusy, Is.False);
        Assert.DoesNotThrow(() => VariableDictionary.Instance().GetVariables());
    }

    [Test]
    public async Task AsyncDispatchWaitsForBodyAndAsyncDisposalAndPropagatesLateFailure()
    {
        var bodyEntered = Gate();
        var releaseBody = Gate();
        var disposeEntered = Gate();
        var releaseDispose = Gate();
        var failure = new InvalidOperationException("late-test-failure");
        var work = GuiTestSession.RunAsync(async () =>
        {
            await using var resource = new GatedCleanup(disposeEntered, releaseDispose);
            await Task.Run(() => InterpreterExecution.RunAsync(async () =>
            {
                bodyEntered.SetResult();
                await releaseBody.Task.ConfigureAwait(false);
                return true;
            }));
            throw failure;
        });
        Exception? observed = null;
        try
        {
            await bodyEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(work.IsCompleted, Is.False);
            Assert.That(InterpreterExecution.IsBusy, Is.True);
            releaseBody.SetResult();
            await disposeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.That(work.IsCompleted, Is.False, "async disposal must finish before NUnit receives completion");
            Assert.That(InterpreterExecution.IsBusy, Is.False);
        }
        finally
        {
            releaseBody.TrySetResult();
            releaseDispose.TrySetResult();
            try { await work.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (InvalidOperationException error) when (ReferenceEquals(error, failure)) { observed = error; }
        }
        Assert.That(observed, Is.SameAs(failure));
        Assert.That(InterpreterExecution.IsBusy, Is.False);
    }

    private sealed class GatedCleanup(TaskCompletionSource entered, TaskCompletionSource release) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            entered.SetResult();
            await release.Task;
        }
    }
}
