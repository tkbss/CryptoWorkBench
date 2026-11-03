using CryptoScript.Model;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B3F4Tests
{
    [TestCase("first", false), TestCase("second", false), TestCase("replace", false),
     TestCase("activate", false), TestCase("replace", true), TestCase("activate", true)]
    public Task ViewPublicationAllowsL1BeforeObserverReturns(string change, bool shutdown) => Ui(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var a = new WorkbenchTextEditor { Text = "A" };
        var b = new WorkbenchTextEditor { Text = "B" };
        var owner = service.EditorCoordinator;
        var initial = change == "first" ? null : service.AttachEditor(a, null!);
        if (change == "activate") service.AttachEditor(b, null!, true);
        service.ActivateMcp();
        var phases = new List<EditorControlPhase>();
        bool returned = false, pending = false;
        Task? drain = null;
        void Changed()
        {
            phases.Add(owner.Phase);
            if (phases.Count != 1) return;
            var l1 = Task.Run(() =>
            {
                if (shutdown) service.BeginShutdown(); else service.DisableMcp();
                drain = service.DrainAsync();
            });
            returned = l1.Wait(TimeSpan.FromSeconds(3));
            pending = drain is { IsCompleted: false };
        }
        owner.WorkspaceChanged += Changed;
        try
        {
            switch (change)
            {
                case "activate": service.ActivateEditor(initial!); break;
                case "replace": Assert.Throws<ExecutionBusyException>(() => service.AttachEditor(b, null!)); break;
                default: service.AttachEditor(b, null!, true); break;
            }
        }
        finally { owner.WorkspaceChanged -= Changed; }
        Assert.That(phases, Is.EqualTo(new[] { change == "replace" ? EditorControlPhase.Releasing : EditorControlPhase.Gui }));
        Assert.That((returned, pending), Is.EqualTo((true, true)));
        await drain!.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That((a.Text, b.Text, owner.Pending), Is.EqualTo(("A", "B", 0)));
        if (change == "replace") Assert.That(initial!.Attached, Is.False);
        if (!shutdown)
        {
            var target = change == "activate" ? a : b;
            if (change == "replace") service.AttachEditor(target, null!);
            target.CaretOffset = 1;
            target.TextArea.PerformTextInput("!");
            Assert.That(target.Text, Is.EqualTo(change == "activate" ? "A!" : "B!"));
        }
    });

    [Test]
    public Task LegacyGuiEditPublishesAdmissionBeforeNativeObservers() => Ui(async () =>
    {
        await using var h = new Host("A");
        var service = h.Service;
        service.ActivateMcp();
        var before = h.Registration.Snapshot();
        bool returned = false;
        Task? drain = null;
        void Revoke(object? sender, EventArgs args)
        {
            var worker = Task.Run(() => { service.DisableMcp(); drain = service.DrainAsync(); });
            returned = worker.Wait(TimeSpan.FromSeconds(3)) && drain is { IsCompleted: false };
        }
        h.Editor.Document.UpdateStarted += Revoke;
        try
        {
            var result = await service.UpdateEditorAsync(WorkbenchAutomationService.LocalCaller,
                new(service.InstanceId, before.EditorId, before.Revision, EditorEditMode.Append, "!"), default);
            Assert.That((returned, result.Ok, h.Editor.Text), Is.EqualTo((true, true, "A!")));
            await drain!.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(h.Owner.Pending, Is.Zero);
        }
        finally { h.Editor.Document.UpdateStarted -= Revoke; }
    });

    [Test]
    public Task DetachDuringActivationSupersedesThePendingPublication() => Ui(async () =>
    {
        await using var h = new Host("A");
        var other = new WorkbenchTextEditor { Text = "B" };
        var retained = h.Service.AttachEditor(other, null!, true)!;
        var observed = new List<EditorControlPhase>();
        void Retire()
        {
            if (h.Owner.Targets(h.Editor)) h.Service.DetachEditor(h.Registration);
        }
        void Receive() => observed.Add(h.Owner.Phase);
        h.Owner.WorkspaceChanged += Retire;
        h.Owner.WorkspaceChanged += Receive;
        try
        {
            Assert.Throws<ExecutionBusyException>(() => h.Service.ActivateEditor(h.Registration));
            Assert.That(observed, Is.EqualTo(new[] { EditorControlPhase.Unavailable }));
            Assert.That(h.Registration.Attached, Is.False);
            h.Service.ActivateEditor(retained);
            Assert.That(observed, Is.EqualTo(new[] { EditorControlPhase.Unavailable, EditorControlPhase.Gui }));
            Assert.That(h.Owner.Targets(other), Is.True);
        }
        finally { h.Owner.WorkspaceChanged -= Retire; h.Owner.WorkspaceChanged -= Receive; }
    });

    [Test]
    public Task ReplacementPublishesRetirementAndSuccessorOnceAndKnownAttachIsSilent() => Ui(async () =>
    {
        await using var h = new Host("A");
        var incoming = new WorkbenchTextEditor { Text = "B" };
        var observations = new List<(EditorControlPhase, bool, bool)>();
        void Changed() => observations.Add((h.Owner.Phase, h.Owner.Targets(h.Editor), h.Owner.Targets(incoming)));
        h.Owner.WorkspaceChanged += Changed;
        try
        {
            var next = h.Service.AttachEditor(incoming, null!);
            Assert.That(h.Service.AttachEditor(incoming, null!), Is.SameAs(next));
            Assert.That(observations, Is.EqualTo(new[]
            {
                (EditorControlPhase.Releasing, true, false), (EditorControlPhase.Gui, false, true)
            }));
            Assert.That(h.Registration.Attached, Is.False);
            Assert.Throws<ExecutionBusyException>(() => h.Service.ActivateEditor(h.Registration));
            Assert.That((h.Editor.Text, incoming.Text), Is.EqualTo(("A", "B")));
        }
        finally { h.Owner.WorkspaceChanged -= Changed; }
    });
}
