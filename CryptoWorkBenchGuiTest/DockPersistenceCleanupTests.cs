using System.Reflection;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockPersistenceCleanupTests
{
    internal static readonly LayoutWindow Bounds = new(80, 90, 1200, 800);
    internal static string HistoricalJson()
    {
        using var stream = typeof(DockPersistenceCleanupTests).Assembly.GetManifestResourceStream("CryptoWorkBenchGuiTest.Fixtures.dock-schema1-pre-a02.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    internal static IDockable FloatSource(DockShellLayout layout, string scenario)
    {
        var document = scenario is "document" or "document-anchor";
        var pane = layout.Pane(document ? DockShellState.DocumentId : DockShellState.VariablesId);
        if (scenario is "anchor" or "document-anchor")
        {
            var source = (IDock)pane.Owner!;
            layout.Factory.FloatDockable(source);
            return source;
        }
        layout.Float(pane.Id);
        if (scenario == "multiple")
        {
            var output = layout.Pane(DockShellState.OutputId);
            layout.Factory.MoveDockable((IDock)output.Owner!, (IDock)pane.Owner!, output, null);
        }
        if (scenario == "nested")
            Assert.That(new DockManager(new DockService()).ValidateDockable(layout.Pane(DockShellState.OutputId), pane.Owner!, DragAction.Move, DockOperation.Right, true), Is.True);
        return layout.Factory.FindRoot(pane)!.ActiveDockable!;
    }

    internal static string LegacyNames(string json) => json.Replace(DockContainerIdentity.DynamicPrefix, "legacy:container:", StringComparison.Ordinal);

    [Test]
    public Task StartupPresentsRestoredHostsThroughProductWindowOpened() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host(DockLayoutStore.Decode(HistoricalJson()));
        var window = h.Layout.Root.Windows!.Single();
        Assert.That(window.Host, Is.InstanceOf<Window>());
        Assert.That(((Window)window.Host!).IsVisible, Is.True);
        Assert.That(h.Layout.Factory.HostWindows, Does.Contain(window.Host));
        Assert.That(h.Service.HasRegisteredEditor, Is.True);
        return true;
    }, CancellationToken.None);

    public static IEnumerable<TestCaseData> CleanupCases()
    {
        foreach (var scenario in new[] { "single", "multiple", "anchor", "document", "document-anchor", "nested" })
        foreach (var legacy in new[] { false, true })
        foreach (var manager in new[] { false, true })
            yield return new TestCaseData(scenario, legacy, manager);
    }

    [TestCaseSource(nameof(CleanupCases))]
    public Task SaveShutdownRestartThenEmptySourceReleasesOnlyThatHost(string scenario, bool legacy, bool manager)
        => GuiTestSession.Session.Dispatch(async () =>
    {
        string json;
        await using (var original = new DockToolTests.Host())
        {
            original.Float(DockShellState.DiagnosticsId);
            FloatSource(original.Layout, scenario); original.Pump();
            original.Layout.Close(DockShellState.StatusId);
            json = original.Layout.Capture(Bounds);
        }
        if (legacy) json = LegacyNames(json);
        var saved = DockLayoutStore.Decode(json);
        var ids = DockShellLayout.VisibleTree(saved.Root).Select(p => p.Id).ToArray();
        await using var h = new DockToolTests.Host(saved);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Select(p => p.Id), Is.EqualTo(ids));
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)).ClosedTools, Does.Contain(DockShellState.StatusId));
        var pane = h.Layout.Pane(scenario.StartsWith("document", StringComparison.Ordinal) ? DockShellState.DocumentId : DockShellState.VariablesId);
        var sourceRoot = h.Layout.Factory.FindRoot(pane)!;
        var sourceWindow = sourceRoot.Window!;
        var sourceHost = (Window)sourceWindow.Host!;
        var retained = (Window)h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.DiagnosticsId))!.Window!.Host!;
        var editor = h.Session.Editor; var text = editor.Document; var undo = text.UndoStack;
        h.Service.Mutate(() => editor.Text = "// A03 retained");
        h.Service.Mutate(() => text.Insert(text.TextLength, " edit"));
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
        h.History.AddLine("one"); h.History.AddLine("two");
        var views = new Control[] { h.Presentation.Variables!, h.Presentation.Output!, h.Presentation.Info!, h.Presentation.Diagnostics!, h.Presentation.Status! };
        var contexts = views.Select(v => v.DataContext).ToArray();
        IDock Target(string id) => DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IDock>().Single(d => d.Id ==
            (id == DockShellState.DocumentId ? DockShellLayout.Documents : DockShellLayout.Tools));
        if (manager)
            Assert.That(new DockManager(new DockService()).ValidateDockable(sourceRoot.ActiveDockable!, Target(pane.Id), DragAction.Move, DockOperation.Fill, true), Is.True);
        else
            foreach (var child in DockShellLayout.VisibleTree(sourceRoot).Where(p => p is IDocument or ITool).ToArray())
                h.Layout.Factory.MoveDockable((IDock)child.Owner!, Target(child.Id), child, null);
        h.Pump();
        // A02 Legacy red: the move is accepted, but a non-collapsable legacy root
        // remains visible and registered after its last content has left.
        Assert.That(sourceHost.IsVisible, Is.False, "An accepted restored drop must destroy its emptied source host.");
        Assert.That(h.Layout.Root.Windows, Does.Not.Contain(sourceWindow));
        Assert.That(h.Layout.Factory.HostWindows, Does.Not.Contain(sourceHost));
        Assert.That(sourceWindow.Owner, Is.Null); Assert.That(sourceWindow.Layout, Is.Null);
        Assert.That(retained.IsVisible, Is.True);
        Assert.That(h.Layout.Factory.HostWindows, Has.Count.EqualTo(1));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(editor.Document, Is.SameAs(text)); Assert.That(text.UndoStack, Is.SameAs(undo));
        Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
        Assert.That(views.Select(v => v.DataContext), Is.EqualTo(contexts)); Assert.That(h.History.LineCanBack, Is.True);
        var before = h.Layout.Capture(Bounds);
        Assert.That(h.Layout.Maximize(pane.Id), Is.True); h.Pump();
        Assert.That(h.Layout.Restore(), Is.True); h.Pump();
        Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(before));
        foreach (var id in DockLayoutStore.PaneIds.Where(id => id != DockShellState.StatusId))
            Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Count(p => ReferenceEquals(p, h.Layout.Pane(id))), Is.EqualTo(1));
        h.Service.Mutate(() => editor.Undo()); Assert.That(text.Text, Is.EqualTo("// A03 retained"));
        h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(retained.IsVisible, Is.False); Assert.That(h.Service.HasRegisteredEditor, Is.False);
        Assert.That(h.Layout.Factory.HostWindows, Is.Empty); Assert.That(h.Layout.Factory.DockControls, Is.Empty);
        return true;
    }, CancellationToken.None);

    [Test]
    public Task OriginalPreA02SchemaOneLoadsAndReleasesFloatingInfo() => GuiTestSession.Session.Dispatch(async () =>
    {
        var saved = DockLayoutStore.Decode(HistoricalJson());
        await using var h = new DockToolTests.Host(saved);
        var info = h.Layout.Pane(DockShellState.InfoId);
        var model = h.Layout.Factory.FindRoot(info)!.Window!;
        var host = (Window)model.Host!;
        var target = DockShellLayout.VisibleTree(h.Layout.Root, false).OfType<IToolDock>().Single(d => d.Id == DockShellLayout.Tools);
        Assert.That(new DockManager(new DockService()).ValidateDockable(info, target, DragAction.Move, DockOperation.Fill, true), Is.True);
        h.Pump(); Assert.That(host.IsVisible, Is.False); Assert.That(h.Layout.Root.Windows, Is.Empty);
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)).ClosedTools, Does.Contain(DockShellState.StatusId));
        return true;
    }, CancellationToken.None);

    [Test]
    public Task RestoredRolesDependOnMainTreeNotHistoricalAnchorString() => GuiTestSession.Session.Dispatch(async () =>
    {
        string json;
        await using (var original = new DockToolTests.Host())
        {
            FloatSource(original.Layout, "anchor"); original.Pump();
            json = LegacyNames(original.Layout.Capture(Bounds));
        }
        var loaded = DockLayoutStore.Decode(json);
        var main = DockShellLayout.VisibleTree(loaded.Root, false).OfType<IDock>().ToArray();
        Assert.That(main.Where(d => d.Id is "dock:root" or "dock:main" or DockShellLayout.Tools or DockShellLayout.Documents or DockShellLayout.Diagnostics).All(d => !d.IsCollapsable), Is.True);
        foreach (var floating in loaded.Root.Windows!)
            Assert.That(DockShellLayout.VisibleTree(floating.Layout!).OfType<IDock>().All(d => d.IsCollapsable), Is.True);
        return true;
    }, CancellationToken.None);

    [TestCase("root")][TestCase("main")][TestCase("pane")][TestCase("whitespace")]
    [TestCase("duplicate-pane")][TestCase("duplicate-anchor")][TestCase("duplicate-dynamic")]
    [TestCase("cycle")][TestCase("context")][TestCase("wrong-anchor-type")]
    public void LegacyNormalizationDoesNotAdmitInvalidLayouts(string kind)
    {
        var node = JsonNode.Parse(HistoricalJson())!;
        var root = node["Dock"]!;
        var main = root["VisibleDockables"]!["$values"]![0]!;
        var tools = main["VisibleDockables"]!["$values"]![0]!;
        var floating = root["Windows"]!["$values"]![0]!["Layout"]!;
        var group = floating["VisibleDockables"]!["$values"]![0]!;
        var pane = group["VisibleDockables"]!["$values"]![0]!;
        switch (kind)
        {
            case "root": root["Id"] = ""; break;
            case "main": main["Id"] = ""; break;
            case "pane": pane["Id"] = ""; break;
            case "whitespace": floating["Id"] = " "; break;
            case "duplicate-pane": pane["Id"] = DockShellState.OutputId; break;
            case "duplicate-anchor": tools["Id"] = DockShellLayout.Diagnostics; break;
            case "duplicate-dynamic":
                var duplicate = DockContainerIdentity.DynamicPrefix + Guid.NewGuid().ToString("N");
                floating["Id"] = duplicate; group["Id"] = duplicate; break;
            case "cycle":
                group["ActiveDockable"] = null;
                group["VisibleDockables"]!["$values"]![0] = new JsonObject { ["$ref"] = floating["$id"]!.GetValue<string>() }; break;
            case "context": group["Context"] = "application object"; break;
            case "wrong-anchor-type": group["Id"] = DockShellLayout.Documents; break;
        }
        Assert.Throws<InvalidDataException>(() => DockLayoutStore.Decode(node.ToJsonString()));
    }

    [Test]
    public Task HistoricalNormalizationIsStableAcrossThreeStartsAndAtomicSaves() => GuiTestSession.Session.Dispatch(async () =>
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "g1a03", Guid.NewGuid().ToString("N"), "layout.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, HistoricalJson());
        var store = new DockLayoutStore(path);
        string[]? ids = null;
        for (int i = 0; i < 3; i++)
        {
            var saved = store.Load()!;
            var current = DockShellLayout.VisibleTree(saved.Root).OfType<IDock>().Select(d => d.Id).ToArray();
            if (ids != null) Assert.That(current, Is.EqualTo(ids));
            ids = current;
            await using var h = new DockToolTests.Host(saved);
            Assert.That(h.Service.HasRegisteredEditor, Is.True);
            Assert.That(h.Layout.Root.Windows!.Single().Layout!.Id, Does.StartWith(DockContainerIdentity.DynamicPrefix));
            Assert.That(store.Save(h.Layout.Capture(Bounds)), Is.True, store.LastError);
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp"), Is.Empty);
        }
        return true;
    }, CancellationToken.None);

    [TestCase(false)][TestCase(true)]
    public Task RestoredPartialGroupStaysUntilLastPaneAndResetPreservesSession(bool legacy) => GuiTestSession.Session.Dispatch(async () =>
    {
        string json;
        await using (var original = new DockToolTests.Host())
        {
            FloatSource(original.Layout, "nested"); original.Pump();
            json = original.Layout.Capture(Bounds);
        }
        await using var h = new DockToolTests.Host(DockLayoutStore.Decode(legacy ? LegacyNames(json) : json));
        await h.Session.ViewModel.ParseLineAsync("Print(0x(AA)) Print(missing)");
        h.Status.Filter = CryptoWorkBenchAvalonia.Models.DiagnosticFilter.Errors;
        var outputRows = h.Session.Output.Entries.ToArray();
        var diagnosticRows = h.Status.Diagnostics.ToArray();
        Assert.That(outputRows, Has.Length.EqualTo(1)); Assert.That(diagnosticRows, Is.Not.Empty);
        var panes = DockLayoutStore.PaneIds.Select(h.Layout.Pane).ToArray();
        var editor = h.Session.Editor;
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        var output = h.Session.Output; var entries = output.Entries;
        var diagnostics = h.Status.VisibleDiagnostics;
        h.Presentation.Diagnostics!.Feedback.Text = "retained feedback";
        var source = (Window)h.Layout.Factory.FindRoot(h.Layout.Pane(DockShellState.VariablesId))!.Window!.Host!;
        h.Layout.Dock(DockShellState.VariablesId); h.Pump();
        Assert.That(source.IsVisible, Is.True);
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)).Root.Windows, Has.Count.EqualTo(1));
        h.Layout.Close(DockShellState.OutputId); h.Pump();
        Assert.That(source.IsVisible, Is.False); Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
        for (int i = 0; i < 3; i++)
        {
            h.Layout.Show(DockShellState.OutputId);
            FloatSource(h.Layout, "anchor"); h.Pump();
            var oldHosts = h.Layout.Factory.HostWindows.Cast<Window>().ToArray();
            Assert.That(h.Layout.Reset(), Is.True); h.Pump();
            Assert.That(oldHosts.All(w => !w.IsVisible), Is.True);
            var host = h.Float(DockShellState.OutputId);
            h.Layout.Dock(DockShellState.OutputId); h.Pump();
            Assert.That(host.IsVisible, Is.False);
            Assert.That(DockContainerRoles.MainAnchors(h.Layout.Root).Count(), Is.EqualTo(3));
            Assert.That(DockContainerRoles.ProtectedMainDocks(h.Layout.Root).All(d => !d.IsCollapsable), Is.True);
            Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)), Is.Not.Null);
        }
        Assert.That(DockLayoutStore.PaneIds.Select(h.Layout.Pane), Is.EqualTo(panes));
        Assert.That(h.Session.Editor, Is.SameAs(editor)); Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
        Assert.That(h.Session.Output, Is.SameAs(output)); Assert.That(output.Entries, Is.SameAs(entries));
        Assert.That(h.Status.VisibleDiagnostics, Is.SameAs(diagnostics));
        Assert.That(output.Entries, Is.EqualTo(outputRows)); Assert.That(h.Status.Diagnostics, Is.EqualTo(diagnosticRows));
        Assert.That(h.Status.Filter, Is.EqualTo(CryptoWorkBenchAvalonia.Models.DiagnosticFilter.Errors));
        Assert.That(h.Presentation.Diagnostics.Feedback.Text, Is.EqualTo("retained feedback"));
        return true;
    }, CancellationToken.None);

    [TestCase("pending")][TestCase("active")][TestCase("observer")]
    public Task RestoredLegacyHostCannotBypassAdmission(string state) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host(DockLayoutStore.Decode(HistoricalJson()));
        var info = h.Layout.Pane(DockShellState.InfoId);
        var source = (IDock)info.Owner!;
        var host = (Window)h.Layout.Factory.FindRoot(info)!.Window!.Host!;
        var before = h.Layout.Capture(Bounds);
        void Reject()
        {
            h.Layout.Dock(info.Id); h.Layout.Close(info.Id);
            h.Layout.Factory.MoveDockable(source, DockContainerRoles.MainAnchors(h.Layout.Root).Single(d => d.Id == DockShellLayout.Tools), info, null);
            host.Close();
            Assert.That(host.IsVisible, Is.True); Assert.That(info.Owner, Is.SameAs(source));
        }
        var coordinator = h.Service.EditorCoordinator;
        if (state == "pending") { using var pending = coordinator.TryBegin(); Reject(); }
        else if (state == "active") coordinator.RunGui(Reject);
        else coordinator.Observe(Reject);
        Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(before));
        h.Layout.Dock(info.Id); h.Pump(); Assert.That(host.IsVisible, Is.False);
        return true;
    }, CancellationToken.None);

    [Test]
    public Task InvalidLegacyRestoreFallsBackThenSupportsOrdinaryDocking() => GuiTestSession.Session.Dispatch(async () =>
    {
        var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "g1a03", Guid.NewGuid().ToString("N"), "layout.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, HistoricalJson().Replace(DockShellState.InfoId, DockShellState.OutputId));
        var store = new DockLayoutStore(path);
        var loaded = store.Load(); Assert.That(loaded, Is.Null); Assert.That(store.LastError, Is.Not.Null);
        await using var h = new DockToolTests.Host(loaded);
        var host = h.Float(DockShellState.InfoId);
        h.Layout.Dock(DockShellState.InfoId); h.Pump(); Assert.That(host.IsVisible, Is.False);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root).Count(p => p is IDocument or ITool), Is.EqualTo(6));
        Assert.That(DockLayoutStore.Decode(h.Layout.Capture(Bounds)), Is.Not.Null);
        return true;
    }, CancellationToken.None);
}
