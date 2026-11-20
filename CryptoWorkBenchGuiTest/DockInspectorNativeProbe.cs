using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CryptoScript.SecretSharing;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Appearance;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBenchAvalonia.ViewModels;
using CryptoWorkBenchAvalonia.Views;
using Dock.Model.Core;
using Prism.Ioc;

namespace CryptoWorkBenchGuiTest;

public sealed partial class DockShellNativeProbe
{
    private static void VerifyFullSecret(string text)
    {
        int at = 0;
        void Match(string expected) {
            if(!text.AsSpan(at).StartsWith(expected,StringComparison.Ordinal)) throw new InvalidDataException("Independent full-root mismatch at "+at);
            at+=expected.Length;
        }
        Match("{\"Id\":\"large\",\"SecretSet\":{\"Descriptor\":{\"Field\":null,\"KeySizeBits\":1048576,\"Mechanism\":\"SECRET_SPLIT\",\"MinParts\":255,\"Parts\":255,\"Profile\":null,\"ReductionPolynomial\":null,\"SecretPosition\":null},\"OriginalKeyMetadata\":null,\"PreferredShareFormat\":\"X-Y\",\"Shares\":[");
        for(int x=1;x<=255;x++){
            if(x>1) Match(",");
            Match("{\"Value\":\"0x(");
            var block=string.Concat(Enumerable.Repeat(x.ToString("X2"),1024));
            for(int j=0;j<128;j++)Match(block);
            Match(")\",\"X\":"+x.ToString(System.Globalization.CultureInfo.InvariantCulture)+"}");
        }
        Match("]},\"Text\":\"\",\"Type\":{\"Id\":\"'SECRET'\",\"Name\":\"SECRET\",\"Text\":\"\"},\"Value\":\"SECRET\",\"ValueFormat\":\"\"}");
        if(at!=text.Length) throw new InvalidDataException("Unexpected trailing text");
    }

