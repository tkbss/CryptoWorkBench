using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Services.Appearance;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private async Task RunThemeReview(IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        var shell = (DockShellWindow)desktop.MainWindow!;
        Appearance!.Select(Mode == "theme-light" ? ThemePreference.Light : ThemePreference.Dark);
        var session = Container.Resolve<DockEditorSession>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        const string script = "VAR message=0x(616263)\nPARAM hash=Parameters(HASH-SHA256)\nVAR digest=Hash(hash,message)\nVAR label=\"SHA-256 / abc\"\n\nPrint(label)\nPrint(message)\nPrint(digest)\nInfo(HASH-SHA256)";
        session.LoadText(service.CaptureSaveIntent()!, script);
        await session.ViewModel.ParseLineAsync(script);
        shell.Width = 1920 / shell.RenderScaling;
        shell.Height = 1040 / shell.RenderScaling;
        await Task.Delay(500);
        foreach (var grid in shell.GetVisualDescendants().OfType<DataGrid>())
            if (grid.ItemsSource != null) grid.SelectedIndex = 2;
        File.WriteAllText(Output, JsonSerializer.Serialize(new
        {
            theme = Appearance.Preference.ToString(), scale = shell.RenderScaling,
            clientWidth = shell.ClientSize.Width, clientHeight = shell.ClientSize.Height,
            printCount = session.Output.Entries.Count,
            failures = session.Output.Entries.Count == 3 ? Array.Empty<string>() : new[] { "Expected three Print entries" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
