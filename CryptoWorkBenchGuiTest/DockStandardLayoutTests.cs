using Avalonia.Controls;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockStandardLayoutTests
{
    private static void Standard(DockShellLayout layout)
    {
        var row = (IProportionalDock)layout.Pane(DockShellState.DocumentId).Owner!.Owner!;
        var main = (IProportionalDock)row.Owner!;
        Assert.That(row.Orientation, Is.EqualTo(Orientation.Horizontal));
        Assert.That(main.Orientation, Is.EqualTo(Orientation.Vertical));
        Assert.That(row.Proportion, Is.EqualTo(.75));
        Assert.That(row.VisibleDockables![0], Is.SameAs(layout.Pane(DockShellState.DocumentId).Owner));
        Assert.That(row.VisibleDockables[0].Proportion, Is.EqualTo(.70));
        var right = (IDock)row.VisibleDockables[2];
        Assert.That(right.Proportion, Is.EqualTo(.30));
        Assert.That(right.VisibleDockables!.Select(p => p.Id), Is.EqualTo(new[] { DockShellState.VariablesId, DockShellState.InfoId }));
        var bottom = (IDock)main.VisibleDockables![2];
        Assert.That(bottom.Proportion, Is.EqualTo(.25));
        Assert.That(bottom.VisibleDockables!.Select(p => p.Id), Is.EqualTo(new[] { DockShellState.OutputId, DockShellState.DiagnosticsId }));
        Assert.That(bottom.ActiveDockable!.Id, Is.EqualTo(DockShellState.OutputId));
        Assert.That(DockLayoutStore.Decode(layout.Capture(DockLayoutRoleTests.Bounds)).ClosedTools, Is.EqualTo(new[] { DockShellState.StatusId }));
    }

    [Test] public Task InitialGroupingRoundtripResetAndFocus() => GuiTestSession.Session.Dispatch(() =>
    {
        using var layout = new DockShellLayout(new());
        Standard(layout);
        var original = layout.Capture(DockLayoutRoleTests.Bounds);
        Assert.That(layout.Maximize(DockShellState.DocumentId), Is.True);
        Assert.That(layout.Restore(), Is.True);
        Assert.That(layout.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(original));
        layout.Move(DockShellState.OutputId, DockShellLayout.Tools);
        layout.Show(DockShellState.StatusId);
        var custom = layout.Capture(DockLayoutRoleTests.Bounds);
        using var restored = new DockShellLayout(new());
        restored.RestoreAtStartup(DockLayoutStore.Decode(custom));
        Assert.That(restored.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(custom));
        Assert.That(restored.Reset(), Is.True);
        Standard(restored);
    }, CancellationToken.None);

    // Logical client sizes also cover the space available at 125% and 150% DPI.
    // These are layout checks, not a claim of native Windows DPI acceptance.
    [TestCase(1920,1080)] [TestCase(1366,768)]
    [TestCase(1536,864)] [TestCase(1280,720)]
    [TestCase(1093,614)] [TestCase(911,512)] [TestCase(640,420)]
    public Task TabsAndCentralStatusRemainVisible(int width, int height) => GuiTestSession.Session.Dispatch(async () =>
    {
        await using var h = new DockEditorTests.Host();
        h.Window.Width = width; h.Window.Height = height;
        DockEditorTests.Pump(h.Window);
        var tabs = h.Window.GetVisualDescendants().OfType<ToolTabStrip>().ToArray();
        Assert.That(tabs, Has.Length.EqualTo(2));
        foreach (var tab in tabs)
        {
            Assert.That(DockPanel.GetDock(tab), Is.EqualTo(Avalonia.Controls.Dock.Top));
            Assert.That(tab.Bounds.Height, Is.GreaterThanOrEqualTo(28));
            Assert.That(tab.Bounds.Width, Is.GreaterThan(150));
        }
        Assert.That(h.Session.View.FindControl<Border>("EditorStatusFrame")!.IsVisible, Is.False);
        var status = h.Window.FindControl<SelectableTextBlock>("ExecutionStatus")!;
        h.Session.ViewModel.Status.StatusString = "Execution failed: retained detail";
        h.Layout.ReportFeedback("Layout restored");
        h.Editor.Text = "abc"; h.Editor.CaretOffset = 2;
        DockEditorTests.Pump(h.Window);
        Assert.That(status.Text, Is.EqualTo("Execution failed: retained detail"));
        Assert.That(h.Window.FindControl<TextBlock>("ShellStatus")!.Text, Is.EqualTo("Layout restored"));
        Assert.That(h.Window.FindControl<TextBlock>("CaretStatus")!.Text, Is.EqualTo("Line 1 Column 3"));
        Assert.That(status.GetVisualAncestors().OfType<DockControl>(), Is.Empty);
        Assert.That(h.Window.FindControl<Border>("ShellStatusBar")!.Bounds.Height, Is.LessThan(45));
    }, CancellationToken.None);
}
