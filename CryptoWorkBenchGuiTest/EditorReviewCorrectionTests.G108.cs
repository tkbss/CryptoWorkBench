using AvaloniaEdit;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public partial class EditorReviewCorrectionTests
{
    [Test]
    public Task R3PreShutdownDrainCannotCertifyLaterCoordinatorCallbacks() => Ui(async () =>
    {
        await using var h = new H();
        h.Editor.Text = "confirmed";
        File.WriteAllText(h.FilePath, "sentinel");
        var intent = h.Vm.CaptureSaveIntent()!;
        var owner = h.Service.EditorCoordinator;
        // A real original obligation makes the otherwise timing-dependent Releasing interval
        // deterministic. No sleep, retry, private field mutation, or admission bypass.
        using var operation = owner.TryBegin()!;
        using var callback = operation.RegisterCallback();
        operation.Dispose();
        Task? beforeShutdown = null;
        Task? shutdown = null;
        bool called = false;
        int writes = 0;
        h.Editor.PropertyChanged += (_, args) =>
        {
            if (args.Property != TextEditor.IsReadOnlyProperty || !h.Editor.IsReadOnly || called) return;
            called = true;
            beforeShutdown = h.Service.DrainAsync();
            Assert.That(beforeShutdown.IsCompleted, Is.False);
            h.Service.BeginShutdown();
            shutdown = h.Service.DrainAsync();
            throw new IOException("lease callback");
        };
        try
        {
            await Assert.ThatAsync(async () => await h.Service.CommitSaveAsync(intent,
                new(h.FilePath, true, () => { writes++; return Task.CompletedTask; })), Throws.InstanceOf<IOException>());
            await beforeShutdown!;
            // Fence the queued BeginDrain callback, not the still-outstanding obligation.
            await UiDispatch.InvokeAsync(() => true);
            Assert.That(owner.Phase, Is.EqualTo(EditorControlPhase.Releasing));
            Assert.That(shutdown!.IsCompleted, Is.False);
            Assert.That(h.Service.IsBusy || h.Editor.MutationLocked || h.Editor.IsReadOnly || h.Editor.Document.IsInUpdate, Is.False);
            TestContext.Out.WriteLine($"earlyDrain={beforeShutdown.IsCompletedSuccessfully}; shutdownDrain={shutdown.IsCompleted}; phase={owner.Phase}; pending={owner.Pending}; active={owner.Active}; observer={owner.ObserverDepth}");
            var rejected = Assert.Throws<ExecutionBusyException>(() => h.Editor.Document.Insert(0, "after"));
            TestContext.Out.WriteLine(rejected!.ToString());
            Assert.That(h.Editor.Text, Is.EqualTo("confirmed"));
            Assert.That(h.Editor.Document.IsInUpdate, Is.False);
            Assert.That(writes, Is.Zero);
            Assert.That(File.ReadAllText(h.FilePath), Is.EqualTo("sentinel"));
            Assert.That(owner.TryBegin(), Is.Null, "Shutdown must not readmit producers.");
        }
        finally { callback.Dispose(); }
        await shutdown!;
        Assert.That(owner.Pending, Is.Zero);
        Assert.That(owner.Active, Is.False);
        h.Editor.Document.Insert(0, "after");
        Assert.That(h.Editor.Text, Is.EqualTo("afterconfirmed"));
        Assert.That(h.Service.IsStopping, Is.True);
        Assert.That(owner.TryBegin(), Is.Null, "Native orphan mutability is not new execution admission.");
    });
}
