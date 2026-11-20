using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Settings;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class DockVisualCorrectionTests
{
    private static Task Run(Func<DockToolTests.Host, Task> test) => GuiTestSession.RunAsync(async () =>
    { await using var h = new DockToolTests.Host(); await test(h); });

    private static T NamedVisual<T>(Control view, string name) where T : Control
    {
        var matches = view.GetVisualDescendants().OfType<T>().Where(control => control.Name == name).ToArray();
        Assert.That(matches, Has.Length.EqualTo(1), $"Expected one {typeof(T).Name} named {name} in {view.GetType().Name}.");
        return matches[0];
    }

    private static void Headers(DockToolTests.Host h)
    {
        var visuals = h.Window.GetVisualDescendants().ToArray();
        Assert.That(visuals.OfType<ToolTabStrip>().Count(t => t.IsEffectivelyVisible), Is.EqualTo(2));
        Assert.That(visuals.OfType<DocumentTabStrip>().Count(t => t.IsEffectivelyVisible), Is.EqualTo(1));
        foreach (var chrome in visuals.OfType<ToolChromeControl>())
        {
            Assert.That(((IToolDock)chrome.DataContext!).GripMode, Is.EqualTo(GripMode.Hidden));
            Assert.That(chrome.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Border" && ReferenceEquals(b.TemplatedParent, chrome)).IsEffectivelyVisible, Is.False);
        }
        foreach (var strip in visuals.OfType<ToolTabStrip>())
        {
            var fill = strip.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_BorderFill");
            Assert.That(DockProperties.GetIsDragArea(fill), Is.True);
            Assert.That(fill.DataContext, Is.InstanceOf<IToolDock>());
            Assert.That(strip.GetVisualDescendants().OfType<ItemsPresenter>().Single().IsEffectivelyVisible, Is.True);
        }
    }

    [Test] public Task SingleTabRowsSurviveSwitchCloseResetRestoreAndStartup() => Run(async h =>
    {
        Headers(h);
        foreach (var id in new[] { DockShellState.InfoId, DockShellState.DiagnosticsId, DockShellState.VariablesId, DockShellState.OutputId })
        {
            h.Show(id);
            Assert.That(((IDock)h.Layout.Pane(id).Owner!).ActiveDockable, Is.SameAs(h.Layout.Pane(id)));
            Headers(h);
        }
        h.Layout.Close(DockShellState.InfoId); h.Pump(); Headers(h);
        var json = h.Layout.Capture(DockLayoutRoleTests.Bounds);
        using var restored = new DockShellLayout(new());
        restored.RestoreAtStartup(DockLayoutStore.Decode(json));
        Assert.That(restored.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(json));
        Assert.That(DockShellLayout.VisibleTree(restored.Root).OfType<IToolDock>().All(t => t.GripMode == GripMode.Hidden), Is.True);
        Assert.That(h.Layout.Maximize(DockShellState.DocumentId), Is.True);
        Assert.That(h.Layout.Restore(), Is.True); h.Pump(); Headers(h);
        Assert.That(h.Layout.Reset(), Is.True); h.Pump(); Headers(h);
        var reset = h.Layout.Capture(DockLayoutRoleTests.Bounds);
        using var restarted = new DockShellLayout(new());
        restarted.RestoreAtStartup(DockLayoutStore.Decode(reset));
        Assert.That(restarted.Capture(DockLayoutRoleTests.Bounds), Is.EqualTo(reset));
        await Task.CompletedTask;
    });

    [TestCase(1200,760)] [TestCase(911,512)] [TestCase(640,420)]
    public Task ShortPrintIsVisibleAndSourceButtonNavigates(int width, int height) => Run(async h =>
    {
        h.Window.Width = width; h.Window.Height = height;
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, "  Print(0x(AB))"), Is.True);
        await h.Session.ViewModel.ParseLineAsync(h.Session.Editor.Text);
        h.Show(DockShellState.OutputId);
        var view = h.Presentation.Output;
        Assert.That(view, Is.Not.Null, "Expected the active Output dock view.");
        var text = view.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "PrintText");
        var list = NamedVisual<ListBox>(view, "OutputEntries");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().Single();
        var location = text.TranslatePoint(default, scroller)!.Value;
        Assert.That(text.Text, Is.EqualTo("out: 0x(AB)"));
        Assert.That(location.Y, Is.GreaterThanOrEqualTo(0));
        Assert.That(location.Y + text.Bounds.Height, Is.LessThanOrEqualTo(scroller.Bounds.Height));
        var source = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "OutputSource");
        h.Session.Editor.CaretOffset = 0;
        source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.That(h.Session.Editor.CaretOffset, Is.EqualTo(2));
        Assert.That(h.Session.Output.Entries, Has.Count.EqualTo(1));
    });

    [Test] public Task LongPrintEntriesRemainCompleteAndScrollInBothDirections() => Run(async h =>
    {
        h.Window.Width = 911; h.Window.Height = 512;
        var hex = string.Concat(Enumerable.Repeat("0123456789ABCDEF", 64));
        var script = string.Join("\n", Enumerable.Repeat($"Print(0x({hex}))", 12));
        Assert.That(h.Session.LoadText(h.Service.CaptureSaveIntent()!, script), Is.True);
        await h.Session.ViewModel.ParseLineAsync(script);
        h.Show(DockShellState.OutputId);
        var view = h.Presentation.Output;
        Assert.That(view, Is.Not.Null, "Expected the active Output dock view.");
        var list = NamedVisual<ListBox>(view, "OutputEntries");
        var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().Single();
        Assert.That(h.Session.Output.Entries, Has.Count.EqualTo(12));
        Assert.That(h.Session.Output.Entries.All(e => e.Text == $"out: 0x({hex})"), Is.True);
        Assert.That(scroller.Extent.Width, Is.GreaterThan(scroller.Viewport.Width));
        Assert.That(scroller.Extent.Height, Is.GreaterThan(scroller.Viewport.Height));
        scroller.Offset = new Vector(scroller.Extent.Width, scroller.Extent.Height); h.Pump();
        Assert.That(scroller.Offset.X, Is.GreaterThan(0));
        Assert.That(scroller.Offset.Y, Is.GreaterThan(0));
        var texts = list.GetVisualDescendants().OfType<SelectableTextBlock>().Where(t => t.Name == "PrintText").ToArray();
        Assert.That(texts, Is.Not.Empty);
        Assert.That(texts.All(t => t.Text == $"out: 0x({hex})"), Is.True);
    });

    [Test] public Task NoSelectionHidesVariableDetailsAndTableUsesAvailableWidth() => Run(async h =>
    {
        h.Show(DockShellState.VariablesId);
        var view = h.Presentation.Variables;
        Assert.That(view, Is.Not.Null, "Expected the active Variables dock view.");
        var detailHosts = view.GetVisualDescendants().OfType<ScrollViewer>()
            .Where(scroll => scroll.Content is ContentControl { Name: "VariableDetails" }).ToArray();
        Assert.That(detailHosts, Has.Length.EqualTo(1), "Expected one scroll viewer hosting VariableDetails in DockVariablesView.");
        var detailHost = detailHosts.Single();
        var detail = (ContentControl)detailHost.Content!;
        Assert.That(h.Variables.SelectedVariable, Is.Null);
        Assert.That(detailHost.IsEffectivelyVisible, Is.False);
        // The hidden host can own its content before the content is attached to a visual root.
        Assert.That(detail.IsAttachedToVisualTree() && detail.IsEffectivelyVisible, Is.False);
        var grid = NamedVisual<DataGrid>(view, "VariablesGrid");
        Assert.That(grid.Columns.All(c => c.Width.IsStar), Is.True);
        Assert.That(grid.Bounds.Height, Is.GreaterThan(view.Bounds.Height * .8));
        await Task.CompletedTask;
    });
}

