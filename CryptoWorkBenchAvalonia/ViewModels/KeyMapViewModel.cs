using Avalonia.Controls.Shapes;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using Prism.Mvvm;
using System;
using System.Collections.Generic;

namespace CryptoWorkBenchAvalonia.ViewModels
{
	public class KeyMapViewModel : BindableBase
	{
        private string _infoText;
        public string InfoText
        {
            get => _infoText;
            private set => SetProperty(ref _infoText, value);
        }
        public KeyMapViewModel(StatusViewModel statusViewModel)
		{
            OutputOperations.InfoEvent += OnInfoEvent;
            LoadInfo(statusViewModel, new CryptoScriptRunner());
        }

        private void LoadInfo(StatusViewModel statusViewModel, CryptoScriptRunner prog)
        {
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
            if (message.Contains("Gesture="))
                InfoText = message;
        }
    }
}
