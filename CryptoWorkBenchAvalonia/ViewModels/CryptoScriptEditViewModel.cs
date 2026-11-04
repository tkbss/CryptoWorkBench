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
    public class CryptoScriptEditViewModel : ViewModelBase, IDisposable, IEditorExecutionPresentation
    {
        StatusViewModel _statusViewModel;
        VariableViewModel _variableViewModel;
        InfoViewModel _infoViewModel;
        MainViewModel _mainViewModel;
        IRegionManager _regions;
        IHistoryService _historyService;
        private readonly IDisposable? _historyBinding;
        private bool disposed;
        string _printMessage = string.Empty;
        private TextEditor? _textEditor;
        bool _syntaxErrorOccured = false;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly PasswordDialogHost _passwordHost = new();
        private bool lineActive, bookActive;
        public bool IsExecuting => lineActive || bookActive || _automation.IsBusy;
        private readonly WorkbenchAutomationService _automation;

        internal long EditorLifecycle { get; private set; }
        private EditorBridge.Registration? _registration;
        private bool previouslyAttached;
        private bool OwnsDestination => !disposed && (_registration is { } r
            ? _automation.IsEditorLive(r, r.Id) : !previouslyAttached && !_automation.HasRegisteredEditor);
        internal void AttachEditor()
        {
            if (disposed || _textEditor is null) return;
            if (_registration is { Attached: true } && ReferenceEquals(_registration.Editor, _textEditor)) return;
            _registration = _automation.AttachEditor(_textEditor, this);
            previouslyAttached |= _registration != null;
            EditorLifecycle++;
        }
        internal void DetachEditor(TextEditor? expectedEditor = null)
        {
            if (expectedEditor is not null && !ReferenceEquals(_textEditor, expectedEditor)) return;
            if (_textEditor is CryptoWorkBenchAvalonia.Controls.WorkbenchTextEditor { IdentityLocked: true } guarded)
            {
                guarded.AfterIdentity(() => DetachEditor(expectedEditor));
                return;
            }
            if (_registration is not null) _automation.DetachEditor(_registration);
            _registration = null;
            EditorLifecycle++;
            TextEditor = null;
        }
        internal WorkbenchAutomationService.SaveIntent? CaptureSaveIntent() => OwnsDestination ? _automation.CaptureSaveIntent() : null;
        internal Task WithFileDialogAsync(Func<WorkbenchAutomationService.SaveIntent, Task> action) =>
            _registration is { } r && OwnsDestination ? _automation.WithFileDialogAsync(r, action) : Task.CompletedTask;
        internal Task<CryptoWorkBench.Automation.Abstractions.AutomationErrorCode?> CommitSaveAsync(
            WorkbenchAutomationService.SaveIntent intent, WorkbenchAutomationService.SaveTarget target) =>
            _automation.CommitSaveAsync(intent, target, _lifetime.Token);
        internal Task SaveScriptBookAsync(Func<Task<WorkbenchAutomationService.SaveTarget?>> selectTarget) => SaveScriptBookAsync(_ => selectTarget());
        internal Task SaveScriptBookAsync(Func<WorkbenchAutomationService.SaveIntent, Task<WorkbenchAutomationService.SaveTarget?>> selectTarget) => WithFileDialogAsync(async intent =>
        {
            // Capture occurs synchronously before invoking the picker, hence
            // before the GUI save command's first await anywhere in its chain.
            var target = await selectTarget(intent);
            using var item = target?.ProviderItem;
            if (target is null) return;
            var error = await CommitSaveAsync(intent, target);
            if (!_automation.IsEditorLive(intent.Registration, intent.Snapshot.EditorId) || _automation.IsStopping ||
                _automation.EditorCoordinator.WorkspaceGeneration != intent.WorkspaceGeneration) return;
            Status.StatusString = error.HasValue ? "Script book save aborted: " + error.Value
                : "Script book saved in: " + target.Path;
        });
        void IEditorExecutionPresentation.BeginExecution()
        {
            var current = CaptureDestination();
            _statusViewModel.ClearDiagnostics();
            if (current()) _statusViewModel.StatusString = "Script book execution started.";
        }
        void IEditorExecutionPresentation.Begin(string line) => BeginLine(line);
        bool IEditorExecutionPresentation.Present(ExecutionPresentation result, int number) => Present(result, number);
        string IEditorExecutionPresentation.Output => _printMessage;

        public StatusViewModel Status { get => _statusViewModel; }
        public TextEditor? TextEditor
        {
            get
            {
                return _textEditor;
            }

            set
            {
                if (ReferenceEquals(_textEditor, value)) return;
                if (_textEditor is CryptoWorkBenchAvalonia.Controls.WorkbenchTextEditor guarded &&
                    (guarded.IdentityLocked || (guarded.MutationLocked && value is not null)))
                    throw new ExecutionBusyException();
                if (_registration is not null) { _automation.DetachEditor(_registration); _registration = null; }
                EditorLifecycle++;
                SetProperty(ref _textEditor, value);
            }
        }
        public string PrintMessage
        {
            get => _printMessage;
            set => SetProperty(ref _printMessage, value);
        }

        public CryptoScriptEditViewModel(StatusViewModel statusViewModel, VariableViewModel variableViewModel, InfoViewModel info, MainViewModel mvm, IRegionManager regions, IHistoryService historyService, WorkbenchAutomationService? automation = null)
        {
            _automation = automation ?? WorkbenchAutomationService.Shared;
            Automation = _automation;
            statusViewModel.Automation = variableViewModel.Automation = info.Automation = mvm.Automation = _automation;
            _historyService = historyService;
            if (historyService is HistoryService history)
                _historyBinding = history.BindLineNavigation(NavigateHistory);
            _statusViewModel = statusViewModel;
            _variableViewModel = variableViewModel;
            _variableViewModel.IsExecutionActive = () => IsExecuting;
            _regions = regions;
            _infoViewModel = info;
            _mainViewModel = mvm;
        }
        private bool NavigateHistory(string entry, Action advanceIndex)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting || _registration is null ||
                _textEditor is not CryptoWorkBenchAvalonia.Controls.WorkbenchTextEditor editor ||
                !_automation.IsEditorLive(_registration, _registration.Id)) return false;
            var coordinator = _automation.EditorCoordinator;
                return coordinator.RunHistory(capability =>
                {
                    using var identity = editor.LockIdentity();
                    advanceIndex();
                    var doc = capability.Document;
                    var line = doc.Lines[doc.Lines.Count - 1];
                    if (doc.GetText(line) != entry)
                    {
                        editor.ReplaceHistoryLine(doc, line.Offset, line.Length, entry, capability);
                        coordinator.Observe(() => editor.IsModified = true);
                    }
                    coordinator.Observe(() => ((HistoryService)_historyService).PublishLine(entry));
                });
        }
        private Func<bool> CaptureDestination()
        {
            var lifecycle = EditorLifecycle;
            var registration = _registration;
            var id = registration?.Id;
            var document = _textEditor?.Document;
            var owner = _automation.EditorCoordinator;
            long generation = owner.WorkspaceGeneration;
            return () => !disposed && !_automation.IsStopping && owner.WorkspaceGeneration == generation && EditorLifecycle == lifecycle &&
                ReferenceEquals(document, _textEditor?.Document) &&
                (registration == null || _automation.IsEditorLive(registration, id!.Value));
        }

        private void PresentInfo(string message, Func<bool> current)
        {
            if (message.Contains("Gesture="))
                return;
            _infoViewModel.SetInfoText(message);
            if (!current()) return;
            _mainViewModel.InfoViewTitle = "InfoView";
            if (!current()) return;
            _automation.ObservePresentation(() => _regions?.RequestNavigate("InfoRegion", "InfoView"));
        }


        public void SaveScriptBook(string filePath) => _automation.SaveScriptBook(_textEditor!, filePath);

        public bool IsTextEditorEmpty()
        {
            if (_textEditor is null || _textEditor.Document.Text.Length == 0)
                return true;
            return false;
        }
        public bool LastScriptBookSucceeded { get; private set; }

        private static async Task OnUiAsync(Action action)
        {
            if (Avalonia.Application.Current is null) action();
            else await UiDispatch.InvokeAsync(action, DispatcherPriority.Background);
        }

        private void BeginLine(string line)
        {
            _historyService.AddLine(line);
            _printMessage = string.Empty;
        }

        private bool Present(ExecutionPresentation result, int? bookLine)
        {
            var current = CaptureDestination();
            _printMessage = string.Join(Environment.NewLine, result.Output.Where(item => item.Kind == "print").Select(item => item.Text));
            foreach (var item in result.Output)
                if (item.Kind == "info" && current()) PresentInfo(item.Text, current);
            if (!current()) return result.HostSucceeded;
            _statusViewModel.AppendDiagnostics(result.Diagnostics, result.Error, bookLine, current);
            if (!current()) return result.HostSucceeded;
            _statusViewModel.StatusString = result.PresentationStatus;
            if (!current()) return result.HostSucceeded;
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

        public Task ParseLineAsync(string line, CancellationToken cancellationToken = default) =>
            ParseInputLineAsync(line, null, cancellationToken);

        internal async Task<bool> ParseInputLineAsync(string line, Func<bool>? isCurrent, CancellationToken cancellationToken = default, Action<string>? publishOutput = null, EditorCoordinator.OperationTicket? native = null)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (!OwnsDestination) return false;
            if (IsExecuting) { _statusViewModel.StatusString = "Interpreter execution is busy."; return false; }
            if (isCurrent != null && !isCurrent()) return false;
            // Empty Reset remains the B1 preflight notification. Existing diagnostic records
            // are cleared only by the accepted job, after native admission succeeds.
            bool preflightReset = native != null && !_statusViewModel.HasDiagnostics;
            if (preflightReset) _statusViewModel.ClearDiagnostics();
            if (isCurrent != null && !isCurrent()) return false;
            bool admitted = false;
            await ExecuteLineAsync(line, cancellationToken: cancellationToken,
                validateIntent: isCurrent, onAdmitted: () => admitted = true, publishOutput: publishOutput, native: native, clearDiagnostics: !preflightReset);
            return admitted;
        }

        internal async Task<bool> ExecuteLineAsync(string line, int? scriptBookLine = null,
            CryptoScriptRunner? runner = null, CancellationToken cancellationToken = default,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null,
            PasswordRequestHandler? passwordHandler = null, Func<bool>? validateIntent = null, Action? onAdmitted = null, Action<string>? publishOutput = null, EditorCoordinator.OperationTicket? native = null, bool clearDiagnostics = false)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting || !OwnsDestination) throw new ExecutionBusyException();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            var destination = CaptureDestination();
            lineActive = true;
            try
            {
                var work = _automation.RunAsync(async pipeline =>
                {
                    bool Current() => destination() && pipeline.EditorIntent?.Current != false;
                    await OnUiAsync(() =>
                    {
                        if (Current() && clearDiagnostics) _statusViewModel.ClearDiagnostics();
                        if (Current()) BeginLine(line);
                        if (!Current()) pipeline.CancelStaleDestination();
                    });
                    var result = await pipeline.ExecuteAsync(line, passwordHandler ?? _passwordHost.RequestAsync,
                        pipeline.ExecutionToken, runner, testStatements).ConfigureAwait(false);
                    bool succeeded = false;
                    // Publish the original job result only to its original destination.
                    await OnUiAsync(() =>
                    {
                        succeeded = result.HostSucceeded;
                        if (!Current()) return;
                        succeeded = Present(result, scriptBookLine);
                        if (publishOutput != null && _printMessage.Length != 0 && Current())
                            pipeline.EditorIntent?.Write(() => publishOutput(_printMessage));
                    });
                    return succeeded;
                }, linked.Token, "guiLine\0" + line, validateIntent, native);
                onAdmitted?.Invoke();
                return await work;
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            { if (destination()) _statusViewModel.StatusString = "Script execution cancelled."; return false; }
            catch (ExecutionBusyException)
            { if (destination()) _statusViewModel.StatusString = "Interpreter execution is busy."; return false; }
            finally { lineActive = false; }
        }

        public async Task OpenScriptBookAsync(string filePath, bool clear = false, CancellationToken cancellationToken = default)
        {
            Dispatcher.UIThread.VerifyAccess();
            if (IsExecuting || !OwnsDestination) throw new ExecutionBusyException();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            var targetEditor = _textEditor;
            var targetDocument = targetEditor?.Document;
            var destination = CaptureDestination();
            bookActive = true;
            LastScriptBookSucceeded = false;
            try
            {
                await _automation.RunAsync(async pipeline =>
                {
                    await OnUiAsync(() =>
                    {
                        if (destination() && pipeline.EditorIntent?.Current != false) _statusViewModel.ClearDiagnostics();
                        else pipeline.CancelStaleDestination();
                    });
                    var lines = await File.ReadAllLinesAsync(filePath, pipeline.ExecutionToken).ConfigureAwait(false);
                    bool Current() => targetDocument != null && destination() && pipeline.EditorIntent?.Current != false;
                    await OnUiAsync(() =>
                    {
                        if (!Current()) pipeline.CancelStaleDestination();
                        else if (clear && !Write(pipeline, () => targetDocument!.Text = string.Empty)) pipeline.CancelStaleDestination();
                    });
                    pipeline.ExecutionToken.ThrowIfCancellationRequested();
                    if (clear)
                    {
                        var variables = _automation.ClearAdmittedState();
                        await OnUiAsync(() =>
                        {
                            if (Current()) _variableViewModel.ApplyVariables(variables);
                        });
                    }
                    bool success = await ExecuteBookAdmittedAsync(pipeline, lines, line =>
                    {
                        targetDocument!.Insert(targetDocument.TextLength, line + "\n");
                    }, pipeline.ExecutionToken, destination);
                    await OnUiAsync(() => { if (Current()) { LastScriptBookSucceeded = success; targetEditor!.IsModified = true; } });
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
            if (IsExecuting || !OwnsDestination) throw new ExecutionBusyException();
            bookActive = true;
            LastScriptBookSucceeded = false;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
            try
            {
                var destination = CaptureDestination();
                var acceptedLines = lines.ToArray();
                var succeeded = await _automation.RunAsync(pipeline =>
                    ExecuteBookAdmittedAsync(pipeline, acceptedLines, appendLine, pipeline.ExecutionToken, destination, runnerFactory,
                        testStatements, passwordHandler), linked.Token, "scriptBook\0" + string.Join("\n", acceptedLines));
                if (destination()) LastScriptBookSucceeded = succeeded;
            }
            finally { bookActive = false; }
        }

        private static bool Write(WorkbenchExecutionPipeline pipeline, Action action)
        {
            if (pipeline.EditorIntent is { } intent) return intent.Write(action);
            action();
            return true;
        }

        private Task<bool> ExecuteBookAdmittedAsync(WorkbenchExecutionPipeline pipeline,
            IEnumerable<string> lines, Action<string> appendLine, CancellationToken token, Func<bool> destination,
            Func<CryptoScriptRunner>? runnerFactory = null,
            IEnumerable<CryptoScript.Model.Ast.StatementNode?>? testStatements = null,
            PasswordRequestHandler? passwordHandler = null) =>
            _automation.ExecuteScriptBookAsync(pipeline, lines, passwordHandler ?? _passwordHost.RequestAsync,
                (line, _) => OnUiAsync(() =>
                {
                    if (destination() && pipeline.EditorIntent?.Current != false) BeginLine(line);
                    if (!destination() || pipeline.EditorIntent?.Current == false) pipeline.CancelStaleDestination();
                }),
                async (line, number, result) =>
                {
                    bool succeeded = true;
                    await OnUiAsync(() =>
                    {
                        if (!destination() || pipeline.EditorIntent?.Current == false) return;
                        if (result is not null) succeeded = Present(result, number);
                        if (destination() && pipeline.EditorIntent?.Current != false && !Write(pipeline, () => appendLine(line)))
                            succeeded = false;
                    });
                    return succeeded;
                }, token, runnerFactory, testStatements);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var failures = new List<Exception>();
            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception error) { failures.Add(error); }
            }
            Cleanup(() => { if (_registration is not null) DetachEditor(); });
            Cleanup(() => _lifetime.Cancel());
            Cleanup(() => _passwordHost.Dispose());
            Cleanup(() => _historyBinding?.Dispose());
            if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new AggregateException(failures);
        }
    }
}
