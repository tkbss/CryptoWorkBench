using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CryptoWorkBenchAvalonia.Services.Appearance;
using CryptoWorkBenchAvalonia.Shell;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

[NonParallelizable]
public sealed class ThemeTests
{
    private string _directory = null!;
    private string FilePath => Path.Combine(_directory, "appearance-v1.json");
    [SetUp] public void Setup() { _directory = Path.Combine(Path.GetTempPath(), "cwb-theme-" + Guid.NewGuid()); Directory.CreateDirectory(_directory); }
    [TearDown] public void Cleanup() => Directory.Delete(_directory, true);

    [TestCase(ThemePreference.System)] [TestCase(ThemePreference.Light)] [TestCase(ThemePreference.Dark)]
    public void PreferenceRoundTrip(ThemePreference preference)
    {
        var store = new ThemePreferenceStore(FilePath);
        Assert.That(store.Save(preference), Is.True);
        Assert.That(new ThemePreferenceStore(FilePath).Load(), Is.EqualTo(preference));
        Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
    }
    [Test] public void MissingDefaultsWithoutWriting()
    {
        var store = new ThemePreferenceStore(FilePath);
        Assert.That(store.Load(), Is.EqualTo(ThemePreference.System));
        Assert.That(store.Error, Is.Null);
        Assert.That(File.Exists(FilePath), Is.False);
    }
    [TestCase("{")] [TestCase("{}")] [TestCase("null")]
    [TestCase("{\"schemaVersion\":2,\"theme\":\"Dark\"}")]
    [TestCase("{\"schemaVersion\":1,\"theme\":\"dark\"}")]
    [TestCase("{\"schemaVersion\":1,\"theme\":2}")]
    public void InvalidDefaultsAndPreservesOriginal(string json)
    {
        File.WriteAllText(FilePath, json);
        var store = new ThemePreferenceStore(FilePath);
        Assert.That(store.Load(), Is.EqualTo(ThemePreference.System));
        Assert.That(store.Error, Is.Not.Null);
        Assert.That(File.ReadAllText(FilePath), Is.EqualTo(json));
    }
    [Test] public void OversizedDefaultsAndFailedSaveIsReported()
    {
        File.WriteAllText(FilePath, new string(' ', 4097));
        var store = new ThemePreferenceStore(FilePath);
        Assert.That(store.Load(), Is.EqualTo(ThemePreference.System));
        Assert.That(store.Error, Is.Not.Null);
        var blocked = new ThemePreferenceStore(Path.Combine(FilePath, "appearance.json"));
        Assert.That(blocked.Save(ThemePreference.Dark), Is.False);
        Assert.That(blocked.Error, Does.Contain("could not be saved"));
    }
    [Test] public Task SwitchingPreservesSessionAndFlowsToExistingAndNewFloatingWindows() => GuiTestSession.RunAsync(async () =>
    {
        var app = Application.Current!;
        var previous = app.RequestedThemeVariant;
        var resources = new ResourceInclude(new Uri("avares://CryptoWorkBenchAvalonia/"))
            { Source = new Uri("avares://CryptoWorkBenchAvalonia/Themes/ThemeResources.axaml") };
        app.Resources.MergedDictionaries.Add(resources);
        var styles = new StyleInclude(new Uri("avares://CryptoWorkBenchAvalonia/"))
            { Source = new Uri("avares://CryptoWorkBenchAvalonia/Themes/Controls.axaml") };
        app.Styles.Add(styles);
        try
        {
            var service = new ThemePreferenceService(app, new ThemePreferenceStore(FilePath));
            Assert.That(app.RequestedThemeVariant, Is.EqualTo(ThemeVariant.Default));
            await using var host = new DockToolTests.Host();
            var document = host.Session.Editor.Document;
            var undo = document.UndoStack;
            var floating = host.Float(DockShellState.VariablesId);
            var menu = service.CreateMenu(_ => { });
            Assert.That(menu.Items.OfType<MenuItem>().Select(i => i.Header), Is.EqualTo(new[] { "System", "Light", "Dark" }));
            await host.Session.ViewModel.ParseLineAsync("Print(42)");
            host.Show(DockShellState.OutputId);
            host.Pump();
            var gutter = host.Session.Editor.TextArea.LeftMargins.OfType<AvaloniaEdit.Editing.LineNumberMargin>().Single();
            Assert.That(gutter.Margin.Right, Is.EqualTo(8));
            var source = host.Window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "OutputSource");
            var print = host.Window.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Name == "PrintText");
            Assert.That(print.Text, Does.Contain("42"));
            var layout = host.Layout.Capture(new(0, 0, 1400, 850));
            foreach (var preference in new[] { ThemePreference.Dark, ThemePreference.Light })
            {
                Assert.That(service.Select(preference), Is.True);
                host.Pump();
                var expected = preference == ThemePreference.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
                Assert.That(host.Window.ActualThemeVariant, Is.EqualTo(expected));
                Assert.That(floating.ActualThemeVariant, Is.EqualTo(expected));
                Assert.That(((Avalonia.Media.ISolidColorBrush)source.Background!).Color.A, Is.Zero);
                Assert.That(((Avalonia.Media.ISolidColorBrush)source.Foreground!).Color,
                    Is.EqualTo(Avalonia.Media.Color.Parse(preference == ThemePreference.Dark ? "#B4BDCB" : "#505968")));
                Assert.That(gutter.Margin.Right, Is.EqualTo(8));
                Assert.That(host.Session.Editor.Document, Is.SameAs(document));
                Assert.That(document.UndoStack, Is.SameAs(undo));
                Assert.That(host.Layout.Capture(new(0, 0, 1400, 850)), Is.EqualTo(layout));
                Assert.That(host.Window.TryFindResource("CwbSurfacePanelBrush", host.Window.ActualThemeVariant, out var brush), Is.True);
                Assert.That(brush!.ToString(), Is.EqualTo(preference == ThemePreference.Dark ? "#ff24272e" : "#fff7f8fa").IgnoreCase);
                Assert.That(((Avalonia.Media.ISolidColorBrush)host.Session.Editor.Background!).Color, Is.EqualTo(Avalonia.Media.Color.Parse(preference == ThemePreference.Dark ? "#1e2026" : "#ffffff")));
            }
            var second = host.Float(DockShellState.InfoId);
            Assert.That(second.ActualThemeVariant, Is.EqualTo(ThemeVariant.Light));
            await host.Session.ViewModel.ParseLineAsync("Print(themeMissingVariable)");
            host.Show(DockShellState.DiagnosticsId);
            foreach (var preference in new[] { ThemePreference.Dark, ThemePreference.Light })
            {
                service.Select(preference);
                host.Pump();
                var expected = Avalonia.Media.Color.Parse(preference == ThemePreference.Dark ? "#FFB4B4" : "#A32121");
                var texts = host.Window.GetVisualDescendants().OfType<TextBlock>()
                    .Where(t => t.Name == "DiagnosticText" || t.Text == "Semantic Error").ToArray();
                Assert.That(texts.Length, Is.GreaterThanOrEqualTo(2));
                foreach (var text in texts)
                    Assert.That(((Avalonia.Media.ISolidColorBrush)text.Foreground!).Color, Is.EqualTo(expected));
            }
            service.Select(ThemePreference.System);
            Assert.That(app.RequestedThemeVariant, Is.EqualTo(ThemeVariant.Default));
            Assert.That(floating.ActualThemeVariant, Is.EqualTo(app.ActualThemeVariant));
        }
        finally { app.Styles.Remove(styles); app.Resources.MergedDictionaries.Remove(resources); app.RequestedThemeVariant = previous; }
    });
}
