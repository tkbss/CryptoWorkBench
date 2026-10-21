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
            if (Avalonia.Application.Current is null)
            {
                LoadInfo(statusViewModel, new CryptoScriptRunner());
                Initialization = Task.CompletedTask;
            }
            else Initialization = LoadInfoAsync(statusViewModel);
        }

        private void LoadInfo(StatusViewModel statusViewModel, CryptoScriptRunner prog)
        {
            using var lease = InterpreterExecution.Enter();
            string infoCmd = "Info(keymap)";            
            try
            {
                CryptoScriptParser parser = ParserBuilder.StringBuild(infoCmd);
                CryptoScriptParser.ProgramContext context = parser.program();
                var res = prog.Execute(context);
                statusViewModel.AppendDiagnostics(res.Diagnostics);
                
            }
            catch (Exception e)
            {
                statusViewModel.AppendDiagnostics(prog.Diagnostics, (e as SemanticErrorException)?.SemanticError);
                statusViewModel.StatusString="Error in loading KeyMap information";
                InfoText = e is SemanticErrorException
                    ? "Error in loading KeyMap information (semantic error)."
                    : "Error in loading KeyMap information: " + e.Message;

            }
        }
        private void OnInfoEvent(string message)
        {
            if (Avalonia.Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
            { Dispatcher.UIThread.Post(() => OnInfoEvent(message)); return; }
            if (message.Contains("Gesture="))
                InfoText = message;
        }
        private async Task LoadInfoAsync(StatusViewModel status)
        {
            try
            {
                var diagnostics = await InterpreterHostQueue.RunAsync(async () =>
                {
                    var runner = new CryptoScriptRunner();
                    var context = InterpreterExecution.AccessState(() => ParserBuilder.StringBuild("Info(keymap)").program());
                    return (await runner.ExecuteAsync(context, lifetime.Token).ConfigureAwait(false)).Diagnostics;
                }, lifetime.Token);
                await Dispatcher.UIThread.InvokeAsync(() => status.AppendDiagnostics(diagnostics));
            }
            catch (Exception)
            {
                await Dispatcher.UIThread.InvokeAsync(() => status.StatusString = "Error in loading KeyMap information.");
            }
        }
        public void Dispose() { lifetime.Cancel(); OutputOperations.InfoEvent -= OnInfoEvent; }
    }
}
