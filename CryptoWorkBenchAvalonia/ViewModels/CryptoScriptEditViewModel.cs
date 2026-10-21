using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Services;
using Prism.Navigation;
using Prism.Navigation.Regions;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Models;
using static System.Net.Mime.MediaTypeNames;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class CryptoScriptEditViewModel : ViewModelBase, IDisposable
    {
        StatusViewModel _statusViewModel;
        VariableViewModel _variableViewModel;
        InfoViewModel _infoViewModel;
        MainViewModel _mainViewModel;
        IRegionManager _regions;
        IHistoryService _historyService;
        string _printMessage = string.Empty;
        private TextEditor? _textEditor;
        bool _syntaxErrorOccured = false;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly PasswordDialogHost _passwordHost = new();
        private bool lineActive, bookActive;
        public bool IsExecuting => lineActive || bookActive;
        
        public StatusViewModel Status { get => _statusViewModel; }
        public TextEditor? TextEditor
        {
            get
            {
                return _textEditor;
            }

            set => SetProperty(ref _textEditor, value);
        }
        public string PrintMessage 
        { 
            get => _printMessage; 
            set => SetProperty(ref _printMessage, value); 
        }
        
        public CryptoScriptEditViewModel(StatusViewModel statusViewModel,VariableViewModel variableViewModel,InfoViewModel info,MainViewModel mvm,IRegionManager regions, IHistoryService historyService)
        {
            _historyService = historyService;
            _historyService.LineHistoryChanged += OnHistoryChanged;
            _statusViewModel = statusViewModel;
            _variableViewModel = variableViewModel;
            _variableViewModel.IsExecutionActive = () => IsExecuting;
            OutputOperations.PrintEvent += OnPrintEvent;
            OutputOperations.InfoEvent += OnInfoEvent;
            _regions = regions; 
            _infoViewModel = info;
            _mainViewModel = mvm;
        }
        private void OnHistoryChanged(object? sender, string entry)
        {
            if (IsExecuting) return;
            TextDocument doc = _textEditor!.Document;
            // letzte Zeile holen
            DocumentLine line = doc.Lines[doc.Lines.Count - 1];            
            doc.Remove(line.Offset, line.Length);
            // neuen Text am Zeilenanfang einsetzen
            doc.Insert(line.Offset, entry);
            _textEditor.Document = doc;
            _textEditor.IsModified = true;
        }
        public void ParseLine(string line)
        {
            _statusViewModel.ClearDiagnostics();
            ExecuteLine(line);
        }

        // Optional runner is a private transport-test seam, not a script function.
        private bool ExecuteLine(string line, int? scriptBookLine = null, CryptoScriptRunner? runner = null)
        {
            using var lease = InterpreterExecution.Enter();
            _historyService.AddLine(line);
            _printMessage = string.Empty;
            var prog = runner ?? new CryptoScriptRunner();
            CryptoScriptParser parser = ParserBuilder.StringBuild(line);
            CryptoScriptParser.ProgramContext context = parser.program();
            _statusViewModel.StatusString =string.Empty;
            if (SyntaxErrorListner.SyntaxErrorOccured)
            {
                string e = SyntaxErrorListner.ErrorMessage.ToString();
                _statusViewModel.StatusString = e;
                SyntaxErrorListner.SyntaxErrorOccured = false;
                _syntaxErrorOccured = true;
                return false;
            }
            else
            {
                _statusViewModel.StatusString = "Line successfull parsed";
                bool diagnosticsReported = false;
                try
                {
                    var res = prog.Execute(context);
                    _statusViewModel.AppendDiagnostics(res.Diagnostics, scriptBookLine: scriptBookLine);
                    diagnosticsReported = true;
                    _variableViewModel.SetupVariables();
                    _syntaxErrorOccured = false;
                    return !_syntaxErrorOccured;
                }
                catch (Exception e) {
                    if (!diagnosticsReported)
                        _statusViewModel.AppendDiagnostics(prog.Diagnostics,
                            (e as SemanticErrorException)?.SemanticError, scriptBookLine);
                    if(e is SemanticErrorException)
                        _statusViewModel.StatusString = "Script execution aborted (semantic error).";
                    else
                    {
                        _statusViewModel.StatusString = "Semantic "+e.Message;
                    }
                    _syntaxErrorOccured = true;
                    return false;
                }
            }
        }
        
        private void OnPrintEvent(string message)
        {
            if (Avalonia.Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
            { Dispatcher.UIThread.Post(() => OnPrintEvent(message)); return; }
            // Handle the event (e.g., update the status view model)
            _printMessage = IsExecuting && _printMessage.Length != 0
                ? _printMessage + Environment.NewLine + message : message;
        }
        private void OnInfoEvent(string message)
        {
            if (Avalonia.Application.Current is not null && !Dispatcher.UIThread.CheckAccess())
            { Dispatcher.UIThread.Post(() => OnInfoEvent(message)); return; }
            if (message.Contains("Gesture="))
                return;
                _infoViewModel.SetInfoText(message);
            _mainViewModel.InfoViewTitle = "InfoView";
            _regions?.RequestNavigate("InfoRegion", "InfoView");
        }
        

        public void SaveScriptBook(string filePath)
        {

            using (FileStream fs = System.IO.File.Open(filePath, System.IO.FileMode.Append))
            {
                TextDocument doc = _textEditor!.Document;
                foreach (DocumentLine line in doc.Lines)
                {
                    string lineText = doc.GetText(line);
                    string trimmedLine = lineText;
                    if (string.IsNullOrEmpty(trimmedLine.Trim()) == true)
                        continue;
                    if (lineText != string.Empty)
                    {
                        byte[] lineBytes = Encoding.ASCII.GetBytes(lineText);
                        fs.Write(lineBytes, 0, lineBytes.Length);
                        fs.WriteByte((byte)'\n');
                    }
                }
            }
        }
        public bool IsTextEditorEmpty()
        {
            if (_textEditor!.Document.Text.Length == 0)
                return true;
            return false;
        }
        public void OpenScriptBook(string filePath)
        {
            _statusViewModel.ClearDiagnostics();
            LastScriptBookSucceeded = false;
            // Read all lines from the file
            var lines = File.ReadAllLines(filePath).ToList();
            TextDocument doc = _textEditor!.Document;
            ExecuteScriptBook(lines, l =>
                doc.Insert(doc.Lines[doc.Lines.Count - 1].EndOffset, l + "\n"));
            _textEditor.Document= doc;
            _textEditor.IsModified = true;
        }

        public bool LastScriptBookSucceeded { get; private set; }

        private void ExecuteScriptBook(IEnumerable<string> lines, Action<string> appendLine,
            Func<CryptoScriptRunner>? runnerFactory = null)
        {
            LastScriptBookSucceeded = false;
            int bookLine = 0;
            foreach (var line in lines)
            {
                ++bookLine;
                bool succeeded = line.StartsWith("out:", StringComparison.Ordinal) ||
                    ExecuteLine(line, bookLine, runnerFactory?.Invoke());
                appendLine(line);
                if (!succeeded)
                    return;
            }
            LastScriptBookSucceeded = true;
        }

        public async Task ParseLineAsync(string line, CancellationToken cancellationToken = default)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting) { _statusViewModel.StatusString = "Interpreter execution is busy."; return; }
            _statusViewModel.ClearDiagnostics();
            await ExecuteLineAsync(line, cancellationToken: cancellationToken);
        }

        internal async Task<bool> ExecuteLineAsync(string line, int? scriptBookLine = null,
            CryptoScriptRunner? runner = null, CancellationToken cancellationToken = default,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (lineActive) throw new ExecutionBusyException();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            lineActive = true;
            _historyService.AddLine(line);
            _printMessage = string.Empty;
            var prog = runner ?? new CryptoScriptRunner();
            // Production subscribes exactly for the execution lifetime; fake hosts own their test runner.
            if (runner is null) prog.PasswordRequested += _passwordHost.RequestAsync;
            try
            {
                var result = await InterpreterHostQueue.RunAsync(async () =>
                {
                    string? syntax = null;
                    var context = InterpreterExecution.AccessState(() =>
                    {
                        SyntaxErrorListner.SyntaxErrorOccured = false;
                        SyntaxErrorListner.ErrorMessage = new();
                        var parsed = ParserBuilder.StringBuild(line).program();
                        if (SyntaxErrorListner.SyntaxErrorOccured) syntax = SyntaxErrorListner.ErrorMessage.ToString();
                        SyntaxErrorListner.SyntaxErrorOccured = false;
                        return parsed;
                    });
                    if (syntax is not null)
                    {
                        return new ExecutionPresentation(false, syntax, prog.Diagnostics, null, null);
                    }
                    try
                    {
                        var program = testStatements is null
                            ? await prog.ExecuteAsync(context, linked.Token).ConfigureAwait(false)
                            : await prog.ExecuteAsync(testStatements, linked.Token).ConfigureAwait(false);
                        return new ExecutionPresentation(true, "Line successfull parsed", program.Diagnostics,
                            null, InterpreterExecution.AccessState(VariableViewModel.CaptureVariables));
                    }
                    catch (SecurePasswordCancelledException)
                    { return new ExecutionPresentation(false, "Script execution cancelled by user.", prog.Diagnostics, null, null); }
                    catch (OperationCanceledException) when (linked.IsCancellationRequested)
                    { return new ExecutionPresentation(false, "Script execution cancelled.", prog.Diagnostics, null, null); }
                    catch (Exception error)
                    {
                        return new ExecutionPresentation(false,
                            error is SemanticErrorException ? "Script execution aborted (semantic error)." : "Script execution failed.",
                            prog.Diagnostics, (error as SemanticErrorException)?.SemanticError, null);
                    }
                }, linked.Token);
                // FIFO dispatcher barrier: output handlers posted before completion are applied first.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                _statusViewModel.AppendDiagnostics(result.Diagnostics, result.Error, scriptBookLine);
                _statusViewModel.StatusString = result.Status;
                if (result.Variables is not null) _variableViewModel.ApplyVariables(result.Variables);
                return result.Succeeded;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            { _statusViewModel.StatusString = "Script execution cancelled."; return false; }
            catch (ExecutionBusyException)
            { _statusViewModel.StatusString = "Interpreter execution is busy."; return false; }
            finally
            {
                if (runner is null) prog.PasswordRequested -= _passwordHost.RequestAsync;
                lineActive = false;
            }
        }

        private sealed record ExecutionPresentation(bool Succeeded, string Status,
            IReadOnlyList<SemanticError> Diagnostics, SemanticError? Error, IReadOnlyList<VariableModel>? Variables);

        public async Task OpenScriptBookAsync(string filePath, bool clear = false, CancellationToken cancellationToken = default)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting) throw new ExecutionBusyException();
            bookActive = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            try
            {
            _statusViewModel.ClearDiagnostics();
            LastScriptBookSucceeded = false;
            var lines = await File.ReadAllLinesAsync(filePath, linked.Token);
            if (clear)
            {
                await InterpreterHostQueue.RunAsync(() =>
                    Task.FromResult(InterpreterExecution.AccessState(() =>
                    { VariableDictionary.Instance().Clear(); return true; })), linked.Token);
                _textEditor!.Document.Text = string.Empty;
            }
            await ExecuteScriptBookCoreAsync(lines, line =>
                _textEditor!.Document.Insert(_textEditor.Document.TextLength, line + "\n"), linked.Token);
            _textEditor!.IsModified = true;
            }
            finally { bookActive = false; }
        }

        internal async Task ExecuteScriptBookAsync(IEnumerable<string> lines, Action<string> appendLine,
            CancellationToken cancellationToken = default, Func<CryptoScriptRunner>? runnerFactory = null,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null)
        {
            if (IsExecuting) throw new ExecutionBusyException();
            bookActive = true;
            try
            {
                await ExecuteScriptBookCoreAsync(lines, appendLine, cancellationToken, runnerFactory, testStatements);
            }
            finally { bookActive = false; }
        }

        private async Task ExecuteScriptBookCoreAsync(IEnumerable<string> lines, Action<string> appendLine,
            CancellationToken cancellationToken, Func<CryptoScriptRunner>? runnerFactory = null,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null)
        {
            LastScriptBookSucceeded = false;
            int bookLine = 0;
            foreach (var line in lines)
            {
                if (cancellationToken.IsCancellationRequested || _lifetime.IsCancellationRequested) return;
                ++bookLine;
                bool succeeded = line.StartsWith("out:", StringComparison.Ordinal) ||
                    await ExecuteLineAsync(line, bookLine, runnerFactory?.Invoke(), cancellationToken, testStatements);
                appendLine(line);
                if (!succeeded) return;
            }
            LastScriptBookSucceeded = true;
        }

        public void Dispose()
        {
            _lifetime.Cancel();
            _passwordHost.Dispose();
            _historyService.LineHistoryChanged -= OnHistoryChanged;
            OutputOperations.PrintEvent -= OnPrintEvent;
            OutputOperations.InfoEvent -= OnInfoEvent;
        }
    }
}
