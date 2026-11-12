using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockLayoutTests
{
    private static readonly LayoutWindow Bounds = new(80, 90, 1100, 730, true);
    private static Task Ui(Action test) => GuiTestSession.Session.Dispatch(() => { test(); return true; }, CancellationToken.None);
    private static Task Run(Func<DockEditorTests.Host, Task> test) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockEditorTests.Host(); await test(h); return true;
    }, CancellationToken.None);
    private static string Temp() => Path.Combine(TestContext.CurrentContext.WorkDirectory, "g107", Guid.NewGuid().ToString("N"), "layout.json");
    private static IDock Anchor(DockShellLayout layout, string id) => DockShellLayout.VisibleTree(layout.Root, false).OfType<IDock>().Single(d => d.Id == id);

    [Test] public Task MissingFileKeepsCompleteDefault() => Ui(() =>
    {
        Assert.That(new DockLayoutStore(Temp()).Load(), Is.Null);
        using var layout = new DockShellLayout(new());
        Assert.That(DockShellLayout.VisibleTree(layout.Root).Count(p => p is IDocument or ITool), Is.EqualTo(6));
        Assert.That(DockLayoutStore.Decode(layout.Capture(Bounds)).MainWindow, Is.EqualTo(Bounds));
    });

    [Test] public Task RoundtripPreservesOrderSplitsClosedToolsAndStableContexts() => Ui(() =>
    {
        var state = new DockShellState();
        using var layout = new DockShellLayout(state);
        layout.Move(DockShellState.OutputId, DockShellLayout.Diagnostics);
        layout.Close(DockShellState.StatusId);
        Anchor(layout, DockShellLayout.Tools).Proportion = .31;
        layout.Show(DockShellState.InfoId);
        var original = layout.Capture(Bounds);
        var store = new DockLayoutStore(Temp());
        Assert.That(store.Save(original), Is.True, store.LastError);
        for (int i = 0; i < 5; i++)
        {
            using var restored = new DockShellLayout(state);
            restored.RestoreAtStartup(store.Load()!);
            Assert.That(restored.Pane(DockShellState.DocumentId).Context, Is.SameAs(state.Document));
            foreach (var tool in state.Tools) Assert.That(restored.Pane(tool.Id).Context, Is.SameAs(tool));
            Assert.That(Anchor(restored, DockShellLayout.Tools).Proportion, Is.EqualTo(.31));
            Assert.That(Anchor(restored, DockShellLayout.Tools).ActiveDockable!.Id, Is.EqualTo(DockShellState.InfoId));
            Assert.That(Anchor(restored, DockShellLayout.Diagnostics).VisibleDockables!.Select(p => p.Id),
                Is.EqualTo(new[] { DockShellState.DiagnosticsId, DockShellState.OutputId }));
            Assert.That(DockShellLayout.VisibleTree(restored.Root).Contains(restored.Pane(DockShellState.StatusId)), Is.False);
            Assert.That(store.Save(restored.Capture(Bounds)), Is.True, store.LastError);
            Assert.That(File.ReadAllText(store.Path), Is.EqualTo(original));
            restored.Show(DockShellState.StatusId);
            Assert.That(restored.Pane(DockShellState.StatusId).Context, Is.SameAs(state.Tools.Last()));
        }
    });

    [Test] public Task JsonContainsOnlyPresentationNotApplicationObjects() => Run(async h =>
    {
        h.Editor.Text = "SECRET-SOURCE-SENTINEL";
        h.Layout.State.Document.Note = "SECRET-NOTE-SENTINEL";
        h.Layout.Pane(DockShellState.DocumentId).Title = "SECRET-TITLE-SENTINEL";
        var json = h.Layout.Capture(Bounds);
        foreach (var forbidden in new[] { "SECRET", "Context", "TextDocument", "ViewModel", "EditorBridge", "Coordinator", "Password", "Credential", "OutputEntries", "InfoDoc" })
            Assert.That(json, Does.Not.Contain(forbidden));
        await Task.CompletedTask;
    });

    [TestCase("")][TestCase("{")][TestCase("null")][TestCase("[]")]
    public Task InvalidJsonUsesDefaultWithoutEditorCreation(string json) => Ui(() =>
    {
        var path = Temp(); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, json);
        var store = new DockLayoutStore(path);
        Assert.That(store.Load(), Is.Null); Assert.That(store.LastError, Is.Not.Null);
        using var layout = new DockShellLayout(new());
        Assert.That(layout.Pane(DockShellState.DocumentId), Is.Not.Null);
        Assert.That(((EditorDockFactory)layout.Factory).Session, Is.Null);
    });

    [TestCase("version")][TestCase("missing")][TestCase("duplicate")][TestCase("type")]
    [TestCase("unknown")][TestCase("hierarchy")][TestCase("context")][TestCase("cycle")]
    [TestCase("duplicate-property")][TestCase("selection")][TestCase("closed-document")][TestCase("split")]
    [TestCase("nested-floating")][TestCase("null-child")][TestCase("missing-main")][TestCase("duplicate-anchor")]
    public Task InvalidStructureRejectedBeforeHostInitialization(string kind) => Ui(() =>
    {
        using var layout = new DockShellLayout(new());
        var json = layout.Capture(Bounds);
        var node = JsonNode.Parse(json)!;
        var dock = node["Dock"]!;
        var main = dock["VisibleDockables"]!["$values"]![0]!;
        var tools = main["VisibleDockables"]!["$values"]![0]!;
        switch (kind)
        {
            case "version": node["SchemaVersion"] = 99; break;
            case "missing": tools["VisibleDockables"]!["$values"]!.AsArray().RemoveAt(2); break;
            case "duplicate": json = json.Replace(DockShellState.InfoId, DockShellState.OutputId); break;
            case "unknown": json = json.Replace(DockShellState.InfoId, "tool:unknown"); break;
            case "type": json = json.Replace("Dock.Model.Mvvm.Controls.Tool\"", "System.Diagnostics.Process\""); break;
            case "context": tools["Context"] = "No application state"; break;
            case "hierarchy": tools["$type"] = "Dock.Model.Mvvm.Controls.ProportionalDock"; break;
            case "cycle": tools["VisibleDockables"]!["$values"]![0] = new JsonObject { ["$ref"] = dock["$id"]!.GetValue<string>() }; break;
            case "duplicate-property": json = json.Replace("\"SchemaVersion\": 1", "\"SchemaVersion\": 1, \"SchemaVersion\": 1"); break;
            case "selection": tools["ActiveDockable"] = new JsonObject { ["$ref"] = dock["$id"]!.GetValue<string>() }; break;
            case "closed-document": node["ClosedTools"]!.AsArray().Add(DockShellState.DocumentId); break;
            case "split": tools["Proportion"] = -5; break;
            case "nested-floating": dock["FloatingWindowHostMode"] = 1; break;
            case "null-child": tools["VisibleDockables"]!["$values"]![1] = null; break;
            case "missing-main": main["Id"] = "unknown-main"; break;
            case "duplicate-anchor": tools["Id"] = DockShellLayout.Diagnostics; break;
        }
        if (kind is not ("duplicate" or "unknown" or "type" or "duplicate-property")) json = node.ToJsonString();
        var path = Temp(); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, json);
        var store = new DockLayoutStore(path);
        Assert.That(store.Load(), Is.Null, kind);
        Assert.That(store.LastError, Is.Not.Null);
        Assert.That(layout.Factory.HostWindows, Is.Empty);
    });

    [Test] public Task ReadFailureAndOversizeAreControlled() => Ui(() =>
    {
        var path = Temp(); Directory.CreateDirectory(path);
        var store = new DockLayoutStore(path);
        Assert.That(store.Load(), Is.Null); Assert.That(store.LastError, Is.Not.Null);
        var big = Temp(); Directory.CreateDirectory(Path.GetDirectoryName(big)!); File.WriteAllText(big, new string(' ', DockLayoutStore.MaxBytes + 1));
        Assert.That(new DockLayoutStore(big).Load(), Is.Null);
    });

    [TestCase(false)][TestCase(true)] public Task FailedOrInterruptedWritePreservesLastValidFile(bool interrupted) => Ui(() =>
    {
        using var layout = new DockShellLayout(new());
        var json = layout.Capture(Bounds);
        var path = Temp(); var store = new DockLayoutStore(path);
        Assert.That(store.Save(json), Is.True, store.LastError);
        var bytes = File.ReadAllBytes(path);
        layout.Close(DockShellState.InfoId);
        var next = layout.Capture(Bounds);
        if (interrupted)
        {
            var failing = new DockLayoutStore(path) { BeforeReplace = () => throw new IOException("Interrupted") };
            Assert.That(failing.Save(next), Is.False);
        }
        else
        {
            using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Assert.That(store.Save(next), Is.False);
        }
        Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"), Is.Empty);
    });

    [TestCase(DockShellState.DocumentId)][TestCase(DockShellState.VariablesId)][TestCase(DockShellState.InfoId)]
    [TestCase(DockShellState.OutputId)][TestCase(DockShellState.DiagnosticsId)][TestCase(DockShellState.StatusId)]
    public Task FocusCyclesPreserveExactTreeDocumentHistoryAndRegistration(string id) => Run(async h =>
    {
        h.Editor.TextArea.PerformTextInput("Layout history ä€😀");
        h.Layout.Show(id);
        var doc = h.Editor.Document; var history = doc.UndoStack; var registration = h.Snapshot.EditorId;
        var generation = h.Coordinator.WorkspaceGeneration; var vm = h.Session.ViewModel;
        var panes = DockLayoutStore.PaneIds.Select(h.Layout.Pane).ToArray();
        var original = h.Layout.Capture(Bounds);
        var tree = DockShellLayout.VisibleTree(h.Layout.Root).ToArray();
        for (int i = 0; i < 10; i++)
        {
            Assert.That(h.Layout.Maximize(id), Is.True);
            DockEditorTests.Pump(h.Window);
            Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Where(p => p is IDocument or ITool).Select(p => p.Id), Is.EqualTo(new[] { id }));
            Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(original));
            h.Layout.Float(id); h.Layout.Close(id);
            Assert.That(h.Layout.IsMaximized, Is.True);
            Assert.That(h.Layout.Restore(), Is.True);
            DockEditorTests.Pump(h.Window);
            Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Is.EqualTo(tree));
            Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(original));
        }
        Assert.That(DockLayoutStore.PaneIds.Select(h.Layout.Pane), Is.EqualTo(panes));
        Assert.That(h.Editor.Document, Is.SameAs(doc)); Assert.That(doc.UndoStack, Is.SameAs(history));
        Assert.That(h.Session.ViewModel, Is.SameAs(vm)); Assert.That(h.Snapshot.EditorId, Is.EqualTo(registration));
        Assert.That(h.Coordinator.WorkspaceGeneration, Is.EqualTo(generation));
        ApplicationCommands.Undo.Execute(null, h.Editor.TextArea); Assert.That(h.Editor.Text, Is.Empty);
        ApplicationCommands.Redo.Execute(null, h.Editor.TextArea); Assert.That(h.Editor.Text, Is.EqualTo("Layout history ä€😀"));
        await Task.CompletedTask;
    });

    [Test] public Task FloatingFocusAndResetKeepPaneAndEditorObjects() => Run(async h =>
    {
        h.Layout.Float(DockShellState.InfoId);
        var floating = h.Layout.Root.Windows!.Single();
        var host = (Window)floating.Host!; DockEditorTests.Pump(host);
        host.Position = new(120, 130); host.Width = 450; host.Height = 350;
        var json = h.Layout.Capture(Bounds);
        var decoded = DockLayoutStore.Decode(json);
        Assert.That(decoded.Root.Windows!.Single().X, Is.EqualTo(120));
        Assert.That(decoded.Root.Windows!.Single().Width, Is.EqualTo(450));
        var pane = h.Layout.Pane(DockShellState.InfoId); var owner = pane.Owner;
        Assert.That(h.Layout.Maximize(pane.Id), Is.True); Assert.That(h.Layout.Restore(), Is.True);
        Assert.That(pane.Owner, Is.SameAs(owner)); Assert.That(floating.Host, Is.SameAs(host));
        Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(json));
        var doc = h.Editor.Document; var id = h.Snapshot.EditorId;
        Assert.That(h.Layout.Reset(), Is.True); DockEditorTests.Pump(h.Window);
        Assert.That(h.Layout.Pane(pane.Id), Is.SameAs(pane));
        Assert.That(h.Layout.Root.Windows, Is.Empty); Assert.That(host.IsVisible, Is.False);
        Assert.That(h.Editor.Document, Is.SameAs(doc)); Assert.That(h.Snapshot.EditorId, Is.EqualTo(id));
        await Task.CompletedTask;
    });

    private static void Rejected(DockEditorTests.Host h)
    {
        var focused = h.Layout.IsMaximized;
        var tree = DockShellLayout.VisibleTree(h.Layout.Root).ToArray();
        Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.False);
        Assert.That(h.Layout.Restore(), Is.False); Assert.That(h.Layout.Reset(), Is.False);
        Assert.That(h.Layout.IsMaximized, Is.EqualTo(focused));
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Is.EqualTo(tree));
    }
    [TestCase("pending")][TestCase("active")][TestCase("host-busy")][TestCase("modal")][TestCase("observer")]
    public Task LayoutCommandsRejectLockedStatesWithoutReplay(string mode) => Run(async h =>
    {
        foreach (bool focus in new[] { false, true })
        {
            if (focus) Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.True);
            switch (mode)
            {
                case "pending": using (h.Coordinator.TryBegin()) Rejected(h); break;
                case "active": h.Coordinator.RunGui(() => Rejected(h)); break;
                case "host-busy": using (InterpreterHostQueue.Reserve()) Rejected(h); break;
                case "modal": h.Window.IsEnabled = false; try { Rejected(h); } finally { h.Window.IsEnabled = true; } break;
                case "observer": h.Coordinator.Observe(() => Rejected(h)); break;
            }
            Assert.That(h.Layout.IsMaximized, Is.EqualTo(focus));
            if (focus) h.Layout.Restore();
        }
        await Task.CompletedTask;
    });

    [Test] public Task LeaseBlocksLayoutAndNoEditorStateIsReleased() => Run(async h =>
    {
        var before = h.Snapshot;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("layout-test", WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, before.EditorId, Guid.NewGuid(), before.Revision), default)).Result!;
        Rejected(h);
        Assert.That(h.Coordinator.Targets(h.Editor), Is.True);
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, before.EditorId, lease.LeaseId), default);
        Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.True); h.Layout.Restore();
    });

    [Test] public Task CloseCommitsNormalSnapshotOnlyAfterDrainWithFloatingHost() => Run(async h =>
    {
        var path = Temp(); var store = new DockLayoutStore(path); h.Window.EnablePersistence(store);
        h.Layout.Float(DockShellState.InfoId);
        var normal = h.Layout.Capture(Bounds);
        h.Layout.Maximize(DockShellState.DocumentId);
        using (var pending = h.Coordinator.TryBegin())
        {
            h.Window.Close(); await Task.Delay(50);
            Assert.That(h.Window.IsVisible, Is.True); Assert.That(File.Exists(path), Is.False);
            Rejected(h);
        }
        await DockEditorTests.Wait(() => !h.Window.IsVisible);
        var saved = store.Load(); Assert.That(saved, Is.Not.Null, store.LastError);
        Assert.That(saved!.Root.Windows, Has.Count.EqualTo(1));
        Assert.That(DockShellLayout.VisibleTree(saved.Root).Count(p => p is IDocument or ITool), Is.EqualTo(6));
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
        Assert.Throws<ObjectDisposedException>(() => h.Layout.Reset());
    });

    [TestCase(1920, 1080, 1)][TestCase(320, 240, 1)][TestCase(1920, 1080, 2)]
    public void RemovedMonitorAndInvalidSizesAreClamped(int width, int height, double scale)
    {
        var area = new PixelRect(-width, 0, width, height);
        var result = DockWindowGeometry.Clamp(new(999999, -999999, double.NaN, -1), [new(area, scale)]);
        Assert.That(result.X, Is.GreaterThanOrEqualTo(area.X)); Assert.That(result.Y, Is.GreaterThanOrEqualTo(area.Y));
        Assert.That(result.X + result.Width * scale, Is.LessThanOrEqualTo(area.Right));
        Assert.That(result.Y + result.Height * scale, Is.LessThanOrEqualTo(area.Bottom));
        Assert.That(result.Width, Is.GreaterThan(0)); Assert.That(result.Height, Is.GreaterThan(0));
    }
    [Test] public Task ResetWhileEditorFloatsRetainsDocumentAndRetiresHost() => Run(async h =>
    {
        h.Editor.Text = "Editor reset sentinel";
        var document = h.Editor.Document; var history = document.UndoStack; var id = h.Snapshot.EditorId;
        h.Float(); var host = (Window)h.Layout.Root.Windows!.Single().Host!;
        h.Layout.Maximize(DockShellState.DocumentId);
        Assert.That(h.Layout.Reset(), Is.True); DockEditorTests.Pump(h.Window);
        Assert.That(host.IsVisible, Is.False); Assert.That(h.Layout.IsMaximized, Is.False);
        Assert.That(h.Editor.Document, Is.SameAs(document)); Assert.That(document.UndoStack, Is.SameAs(history));
        Assert.That(h.Editor.Text, Is.EqualTo("Editor reset sentinel")); Assert.That(h.Snapshot.EditorId, Is.EqualTo(id));
        await Task.CompletedTask;
    });

    [Test] public Task GlobalViewsAndModelsSurviveFocusAndReset() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var variables = h.Presentation.Variables; var info = h.Presentation.Info;
        var output = h.Presentation.Output; var diagnostics = h.Presentation.Diagnostics; var status = h.Presentation.Status;
        foreach (var pane in DockLayoutStore.PaneIds)
        {
            h.Show(pane); Assert.That(h.Layout.Maximize(pane), Is.True); h.Pump();
            Assert.That(h.Layout.Restore(), Is.True); h.Pump();
        }
        h.Layout.Reset(); h.Pump();
        Assert.That(h.Presentation.Variables, Is.SameAs(variables)); Assert.That(variables!.DataContext, Is.SameAs(h.Variables));
        Assert.That(h.Presentation.Info, Is.SameAs(info)); Assert.That(info!.DataContext, Is.SameAs(h.Info));
        Assert.That(h.Presentation.Output, Is.SameAs(output)); Assert.That(h.Presentation.Diagnostics, Is.SameAs(diagnostics));
        Assert.That(h.Presentation.Status, Is.SameAs(status)); Assert.That(h.Session.ViewModel.Status, Is.SameAs(h.Status));
        return true;
    }, CancellationToken.None);

    [Test] public Task FailedShutdownDoesNotWriteOrDestroyLayout() => GuiTestSession.Session.Dispatch(async () =>
    {
        using var layout = new DockShellLayout(new());
        await using var service = new WorkbenchAutomationService();
        var window = new DockShellWindow(layout);
        var path = Temp(); var store = new DockLayoutStore(path);
        Assert.That(store.Save(layout.Capture(Bounds)), Is.True);
        var original = File.ReadAllBytes(path);
        window.EnablePersistence(store);
        bool fail = true;
        CryptoWorkBenchAvalonia.App.CoordinateClosing(window, service,
            () => fail ? Task.FromException(new IOException("injected transport shutdown failure")) : Task.CompletedTask);
        window.Show(); DockEditorTests.Pump(window);
        layout.Float(DockShellState.InfoId);
        var floating = (Window)layout.Root.Windows!.Single().Host!;
        window.Close(); await Task.Delay(80);
        Assert.That(window.IsVisible, Is.True); Assert.That(floating.IsVisible, Is.True);
        Assert.That(layout.IsDisposed, Is.False); Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        fail = false; window.Close(); await DockEditorTests.Wait(() => !window.IsVisible);
        Assert.That(floating.IsVisible, Is.False);
        return true;
    }, CancellationToken.None);

    [Test] public void ExistingSecondMonitorRetainsReachableGeometry()
    {
        var original = new LayoutWindow(-1200, 100, 500, 400);
        Assert.That(DockWindowGeometry.Clamp(original, [new(new(0, 0, 1920, 1080), 1), new(new(-1920, 0, 1920, 1080), 1)]), Is.EqualTo(original));
    }
}
