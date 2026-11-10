using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.ViewModels;

namespace CryptoWorkBenchAvalonia.Shell;

/// <summary>One logical editor, independent of Dock's disposable visual presenters.</summary>
public sealed partial class DockEditorSession : IDisposable
{
    private readonly WorkbenchAutomationService _service;
    private ContentControl? _presenter;
    private Window? _mainWindow;
    private bool _disposed;
    public CryptoScriptEditView View { get; }
    public WorkbenchTextEditor Editor => View.SessionEditor;
    public CryptoScriptEditViewModel ViewModel { get; }
    public string DocumentId => DockShellState.DocumentId;
    public bool Started { get; private set; }
    internal DockOutputService Output { get; } = new();
    internal OutputSource? CaptureOutputSource(string? text, int? line = null)
    {
        var id = ViewModel.OutputDocumentId;
        if (_disposed || id == null) return null;
        if (line is { } number && (number < 1 || number > Editor.Document.LineCount ||
            Editor.Document.GetText(Editor.Document.GetLineByNumber(number)) != text)) line = null;
        return new(id.Value, Editor.Document.Version,
            line ?? (text == null || text == Editor.Text ? 1 : null));
    }
    internal bool NavigateOutput(PrintOutputEntry entry)
        => NavigateSource(entry.DocumentId, entry.SourceRevision, entry.SourceLine, entry.SourceColumn) == SourceNavigationResult.Success;

    internal SourceNavigationResult NavigateDiagnostic(Models.DiagnosticDisplayItem entry)
        => NavigateSource(entry.DocumentId, entry.SourceRevision, entry.SourceLine, entry.SourceColumn);

    internal SourceNavigationResult NavigateSource(Guid? documentId, AvaloniaEdit.Document.ITextSourceVersion? revision,
        int? sourceLineNumber, int? sourceColumn)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!CanRelocate) return SourceNavigationResult.Busy;
        if (documentId == null || revision == null || sourceLineNumber is not { } line)
            return SourceNavigationResult.Unknown;
        if (_service.CaptureSaveIntent()?.Snapshot.EditorId != documentId ||
            !ReferenceEquals(Editor.Document.Version, revision)) return SourceNavigationResult.Stale;
        if (line < 1 || line > Editor.Document.LineCount) return SourceNavigationResult.Invalid;
        int column = sourceColumn ?? 1;
        var sourceLine = Editor.Document.GetLineByNumber(line);
        if (column < 1 || column > sourceLine.Length + 1) return SourceNavigationResult.Invalid;
        _service.EditorCoordinator.Observe(() =>
        {
            Editor.CaretOffset = sourceLine.Offset + column - 1;
            Editor.ScrollTo(line, column);
            (TopLevel.GetTopLevel(Editor) as Window)?.Activate();
            Editor.TextArea.Focus();
        }, propagate: true);
        return SourceNavigationResult.Success;
    }
    internal bool StartAttempted { get; private set; }

    public DockEditorSession(CryptoScriptEditViewModel viewModel, MainViewModel main, WorkbenchAutomationService service)
    {
        ViewModel = viewModel;
        viewModel.DockOutput = Output;
        viewModel.CaptureOutputSource = CaptureOutputSource;
        _service = service;
        View = new CryptoScriptEditView(viewModel, sessionOwned: true);
        // Keep the same gestures in the editor's route, including when its TopLevel is floating.
        View.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse("Alt+PageUp"), Command = main.LineHistoryBackCommand });
        View.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse("Alt+PageDown"), Command = main.LineHistoryForwardCommand });
        View.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse("Ctrl+PageUp"), Command = main.InfoHistoryBackCommand });
        View.KeyBindings.Add(new KeyBinding { Gesture = KeyGesture.Parse("Ctrl+PageDown"), Command = main.InfoHistoryForwardCommand });
        AddFileCommands();
    }

    internal void Start(Window mainWindow)
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Started) return;
        StartAttempted = true;
        _mainWindow = mainWindow;
        ViewModel.TextEditor = Editor;
        ViewModel.AttachEditor();
        if (!_service.HasRegisteredEditor || !_service.EditorCoordinator.Targets(Editor))
            throw new InvalidOperationException("The dock editor session could not be registered.");
        Started = true;
    }

    internal bool CanRelocate => !_disposed && Started && !_service.IsStopping && !_service.IsBusy &&
        !ViewModel.IsExecuting && _mainWindow?.IsEnabled != false &&
        TopLevel.GetTopLevel(View)?.IsEnabled != false &&
        _service.EditorCoordinator is { Phase: EditorControlPhase.Gui, Active: false, Pending: 0, ObserverDepth: 0 } &&
        Editor is { MutationLocked: false, IdentityLocked: false } && !Editor.Document.IsInUpdate;

    internal void Relocate(Action action)
    {
        if (!CanRelocate) return;
        // Visual callbacks cannot acquire input, history, interpreter or MCP authority.
        // No ticket is created, no generation is forged and no declined action is retried.
        _service.EditorCoordinator.Observe(() =>
        {
            // Retire host-owned popups before Dock closes/reparents their native owner.
            // A presenter can be reused, so visual Detach alone is not a sufficient boundary.
            View.PrepareForHostChange();
            action();
        }, propagate: true);
    }

    internal Control CreatePresenter()
    {
        Dispatcher.UIThread.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        // A deferred old presenter can remain alive after Dock moves its model. Explicitly
        // release its content before assigning the same View to the new visual parent.
        if (_presenter != null) _presenter.Content = null;
        _presenter = new ContentControl { Content = View };
        return _presenter;
    }

    public void Dispose()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (_disposed) return;
        _disposed = true;
        Output.Dispose();
        ViewModel.DockOutput = null;
        ViewModel.CaptureOutputSource = null;
        try { if (StartAttempted) ViewModel.Dispose(); }
        finally
        {
            View.DismissTransientEditorUi();
            if (_presenter != null) _presenter.Content = null;
            _presenter = null;
            _mainWindow = null;
        }
    }
}
