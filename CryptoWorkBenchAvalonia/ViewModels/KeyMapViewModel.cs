using Avalonia.Controls.Shapes;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Services;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class KeyMapViewModel : BindableBase, IDisposable
    {
        private string _infoText = string.Empty;
        private readonly CancellationTokenSource lifetime = new();
        public Task Initialization { get; }
        public string InfoText
        {
            get => _infoText;
            private set => SetProperty(ref _infoText, value);
        }
        public KeyMapViewModel(StatusViewModel statusViewModel)
        {
            OutputOperations.InfoEvent += OnInfoEvent;
            Initialization = LoadInfoAsync(statusViewModel);
        }

        private void OnInfoEvent(string message)
        {
            if (Avalonia.Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
            { UiDispatch.Post(() => OnInfoEvent(message)); return; }
            if (message.Contains("Gesture="))
                InfoText = message;
        }
        internal async Task LoadInfoAsync(StatusViewModel status, CryptoScriptRunner? testRunner = null)
        {
            try
            {
                using var password = new PasswordDialogHost();
                var result = await Services.Automation.WorkbenchAutomationService.Shared.ExecuteSubmissionAsync(
                    "Info(keymap)", password.RequestAsync, lifetime.Token, testRunner);
                await UiDispatch.InvokeAsync(() =>
                {
                    status.AppendDiagnostics(result.Diagnostics, result.Error);
                    if (!result.HostSucceeded) status.StatusString = result.CaptureFailed
                        ? result.PresentationStatus : "Error in loading KeyMap information.";
                });
            }
            catch (Exception)
            {
                await UiDispatch.InvokeAsync(() => status.StatusString = "Error in loading KeyMap information.");
            }
        }
        public void Dispose() { lifetime.Cancel(); OutputOperations.InfoEvent -= OnInfoEvent; }
    }
}
