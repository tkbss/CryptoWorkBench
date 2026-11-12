using System.Text.Json.Nodes;
using Avalonia;
using CryptoWorkBenchAvalonia.Shell;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockFinalIntegrationTests
{
    [TestCase(false)][TestCase(true)]
    public Task EditorRetemplateReleasesPreviousScrollPresenterWithoutReplacingDocument(bool textArea) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var editor = h.Session.Editor;
        var document = editor.Document;
        var area = editor.TextArea;
        editor.Text = "original"; editor.Document.Insert(8, "!");
        var original = h.Service.CaptureSaveIntent()!.Snapshot;
        for (int i = 0; i < 3; i++)
        {
            if (textArea) area.Template = new Avalonia.Controls.Templates.FuncControlTemplate<AvaloniaEdit.Editing.TextArea>((_, scope) =>
            {
                var presenter = new Avalonia.Controls.Presenters.ContentPresenter { Name = "PART_CP" };
                scope.Register(presenter.Name, presenter);
                return presenter;
            });
            else editor.Template = new Avalonia.Controls.Templates.FuncControlTemplate<AvaloniaEdit.TextEditor>((_, scope) =>
            {
                var viewer = new Avalonia.Controls.ScrollViewer { Name = "PART_ScrollViewer" };
                scope.Register(viewer.Name, viewer);
                return viewer;
            });
            h.Pump();
            Assert.That(editor.Document, Is.SameAs(document));
            Assert.That(editor.TextArea, Is.SameAs(area));
            Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(original));
        }
        editor.Undo(); Assert.That(editor.Text, Is.EqualTo("original"));
        editor.Redo(); Assert.That(editor.Text, Is.EqualTo("original!"));
        return true;
    }, CancellationToken.None);

    [TestCase(false)][TestCase(true)]
    public Task ConsecutiveDockCommandsBeforeRenderKeepSingleTextAreaParent(bool example) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        if (example) { h.Info.SetInfoText("```text\nPrint(0x(AB))\n```\n"); h.Show(DockShellState.InfoId); }
        var editor = h.Session.Editor;
        editor.Text = "// rapid layout";
        foreach (var id in DockLayoutStore.PaneIds)
        {
            h.Layout.Float(id); h.Pump();
            h.Layout.Dock(id);
            // The next command can precede a render pass; admission must still be safe.
        }
        h.Pump();
        Assert.That(editor.Text, Is.EqualTo("// rapid layout"));
        Assert.That(Avalonia.Controls.TopLevel.GetTopLevel(editor), Is.SameAs(h.Window));
        Assert.That(h.Service.HasRegisteredEditor, Is.True);
        return true;
    }, CancellationToken.None);

    [Test]
    public Task DiagnosticsFiltersRemainInsideNarrowView() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        var view = new DockDiagnosticsView(h.Session) { Width = 220, Height = 400 };
        var window = new Avalonia.Controls.Window { Width = 220, Height = 400, Content = view };
        try
        {
            window.Show(); DockEditorTests.Pump(window);
            foreach (var button in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(view).OfType<Avalonia.Controls.Button>())
            {
                var position = button.TranslatePoint(default, view)!.Value;
                Assert.That(position.X + button.Bounds.Width, Is.LessThanOrEqualTo(view.Bounds.Width), "Clipped filter: " + button.Content);
            }
        }
        finally { window.Close(); }
        return true;
    }, CancellationToken.None);

    private static int Subscribers(object source, string name) =>
        ((Delegate?)source.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(source))?.GetInvocationList().Length ?? 0;

    [Test]
    public Task BoundedLifecycleCyclesKeepRegistrationsSubscriptionsAndHostsBalanced() => GuiTestSession.Session.Dispatch(async () =>
    {
        var memory = new List<long>();
        for (int launch = 0; launch < 3; launch++)
        {
            await using var h = new DockToolTests.Host();
            h.Session.Editor.Text = "// resource sentinel ä€😀";
            var document = h.Session.Editor.Document;
            var undo = document.UndoStack;
            var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
            var generation = h.Service.EditorCoordinator.WorkspaceGeneration;
            int history = Subscribers(h.History, "InfoHistoryChanged");
            int workspace = Subscribers(h.Service.EditorCoordinator, "WorkspaceChanged");
            for (int cycle = 0; cycle < 12; cycle++)
            {
                foreach (string id in DockLayoutStore.PaneIds)
                {
                    var host = h.Float(id);
                    Assert.That(h.Layout.Maximize(id), Is.True);
                    var json = h.Layout.Capture(new(0, 0, 1000, 700));
                    Assert.That(DockLayoutStore.Decode(json).Root, Is.Not.Null);
                    Assert.That(h.Layout.Restore(), Is.True);
                    h.Layout.Dock(id); h.Pump();
                    Assert.That(host.IsVisible, Is.False);
                    if (id != DockShellState.DocumentId) { h.Layout.Close(id); h.Show(id); }
                }
                Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
                Assert.That(h.Layout.Factory.DockControls.Count, Is.EqualTo(1));
                Assert.That(h.Session.Editor.Document, Is.SameAs(document));
                Assert.That(document.UndoStack, Is.SameAs(undo));
                Assert.That(h.Service.CaptureSaveIntent()!.Snapshot, Is.EqualTo(snapshot));
                Assert.That(h.Service.EditorCoordinator.WorkspaceGeneration, Is.EqualTo(generation));
                Assert.That(Subscribers(h.History, "InfoHistoryChanged"), Is.EqualTo(history));
                Assert.That(Subscribers(h.Service.EditorCoordinator, "WorkspaceChanged"), Is.EqualTo(workspace));
                Assert.That(h.Service.EditorCoordinator.Pending, Is.Zero);
                Assert.That(h.Service.EditorCoordinator.Active, Is.False);
            }
            // Publish while the presenters are absent, then bind the same models again.
            h.Layout.Close(DockShellState.VariablesId); h.Layout.Close(DockShellState.OutputId);
            await h.Session.ViewModel.ParseLineAsync("VAR resourceValue=0x(11) Print(0x(22))");
            h.Show(DockShellState.VariablesId); h.Show(DockShellState.OutputId);
            Assert.That(h.Variables.DataVariables.Any(v => v.Identifier == "resourceValue"), Is.True);
            Assert.That(h.Session.Output.Entries.Single().Text, Is.EqualTo("out: 0x(22)"));
            h.Float(DockShellState.InfoId); h.Float(DockShellState.OutputId);
            h.Window.Close(); await DockEditorTests.Wait(() => !h.Window.IsVisible);
            Assert.That(h.Service.HasRegisteredEditor, Is.False);
            Assert.That(h.Layout.Factory.HostWindows, Is.Empty);
            Assert.That(h.Layout.Factory.DockControls, Is.Empty);
            Assert.That(Subscribers(h.History, "InfoHistoryChanged"), Is.Zero);
            Assert.That(Subscribers(h.Service.EditorCoordinator, "WorkspaceChanged"), Is.Zero);
            h.Session.Dispose(); h.Presentation.Dispose(); h.Layout.Dispose();
            memory.Add(GC.GetTotalMemory(true));
        }
        TestContext.Out.WriteLine("Retained managed bytes after bounded launches (not a leak guarantee): " + string.Join(",", memory));
        return true;
    }, CancellationToken.None);

    [Test]
    public Task LiveBusinessAndCredentialSentinelsNeverEnterLayout() => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockToolTests.Host();
        const string source = "VAR sentinelPrivate=0x(CAFEDEADBEEF) Print(0x(CAFEDEADBEEF))";
        h.Session.Editor.Text = source;
        await h.Session.ViewModel.ParseLineAsync(source);
        h.Info.SetInfoText("private-info-sentinel-87");
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("private-mcp-sentinel-87", CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService.LocalCaller.Permissions);
        foreach (var pane in DockLayoutStore.PaneIds.Select(h.Layout.Pane)) pane.Title = "private-title-sentinel-87";
        h.Float(DockShellState.InfoId);
        Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.True);
        var json = h.Layout.Capture(new(0, 0, 1000, 700));
        foreach (string sentinel in new[] { source, "sentinelPrivate", "CAFEDEADBEEF", "private-info-sentinel-87", "private-mcp-sentinel-87", "private-title-sentinel-87", h.Service.InstanceId.ToString(), h.Service.CaptureSaveIntent()!.Snapshot.EditorId.ToString() })
            Assert.That(json, Does.Not.Contain(sentinel));
        Assert.That(DockLayoutStore.Decode(json).Root.Windows!.Count, Is.EqualTo(1));
        Assert.That(h.Layout.Restore(), Is.True);
        return true;
    }, CancellationToken.None);

    [Test]
    public Task NullFloatingEntryIsRejectedBeforeAnyHostOrEditorExists() => GuiTestSession.Session.Dispatch(() =>
    {
        using var layout = new DockShellLayout(new());
        var node = JsonNode.Parse(layout.Capture(new(0, 0, 1000, 700)))!;
        node["Dock"]!["Windows"]!["$values"]!.AsArray().Add(null);
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "g108", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "layout.json");
        File.WriteAllText(path, node.ToJsonString());
        var store = new DockLayoutStore(path);
        Assert.That(store.Load(), Is.Null);
        Assert.That(store.LastError, Does.Contain("InvalidDataException"));
        Assert.That(layout.Factory.HostWindows, Is.Empty);
        Assert.That(((EditorDockFactory)layout.Factory).Session, Is.Null);
        Assert.That(DockShellLayout.VisibleTree(layout.Root).Count(p => DockLayoutStore.PaneIds.Contains(p.Id)), Is.EqualTo(6));
        return true;
    }, CancellationToken.None);
}
