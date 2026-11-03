using System.Collections.Specialized;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using AvaloniaEdit.Utils;
using CryptoScript.Documentation;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B1F1CorrectionTests
{
    private class Protection(Func<int, bool> insert, Func<ISegment, bool>? delete = null) : IReadOnlySectionProvider
    {
        public bool CanInsert(int offset) => insert(offset);
        public IEnumerable<ISegment> GetDeletableSegments(ISegment segment)
        {
            if (delete?.Invoke(segment) != false) yield return segment;
        }
    }

    private sealed class SearchProtection(Func<int, bool> insert, Func<long> revision) :
        Protection(insert), IRevisionedSectionProvider
    {
        public long PermissionRevision => revision();
    }

    [TestCase("caret")] [TestCase("drain")] [TestCase("detach")] [TestCase("diagnostics")]
    public Task ReturnKeepsItsLineThroughPopupAndPublication(string cause) => Ui(async () =>
    {
        await using var h = new Host("unused");
        var history = new HistoryService();
        var status = new StatusViewModel();
        using var vm = new CryptoScriptEditViewModel(status, new(), new(), new(history), null!, history, h.Service);
        var view = new CryptoScriptEditView(vm);
        h.Window.Content = view;
        h.Window.UpdateLayout();
        var editor = view.FindControl<WorkbenchTextEditor>("Editor")!;
        editor.TextArea.Focus();
        editor.Text = "VAR reviewReturnF1=0x(12)\nHash";
        editor.CaretOffset = editor.Text.Length;
        editor.TextArea.PerformTextInput("(");
        var window = (OverloadInsightWindow)typeof(CryptoScriptEditView).GetField("_insightWindow",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        var before = editor.Text;
        int closures = 0;
        EditorCoordinator.DrainTicket? drain = null;
        window.Closed += (_, _) =>
        {
            closures++;
            if (cause == "caret") editor.CaretOffset = 0;
            if (cause == "drain") drain = h.Owner.BeginDrain();
            if (cause == "detach") { h.Window.Content = null; h.Window.UpdateLayout(); }
        };
        if (cause == "diagnostics")
            ((INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, _) => editor.CaretOffset = 0;
        editor.TextArea.PerformTextInput("\n");
        await view.PendingExecution;
        var state = (await h.Service.GetStatusAsync(WorkbenchAutomationService.LocalCaller, default)).Data!;
        Assert.That(closures, Is.EqualTo(1));
        Assert.That(state.LastExecutionId, Is.Null);
        Assert.That(state.StateVersion, Is.EqualTo("0"));
        Assert.That(VariableDictionary.Instance().Contains("reviewReturnF1"), Is.False);
        Assert.That(editor.Text, Is.EqualTo(before));
        Assert.That(h.Owner.Pending, Is.Zero);
        if (drain != null) Assert.That(h.Owner.ResumeGui(drain), Is.True);
        h.Window.Content = null;
    });

    [TestCase(false)] [TestCase(true)]
    public Task ObservationRejectsFreshExecutionButPermitsReplayAndLaterSubmission(bool collection) => Ui(async () =>
    {
        await using var h = new Host("script");
        var caller = WorkbenchAutomationService.LocalCaller;
        var state = VariableDictionary.Instance();
        var previous = state.GetVariables().ToArray();
        try
        {
            var request = new TextExecution(h.Service.InstanceId, Guid.NewGuid(), "0",
                "VAR reviewObserverF1=0x(42)", TextExecutionMode.Submission);
            var status = new StatusViewModel { Automation = h.Service };
            Task<Reply<ExecutionResult>>? attempt = null;
            bool directRejected = false;
            void Submit()
            {
                attempt = h.Service.ExecuteTextAsync(caller, request, default);
                try { h.Service.RunAsync(_ => Task.FromResult(true), default).GetAwaiter().GetResult(); }
                catch (ExecutionBusyException) { directRejected = true; }
            }
            if (collection)
            {
                ((INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, _) => Submit();
                status.ClearDiagnostics();
            }
            else
            {
                status.PropertyChanged += (_, _) => Submit();
                status.StatusString = "observe";
            }
            Assert.That((await attempt!).Error?.Code, Is.EqualTo(AutomationErrorCode.Busy));
            Assert.That(directRejected, Is.True);
            Assert.That(h.Service.IsBusy || state.Contains("reviewObserverF1"), Is.False);
            Assert.That((await h.Service.GetExecutionAsync(caller, request.ExecutionId, default)).Error?.Code,
                Is.EqualTo(AutomationErrorCode.NotFound));
            var accepted = await h.Service.ExecuteTextAsync(caller, request, default);
            Assert.That(accepted.Ok && state.Contains("reviewObserverF1"), Is.True);
            h.Owner.Observe(() => attempt = h.Service.ExecuteTextAsync(caller, request, default));
            Assert.That((await attempt!).Data?.ExecutionId, Is.EqualTo(request.ExecutionId));
        }
        finally { state.Clear(); foreach (var v in previous) state.Add(v); }
    });

    [Test]
    public Task AttachChecksLiveBufferBeforeBridgeNotification() => Ui(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var document = new TextDocument("shared");
        var a = new WorkbenchTextEditor();
        var b = new WorkbenchTextEditor { Document = document };
        bool denied = false;
        a.DocumentChanged += (_, _) =>
        {
            try { service.AttachEditor(b, null!, true); }
            catch (ExecutionBusyException) { denied = true; }
        };
        var ra = service.AttachEditor(a, null!)!;
        a.Document = document;
        Assert.That(denied && service.EditorCoordinator.Targets(a), Is.True);
        Assert.That(b.Coordinator, Is.Null);
        a.CaretOffset = 6;
        a.TextArea.PerformTextInput("!");
        b.Document = new TextDocument("B");
        var rb = service.AttachEditor(b, null!, true)!;
        b.TextArea.PerformTextInput("2");
        service.ActivateEditor(ra);
        ApplicationCommands.Undo.Execute(null, a.TextArea);
        Assert.That(a.Text, Is.EqualTo("shared"));
        Assert.That(b.Text, Is.EqualTo("2B"));
        ApplicationCommands.Redo.Execute(null, a.TextArea);
        Assert.That(a.Text, Is.EqualTo("shared!"));
        Assert.That(rb.Attached && service.EditorCoordinator.CanStartGui, Is.True);
    });

    [TestCase("pattern")] [TestCase("replacement")] [TestCase("options")]
    [TestCase("provider")] [TestCase("permissions")] [TestCase("update")]
    public Task ReplaceAllBindsOptionsRangesAndProtection(string change) => Ui(async () =>
    {
        await using var h = new Host("open LOCK");
        h.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, h.Area);
        var before = h.Registration.Snapshot();
        var panel = h.Editor.SearchPanel!;
        panel.Open(); panel.IsReplaceMode = true; panel.SearchPattern = "open"; panel.ReplacePattern = "done";
        int calls = 0;
        var writable = h.Area.ReadOnlySectionProvider;
        var deny = new Protection(_ => false, _ => false);
        h.Area.ReadOnlySectionProvider = new SearchProtection(offset =>
        {
            if (++calls == 1)
            {
                if (change == "pattern") panel.SearchPattern = "LOCK";
                if (change == "replacement") panel.ReplacePattern = "bad";
                if (change == "options") panel.MatchCase = !panel.MatchCase;
                if (change == "provider") h.Area.ReadOnlySectionProvider = deny;
            }
            return offset == 0 && (change != "permissions" || calls == 1);
        }, () => change == "permissions" ? calls : 0);
        EventHandler update = (_, _) => { if (change == "update") panel.SearchPattern = "LOCK"; };
        h.Editor.Document.UpdateStarted += update;
        SearchCommands.ReplaceAll.Execute(null, panel);
        h.Editor.Document.UpdateStarted -= update;
        if (change == "update")
        {
            // The transaction was already accepted before native UpdateStarted.
            // A renderer change can never redirect its frozen, writable offsets.
            Assert.That(h.Editor.Text, Is.EqualTo("done LOCK"));
            ApplicationCommands.Undo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
        }
        else Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
        Assert.That(h.Editor.Document.UndoStack.CanRedo && h.Owner.CanStartGui, Is.True);
        h.Area.ReadOnlySectionProvider = writable;
        panel.SearchPattern = "open"; panel.ReplacePattern = "done";
        SearchCommands.ReplaceAll.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("done LOCK"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
    });

    [TestCase(0, false)] [TestCase(1, false)] [TestCase(3, false)] [TestCase(3, true)]
    public Task DisposalRetiresAllRegistrations(int count, bool closeSelection) => Ui(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        var owner = service.EditorCoordinator;
        var registrations = Enumerable.Range(0, count)
            .Select(_ => service.AttachEditor(new WorkbenchTextEditor { Text = "open" }, null!, true)!).ToArray();
        if (closeSelection) service.DetachEditor(registrations[^1]);
        service.StopAccepting();
        Assert.That(owner.CanStartGui, Is.False);
        await service.DisposeAsync();
        foreach (var r in registrations)
        {
            Assert.That(r.Attached, Is.False);
            Assert.That(((WorkbenchTextEditor)r.Editor).Coordinator, Is.Null);
            Assert.Throws<ExecutionBusyException>(() => service.ActivateEditor(r));
            r.Editor.TextArea.PerformTextInput("!");
            Assert.That(r.Editor.Text, Is.EqualTo("open"));
            var revision = r.Revision;
            r.Document.Insert(0, "retired");
            Assert.That(r.Revision, Is.EqualTo(revision));
        }
        Assert.That(owner.Phase, Is.EqualTo(EditorControlPhase.Unavailable));
        Assert.That(owner.Pending, Is.Zero);
    });

    [Test]
    public Task NativeUpdateBlocksAttachUntilOriginalGroupCloses() => Ui(async () =>
    {
        await using var h = new Host("A");
        var b = new WorkbenchTextEditor { Text = "B" };
        using (h.Editor.Document.RunUpdate())
        {
            Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(b, null!, true));
            Assert.That(b.Coordinator, Is.Null);
            b.TextArea.PerformTextInput("!");
            Assert.That(h.Owner.Targets(h.Editor), Is.True);
        }
        Assert.That(b.Text, Is.EqualTo("B"));
        h.Service.AttachEditor(b, null!, true);
        b.TextArea.PerformTextInput("!");
        Assert.That(b.Text, Is.EqualTo("!B"));
        Assert.That(h.Editor.Text, Is.EqualTo("A"));
    });

    [TestCase("dispose")] [TestCase("caret")] [TestCase("successor")] [TestCase("generation")]
    public Task CompletionRechecksSessionAfterWritability(string change) => Ui(async () =>
    {
        await using var h = new Host("Info(AES");
        using var completion = new InfoCompletionController(h.Editor, new());
        Assert.That(completion.OpenForTypedParenthesis(), Is.True);
        var field = typeof(InfoCompletionController).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var window = (CompletionWindow)field.GetValue(completion)!;
        bool called = false;
        h.Area.ReadOnlySectionProvider = new Protection(_ =>
        {
            called = true;
            if (change == "dispose") completion.Dispose();
            if (change == "caret") h.Editor.CaretOffset--;
            if (change == "successor") completion.OpenForTypedParenthesis();
            if (change == "generation") h.Owner.InvalidateInputIntents();
            return true;
        });
        var before = h.Registration.Snapshot();
        window.CompletionList.RequestInsertion(EventArgs.Empty);
        Assert.That(called, Is.True);
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
        Assert.That(h.Owner.Pending, Is.Zero);
        Assert.That(h.Owner.CanStartGui, Is.True);
        Assert.That(field.GetValue(completion) != null, Is.EqualTo(change == "successor"));
    });

    [Test]
    public Task ChoosingOwnPopupRowKeepsEditorFocusAndOriginalGeneration() => Ui(async () =>
    {
        await using var h = new Host("Info(AES");
        using var completion = new InfoCompletionController(h.Editor, new());
        Assert.That(completion.OpenForTypedParenthesis(), Is.True);
        var window = (CompletionWindow)typeof(InfoCompletionController).GetField("_window",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(completion)!;
        var list = window.CompletionList;
        var row = list.ListBox.ContainerFromIndex(list.ListBox.SelectedIndex)!;
        long generation = h.Owner.Generation;
        var text = list.SelectedItem!.Text;
        row.Focus(NavigationMethod.Pointer);
        Assert.That(h.Area.IsFocused, Is.True);
        Assert.That(h.Owner.Generation, Is.EqualTo(generation));
        list.RequestInsertion(EventArgs.Empty);
        Assert.That(h.Editor.Text, Is.EqualTo("Info(" + text));
        Assert.That(h.Owner.Pending, Is.Zero);
    });

    [TestCase("virtual")] [TestCase("provider")] [TestCase("rectangle")]
    public Task DelayedPasteRequiresFullGeometryAfterProvider(string change) => Ui(async () =>
    {
        await using var h = new Host("line\nline");
        h.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, h.Area);
        var before = h.Registration.Snapshot();
        h.Editor.Options.EnableVirtualSpace = true;
        h.Area.Caret.Position = new TextViewPosition(1, 5, 6);
        if (change == "rectangle")
            h.Area.Selection = new RectangleSelection(h.Area, new TextViewPosition(1, 2), new TextViewPosition(2, 3));
        bool moved = false;
        if (change == "provider")
            h.Area.ReadOnlySectionProvider = new Protection(_ =>
            {
                if (h.Owner.Pending != 0 && !moved) { moved = true; h.Editor.CaretOffset = 0; }
                return true;
            });
        var clipboard = h.Window.Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var adapter = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        adapter.Inner = clipboard;
        var read = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.DelayedRead = read.Task;
        EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        try
        {
            ApplicationCommands.Paste.Execute(null, h.Area);
            Assert.That(h.Area.PendingClipboard.IsCompleted, Is.False);
            if (change == "virtual") h.Area.Caret.Position = new TextViewPosition(1, 5, 10);
            if (change == "rectangle")
                h.Area.Selection = new RectangleSelection(h.Area, new TextViewPosition(1, 1), new TextViewPosition(2, 3));
            read.SetResult("X");
            await h.Area.PendingClipboard;
            Assert.That(moved, Is.EqualTo(change == "provider"));
            Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
            Assert.That(h.Editor.Document.UndoStack.CanRedo && h.Owner.CanStartGui, Is.True);
            Assert.That(h.Owner.Pending, Is.Zero);
        }
        finally { read.TrySetResult(null); EditorCoordinatorTests.ClipboardProxy.Bind(clipboard); }
    });

    [Test]
    public Task RectangularPasteUsesTheApprovedNativeSegmentsAndOneUndo() => Ui(async () =>
    {
        await using var h = new Host("abc\ndef");
        h.Area.Selection = new RectangleSelection(h.Area, new TextViewPosition(1, 2), new TextViewPosition(2, 3));
        await h.Window.Clipboard!.SetTextAsync("Z");
        ApplicationCommands.Paste.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        Assert.That(h.Editor.Text, Is.EqualTo("aZc\ndZf"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("abc\ndef"));
        Assert.That(h.Owner.CanStartGui, Is.True);
    });

    [TestCase("key")] [TestCase("declaration")]
    public Task ParameterDraftChecksOriginalKeyAndDeclarationBeforeAdvancingVersion(string change) => Ui(async () =>
    {
        await using var h = new Host("script");
        await using var foreign = new WorkbenchAutomationService();
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        try
        {
            var variable = new ParameterVariableDeclaration { Id = "reviewParam" };
            variable.SetParameter("PAD", "NONE"); variable.SetParameter("IV", "old");
            h.Service.Mutate(() => dictionary.Add(variable));
            var vm = new VariableViewModel { Automation = h.Service };
            vm.SetupVariables();
            vm.SelectedVariable = vm.DataVariables.Single(v => v.Identifier == variable.Id);
            var row = vm.SelectedVariable.Parameters.Single(p => p.Parameter == "#PAD");
            var before = h.Service.PresentationVersion;
            if (change == "key") row.Parameter = "#IV";
            else
            {
                variable = new ParameterVariableDeclaration { Id = variable.Id };
                variable.SetParameter("PAD", "NONE"); variable.SetParameter("IV", "old");
                foreign.Mutate(() => dictionary.Add(variable));
            }
            row.Value = "bad";
            vm.ModifyVariableCommand.Execute(row);
            Assert.That(variable.GetParameters()["#PAD"], Is.EqualTo("NONE"));
            Assert.That(variable.GetParameters()["#IV"], Is.EqualTo("old"));
            Assert.That(h.Service.PresentationVersion, Is.EqualTo(before));
            vm.SetupVariables();
            vm.SelectedVariable = vm.DataVariables.Single(v => v.Identifier == variable.Id);
            row = vm.SelectedVariable.Parameters.Single(p => p.Parameter == "#PAD");
            row.Value = "PKCS-7"; vm.ModifyVariableCommand.Execute(row);
            Assert.That(variable.GetParameters()["#PAD"], Is.EqualTo("PKCS-7"));
            Assert.That(h.Registration.Revision, Is.Zero);
        }
        finally { dictionary.Clear(); foreach (var item in previous) dictionary.Add(item); }
    });

    [TestCase("add")] [TestCase("clear")] [TestCase("selection")] [TestCase("rebind")]
    public Task VariablePublicationCannotOutliveViewBinding(string during) => Ui(async () =>
    {
        await using var h = new Host("script");
        var dictionary = VariableDictionary.Instance();
        var previous = dictionary.GetVariables().ToArray();
        var vm = new VariableViewModel { Automation = h.Service };
        var successor = new VariableViewModel { Automation = h.Service };
        var view = new VariableView { DataContext = vm };
        var window = new Window { Content = view };
        try
        {
            var declaration = new StringVariableDeclaration { Id = "reviewDraft", Value = "0x(01)" };
            h.Service.Mutate(() => dictionary.Add(declaration));
            window.Show(); window.UpdateLayout(); vm.SetupVariables();
            var old = vm.DataVariables.Single(v => v.Identifier == declaration.Id);
            vm.SelectedVariable = old;
            bool detached = false;
            VariableModel? added = null;
            void Invalidate()
            {
                if (detached) return;
                detached = true;
                view.DataContext = during == "rebind" ? successor : null;
            }
            vm.DataVariables.CollectionChanged += (_, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add)
                {
                    added = (VariableModel)args.NewItems![0]!;
                    if (during is "add" or "rebind") Invalidate();
                }
                if (during == "clear" && args.Action == NotifyCollectionChangedAction.Reset) Invalidate();
            };
            vm.PropertyChanged += (_, args) =>
            {
                if (during == "selection" && args.PropertyName == nameof(vm.SelectedVariable)) Invalidate();
            };
            var before = h.Service.PresentationVersion;
            vm.SetupVariables();
            vm.RefreshCommand.Execute();
            vm.SelectedVariable = added ?? old;
            vm.SelectedVariable.Value = "0x(FF)";
            vm.ModifyValueCommand.Execute(vm.SelectedVariable);
            Assert.That(detached && vm.IsSnapshotStale, Is.True);
            Assert.That(h.Service.PresentationVersion, Is.EqualTo(before));
            Assert.That(declaration.Value, Is.EqualTo("0x(01)"));
            view.DataContext = vm;
            Assert.That(vm.IsSnapshotStale, Is.False);
            Assert.That(h.Owner.CanStartGui, Is.True);
        }
        finally { window.Close(); dictionary.Clear(); foreach (var v in previous) dictionary.Add(v); }
    });

    [Test]
    public Task OnlyOriginalCertificateResumesNeverAttachedSlot() => Ui(async () =>
    {
        await using var service = new WorkbenchAutomationService();
        await using var foreign = new WorkbenchAutomationService();
        var owner = service.EditorCoordinator;
        var drain = owner.BeginDrain();
        Assert.That(drain.Completion.IsCompletedSuccessfully, Is.True);
        Assert.That(owner.ResumeGui(foreign.EditorCoordinator.BeginDrain()), Is.False);
        Assert.That(owner.ResumeGui(drain), Is.True);
        Assert.That(owner.Phase, Is.EqualTo(EditorControlPhase.Unavailable));
        service.AttachEditor(new WorkbenchTextEditor(), null!);
        Assert.That(owner.CanStartGui, Is.True);
        Assert.That(owner.ResumeGui(drain), Is.False);
    });

    [Test]
    public Task RetiredSelectionCannotCancelSuccessorPaste() => Ui(async () =>
    {
        await using var h = new Host("B");
        var a = new WorkbenchTextEditor { Text = "A" };
        var ra = h.Service.AttachEditor(a, null!, true)!;
        h.Service.DetachEditor(ra);
        h.Service.ActivateEditor(h.Registration);
        await using var foreign = new WorkbenchAutomationService();
        var clipboard = h.Window.Clipboard!;
        var proxy = DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var adapter = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        adapter.Inner = clipboard;
        var read = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.DelayedRead = read.Task;
        EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        try
        {
            ApplicationCommands.Paste.Execute(null, h.Area);
            var generation = h.Owner.Generation;
            a.Document = new TextDocument("orphan");
            foreign.AttachEditor(a, null!);
            a.Document = new TextDocument("rebound");
            Assert.That(h.Owner.Generation, Is.EqualTo(generation));
            read.SetResult("!");
            await h.Area.PendingClipboard;
            Assert.That(h.Editor.Text, Is.EqualTo("B!"));
            Assert.That(h.Owner.Pending, Is.Zero);
        }
        finally { read.TrySetResult(null); EditorCoordinatorTests.ClipboardProxy.Bind(clipboard); }
    });

    [TestCase(false)] [TestCase(true)]
    public Task IdenticalReplaceNextAdvancesAndWrapsWithoutAnUndoEntry(bool fromPanel) => Ui(async () =>
    {
        await using var h = new Host("cat CAT");
        h.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, h.Area);
        var panel = h.Editor.SearchPanel!;
        panel.Open(); panel.IsReplaceMode = true; panel.MatchCase = false; panel.SearchPattern = "cat"; panel.ReplacePattern = "cat";
        h.Editor.Select(0, 3); h.Editor.CaretOffset = 3;
        IInputElement target = fromPanel ? panel : h.Area;
        var version = h.Editor.Document.Version;
        SearchCommands.ReplaceNext.Execute(null, target);
        Assert.That(h.Editor.Document.Version, Is.SameAs(version));
        Assert.That(h.Editor.Document.UndoStack.CanRedo, Is.True);
        Assert.That(h.Editor.SelectionStart, Is.EqualTo(4));
        SearchCommands.ReplaceNext.Execute(null, target);
        Assert.That(h.Editor.Text, Is.EqualTo("cat cat"));
        version = h.Editor.Document.Version;
        SearchCommands.ReplaceNext.Execute(null, target);
        int offset = h.Editor.SelectionStart;
        SearchCommands.ReplaceNext.Execute(null, target);
        Assert.That(h.Editor.SelectionStart, Is.Not.EqualTo(offset));
        Assert.That(h.Editor.Document.Version, Is.SameAs(version));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("cat CAT"));
    });

    private sealed class Documentation : IInfoDocumentationProvider
    {
        public bool HasDocumentation(string name) => true;
        public bool TryGetDocumentation(string name, out string value) { value = "opened"; return true; }
        public bool TryGetDocument(InfoDocumentId id, out string value) { value = "opened"; return true; }
    }

    [Test]
    public Task DetachedMarkdownCommandsStayInactiveUntilReattachment() => Ui(async () =>
    {
        await using var h = new Host("script");
        using var first = new InfoViewModel(null, new Documentation()) { Automation = h.Service };
        using var next = new InfoViewModel(null, new Documentation()) { Automation = h.Service };
        var view = new InfoView { DataContext = first };
        var window = new Window { Content = view };
        try
        {
            window.Show(); window.UpdateLayout();
            var markdown = (Markdown.Avalonia.Markdown)view.FindControl<Markdown.Avalonia.MarkdownScrollViewer>("InfoMarkdownViewer")!.Engine;
            var old = markdown.HyperlinkCommand!;
            window.Content = null; window.UpdateLayout(); view.DataContext = next;
            const string link = "cryptoscript-info://mechanism/AES-CBC";
            var detached = markdown.HyperlinkCommand!;
            old.Execute(link); detached.Execute(link);
            Assert.That(old.CanExecute(link) || detached.CanExecute(link) || next.CanNavigateBack, Is.False);
            window.Content = view; window.UpdateLayout();
            markdown.HyperlinkCommand!.Execute(link);
            Assert.That(next.InfoText, Is.EqualTo("opened"));
            Assert.That(old.CanExecute(link) || detached.CanExecute(link), Is.False);
        }
        finally { window.Close(); }
    });
}
