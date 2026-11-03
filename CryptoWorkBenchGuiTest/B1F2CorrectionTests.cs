using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using AvaloniaEdit.Utils;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia;
using NUnit.Framework;
using static CryptoWorkBenchGuiTest.B1EditorIntegrationTests;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class B1F2CorrectionTests
{
    private class Policy(Func<int, bool> insert, Func<ISegment, IEnumerable<ISegment>> delete) : IReadOnlySectionProvider
    {
        public bool CanInsert(int offset) => insert(offset);
        public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) => delete(segment);
    }

    private sealed class RevisionedPolicy(Func<int, bool> insert, Func<ISegment, IEnumerable<ISegment>> delete,
        Func<long> revision) : Policy(insert, delete), IRevisionedSectionProvider
    {
        public long PermissionRevision => revision();
    }

    private static void SeedRedo(Host h)
    {
        h.Area.PerformTextInput("!");
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Document.UndoStack.CanRedo, Is.True);
    }

    [Test]
    public Task ReplacementUsesIncomingLiveDocumentAfterRetirement() => Ui(async () =>
    {
        await using var h = new Host("A");
        h.Area.PerformTextInput("a");
        var b = new WorkbenchTextEditor { Text = "B" };
        var rb = h.Service.AttachEditor(b, null!, true)!;
        var c = new WorkbenchTextEditor { Text = "C" };
        void Alias() { if (h.Owner.Phase == EditorControlPhase.Releasing) c.Document = h.Editor.Document; }
        h.Owner.WorkspaceChanged += Alias;
        Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(c, null!));
        h.Owner.WorkspaceChanged -= Alias;
        Assert.That(c.Coordinator, Is.Null);
        Assert.That(rb.Attached || h.Owner.Targets(h.Editor), Is.False);
        c.TextArea.PerformTextInput("bad");
        h.Service.ActivateEditor(h.Registration);
        Assert.That(h.Editor.Text, Is.EqualTo("Aa"));
        c.Document = new TextDocument("C");
        var rc = h.Service.AttachEditor(c, null!, true)!;
        c.CaretOffset = 1;
        c.TextArea.PerformTextInput("c");
        h.Service.ActivateEditor(h.Registration);
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("A"));
        ApplicationCommands.Redo.Execute(null, h.Area);
        h.Service.ActivateEditor(rc);
        ApplicationCommands.Undo.Execute(null, c.TextArea);
        Assert.That(c.Text, Is.EqualTo("C"));
        ApplicationCommands.Redo.Execute(null, c.TextArea);
        Assert.That((h.Editor.Text, c.Text, h.Owner.CanStartGui), Is.EqualTo(("Aa", "Cc", true)));
    });

    [TestCase(false)] [TestCase(true)]
    public Task DisposalWinsDuringReplacement(bool afterPublication) => Ui(async () =>
    {
        await using var h = new Host("A");
        var incoming = new WorkbenchTextEditor { Text = "B" };
        bool disposed = false;
        void Stop()
        {
            if (disposed || (afterPublication ? !h.Owner.Targets(incoming) : h.Owner.Phase != EditorControlPhase.Releasing)) return;
            disposed = true;
            h.Service.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        h.Owner.WorkspaceChanged += Stop;
        Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(incoming, null!));
        h.Owner.WorkspaceChanged -= Stop;
        Assert.That(disposed, Is.True);
        Assert.That(incoming.Coordinator, Is.Null);
        Assert.That(h.Registration.Attached, Is.False);
        incoming.TextArea.PerformTextInput("bad");
        Assert.That(incoming.Text, Is.EqualTo("B"));
        await using var fresh = new WorkbenchAutomationService();
        fresh.AttachEditor(incoming, null!);
        incoming.TextArea.PerformTextInput("!");
        Assert.That(incoming.Text, Does.Contain("!"));
        Assert.That(h.Owner.Pending, Is.Zero);
    });

    [TestCase(false)] [TestCase(true)]
    public Task NativeFinishAloneWakesOriginalDrain(bool close) => Ui(async () =>
    {
        await using var h = new Host("A");
        var b = new WorkbenchTextEditor { Text = "B" };
        var rb = h.Service.AttachEditor(b, null!, true)!;
        h.Service.ActivateEditor(h.Registration);
        var doc = h.Editor.Document;
        doc.BeginUpdate();
        EditorCoordinator.DrainTicket certificate;
        try
        {
            Assert.Throws<ExecutionBusyException>(() => h.Editor.Document = new TextDocument());
            Assert.Throws<ExecutionBusyException>(() => h.Service.ActivateEditor(rb));
            Assert.Throws<ExecutionBusyException>(() => h.Service.AttachEditor(new WorkbenchTextEditor(), null!));
            b.TextArea.PerformTextInput("bad");
            Assert.That(h.Editor.Document, Is.SameAs(doc));
            Assert.That(b.Text, Is.EqualTo("B"));
            certificate = h.Owner.BeginDrain();
            Assert.That(h.Owner.BeginDrain(), Is.SameAs(certificate));
            if (close) h.Service.DetachEditor(h.Registration);
            Assert.That(certificate.Completion.IsCompleted, Is.False);
        }
        finally { doc.EndUpdate(); }
        await certificate.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(h.Owner.ResumeGui(certificate), Is.True);
        Assert.That(h.Owner.ResumeGui(certificate), Is.False);
        h.Service.ActivateEditor(rb);
        b.TextArea.PerformTextInput("!");
        Assert.That((b.Text, h.Owner.CanStartGui), Is.EqualTo(("!B", true)));
    });

    [TestCase(false)] [TestCase(true)]
    public Task MultiMatchRequiresCoherentProtection(bool revisioned) => Ui(async () =>
    {
        await using var h = new Host("X---X");
        SeedRedo(h);
        var before = h.Registration.Snapshot();
        var original = h.Area.ReadOnlySectionProvider;
        bool rightProtected = false;
        bool Insert(int i)
        {
            if (h.Owner.Active && i == 0) rightProtected = true;
            return i != 4 || !rightProtected;
        }
        IEnumerable<ISegment> Delete(ISegment r) => r.Offset == 4 && rightProtected ? [] : [r];
        h.Area.ReadOnlySectionProvider = revisioned
            ? new RevisionedPolicy(Insert, Delete, () => rightProtected ? 1 : 0)
            : new Policy(Insert, Delete);
        var panel = h.Editor.SearchPanel!;
        panel.Open(); panel.IsReplaceMode = true; panel.SearchPattern = "X"; panel.ReplacePattern = "Y";
        SearchCommands.ReplaceAll.Execute(null, panel);
        Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
        Assert.That(rightProtected, Is.EqualTo(revisioned));
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("X---X!"));
        h.Area.ReadOnlySectionProvider = original;
        SearchCommands.ReplaceAll.Execute(null, panel);
        Assert.That(h.Editor.Text, Is.EqualTo("Y---Y!"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("X---X!"));
    });

    [TestCase("expand")] [TestCase("narrow")] [TestCase("throw")] [TestCase("deny")]
    public Task PasteCommitsOnlyOriginalSegments(string kind) => Ui(async () =>
    {
        await using var h = new Host("LOCKok");
        SeedRedo(h);
        h.Editor.Select(0,6); h.Editor.CaretOffset = 6;
        var before = h.Registration.Snapshot();
        var original = new Policy(_ => true, _ => kind == "deny" ? [] : [new SimpleSegment(4,2)]);
        int successorQueries = 0;
        var successor = new Policy(_ => true, s =>
        {
            successorQueries++;
            return kind switch { "throw" => throw new InvalidOperationException(), "expand" => [s], _ => [] };
        });
        h.Area.ReadOnlySectionProvider = original;
        void Swap(object? sender, EventArgs args) => h.Area.ReadOnlySectionProvider = successor;
        h.Editor.Document.UpdateStarted += Swap;
        await h.Window.Clipboard!.SetTextAsync("P");
        ApplicationCommands.Paste.Execute(null, h.Area);
        await h.Area.PendingClipboard;
        h.Editor.Document.UpdateStarted -= Swap;
        Assert.That(successorQueries, Is.Zero);
        Assert.That(h.Owner.CanStartGui && !h.Editor.Document.IsInUpdate, Is.True);
        if (kind == "deny")
        {
            Assert.That(h.Area.ReadOnlySectionProvider, Is.SameAs(original));
            Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
            ApplicationCommands.Redo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo("LOCKok!"));
            return;
        }
        Assert.That(h.Area.ReadOnlySectionProvider, Is.SameAs(successor));
        Assert.That(h.Editor.Text, Is.EqualTo("LOCKP"));
        ApplicationCommands.Undo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("LOCKok"));
        ApplicationCommands.Redo.Execute(null, h.Area);
        Assert.That(h.Editor.Text, Is.EqualTo("LOCKP"));
    });

    [TestCase(false)] [TestCase(true)]
    public Task ForeignObserverCannotAllocateExecution(bool collection) => Ui(async () =>
    {
        await using var h = new Host("host");
        await using var other = new WorkbenchAutomationService();
        var dict = VariableDictionary.Instance();
        var previous = dict.GetVariables().ToArray();
        try
        {
            var caller = WorkbenchAutomationService.LocalCaller;
            var request = new TextExecution(other.InstanceId, Guid.NewGuid(), "0",
                "VAR observerF2=0x(42)", TextExecutionMode.Submission);
            Task<Reply<ExecutionResult>>? result = null;
            void Submit() => result = other.ExecuteTextAsync(caller, request, default);
            var status = new StatusViewModel { Automation = h.Service };
            if (collection)
            {
                ((INotifyCollectionChanged)status.Diagnostics).CollectionChanged += (_, _) => Submit();
                status.ClearDiagnostics();
            }
            else
            {
                status.PropertyChanged += (_, _) => Submit();
                status.StatusString = "changed";
            }
            Assert.That((await result!).Error?.Code, Is.EqualTo(AutomationErrorCode.Busy));
            Assert.That((await other.GetExecutionAsync(caller, request.ExecutionId, default)).Error?.Code, Is.EqualTo(AutomationErrorCode.NotFound));
            Assert.That((dict.Contains("observerF2"), h.Service.PresentationVersion, other.PresentationVersion), Is.EqualTo((false, 0L, 0L)));
            Assert.That((await other.ExecuteTextAsync(caller, request, default)).Ok && dict.Contains("observerF2"), Is.True);
            h.Owner.Observe(Submit);
            Assert.That((await result!).Data?.ExecutionId, Is.EqualTo(request.ExecutionId));
        }
        finally { dict.Clear(); foreach (var v in previous) dict.Add(v); }
    });

    private sealed class Clock : TimeProvider
    {
        internal Action? OnTimestamp;
        public override long GetTimestamp()
        {
            var callback = OnTimestamp;
            OnTimestamp = null;
            callback?.Invoke();
            return base.GetTimestamp();
        }
    }

    [TestCase(false)] [TestCase(true)]
    public Task ReturnChecksOriginalIntentAtRegistryAdmission(bool late) => Ui(async () =>
    {
        await using var h = new Host("host");
        var clock = new Clock();
        await using var service = new WorkbenchAutomationService(clock);
        var history = new HistoryService();
        using var vm = new CryptoScriptEditViewModel(new(), new(), new(), new(history), null!, history, service);
        var view = new CryptoScriptEditView(vm);
        h.Window.Content = view;
        h.Window.UpdateLayout();
        var ed = view.FindControl<WorkbenchTextEditor>("Editor")!;
        ed.TextArea.Focus();
        var caller = WorkbenchAutomationService.LocalCaller;
        var seed = await service.ExecuteTextAsync(caller, new(service.InstanceId, Guid.NewGuid(), "0", "Print(0x(11))",
            TextExecutionMode.Submission), default);
        var dict = VariableDictionary.Instance();
        var original = dict.GetVariables().ToArray();
        try
        {
            ed.Text = "VAR returnF2=0x(42)";
            ed.CaretOffset = ed.Text.Length;
            var source = ed.Text;
            int calls = 0;
            void Revoke() { calls++; ed.CaretOffset = 0; }
            if (late) service.AdmissionCheckpoint = stage => { if (stage == "worker") Revoke(); };
            else clock.OnTimestamp = Revoke;
            ed.TextArea.PerformTextInput("\n");
            await view.PendingExecution;
            var status = (await service.GetStatusAsync(caller, default)).Data!;
            Assert.That(calls, Is.EqualTo(1));
            Assert.That((status.LastExecutionId, status.StateVersion), Is.EqualTo((seed.Data!.ExecutionId, seed.Data.StateVersion)));
            Assert.That((ed.Text, dict.Contains("returnF2"), service.IsBusy), Is.EqualTo((source, false, false)));
            service.AdmissionCheckpoint = null;
            ed.CaretOffset = ed.Text.Length;
            ed.TextArea.PerformTextInput("\n");
            await view.PendingExecution;
            Assert.That(dict.Contains("returnF2"), Is.True);
            Assert.That(service.PresentationVersion, Is.EqualTo(long.Parse(status.StateVersion) + 1));
        }
        finally { h.Window.Content = null; dict.Clear(); foreach (var v in original) dict.Add(v); }
    });

    [TestCase("dispose")] [TestCase("caret")] [TestCase("accept")]
    public Task CompletionPreservesExecutableHistory(string reason) => Ui(async () =>
    {
        await using var h = new Host("Info(AES");
        SeedRedo(h);
        var doc = h.Editor.Document;
        var before = h.Registration.Snapshot();
        using var completion = new InfoCompletionController(h.Editor, new());
        Assert.That(completion.OpenForTypedParenthesis(), Is.True);
        var window = (CompletionWindow)typeof(InfoCompletionController).GetField("_window",
            BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(completion)!;
        int called = 0;
        void Change(object? sender, EventArgs e)
        {
            called++;
            if (reason == "dispose") completion.Dispose();
            if (reason == "caret") h.Editor.CaretOffset = 0;
        }
        doc.UpdateStarted += Change;
        window.CompletionList.RequestInsertion(EventArgs.Empty);
        doc.UpdateStarted -= Change;
        Assert.That(called, Is.EqualTo(1));
        if (reason == "accept")
        {
            var completed = h.Editor.Text;
            Assert.That(completed, Is.Not.EqualTo(before.Text));
            ApplicationCommands.Undo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo(before.Text));
            ApplicationCommands.Redo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo(completed));
        }
        else
        {
            Assert.That(h.Registration.Snapshot(), Is.EqualTo(before));
            ApplicationCommands.Redo.Execute(null, h.Area);
            Assert.That(h.Editor.Text, Is.EqualTo("Info(AES!"));
        }
        Assert.That(h.Owner.CanStartGui && !doc.IsInUpdate, Is.True);
    });

    [TestCase(false)] [TestCase(true)]
    public Task VirtualPasteMatchesPinnedSelection(bool reverse) => Ui(async () =>
    {
        await using var h = new Host("abcd");
        var native = new TextEditor { Text = "abcd" };
        var window = new Window { Content = native, Width = 600, Height = 350 };
        window.Show(); window.UpdateLayout();
        try
        {
            async Task<(string, TextViewPosition, string, string)> Paste(TextEditor editor, Window host)
            {
                var area = editor.TextArea;
                area.Focus();
                editor.Options.EnableVirtualSpace = true;
                var start = new TextViewPosition(1, 5, reverse ? 9 : 6);
                var end = new TextViewPosition(1, 5, reverse ? 6 : 9);
                area.Caret.Position = end;
                area.Selection = area.Selection.StartSelectionOrSetEndpoint(start, end);
                await host.Clipboard!.SetTextAsync("P");
                ApplicationCommands.Paste.Execute(null, area);
                if (area is WorkbenchTextArea target) await target.PendingClipboard;
                var text = editor.Text;
                var caret = area.Caret.Position;
                ApplicationCommands.Undo.Execute(null, area);
                var undo = editor.Text;
                ApplicationCommands.Redo.Execute(null, area);
                return (text, caret, undo, editor.Text);
            }
            var expected = await Paste(native, window);
            Assert.That((expected.Item1, expected.Item3, expected.Item4), Is.EqualTo((reverse ? "abcd     P" : "abcd  P", "abcd", expected.Item1)));
            Assert.That(await Paste(h.Editor, h.Window), Is.EqualTo(expected));
        }
        finally { window.Close(); }
    });

    [Test]
    public Task MixedBatchMatchesPinnedAnchorAndHistorySemantics() => Ui(async () =>
    {
        await using var h = new Host("cat CAT");
        var native = new TextEditor { Text = h.Editor.Text };
        var window = new Window { Content = native, Width = 600, Height = 350 };
        window.Show(); window.UpdateLayout();
        try
        {
            static object Check(TextEditor ed)
            {
                ed.CaretOffset = ed.Text.Length;
                var a = ed.Document.CreateAnchor(1);
                var b = ed.Document.CreateAnchor(1);
                b.SurviveDeletion = true;
                var panel = ed.SearchPanel!;
                panel.Open(); panel.IsReplaceMode = true; panel.MatchCase = false;
                panel.SearchPattern = "cat"; panel.ReplacePattern = "cat";
                SearchCommands.ReplaceAll.Execute(null, panel);
                var result = (ed.Text, a.IsDeleted, b.Offset, ed.CaretOffset, ed.SelectionStart, ed.SelectionLength);
                Assert.That((ed.Text, a.IsDeleted), Is.EqualTo(("cat cat", true)));
                ApplicationCommands.Undo.Execute(null, ed.TextArea);
                Assert.That(ed.Text, Is.EqualTo("cat CAT"));
                ApplicationCommands.Redo.Execute(null, ed.TextArea);
                Assert.That(ed.Text, Is.EqualTo("cat cat"));
                return result;
            }
            Assert.That(Check(h.Editor), Is.EqualTo(Check(native)));
            SeedRedo(h);
            SearchCommands.ReplaceAll.Execute(null, h.Editor.SearchPanel);
            Assert.That(h.Editor.Document.UndoStack.CanRedo, Is.True);
        }
        finally { window.Close(); }
    });


    [TestCase("in-place")] [TestCase("replacement")] [TestCase("unrelated")]
    public Task DraftRevisionCoversForeignStateWriters(string change) => Ui(async () =>
    {
        await using var h = new Host("A");
        await using var foreign = new WorkbenchAutomationService();
        var dict = VariableDictionary.Instance();
        var previous = dict.GetVariables().ToArray();
        var value = new StringVariableDeclaration { Id = "draftF2", Value = "0x(01)" };
        try
        {
            h.Service.Mutate(() => dict.Add(value));
            var vm = new VariableViewModel { Automation = h.Service };
            vm.SetupVariables();
            var row = vm.SelectedVariable = vm.DataVariables.Single(x => x.Identifier == value.Id);
            row.Value = "0x(03)";
            foreign.Mutate(() =>
            {
                if (change == "replacement")
                {
                    dict.Clear();
                    dict.Add(new StringVariableDeclaration { Id = value.Id, Value = "0x(02)" });
                }
                else if (change == "in-place") value.Value = "0x(02)";
                else dict.Add(new StringVariableDeclaration { Id = "unrelatedF2", Value = "0x(04)" });
            });
            var foreignValue = dict.Get(value.Id).Value;
            var local = h.Service.PresentationVersion;
            vm.ModifyValueCommand.Execute(row);
            Assert.That((dict.Get(value.Id).Value, h.Service.PresentationVersion, vm.IsSnapshotStale), Is.EqualTo((foreignValue, local, true)));
            vm.RefreshCommand.Execute();
            var fresh = vm.SelectedVariable = vm.DataVariables.Single(x => x.Identifier == value.Id);
            fresh.Value = "0x(05)";
            vm.ModifyValueCommand.Execute(fresh);
            Assert.That(h.Service.PresentationVersion, Is.EqualTo(local + 1));
            Assert.That(dict.Get(value.Id).Value, Is.EqualTo(fresh.Value));
            Assert.That(vm.IsSnapshotStale, Is.False);
        }
        finally { dict.Clear(); foreach (var v in previous) dict.Add(v); }
    });

    [Test]
    public Task PreviousViewCommandsCannotDiscardCurrentUnsavedDraft() => Ui(async () =>
    {
        await using var h = new Host("A");
        var dict = VariableDictionary.Instance();
        var previous = dict.GetVariables().ToArray();
        var value = new StringVariableDeclaration { Id = "viewF2", Value = "0x(01)" };
        var vm = new VariableViewModel { Automation = h.Service };
        var w = new Window { Content = new VariableView { DataContext = vm }, Width = 600, Height = 350 };
        try
        {
            h.Service.Mutate(() => dict.Add(value));
            w.Show(); w.UpdateLayout();
            var retired = vm.RefreshCommand;
            var modify = vm.ModifyValueCommand;
            w.Content = null; w.UpdateLayout();
            w.Content = new VariableView { DataContext = vm }; w.UpdateLayout();
            vm.SetupVariables();
            w.UpdateLayout();
            var row = vm.DataVariables.Single(x => x.Identifier == value.Id);
            var grid = w.GetVisualDescendants().OfType<DataGrid>().Single(g => ReferenceEquals(g.ItemsSource, vm.DataVariables));
            grid.SelectedItem = row;
            vm.SelectedVariable = row;
            Assert.That(vm.SelectedVariable, Is.SameAs(row), $"successor selected {grid.SelectedIndex} of {grid.ItemsSource.Cast<object>().Count()}");
            row.Value = "0x(03)";
            Assert.That(retired, Is.Not.SameAs(vm.RefreshCommand));
            Assert.That(vm.SelectedVariable, Is.SameAs(row), "selection before invoking the retired commands");
            var pending = row.Value;
            var version = h.Service.PresentationVersion;
            retired.Execute(); modify.Execute(row);
            Assert.That(vm.SelectedVariable, Is.SameAs(row));
            Assert.That(vm.DataVariables.Single(x => x.Identifier == value.Id), Is.SameAs(row));
            Assert.That((row.Value, vm.IsSnapshotStale, h.Service.PresentationVersion), Is.EqualTo((pending, false, version)));
            vm.ModifyValueCommand.Execute(row);
            Assert.That(value.Value, Is.EqualTo(pending));
            vm.RefreshCommand.Execute();
            Assert.That(vm.DataVariables.Single(x => x.Identifier == value.Id).Value, Is.EqualTo(value.Value));
        }
        finally { w.Close(); dict.Clear(); foreach (var v in previous) dict.Add(v); }
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static VariableModel RemoveOrigin(Host h, VariableViewModel vm)
    {
        var dict = VariableDictionary.Instance();
        var value = new StringVariableDeclaration { Id = "goneF2", Value = "0x(01)" };
        h.Service.Mutate(() => dict.Add(value));
        vm.SetupVariables();
        var row = vm.DataVariables.Single(x => x.Identifier == value.Id);
        h.Service.Mutate(dict.Clear);
        return row;
    }

    [Test]
    public Task ExpiredOriginExposesRefreshWithoutWriting() => Ui(async () =>
    {
        await using var h = new Host("A");
        var dict = VariableDictionary.Instance();
        var previous = dict.GetVariables().ToArray();
        try
        {
            var vm = new VariableViewModel { Automation = h.Service };
            var row = RemoveOrigin(h, vm);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Assert.That(row.Origin!.TryGetTarget(out _), Is.False);
            var before = h.Service.PresentationVersion;
            vm.SelectedVariable = row;
            row.Value = "0x(10)";
            vm.ModifyValueCommand.Execute(row);
            Assert.That((vm.IsSnapshotStale, dict.Contains(row.Identifier), h.Service.PresentationVersion), Is.EqualTo((true, false, before)));
            vm.RefreshCommand.Execute();
            Assert.That(vm.IsSnapshotStale || vm.DataVariables.Contains(row), Is.False);
        }
        finally { dict.Clear(); foreach (var v in previous) dict.Add(v); }
    });


    [TestCase("Z")] [TestCase("Q\nRS")] [TestCase("Q\nRS\nT")] [TestCase("\t")]
    public Task RectangularClipboardMatchesNativeGeometryAndHistory(string paste) => Ui(async () =>
    {
        const string source = "ab\nwxyz\n";
        await using var h = new Host(source);
        var native = new TextEditor { Text = source };
        var window = new Window { Content = native, Width = 600, Height = 350 };
        window.Show(); window.UpdateLayout();
        try
        {
            async Task<object> Paste(TextEditor editor, Window host)
            {
                var area = editor.TextArea;
                area.Focus();
                editor.Options.EnableVirtualSpace = true;
                var start = new TextViewPosition(3, 1, 5);
                var end = new TextViewPosition(1, 2, 1);
                area.Caret.Position = end;
                area.Selection = new RectangleSelection(area, start, end);
                await host.Clipboard!.SetTextAsync(paste);
                ApplicationCommands.Paste.Execute(null, area);
                if (area is WorkbenchTextArea target) await target.PendingClipboard;
                var result = (editor.Text, area.Caret.Position, area.Selection.StartPosition,
                    area.Selection.EndPosition, area.Selection.GetType());
                ApplicationCommands.Undo.Execute(null, area);
                Assert.That(editor.Text, Is.EqualTo(source));
                ApplicationCommands.Redo.Execute(null, area);
                Assert.That(editor.Text, Is.EqualTo(result.Text));
                return result;
            }
            Assert.That(await Paste(h.Editor, h.Window), Is.EqualTo(await Paste(native, window)));
        }
        finally { window.Close(); }
    });


    [TestCase(false)] [TestCase(true)]
    public Task DocumentSwapRechecksLifecycleAfterWorkspaceNotification(bool stop) => Ui(async () =>
    {
        await using var h = new Host("original");
        var original = h.Editor.Document;
        EditorCoordinator.DrainTicket? drain = null;
        bool called = false;
        void Interrupt()
        {
            if (called) return;
            called = true;
            if (stop) h.Service.StopAccepting();
            else drain = h.Owner.BeginDrain();
        }
        h.Owner.WorkspaceChanged += Interrupt;
        Assert.Throws<ExecutionBusyException>(() => h.Editor.Document = new TextDocument("successor"));
        h.Owner.WorkspaceChanged -= Interrupt;
        Assert.That(called, Is.True);
        Assert.That(h.Editor.Document, Is.SameAs(original));
        Assert.That(h.Registration.RawDocument, Is.SameAs(original));
        if (drain != null)
        {
            await drain.Completion.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.That(h.Owner.ResumeGui(drain), Is.True);
            h.Editor.Document = new TextDocument("recovered");
            Assert.That(h.Registration.RawDocument, Is.SameAs(h.Editor.Document));
        }
    });

}
