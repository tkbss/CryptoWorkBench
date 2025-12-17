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
            string infoCmd = "Info(keymap)";            
            try
            {
                var prog = new AntlrToProgram();
                CryptoScriptParser parser = ParserBuilder.StringBuild(infoCmd);
                CryptoScriptParser.ProgramContext context = parser.program();
                var res = prog.Visit(context);
                
            }
            catch (Exception e)
            {
                statusViewModel.StatusString="Error in loading KeyMap information";
                InfoText = "Error in loading KeyMap information: "+e.Message;

            }
        }
        private void OnInfoEvent(string message)
        {
            if (message.Contains("Gesture="))
                InfoText = message;
        }
    }
}