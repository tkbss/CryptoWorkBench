using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Shell;
using Dock.Avalonia.Controls;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task PrepareVisualExample()
    {
        var session = Container.Resolve<DockEditorSession>();
        const string script = "VAR result=0x(0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF)\nPrint(result)\nInfo(HASH-SHA256)";
        var service = Container.Resolve<CryptoWorkBenchAvalonia.Services.Automation.WorkbenchAutomationService>();
        if (!session.LoadText(service.CaptureSaveIntent()!, script)) throw new InvalidOperationException("Example load rejected.");
        await session.ViewModel.ParseLineAsync(script);
        if (session.Output.Entries.Count != 1) throw new InvalidOperationException("Example did not produce Print output.");
    }

    private async Task RunStandardLayout(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var captures = new List<object>();
        var failures = new List<string>();
        try
        {
            await Task.Delay(300);
            var session = Container.Resolve<DockEditorSession>();
            if (Mode == "corrections-layout") await PrepareVisualExample();
            else session.Editor.Text = "// CryptoWorkBench\n// Enter executes the current CryptoScript input.\n";
            foreach (var (width, height) in new[] { (1920,1080), (1366,768), (1536,864), (1280,720), (1093,614), (911,512), (640,420) })
            {
                shell.WindowState = WindowState.Normal;
                shell.Width = width; shell.Height = height;
                await Capture($"{width}x{height}");
            }
            shell.WindowState = WindowState.Maximized;
            await Capture("maximized");
            shell.WindowState = WindowState.Normal;
            shell.Width = 1200; shell.Height = 760;
            shell.Layout.Maximize(DockShellState.DocumentId);
            await Task.Delay(100);
            if (!shell.Layout.Restore()) failures.Add("restore");
            await Capture("restored");
        }
        catch (Exception error) { failures.Add(error.ToString()); }
        finally
        {
            File.WriteAllText(Output, JsonSerializer.Serialize(new { captures, failures }, new JsonSerializerOptions { WriteIndented = true }));
            shell.Close();
            desktop.Shutdown(failures.Count == 0 ? 0 : 1);
        }

        async Task Capture(string label)
        {
            await Task.Delay(250); shell.UpdateLayout();
            var tabs = shell.GetVisualDescendants().OfType<ToolTabStrip>().ToArray();
            if (tabs.Length != 2 || tabs.Any(t => DockPanel.GetDock(t) != Avalonia.Controls.Dock.Top || t.Bounds.Width < 150))
                failures.Add(label + ": tabs");
            if (Mode == "corrections-layout")
            {
                foreach (var chrome in shell.GetVisualDescendants().OfType<ToolChromeControl>())
                    if (chrome.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Border" && ReferenceEquals(b.TemplatedParent, chrome)).IsEffectivelyVisible)
                        failures.Add(label + ": redundant header");
                var text = shell.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "PrintText");
                var list = shell.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "OutputEntries");
                var scroller = list.GetVisualDescendants().OfType<ScrollViewer>().Single();
                var point = text.TranslatePoint(default, scroller)!.Value;
                if (point.Y < 0 || point.Y + text.Bounds.Height > scroller.Bounds.Height)
                    failures.Add(label + ": Print text outside viewport");
            }
            captures.Add(new { label, width = shell.ClientSize.Width, height = shell.ClientSize.Height,
                scale = shell.RenderScaling, tabs = tabs.Select(t => new { width = t.Bounds.Width, height = t.Bounds.Height }).ToArray() });
        }
    }
}