    private async Task RunInspector(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var shell = (DockShellWindow)desktop.MainWindow!;
        var layout = shell.Layout;
        var session = Container.Resolve<DockEditorSession>();
        var variables = Container.Resolve<VariableViewModel>();
        var service = Container.Resolve<WorkbenchAutomationService>();
        var checks = new List<string>();
        var pane = layout.Pane(DockShellState.InspectorId);
        var document = session.Editor.Document;
        var undo = document.UndoStack;
        var scales = shell.Screens.All.Select(s => s.Scaling).Distinct().ToArray();
        void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidDataException(name);
            checks.Add(name);
        }
        static async Task Wait(Func<bool> predicate, string label)
        {
            var watch = Stopwatch.StartNew();
            while (!predicate() && watch.Elapsed < TimeSpan.FromMinutes(3)) await Task.Delay(30);
            if (!predicate()) throw new TimeoutException(label);
        }
        static Button? ButtonNamed(Control view, string name) => view.GetVisualDescendants().OfType<Button>()
            .SingleOrDefault(b => AutomationProperties.GetName(b) == name);
        static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        try
        {
            await Task.Delay(350);
            if (Mode == "inspector-review")
            {
                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                await session.ViewModel.ParseLineAsync("KEY exampleKey=0x(000102030405060708090A0B0C0D0E0F)");
                variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "exampleKey");
                shell.Title = "CryptoWorkBench — I3 Dock Review";
                return;
            }
            if (Mode == "inspector" && File.Exists(Output + ".v1.json"))
            {
                Check(!DockShellLayout.VisibleTree(layout.Root).Contains(pane), "v1 migration keeps Inspector closed");
                var info = layout.Pane(DockShellState.InfoId);
                var floating = (Window)layout.Factory.FindRoot(info)!.Window!.Host!;
                Check(floating.IsVisible && floating.Title == "Info", "migrated legacy Info host");
                Check(((IDock)layout.Pane(DockShellState.OutputId).Owner!).ActiveDockable!.Id == DockShellState.OutputId,
                    "migrated active tab");
                Check(File.Exists(CreateLayoutStore().Path) && layout.Reset(), "v2 installed before shutdown and reset");
            }
            if (Mode == "inspector-restart")
            {
                var restored = layout.Factory.FindRoot(pane)!.Window!.Host as Window;
                Check(restored is { IsVisible: true, Title: "Inspector" }, "restart restores the inspector HWND");
                await Wait(() => restored!.GetVisualDescendants().OfType<DockVariableInspectorView>().Any(), "restored floating presenter");
                Check(restored!.GetVisualDescendants().OfType<DockVariableInspectorView>().Count() == 1, "one restored inspector view");
                Check(variables.SelectedVariable == null, "restart has no stored variable selection");
                layout.Dock(pane.Id);
                Check(layout.Reset(), "reset after restart");
            }
            var group = (IDock)pane.Owner!;
            Check(group.ActiveDockable!.Id == DockShellState.VariablesId, "default Variables active");
            Check(group.VisibleDockables!.Select(p => p.Id).SequenceEqual(new[] { DockShellState.VariablesId, DockShellState.InfoId, pane.Id }), "right tab order");
            var shares = new List<SecretShare>();
            var bytes = new byte[131072];
            for (int x = 1; x <= 255; x++)
            {
                Array.Fill(bytes, (byte)x);
                shares.Add(new SecretShare(x, bytes));
            }
            var secret = new SecretVariableDeclaration(new(new(SecretDescriptor.Split, 255, 255, 1048576), shares)) { Id = "large" };
            service.Mutate(() => VariableDictionary.Instance().Add(secret), variables.ApplyVariables);
            variables.SelectedVariable = variables.DataVariables.Single(v => v.Identifier == "large");
            await Task.Delay(150);
            Check(group.ActiveDockable!.Id == DockShellState.VariablesId, "selection preserves Variables tab");
            await Wait(() => session.CanRelocate, "initial capture");
            Click(shell.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Inspect Variable")));
            await Wait(() => shell.GetVisualDescendants().OfType<DockVariableInspectorView>().Any(), "active inspector presenter");
            var view = shell.GetVisualDescendants().OfType<DockVariableInspectorView>().Single();
            await Wait(() => ButtonNamed(view, "Copy Value") != null, "root page");
            var copy = ButtonNamed(view, "Copy full value")!;
            var revision = InterpreterHostQueue.StateRevision;
            foreach (var theme in new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark })
            {
                Check(Appearance!.Select(theme), "save " + theme);
                shell.Width = theme == ThemePreference.Light ? 880 : 1250;
                await Task.Delay(100);
                Check(copy.Focus() && ReferenceEquals(copy, ButtonNamed(view, "Copy full value")), theme + " retains focusable copy");
                Click(ButtonNamed(view, "Inspect SecretSet")!);
                await Wait(() => ButtonNamed(view, "Inspect Shares") != null, theme + " expand");
                Click(ButtonNamed(view, "Parent property")!);
                await Wait(() => ButtonNamed(view, "Copy Id") != null, theme + " collapse");
                Click(ButtonNamed(view, "Copy Id")!);
                await Wait(() => view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("Copied 5 ") == true), theme + " copy");
                Check(await shell.Clipboard!.TryGetTextAsync() == "large", theme + " exact Id");

            }
            Click(copy);
            await Wait(() => view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("Copied ") == true && !t.Text.Contains("Copied 5 ")), "large copy");
            var text = await shell.Clipboard!.TryGetTextAsync() ?? throw new InvalidDataException("Empty clipboard");
            VerifyFullSecret(text);
            checks.Add($"complete native SECRET root, {text.Length} UTF-16 units");
            session.Editor.Text += "// edit";
            await Task.Delay(50);
            Check(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("Historical") == true), "historical status");
            var snapshot = service.CaptureSaveIntent()!.Snapshot;
            service.ActivateMcp();
            var caller = service.IssueCredential("i3-native", WorkbenchAutomationService.LocalCaller.Permissions.Add(
                CryptoWorkBench.Automation.Abstractions.AutomationPermissions.EditorControl));
            var lease = (await service.AcquireEditorAsync(caller, new("2", service.InstanceId, snapshot.EditorId, Guid.NewGuid(), snapshot.Revision), default)).Result!;
            layout.Float(pane.Id);
            Check(ReferenceEquals(pane.Owner, group), "lease blocks floating");
            Click(ButtonNamed(view, "Copy Id")!);
            await Wait(() => view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("Copied 5 ") == true), "historical copy under lease");
            Check(await shell.Clipboard!.TryGetTextAsync() == "large", "historical native clipboard under MCP lease");
            await service.ReleaseEditorAsync(caller, new("2", service.InstanceId, snapshot.EditorId, lease.LeaseId), default);
            Check(layout.Maximize(pane.Id) && layout.Restore(), "inspector maximize and restore");
            layout.Float(pane.Id);
            var host = (Window)layout.Factory.FindRoot(pane)!.Window!.Host!;
            await Task.Delay(200);
            Check(ReferenceEquals(TopLevel.GetTopLevel(view), host) && host.Title == "Inspector", "same view in floating Inspector");
            var hwnd = host.TryGetPlatformHandle()!.Handle;
            Check(GetWindow(hwnd, 4) == 0 && (GetWindowLongPtr(hwnd, -16).ToInt64() & 0x00c00000) == 0x00c00000, "unowned native caption");
            shell.WindowState = WindowState.Minimized;
            await Task.Delay(130);
            Check(host.IsVisible && host.WindowState == WindowState.Normal, "floating remains visible when shell minimized");
            shell.WindowState = WindowState.Normal;
            layout.Dock(pane.Id);
            Check(!host.IsVisible && ReferenceEquals(group, pane.Owner), "Home closes floating host");
            layout.Float(pane.Id);
            ((Window)layout.Factory.FindRoot(pane)!.Window!.Host!).Close();
            Check(!DockShellLayout.VisibleTree(layout.Root).Contains(pane), "native close");
            layout.Show(pane.Id);
            await Wait(() => shell.GetVisualDescendants().OfType<DockVariableInspectorView>().Any(), "reopened inspector presenter");
            var reopened = shell.GetVisualDescendants().OfType<DockVariableInspectorView>().Single();
            Check(!ReferenceEquals(reopened, view), "reopen creates fresh view");
            Check(ReferenceEquals(session.Editor.Document, document) && ReferenceEquals(document.UndoStack, undo) &&
                InterpreterHostQueue.StateRevision == revision, "editor, undo and revision preserved");
            Check(layout.Reset(), "reset default");
            layout.Float(pane.Id);
            shell.Close();
            await Wait(() => !shell.IsVisible, "shutdown");
            File.WriteAllText(Output + "." + Mode + ".json", JsonSerializer.Serialize(new { checks, scales }));
            desktop.Shutdown(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(Output + "." + Mode + ".json", JsonSerializer.Serialize(new { checks, scales, error = e.ToString() }));
            desktop.Shutdown(1);
        }
    }
}
