using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public class DockInspectorTests
{
    private const string Id = DockShellState.InspectorId;
    private static readonly LayoutWindow Bounds = new(128, 93, 1090, 740, true);

    [Test] public Task LargeValueIsBoundedInCellsAndDetailsWithoutChangingTheSource() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        // A surrogate pair crosses the preview boundary.
        var raw = new string('s', 126) + "🌙" + new string('x', 65536);
        h.Service.Mutate(() =>
        {
            foreach (var (id, value) in new[] { ("short", "  Ω "), ("large", raw) })
                CryptoScript.Variables.VariableDictionary.Instance().Add(new CryptoScript.Variables.StringVariableDeclaration { Id = id, Value = value });
        }, h.Variables.ApplyVariables);
        h.Show(DockShellState.VariablesId);
        var view = h.Presentation.Variables!;
        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "VariablesGrid");
        var shortRow = h.Variables.DataVariables.Single(row => row.Identifier == "short");
        var largeRow = h.Variables.DataVariables.Single(row => row.Identifier == "large");
        string? Detail() => view.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "SelectedValue").Text;
        grid.SelectedItem = shortRow;
        h.Pump();
        var prefix = new string('s', 126) + "…";
        var texts = grid.GetVisualDescendants().OfType<SelectableTextBlock>().Select(t => t.Text).ToArray();
        Assert.That(texts, Does.Contain(prefix));
        Assert.That(texts, Does.Not.Contain(raw));
        Assert.That(Detail(), Is.EqualTo(shortRow.Value));
        grid.SelectedItem = largeRow;
        h.Pump();
        Assert.That(Detail(), Is.EqualTo(prefix));
        Assert.That(largeRow.Value, Is.SameAs(raw));
        grid.SelectedItem = null;
        h.Pump();
        Assert.That(h.Variables.SelectedVariable, Is.Null);
        Assert.That(view.GetVisualDescendants().OfType<SelectableTextBlock>().Any(t => t.Name == "SelectedValue" && t.IsEffectivelyVisible), Is.False);
    });

    [Test] public Task IdenticalIdentifierPreviewsRetainDistinctSelectionsAndHistoricalValues() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        string prefix = "v" + new string('a', 4094);
        string[] ids = [prefix + "a", prefix + "b"];
        string expected = prefix[..127] + "…";
        h.Service.Mutate(() =>
        {
            foreach (var id in ids)
                CryptoScript.Variables.VariableDictionary.Instance().Add(new CryptoScript.Variables.StringVariableDeclaration { Id = id, Value = id[^1].ToString() });
        }, h.Variables.ApplyVariables);
        var pane = h.Layout.Pane(Id);
        Assert.That(new DockManager(new DockService()).ValidateDockable(pane, (IDock)pane.Owner!, DragAction.Move, DockOperation.Right, true), Is.True);
        h.Show(DockShellState.VariablesId);
        var variables = h.Presentation.Variables!;
        var inspector = h.Presentation.Inspector!;
        var model = h.Presentation.InspectorModel!;
        var grid = variables.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "VariablesGrid");
        grid.SelectedItem = null;
        h.Pump();
        Assert.That(h.Variables.SelectedVariable, Is.Null);
        Assert.That(grid.GetVisualDescendants().OfType<SelectableTextBlock>().Count(t => t.Text == expected), Is.EqualTo(2));
        foreach (var id in ids)
        {
            var row = h.Variables.DataVariables.Single(v => v.Identifier == id);
            grid.SelectedItem = row;
            await DockEditorTests.Wait(() => model.Title == id && model.Page != null && !model.Busy);
            h.Pump();
            Assert.That(h.Variables.SelectedVariable, Is.SameAs(row));
            Assert.That(row.Identifier, Is.SameAs(id));
            var detail = variables.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "SelectedIdentifier");
            Assert.That(detail.Text, Is.EqualTo(expected));
            await AssertSelectionCopy(h.Window, detail, 0, expected.Length, id);
            await AssertSelectionCopy(h.Window, detail, 8, 1, id[1..8]);
            await AssertSelectionCopy(h.Window, detail, expected.Length, 125, id[125..]);
            Assert.That(inspector.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text), Does.Contain(expected));
            var idProperty = model.Page!.Rows.Single(r => r.Name == "Id");
            string? copied = null;
            await model.ValueAsync(idProperty, false, text => { copied = text; return Task.CompletedTask; });
            Assert.That(copied, Is.EqualTo(id));
            if (id == ids[1]) h.Session.Editor.Text += "// historical";
            await model.OpenAsync(idProperty);
            h.Pump();
            var ancestor = inspector.GetVisualDescendants().OfType<Button>().Single(b => b.Content is string s && s.StartsWith("▾ "));
            Assert.That(ancestor.Content, Is.EqualTo("▾ " + id[..125] + "…"));
            Assert.That(AutomationProperties.GetName(ancestor), Is.EqualTo("Collapse to " + id[..115] + "…"));
            Assert.That(ToolTip.GetTip(ancestor), Is.EqualTo(AutomationProperties.GetName(ancestor)));
            copied = null;
            await model.ValueAsync(null, false, text => { copied = text; return Task.CompletedTask; });
            Assert.That(copied, Is.EqualTo(id));
            if (id == ids[1]) Assert.That(model.Status, Does.Contain("Historical"));
            await model.OpenAsync(null, back: true);
        }
        grid.SelectedItem = null;
        await DockEditorTests.Wait(() => model.Page == null && h.Variables.SelectedVariable == null);
    });

    [Test] public Task ParameterCellsPreserveSourceKeysValuesOrderAndQuotedFormatting() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        string quoted = "\"" + new string('q', 125) + "🌙" + new string('x', 4096) + "\"";
        var declaration = new CryptoScript.CryptoAlgorithm.KDF.KDF_SP800_108_COUNTER().GenerateParameters(
            "KDF-SP800-108-COUNTER", ["#PRF:HMAC-SHA256", "#OUTLEN:128", "#LABEL:" + quoted]);
        declaration.Id = "paramsForDisplay";
        string key = "#" + new string('k', 125) + "🌙tail";
        // Synthetic metadata exercises the general dictionary template independently of KDF's allowed names.
        declaration.GetParameters().Add(key, "\"short Ω\"");
        h.Service.Mutate(() => CryptoScript.Variables.VariableDictionary.Instance().Add(declaration), h.Variables.ApplyVariables);
        h.Show(DockShellState.VariablesId);
        var view = h.Presentation.Variables!;
        var grid = view.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "VariablesGrid");
        var selected = h.Variables.DataVariables.Single(v => v.Identifier == declaration.Id);
        grid.SelectedItem = selected;
        h.Pump();
        var nested = view.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "ParametersGrid");
        Assert.That(selected.Parameters.Select(p => p.Parameter), Is.EqualTo(declaration.GetParameters().Keys));
        foreach (var entry in selected.Parameters)
        {
            var original = declaration.GetParameters().Single(kv => kv.Key == entry.Parameter);
            Assert.That(entry.Parameter, Is.SameAs(original.Key));
            Assert.That(entry.Value, Is.SameAs(original.Value));
            string[] expectedCells = [entry.Parameter == key ? key[..126] + "…" : original.Key,
                entry.Parameter == "#LABEL" ? quoted[..126] + "…" : original.Value];
            for (int i = 0; i < nested.Columns.Count; i++)
            {
                nested.ScrollIntoView(entry, nested.Columns[i]);
                h.Pump();
                var cell = nested.GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Single(t => ReferenceEquals(t.DataContext, entry) && t.Text == expectedCells[i]);
                await AssertSelectionCopy(h.Window, cell, 0, cell.Text!.Length, i == 0 ? original.Key : original.Value);
            }
        }
        grid.SelectedItem = null;
        h.Pump();
        Assert.That(h.Variables.SelectedVariable, Is.Null);
    });

    [Test] public Task ScalarPropertyRowKeepsItsExistingPreviewWidthAtASurrogateBoundary() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var text = new string('r', 119) + "🌙tail";
        var declaration = new CryptoScript.Variables.StringVariableDeclaration { Id = "withPair", Value = text };
        h.Service.Mutate(() => CryptoScript.Variables.VariableDictionary.Instance().Add(declaration), h.Variables.ApplyVariables);
        h.Variables.SelectedVariable = h.Variables.DataVariables.Single(v => v.Identifier == declaration.Id);
        h.Show(Id);
        var model = h.Presentation.InspectorModel!;
        await DockEditorTests.Wait(() => model.Page != null && !model.Busy);
        var rendered = h.Presentation.Inspector!.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text);
        Assert.That(rendered, Does.Contain(new string('r', 119) + "…"));
        Assert.That(h.Variables.SelectedVariable.Value, Is.SameAs(text));
    });

    [Test] public Task ExpandedSelectionReportsAsyncWriteFailureAndCanRetry() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        string id = "v" + new string('a', 255);
        h.Service.Mutate(() => CryptoScript.Variables.VariableDictionary.Instance().Add(
            new CryptoScript.Variables.StringVariableDeclaration { Id = id, Value = "short" }), h.Variables.ApplyVariables);
        h.Show(DockShellState.VariablesId);
        var row = h.Variables.DataVariables.Single(v => v.Identifier == id);
        h.Variables.SelectedVariable = row;
        h.Pump();
        var field = h.Presentation.Variables!.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "SelectedIdentifier");
        var status = h.Window.FindControl<TextBlock>("ShellStatus")!;
        var clipboard = h.Window.Clipboard!;
        await clipboard.SetTextAsync("before failure");
        var proxy = System.Reflection.DispatchProxy.Create<IClipboard, EditorCoordinatorTests.ClipboardProxy>();
        var adapter = (EditorCoordinatorTests.ClipboardProxy)(object)proxy;
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.Inner = clipboard;
        adapter.DelayedWrite = pending.Task;
        EditorCoordinatorTests.ClipboardProxy.Bind(proxy);
        try
        {
            Assert.That(field.Focus(), Is.True);
            field.SelectionStart = field.Text!.Length;
            field.SelectionEnd = 3;
            h.Window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
            pending.SetException(new System.Runtime.InteropServices.COMException("Clipboard is busy."));
            await DockEditorTests.Wait(() => status.Text == "Copy failed. Clipboard is busy.");
            Assert.That(status.IsEffectivelyVisible, Is.True);
            Assert.That(await clipboard.TryGetTextAsync(), Is.EqualTo("before failure"));
            Assert.That(h.Variables.SelectedVariable, Is.SameAs(row));
            adapter.DelayedWrite = null;
            h.Window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
            await DockEditorTests.Wait(() => status.Text == $"Copied {id.Length - 3:N0} characters.");
            Assert.That(await clipboard.TryGetTextAsync(), Is.EqualTo(id[3..]));
        }
        finally { EditorCoordinatorTests.ClipboardProxy.Bind(clipboard); }
    });

    [Test, Platform("Win")] public Task NulRejectionPreservesClipboardAndAllowsRepresentableSelections() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        string raw = "abc\0" + new string('x', 180) + "tail";
        h.Service.Mutate(() => CryptoScript.Variables.VariableDictionary.Instance().Add(
            new CryptoScript.Variables.StringVariableDeclaration { Id = "nulValue", Value = raw }), h.Variables.ApplyVariables);
        h.Show(DockShellState.VariablesId);
        var row = h.Variables.DataVariables.Single(v => v.Identifier == "nulValue");
        h.Variables.SelectedVariable = row;
        h.Pump();
        var field = h.Presentation.Variables!.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "SelectedValue");
        var status = h.Window.FindControl<TextBlock>("ShellStatus")!;
        var clipboard = h.Window.Clipboard!;
        Assert.That(field.Focus(), Is.True);
        foreach (int end in new[] { field.Text!.Length, 5 })
        {
            await clipboard.SetTextAsync("NUL sentinel");
            field.SelectionStart = 0;
            field.SelectionEnd = end;
            h.Window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
            Assert.That(status.Text, Is.EqualTo("Copy failed. Clipboard text cannot preserve embedded NUL."));
            Assert.That(await clipboard.TryGetTextAsync(), Is.EqualTo("NUL sentinel"));
        }
        await AssertSelectionCopy(h.Window, field, 0, 3, "abc");
        await AssertSelectionCopy(h.Window, field, field.Text.Length, 4, raw[4..]);
        Assert.That(row.Value, Is.SameAs(raw));
    });

    [Test, Platform("Win")]
    public Task NewerNulErrorSurvivesOlderSuccessfulCopy() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var fields = new SelectionCopyFields(h, new string('q', 170) + "\0tail");
        await fields.Clipboard.SetTextAsync("sentinel");
        using var delayed = DelayedSelectionWrite.Install(fields.Clipboard);
        fields.Copy(fields.Identifier, 3);
        fields.Copy(fields.Value);
        const string error = "Copy failed. Clipboard text cannot preserve embedded NUL.";
        Assert.That(fields.Status.Text, Is.EqualTo(error));
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo("sentinel"));

        await delayed.CompleteAsync();
        Assert.That(fields.Status.Text, Is.EqualTo(error));
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(fields.OriginalId[3..]));
    });

    [TestCase(true)]
    [TestCase(false)]
    public Task NewerSelectionSupersedesOlderFailedCopy(bool expand) => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var fields = new SelectionCopyFields(h, new string('w', 300));
        using var delayed = DelayedSelectionWrite.Install(fields.Clipboard);
        fields.Copy(fields.Identifier);
        string? previous = fields.Status.Text;
        fields.Copy(fields.Value, 1, expand ? fields.Value.Text!.Length : 5);
        string expected = expand ? fields.OriginalValue[1..] : fields.OriginalValue[1..5];
        string? status = expand ? $"Copied {expected.Length:N0} characters." : previous;
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(expected));
        Assert.That(fields.Status.Text, Is.EqualTo(status));

        await delayed.CompleteAsync(fail: true);
        Assert.That(fields.Status.Text, Is.EqualTo(status));
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(expected));
    });

    [Test] public Task SequentialSuccessfulSelectionCopiesReportEachResult() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var fields = new SelectionCopyFields(h, new string('s', 360));
        foreach (var (field, original) in new[] { (fields.Identifier, fields.OriginalId), (fields.Value, fields.OriginalValue) })
        {
            fields.Copy(field);
            await DockEditorTests.Wait(() => fields.Status.Text == $"Copied {original.Length:N0} characters.");
            Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(original));
        }
    });

    [TestCase(true)]
    [TestCase(false)]
    public Task InactiveVariablesViewDoesNotReportPendingCopy(bool dispose) => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var fields = new SelectionCopyFields(h, "ordinary");
        using var delayed = DelayedSelectionWrite.Install(fields.Clipboard);
        fields.Copy(fields.Identifier);
        if (dispose) h.Presentation.Variables!.Dispose();
        else
        {
            await WaitForVariablesCloseAdmission(h);
            h.Layout.Close(DockShellState.VariablesId);
            Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Does.Not.Contain(h.Layout.Pane(DockShellState.VariablesId)));
        }
        string? status = fields.Status.Text;
        await delayed.CompleteAsync();
        Assert.That(fields.Status.Text, Is.EqualTo(status));
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(fields.OriginalId));
    });

    private static Task WaitForVariablesCloseAdmission(DockToolTests.Host h) =>
        DockEditorTests.Wait(() => h.Presentation.InspectorModel is { Busy: false, Page: not null } && h.Session.CanRelocate);

    [Test] public Task RejectedVariablesClosePreservesPendingCopyFeedback() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var fields = new SelectionCopyFields(h, "ordinary");
        using var delayed = DelayedSelectionWrite.Install(fields.Clipboard);
        fields.Copy(fields.Identifier);
        await WaitForVariablesCloseAdmission(h);
        using (InterpreterHostQueue.Reserve())
        {
            Assert.That(h.Session.CanRelocate, Is.False);
            h.Layout.Close(DockShellState.VariablesId);
            Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Does.Contain(h.Layout.Pane(DockShellState.VariablesId)));
        }
        await delayed.CompleteAsync();
        Assert.That(fields.Status.Text, Is.EqualTo($"Copied {fields.OriginalId.Length:N0} characters."));
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(fields.OriginalId));
    });

    [TestCase(false)]
    [TestCase(true)]
    public Task ClosedVariablesSuppressFailedCopyAndReopenAllowsFreshFeedback(bool floating) => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        var window = floating ? h.Float(DockShellState.VariablesId) : h.Window;
        var fields = new SelectionCopyFields(h, "ordinary");
        await fields.Clipboard.SetTextAsync("sentinel");
        using var delayed = DelayedSelectionWrite.Install(fields.Clipboard);
        fields.Copy(fields.Identifier);
        await WaitForVariablesCloseAdmission(h);
        if (floating) window.Close();
        else h.Layout.Close(DockShellState.VariablesId);
        Assert.That(DockShellLayout.VisibleTree(h.Layout.Root), Does.Not.Contain(h.Layout.Pane(DockShellState.VariablesId)));

        string? status = fields.Status.Text;
        await delayed.CompleteAsync(fail: true);
        Assert.That(fields.Status.Text, Is.EqualTo(status));
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo("sentinel"));

        h.Show(DockShellState.VariablesId);
        var identifier = h.Presentation.Variables!.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Single(field => field.Name == "SelectedIdentifier");
        fields.Copy(identifier, 3);
        string suffix = fields.OriginalId[3..];
        await DockEditorTests.Wait(() => fields.Status.Text == $"Copied {suffix.Length:N0} characters.");
        Assert.That(await fields.Clipboard.TryGetTextAsync(), Is.EqualTo(suffix));
    });

    private sealed class SelectionCopyFields
    {
        internal string OriginalId { get; } = "v" + new string('i', 255);
        internal string OriginalValue { get; }
        internal IClipboard Clipboard { get; }
        internal SelectableTextBlock Identifier { get; }
        internal SelectableTextBlock Value { get; }
        internal TextBlock Status { get; }

        internal SelectionCopyFields(DockToolTests.Host h, string value)
        {
            OriginalValue = value;
            h.Service.Mutate(() => CryptoScript.Variables.VariableDictionary.Instance().Add(
                new CryptoScript.Variables.StringVariableDeclaration { Id = OriginalId, Value = value }), h.Variables.ApplyVariables);
            h.Show(DockShellState.VariablesId);
            h.Variables.SelectedVariable = h.Variables.DataVariables.Single(v => v.Identifier == OriginalId);
            h.Pump();
            Clipboard = h.Window.Clipboard!;
            Status = h.Window.FindControl<TextBlock>("ShellStatus")!;
            Assert.That(Status.IsEffectivelyVisible, Is.True);
            var fields = h.Presentation.Variables!.GetVisualDescendants().OfType<SelectableTextBlock>();
            Identifier = fields.Single(f => f.Name == "SelectedIdentifier");
            Value = fields.Single(f => f.Name == "SelectedValue");
        }

        internal void Copy(SelectableTextBlock field, int start = 0, int? end = null)
        {
            Assert.That(field.Focus(), Is.True);
            field.SelectionStart = start;
            field.SelectionEnd = end ?? field.Text!.Length;
            ((Window)TopLevel.GetTopLevel(field)!).KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        }
    }

    // Hold the first real transfer; later copies go straight to the headless clipboard.
    public class DelayedSelectionWrite : EditorCoordinatorTests.ClipboardProxy, IDisposable
    {
        private readonly TaskCompletionSource _release = new();
        private Task? _pending;

        internal static DelayedSelectionWrite Install(IClipboard clipboard)
        {
            var proxy = System.Reflection.DispatchProxy.Create<IClipboard, DelayedSelectionWrite>();
            var adapter = (DelayedSelectionWrite)(object)proxy;
            adapter.Inner = clipboard;
            Bind(proxy);
            return adapter;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IClipboard.SetDataAsync) && _pending == null)
                return _pending = ForwardAfterReleaseAsync((IAsyncDataTransfer)args![0]!);
            return base.Invoke(method, args);
        }

        private async Task ForwardAfterReleaseAsync(IAsyncDataTransfer transfer)
        {
            await _release.Task;
            await Inner.SetDataAsync(transfer);
        }

        internal async Task CompleteAsync(bool fail = false)
        {
            Assert.That(_pending, Is.Not.Null);
            if (fail) _release.SetException(new IOException("Older copy failed."));
            else _release.SetResult();
            try { await _pending!; }
            catch (IOException) when (fail) { }
            // Let the product event continuation publish before asserting the final status.
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
        }

        public void Dispose()
        {
            Bind(Inner);
            _release.TrySetResult();
        }
    }

    private static async Task AssertSelectionCopy(Window window, SelectableTextBlock field, int start, int end, string expected)
    {
        var clipboard = window.Clipboard!;
        await clipboard.SetTextAsync("before copy");
        Assert.That(field.Focus(), Is.True);
        field.SelectionStart = start;
        field.SelectionEnd = end;
        window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
        Assert.That(await clipboard.TryGetTextAsync(), Is.EqualTo(expected));
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public Task TemporaryStartupFailurePersistsOnlyChangedDockLayout(bool migration, bool change) => GuiTestSession.RunAsync(async () =>
    {
        var directory = Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "layout-recovery", Guid.NewGuid().ToString("N")));
        string v1 = Path.Combine(directory.FullName, "dock-layout-v1.json");
        string v2 = Path.Combine(directory.FullName, "dock-layout-v2.json");
        File.WriteAllText(v1, DockPersistenceCleanupTests.HistoricalJson());
        var originalV1 = File.ReadAllBytes(v1);
        var store = new DockLayoutStore(v2, v1);
        byte[]? originalV2 = null;
        if (migration)
        {
            var failOnce = true;
            store = new(v2, v1) { BeforeReplace = () =>
            {
                if (!failOnce) return;
                failOnce = false;
                throw new IOException("migration fault");
            } };
            Assert.That(store.Load(), Is.Null);
        }
        else
        {
            Assert.That(store.Load(), Is.Not.Null);
            originalV2 = File.ReadAllBytes(v2);
            using (File.Open(v2, FileMode.Open, FileAccess.Read, FileShare.None))
                Assert.That(store.Load(), Is.Null);
        }
        Assert.That(store.LastError, Is.Not.Null);
        await using (var h = new DockToolTests.Host())
        {
            h.Window.EnablePersistence(store);
            h.Window.Width += 80;
            if (change) h.Float(Id);
        }
        Assert.That(File.ReadAllBytes(v1), Is.EqualTo(originalV1));
        if (migration) Assert.That(File.Exists(v2), Is.False);
        else if (!change) Assert.That(File.ReadAllBytes(v2), Is.EqualTo(originalV2));
        var next = new DockLayoutStore(v2, v1).Load();
        Assert.That(next, Is.Not.Null);
        Assert.That(next!.ClosedTools.Contains(Id), Is.EqualTo(!change));
        Assert.That(next.Root.Windows, Has.Count.EqualTo(1));
        Assert.That(DockShellLayout.VisibleTree(next.Root.Windows![0].Layout!).Any(p => p.Id == (change ? Id : DockShellState.InfoId)), Is.True);
    });

    private static async Task Select(DockToolTests.Host h)
    {
        await h.Session.ViewModel.ParseLineAsync("VAR i3selected=0x(11223344)");
        h.Variables.SelectedVariable = h.Variables.DataVariables.Single(v => v.Identifier == "i3selected");
        await DockEditorTests.Wait(() => h.Presentation.InspectorModel is { Page: not null, Busy: false });
    }

    [Test] public Task RelocationRetainsPageAndSelectionUntilSemanticClose() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        await Select(h);
        var model = h.Presentation.InspectorModel!;
        var page = model.Page;
        var view = h.Presentation.Inspector;
        var pane = h.Layout.Pane(Id);
        var group = (IDock)pane.Owner!;
        var selected = h.Variables.SelectedVariable;
        var editor = h.Session.Editor;
        var document = editor.Document;
        var undo = document.UndoStack;
        long revision = InterpreterHostQueue.StateRevision;
        Assert.That(group.ActiveDockable!.Id, Is.EqualTo(DockShellState.VariablesId));
        var button = h.Presentation.Variables!.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Inspect Variable"));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.That(group.ActiveDockable, Is.SameAs(pane));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(new DockManager(new DockService()).ValidateDockable(pane, group, DragAction.Move, DockOperation.Right, true), Is.True);
            Assert.That(pane.Owner, Is.Not.SameAs(group));
            h.Layout.Dock(Id);
            Assert.That(pane.Owner, Is.SameAs(group));
            var host = h.Float(Id);
            Assert.That(TopLevel.GetTopLevel(view!), Is.SameAs(host));
            h.Layout.Dock(Id);
            Assert.That(host.IsVisible, Is.False);
            var normal = h.Layout.Capture(Bounds);
            Assert.That(h.Layout.Maximize(Id), Is.True);
            h.Layout.Close(Id);
            Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(normal));
            Assert.That(h.Layout.Restore(), Is.True);
            Assert.That(h.Layout.Capture(Bounds), Is.EqualTo(normal));
            Assert.That(h.Layout.Reset(), Is.True);
            group = (IDock)pane.Owner!;
            Assert.That(group.ActiveDockable!.Id, Is.EqualTo(DockShellState.VariablesId));
            h.Show(Id);
            Assert.That(h.Presentation.InspectorModel, Is.SameAs(model));
            Assert.That(h.Presentation.Inspector, Is.SameAs(view));
            Assert.That(model.Page, Is.SameAs(page));
            Assert.That(h.Variables.SelectedVariable, Is.SameAs(selected));
            Assert.That(editor.Document, Is.SameAs(document));
            Assert.That(document.UndoStack, Is.SameAs(undo));
            Assert.That(InterpreterHostQueue.StateRevision, Is.EqualTo(revision));
        }
        var floating = h.Float(Id);
        floating.Close(); h.Pump();
        Assert.That(h.Presentation.InspectorModel, Is.Null);
        Assert.That(model.Page, Is.Null);
        Assert.That(h.Presentation.Inspector, Is.Null);
        h.Show(Id);
        var reopened = h.Presentation.InspectorModel!;
        Assert.That(reopened, Is.Not.SameAs(model));
        Assert.That(h.Layout.Pane(Id), Is.SameAs(pane));
        await DockEditorTests.Wait(() => reopened.Page != null && !reopened.Busy);
        Assert.That(reopened.Title, Is.EqualTo("i3selected"));
        Assert.That(model.Page, Is.Null);
        h.Layout.Factory.CloseDockable(pane);
        Assert.That(h.Presentation.Inspector, Is.Null);
        Assert.That(reopened.Page, Is.Null);
    });

    [Test] public Task BackFromScalarKeepsKeyboardFocusInInspector() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        await Select(h);
        h.Show(Id);
        var model = h.Presentation.InspectorModel!;
        var view = h.Presentation.Inspector!;
        await model.OpenAsync(model.Page!.Rows.Single(row => row.Name == "Value"));
        var back = view.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Parent property");
        Assert.That(back.Focus(), Is.True);
        h.Window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, "");
        await DockEditorTests.Wait(() => model.Depth == 0 && view.IsKeyboardFocusWithin);
        Assert.That(h.Window.FocusManager!.GetFocusedElement(), Is.InstanceOf<Button>());
        Assert.That(back.IsEnabled, Is.False);
        Assert.That(((Button)h.Window.FocusManager.GetFocusedElement()!).IsEffectivelyEnabled, Is.True);
    });

    [Test] public Task ReopenReleasesSubscriptionsAndRetainsNonfirstSelection() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        await h.Session.ViewModel.ParseLineAsync("VAR first=0x(11) VAR second=0x(22)");
        var chosen = h.Variables.DataVariables.Single(v => v.Identifier == "second");
        var grid = h.Presentation.Variables!.GetVisualDescendants().OfType<DataGrid>().Single(g => g.Name == "VariablesGrid");
        grid.SelectedItem = chosen;
        Assert.That(h.Variables.SelectedVariable, Is.SameAs(chosen));
        await DockEditorTests.Wait(() => h.Presentation.InspectorModel is { Title: "second", Page: not null, Busy: false });
        var coordinator = h.Service.EditorCoordinator;
        var change = coordinator.GetType().GetField("WorkspaceChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        int Listeners() => ((Delegate?)change.GetValue(coordinator))?.GetInvocationList().Length ?? 0;
        int openCount = Listeners();
        for (int i = 0; i < 8; i++)
        {
            var old = h.Presentation.InspectorModel!;
            h.Layout.Close(Id);
            Assert.That(Listeners(), Is.EqualTo(openCount - 1));
            h.Show(DockShellState.VariablesId);
            Assert.That(grid.SelectedItem, Is.SameAs(chosen));
            h.Layout.Show(Id);
            Assert.That(Listeners(), Is.EqualTo(openCount));
            // Close before the deferred selection read can publish.
            var transient = h.Presentation.InspectorModel!;
            h.Layout.Close(Id);
            h.Pump();
            Assert.That(transient.Page, Is.Null);
            h.Show(Id);
            await DockEditorTests.Wait(() => h.Presentation.InspectorModel is { Page: not null, Busy: false });
            Assert.That(h.Variables.SelectedVariable, Is.SameAs(chosen));
            Assert.That(h.Presentation.InspectorModel!.Title, Is.EqualTo("second"));
            Assert.That(old.Page, Is.Null);
            Assert.That(Listeners(), Is.EqualTo(openCount));
        }
    });

    [Test] public Task HistoricalCopyRemainsAvailableDuringLeaseAndShutdownDrainsHandoff() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        await Select(h);
        var model = h.Presentation.InspectorModel!;
        var host = h.Float(Id);
        var row = model.Page!.Rows.Single(r => r.Name == "Value");
        string? copied = null;
        Task Copy(string text) { copied = text; return Task.CompletedTask; }
        await model.ValueAsync(row, false, Copy);
        Assert.That(copied, Is.EqualTo("0x(11223344)"));
        h.Session.Editor.Text += "// change";
        var snapshot = h.Service.CaptureSaveIntent()!.Snapshot;
        h.Service.ActivateMcp();
        var caller = h.Service.IssueCredential("i3", CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService.LocalCaller.Permissions.Add(AutomationPermissions.EditorControl));
        var lease = (await h.Service.AcquireEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
        h.Layout.Dock(Id); h.Layout.Close(Id);
        Assert.That(host.IsVisible, Is.True);
        copied = null;
        await model.ValueAsync(row, false, Copy);
        Assert.That(copied, Is.EqualTo("0x(11223344)"));
        await h.Service.ReleaseEditorAsync(caller, new("2", h.Service.InstanceId, snapshot.EditorId, lease.LeaseId), default);
        var started = new TaskCompletionSource();
        var finish = new TaskCompletionSource();
        var transfer = model.ValueAsync(row, false, async text => { started.SetResult(); await finish.Task; });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            h.Window.Close();
            await Task.Delay(40);
            Assert.That(h.Window.IsVisible && host.IsVisible, Is.True);
        }
        finally { finish.TrySetResult(); }
        await transfer;
        await DockEditorTests.Wait(() => !h.Window.IsVisible);
        Assert.That(host.IsVisible, Is.False);
        Assert.That(model.Page, Is.Null);
        Assert.That(h.Service.HasRegisteredEditor, Is.False);
    });

    [Test] public Task MigrationPreservesEachOfZeroThroughSixHosts() => GuiTestSession.RunAsync(async () =>
    {
        await using var h = new DockToolTests.Host();
        h.Layout.Close(Id);
        h.Layout.Move(DockShellState.OutputId, DockShellLayout.Tools);
        var output = h.Layout.Pane(DockShellState.OutputId);
        var source = (IDock)output.Owner!;
        Assert.That(new DockManager(new DockService()).ValidateDockable(output, source, DragAction.Move, DockOperation.Right, true), Is.True);
        Assert.That(output.Owner, Is.Not.SameAs(source));
        var ids = DockLayoutStore.PaneIds.Where(id => id != Id).ToArray();
        for (int n = 0; n <= ids.Length; n++)
        {
            if (n > 0) h.Float(ids[n - 1]);
            var original = h.Layout.Capture(Bounds);
            var legacy = JsonNode.Parse(original)!;
            legacy["SchemaVersion"] = 1;
            var closed = legacy["ClosedTools"]!.AsArray();
            closed.Remove(closed.Single(item => item!.GetValue<string>() == Id));
            var dir = Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.WorkDirectory, "i3-migrate", Guid.NewGuid().ToString("N")));
            string v1 = Path.Combine(dir.FullName, "dock-layout-v1.json");
            string v2 = Path.Combine(dir.FullName, "dock-layout-v2.json");
            File.WriteAllText(v1, legacy.ToJsonString());
            var bytes = File.ReadAllBytes(v1);
            var store = new DockLayoutStore(v2, v1);
            var loaded = store.Load()!;
            Assert.That(loaded, Is.Not.Null, store.LastError);
            Assert.That(loaded.Root.Windows?.Count ?? 0, Is.EqualTo(n));
            Assert.That(File.ReadAllText(v2), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(v1), Is.EqualTo(bytes));
            File.WriteAllText(v1, "{");
            Assert.That(store.Load(), Is.Not.Null);
            File.WriteAllBytes(v1, bytes);
            File.WriteAllText(v2, "{");
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastError, Is.Not.Null);
            File.WriteAllBytes(v2, bytes);
            Assert.That(store.Load(), Is.Null, "A schema-one file at the v2 path must be rejected");
        }
        h.Float(Id);
        var seven = DockLayoutStore.Decode(h.Layout.Capture(Bounds));
        Assert.That(seven.Root.Windows, Has.Count.EqualTo(7));
        var window = seven.Root.Windows![0];
        seven.Root.Windows.Add(window);
        Assert.Throws<InvalidDataException>(() => DockLayoutStore.Validate(seven.Root, []));
        seven.Root.Windows.RemoveAt(7);
        window.Layout!.Windows!.Add(window);
        Assert.Throws<InvalidDataException>(() => DockLayoutStore.Validate(seven.Root, []));
        window.Layout.Windows.Clear();
        var group = (IDock)window.Layout.VisibleDockables!.Single();
        group.VisibleDockables!.Clear();
        group.ActiveDockable = null;
        Assert.Throws<InvalidDataException>(() => DockLayoutStore.Validate(seven.Root, []));
    });

    [Test] public Task MigrationHandlesCompetingV2AndNeverUsesV1WhenV2IsUnreadable() => GuiTestSession.RunAsync(() =>
    {
        var v1 = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".v1");
        var v2 = v1 + ".v2";
        File.WriteAllText(v1, DockPersistenceCleanupTests.HistoricalJson());
        using var normal = new DockShellLayout(new());
        var other = normal.Capture(Bounds);
        var store = new DockLayoutStore(v2, v1) { BeforeReplace = () => File.WriteAllText(v2, other) };
        var loaded = store.Load();
        Assert.That(store.LastError, Is.Null);
        Assert.That(loaded!.ClosedTools, Is.EqualTo(new[] { DockShellState.StatusId }));
        Assert.That(File.ReadAllText(v2), Is.EqualTo(other));
        using (File.Open(v2, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastError, Is.Not.Null);
        }
        var historical = JsonNode.Parse(DockPersistenceCleanupTests.HistoricalJson())!;
        historical["ClosedTools"]!.AsArray().Add(Id);
        Assert.Throws<InvalidDataException>(() => DockLayoutStore.Migrate(historical.ToJsonString()));
        return Task.CompletedTask;
    });

    [Test] public Task HistoricalFixtureReopensOnResetAndRetainsOriginalV1() => GuiTestSession.RunAsync(async () =>
    {
        string v1 = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid() + ".v1");
        File.WriteAllText(v1, DockPersistenceCleanupTests.HistoricalJson());
        var store = new DockLayoutStore(v1 + ".v2", v1) { BeforeReplace = () => throw new IOException("migration interrupted") };
        var bytes = File.ReadAllBytes(v1);
        Assert.That(store.Load(), Is.Null);
        Assert.That(store.LastError, Is.Not.Null);
        Assert.That(File.Exists(store.Path), Is.False);
        store = new DockLayoutStore(store.Path, v1);
        await using var h = new DockToolTests.Host(store.Load());
        Assert.That(h.Presentation.InspectorModel, Is.Null);
        Assert.That(h.Layout.Reset(), Is.True);
        Assert.That(h.Presentation.Inspector, Is.Not.Null);
        Assert.That(((IDock)h.Layout.Pane(Id).Owner!).ActiveDockable!.Id, Is.EqualTo(DockShellState.VariablesId));
        Assert.That(File.ReadAllBytes(v1), Is.EqualTo(bytes));
    });
}
