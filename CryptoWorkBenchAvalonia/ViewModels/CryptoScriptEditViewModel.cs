using CryptoWorkBenchAvalonia.Services.Automation;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.ErrorListner;
using CryptoScript.Model;

using CryptoWorkBenchAvalonia.Services;

using Prism.Navigation.Regions;
using System;
using System.IO;
using System.Linq;

using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;



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
        public bool IsExecuting => lineActive || bookActive || _automation.IsBusy;
        private readonly WorkbenchAutomationService _automation;

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

        public CryptoScriptEditViewModel(StatusViewModel statusViewModel, VariableViewModel variableViewModel, InfoViewModel info, MainViewModel mvm, IRegionManager regions, IHistoryService historyService, WorkbenchAutomationService? automation = null)
        {
            _automation = automation ?? WorkbenchAutomationService.Shared;
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
        public bool LastScriptBookSucceeded { get; private set; }

        private static async Task OnUiAsync(Action action)
        {
            if (Avalonia.Application.Current is null) action();
            else await Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Background);
        }

        private void BeginLine(string line)
        {
            _historyService.AddLine(line);
            _printMessage = string.Empty;
        }

        private bool Present(ExecutionPresentation result, int? bookLine)
        {
            _statusViewModel.AppendDiagnostics(result.Diagnostics, result.Error, bookLine);
            _statusViewModel.StatusString = result.PresentationStatus;
            _syntaxErrorOccured = !result.HostSucceeded;
            if (result.CaptureFailed)
            {
                _variableViewModel.IsSnapshotStale = true;
                return false;
            }
            try { _variableViewModel.ApplyVariables(result.Variables); }
            catch (Exception)
            {
                _statusViewModel.StatusString = "Variable presentation failed.";
                _syntaxErrorOccured = true;
                return false;
            }
            return result.Succeeded;
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
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null,
            PasswordRequestHandler? passwordHandler = null)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting) throw new ExecutionBusyException();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            lineActive = true;
            try
            {
                return await _automation.RunAsync(async pipeline =>
                {
                    await OnUiAsync(() => BeginLine(line));
                    var result = await pipeline.ExecuteAsync(line, passwordHandler ?? _passwordHost.RequestAsync,
                        pipeline.ExecutionToken, runner, testStatements).ConfigureAwait(false);
                    bool succeeded = false;
                    // Background priority drains already posted Print/Info before presentation.
                    await OnUiAsync(() => succeeded = Present(result, scriptBookLine));
                    return succeeded;
                }, linked.Token, "guiLine\0" + line);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            { _statusViewModel.StatusString = "Script execution cancelled."; return false; }
            catch (ExecutionBusyException)
            { _statusViewModel.StatusString = "Interpreter execution is busy."; return false; }
            finally { lineActive = false; }
        }

        public async Task OpenScriptBookAsync(string filePath, bool clear = false, CancellationToken cancellationToken = default)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting) throw new ExecutionBusyException();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            bookActive = true;
            _statusViewModel.ClearDiagnostics();
            LastScriptBookSucceeded = false;
            try
            {
                await _automation.RunAsync(async pipeline =>
                {
                    var lines = await File.ReadAllLinesAsync(filePath, pipeline.ExecutionToken).ConfigureAwait(false);
                    if (clear)
                    {
                        var variables = WorkbenchAutomationService.ClearAdmitted();
                        await OnUiAsync(() =>
                        {
                            _textEditor!.Document.Text = string.Empty;
                            _variableViewModel.ApplyVariables(variables);
                        });
                    }
                    bool success = await ExecuteBookAdmittedAsync(pipeline, lines, line =>
                        _textEditor!.Document.Insert(_textEditor.Document.TextLength, line + "\n"), pipeline.ExecutionToken);
                    await OnUiAsync(() => { LastScriptBookSucceeded = success; _textEditor!.IsModified = true; });
                    return success;
                }, linked.Token, "scriptBookFile\0" + filePath + "\0" + clear);
            }
            finally { bookActive = false; }
        }

        internal async Task ExecuteScriptBookAsync(IEnumerable<string> lines, Action<string> appendLine,
            CancellationToken cancellationToken = default, Func<CryptoScriptRunner>? runnerFactory = null,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null,
            PasswordRequestHandler? passwordHandler = null)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting) throw new ExecutionBusyException();
            bookActive = true;
            LastScriptBookSucceeded = false;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            try
            {
                var acceptedLines = lines.ToArray();
                LastScriptBookSucceeded = await _automation.RunAsync(pipeline =>
                    ExecuteBookAdmittedAsync(pipeline, acceptedLines, appendLine, pipeline.ExecutionToken, runnerFactory,
                        testStatements, passwordHandler), linked.Token, "scriptBook\0" + string.Join("\n", acceptedLines));
            }
            finally { bookActive = false; }
        }

        private Task<bool> ExecuteBookAdmittedAsync(WorkbenchExecutionPipeline pipeline,
            IEnumerable<string> lines, Action<string> appendLine, CancellationToken token,
            Func<CryptoScriptRunner>? runnerFactory = null,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null,
            PasswordRequestHandler? passwordHandler = null) =>
            _automation.ExecuteScriptBookAsync(pipeline, lines, passwordHandler ?? _passwordHost.RequestAsync,
                (line, _) => OnUiAsync(() => BeginLine(line)),
                async (line, number, result) =>
                {
                    bool succeeded = true;
                    await OnUiAsync(() =>
                {
                    if (result is not null) succeeded = Present(result, number);
                    appendLine(line);
                });
                    return succeeded;
                }, token, runnerFactory, testStatements);

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
