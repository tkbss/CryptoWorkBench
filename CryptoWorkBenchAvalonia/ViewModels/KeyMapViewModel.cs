using CryptoScript.Model;
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Services;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class KeyMapViewModel : WorkbenchBindable, IDisposable
    {
        private string _infoText = string.Empty;
        private readonly CancellationTokenSource lifetime = new();
        public Task Initialization { get; }
        public string InfoText
        {
            get => _infoText;
            private set => SetProperty(ref _infoText, value);
        }
        public KeyMapViewModel(StatusViewModel statusViewModel, Services.Automation.WorkbenchAutomationService? automation = null)
        {
            Automation = automation ?? statusViewModel.Automation;
            Initialization = LoadInfoAsync(statusViewModel);
        }

        private void OnInfoEvent(string message)
        {
            if (message.Contains("Gesture="))
                InfoText = message;
        }
        internal async Task LoadInfoAsync(StatusViewModel status, CryptoScriptRunner? testRunner = null)
        {
            var coordinator = Dispatcher.UIThread.CheckAccess() ? Automation.EditorCoordinator : null;
            long? workspace = coordinator?.WorkspaceGeneration;
            bool Current() => !Automation.IsStopping && !lifetime.IsCancellationRequested &&
                (coordinator == null || coordinator.AcceptsWorkspacePresentation(workspace!.Value));
            bool started = false;
            try
            {
                using var password = new PasswordDialogHost();
                await Automation.RunAsync(async pipeline =>
                {
                    started = true;
                    try
                    {
                        var result = await pipeline.ExecuteAsync("Info(keymap)", password.RequestAsync, pipeline.ExecutionToken, testRunner);
                        await UiDispatch.InvokeAsync(() =>
                        {
                            if (!Current() || pipeline.EditorIntent?.DestinationLive == false) return;
                            foreach (var item in result.Output)
                                if (item.Kind == "info" && Current()) OnInfoEvent(item.Text);
                            if (!Current()) return;
                            status.AppendDiagnostics(result.Diagnostics, result.Error, null, Current);
                            if (Current() && !result.HostSucceeded) status.StatusString = result.CaptureFailed
                                ? result.PresentationStatus : "Error in loading KeyMap information.";
                        });
                        return result.HostSucceeded;
                    }
                    catch
                    {
                        await UiDispatch.InvokeAsync(() => { if (Current()) status.StatusString = "Error in loading KeyMap information."; });
                        throw;
                    }
                }, lifetime.Token, "keymap");
            }
            catch (Exception)
            {
                if (!started && Dispatcher.UIThread.CheckAccess() && Current()) status.StatusString = "Error in loading KeyMap information.";
            }
        }
        public void Dispose() { lifetime.Cancel(); }
    }
}
