using Avalonia.Input;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Services.Automation;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;
using CryptoWorkBenchAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CryptoWorkBenchAvalonia.Services;

public sealed class InfoCompletionController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly InfoCompletionProvider _provider;
    private readonly InfoCompletionReopenPolicy _reopenPolicy = new();
    private TextDocument? _document;
    private CompletionWindow? _window;
    private CompletionSession? _session;
    private bool _disposed;
    private CompletionSession? _latestSession;
    private Action? _unsubscribeWindow;
    private long _reopenGeneration;
    private EditorCoordinator? Owner => (_editor as WorkbenchTextEditor)?.Coordinator;

    private void Observe(Action action)
    {
        if (_disposed) return;
        if (Owner is { } owner) owner.Observe(action);
        else action();
    }

    private bool _hasPendingChange;
    private bool _pendingChangeIsDeletion;
    private int _pendingChangeOffset;
    private int _pendingRemovalLength;
    private int _pendingInsertionLength;

    public InfoCompletionController(
        TextEditor editor,
        InfoCompletionProvider provider)
    {
        _editor = editor;
        _provider = provider;
        _editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        _editor.TextArea.DocumentChanged += TextArea_DocumentChanged;
        _editor.TextArea.AddHandler(
            InputElement.KeyDownEvent,
            TextArea_KeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        AttachDocument(_editor.Document);
    }

    public bool OpenForTypedParenthesis()
    {
        _reopenPolicy.Cancel();
        return OpenOrUpdateForCurrentContext();
    }

    public void HandleTextEntering(string text)
    {
        if (text == ")")
            CloseWindow();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
        _editor.TextArea.DocumentChanged -= TextArea_DocumentChanged;
        _editor.TextArea.RemoveHandler(InputElement.KeyDownEvent, TextArea_KeyDown);
        _reopenPolicy.Dispose();
        try { CloseWindow(); }
        finally { AttachDocument(null); }
    }

    private bool OpenOrUpdateForCurrentContext()
    {
        TextDocument? document = _editor.Document;
        if (_disposed || document == null || !ReferenceEquals(document, _document))
        {
            CloseWindow();
            return false;
        }

        if (!_provider.TryGetContext(
                document.Text,
                _editor.TextArea.Caret.Offset,
                out InfoCompletionContext context))
        {
            CloseWindow();
            return false;
        }

        if (_window != null &&
            _session != null &&
            IsCurrentSession(document, context))
        {
            UpdateWindow(context);
            return true;
        }

        CloseWindow();
        CreateWindow(context);
        return true;
    }

    private void CreateWindow(InfoCompletionContext context)
    {
        TextDocument document = _editor.Document;
        string[] allItems = _provider.GetCompletionItems(
                document.Text,
                context.CaretOffset)
            .ToArray();
        var completionData = allItems
            .Select(item => _editor is WorkbenchTextEditor
                ? InfoCompletionData.ForWindow(item, _provider.GetDescription(item))
                : new InfoCompletionData(
                item,
                _provider.GetDescription(item),
                () => MarkCurrentContextAccepted(document)))
            .ToArray();

        var window = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = context.ArgumentStartOffset,
            EndOffset = context.ArgumentEndOffset
        };
        window.CompletionList.IsFiltering = false;

        _window = window;
        _session = new CompletionSession(
            document,
            CreateInvocationAnchor(document, context.InvocationStartOffset),
            completionData);
        var session = _latestSession = _session;
        var owner = Owner;
        long generation = owner?.Generation ?? 0;
        ITextSourceVersion? closedVersion = null;
        int start = 0, length = 0;
        InfoCompletionData? chosen = null;

        // The native handler closes first. Removing our handler during Closed
        // leaves it in the invocation list of this insertion request only.
        void Closed(object? sender, EventArgs args) => Observe(() =>
        {
            if (!ReferenceEquals(_session, session)) return;
            closedVersion = document.Version;
            start = window.StartOffset;
            length = window.EndOffset - start;
            chosen = window.CompletionList.SelectedItem as InfoCompletionData;
            Unsubscribe();
            _session = null;
            _window = null;
            // Native code looks up SelectedItem after Hide and would construct
            // an AnchorSegment on whatever document a later Closed handler installed.
            if (owner != null) window.CompletionList.SelectedItem = null;
        });

        void Insert(object? sender, EventArgs args)
        {
            if (chosen == null || owner == null || _editor is not WorkbenchTextEditor editor) return;
            bool SessionLive() => !_disposed && _window == null && ReferenceEquals(session, _latestSession) &&
                ReferenceEquals(owner, Owner) && owner.Generation == generation && owner.Targets(editor) &&
                !editor.IsReadOnly && ReferenceEquals(editor.Document, document) && !session.InvocationStart.IsDeleted;
            bool RangeCurrent() => SessionLive() && ReferenceEquals(document.Version, closedVersion) &&
                _provider.TryGetContext(document.Text, editor.CaretOffset, out var current) &&
                current.ArgumentStartOffset == start && current.ArgumentEndOffset == start + length &&
                current.InvocationStartOffset == session.InvocationStart.Offset;
            if (!RangeCurrent()) return;
            var caret = editor.TextArea.Caret.Position;
            var selection = editor.TextArea.Selection;
            var provider = editor.TextArea.ReadOnlySectionProvider;
            bool Current() => RangeCurrent() && editor.TextArea.Caret.Position.Equals(caret) &&
                ReferenceEquals(editor.TextArea.Selection, selection) && ReferenceEquals(editor.TextArea.ReadOnlySectionProvider, provider);
            var item = chosen;
            chosen = null;
            if (!session.AllItems.Contains(item)) return;
            using var ticket = owner.TryBegin();
            if (ticket == null) return;
            bool writable = false;
            long? permissionRevision = null;
            owner.Observe(() =>
            {
                permissionRevision = editor.PermissionRevision(provider);
                writable = editor.CanReplaceSegment(new SimpleSegment(start, length), item.Text);
            });
            if (!writable || !permissionRevision.HasValue || !Current()) return;
            bool CanApply()
            {
                bool permitted = false;
                owner.Observe(() => permitted = editor.PermissionRevision(provider) == permissionRevision);
                return permitted && Current() && owner.IsActiveGuiOperation(ticket);
            }
            owner.Commit(ticket, editor, original =>
            {
                if (original.GetText(start, length) != item.Text)
                {
                    if (!ReplaceCurrentCompletion(editor.TextArea, ticket, CanApply, start, length, item.Text))
                        return;
                }
                if (SessionLive()) owner.Observe(() => MarkCurrentContextAccepted(document));
            });
        }

        // Avalonia's selection list handles the pointer. Keep keyboard focus on
        // the editor so selecting its own suggestion cannot invalidate the session.
        void KeepEditorFocus(object? sender, FocusChangingEventArgs args)
        {
            if (ReferenceEquals(args.OldFocusedElement, _editor.TextArea)) args.TryCancel();
        }

        void Unsubscribe()
        {
            window.CompletionList.GettingFocus -= KeepEditorFocus;
            window.Closed -= Closed;
            window.CompletionList.InsertionRequested -= Insert;
            _unsubscribeWindow = null;
        }

        _unsubscribeWindow = Unsubscribe;
        window.CompletionList.GettingFocus += KeepEditorFocus;
        window.Closed += Closed;
        window.CompletionList.InsertionRequested += Insert;

        try
        {
            ApplyMatches(window, session, context.Prefix);
            window.Show();
        }
        catch { CloseWindow(); throw; }
    }

    private static bool ReplaceCurrentCompletion(TextArea area, EditorCoordinator.OperationTicket ticket, Func<bool> current,
        int start, int length, string text)
    {
        var document = ticket.Document;
        var history = document.UndoStack;
        if (!history.CanRedo)
        {
            using (document.RunUpdate())
            {
                if (!current()) return false;
                document.Replace(start, length, text);
            }
            return true;
        }

        // The pinned TextArea pushes an optional caret action in UpdateStarted.
        // That clears executable redo even if no text changes. An outer undo
        // group defers native size enforcement while optional recording is off.
        // Restore the limit before validating and before ending either group.
        var caretHistory = new CompletionCaret(area, area.Caret.Position, area.Selection);
        int limit = history.SizeLimit;
        history.StartUndoGroup();
        IDisposable? update = null;
        try
        {
            try
            {
                ticket.TemporaryHistoryLimit = true;
                history.SizeLimit = 0;
                update = document.RunUpdate();
            }
            finally
            {
                try { history.SizeLimit = limit; }
                finally { ticket.TemporaryHistoryLimit = false; }
            }
            if (!current()) return false;
            history.PushOptional(caretHistory);
            document.Replace(start, length, text);
            return true;
        }
        finally
        {
            try { update?.Dispose(); }
            finally { history.EndUndoGroup(); }
        }
    }

    private sealed class CompletionCaret(TextArea area, TextViewPosition position, Selection selection) : IUndoableOperation
    {
        private readonly WeakReference<TextArea> target = new(area);
        public void Undo()
        {
            if (!target.TryGetTarget(out var editor)) return;
            editor.Caret.Position = position;
            editor.Selection = selection;
        }
        public void Redo() => Undo();
    }

    private void UpdateWindow(InfoCompletionContext context)
    {
        if (_window == null || _session == null)
            return;

        _window.StartOffset = context.ArgumentStartOffset;
        _window.EndOffset = context.ArgumentEndOffset;
        ApplyMatches(_window, _session, context.Prefix);
    }

    private static void ApplyMatches(
        CompletionWindow window,
        CompletionSession session,
        string prefix)
    {
        var list = window.CompletionList;
        string? selectedText = list.SelectedItem?.Text;
        var matches = session.AllItems
            .Where(item => InfoCompletionProvider.MatchesPrefix(item.Text, prefix))
            .ToList();

        list.CompletionData.Clear();
        foreach (InfoCompletionData item in matches)
            list.CompletionData.Add(item);

        list.ListBox.ItemsSource = matches;
        list.IsVisible = matches.Count > 0;
        if (matches.Count == 0)
        {
            list.ListBox.ClearSelection();
            return;
        }

        int selectedIndex = selectedText == null
            ? -1
            : matches.FindIndex(item => item.Text == selectedText);
        list.ListBox.SelectIndex(selectedIndex >= 0 ? selectedIndex : 0);
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_window == null)
            return;

        Observe(UpdateOrCloseActiveWindow);
    }

    private void TextArea_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Observe(CancelAndClose);
    }

    private void Document_UpdateStarted(object? sender, EventArgs e)
    {
        if (_disposed || !ReferenceEquals(sender, _document)) return;
        _hasPendingChange = false;
        _pendingChangeIsDeletion = true;
        _pendingChangeOffset = int.MaxValue;
        _pendingRemovalLength = 0;
        _pendingInsertionLength = 0;
    }

    private void Document_Changed(object? sender, DocumentChangeEventArgs e)
    {
        if (_disposed || !ReferenceEquals(sender, _document)) return;
        _hasPendingChange = true;
        _pendingChangeIsDeletion &=
            e.RemovalLength > 0 && e.InsertionLength == 0;
        _pendingChangeOffset = Math.Min(_pendingChangeOffset, e.Offset);
        _pendingRemovalLength += e.RemovalLength;
        _pendingInsertionLength += e.InsertionLength;
    }

    private void Document_UpdateFinished(object? sender, EventArgs e) => Observe(() => FinishChange(sender));

    private void FinishChange(object? sender)
    {
        if (!_hasPendingChange || !ReferenceEquals(sender, _document))
            return;

        bool isDeletion = _pendingChangeIsDeletion;
        int changeOffset = _pendingChangeOffset;
        int removalLength = _pendingRemovalLength;
        int insertionLength = _pendingInsertionLength;
        _hasPendingChange = false;

        TextDocument? document = _editor.Document;
        if (document == null ||
            !ReferenceEquals(document, _document))
        {
            CloseWindow();
            _reopenPolicy.Cancel();
            return;
        }

        if (_window != null)
        {
            UpdateOrCloseActiveWindow();
            return;
        }

        if (!isDeletion || (Owner?.Generation ?? 0) != _reopenGeneration)
            return;

        if (!_provider.TryGetContext(
                document.Text,
                _editor.TextArea.Caret.Offset,
                out InfoCompletionContext context))
        {
            return;
        }

        if (_reopenPolicy.ShouldReopen(
                document,
                context,
                changeOffset,
                removalLength,
                insertionLength))
        {
            CreateWindow(context);
        }
    }

    private void TextArea_DocumentChanged(object? sender, DocumentChangedEventArgs e) =>
        Observe(() => AttachDocument(_editor.Document));

    private void AttachDocument(TextDocument? document)
    {
        if (ReferenceEquals(_document, document))
            return;

        if (_document != null)
        {
            _document.UpdateStarted -= Document_UpdateStarted;
            _document.Changed -= Document_Changed;
            _document.UpdateFinished -= Document_UpdateFinished;
        }

        CloseWindow();
        _reopenPolicy.Cancel();
        ResetPendingChange();
        _document = document;

        if (_document != null)
        {
            _document.UpdateStarted += Document_UpdateStarted;
            _document.Changed += Document_Changed;
            _document.UpdateFinished += Document_UpdateFinished;
        }
    }

    internal void CancelAndClose()
    {
        _reopenPolicy.Cancel();
        CloseWindow();
    }

    private void CloseWindow()
    {
        CompletionWindow? window = _window;
        _window = null;
        _session = null;
        _latestSession = null;
        if (window == null)
            return;

        _unsubscribeWindow?.Invoke();
        window.Hide();
    }

    private void UpdateOrCloseActiveWindow()
    {
        TextDocument? document = _editor.Document;
        if (_window == null ||
            _session == null ||
            document == null ||
            !ReferenceEquals(document, _document) ||
            !ReferenceEquals(document, _session.Document) ||
            !_provider.TryGetContext(
                document.Text,
                _editor.TextArea.Caret.Offset,
                out InfoCompletionContext context) ||
            !IsCurrentSession(document, context))
        {
            CloseWindow();
            return;
        }

        UpdateWindow(context);
    }

    private bool IsCurrentSession(
        TextDocument document,
        InfoCompletionContext context) =>
        _session != null &&
        ReferenceEquals(_session.Document, document) &&
        !_session.InvocationStart.IsDeleted &&
        _session.InvocationStart.Offset == context.InvocationStartOffset;

    private static TextAnchor CreateInvocationAnchor(
        TextDocument document,
        int offset)
    {
        TextAnchor anchor = document.CreateAnchor(offset);
        anchor.MovementType = AnchorMovementType.AfterInsertion;
        return anchor;
    }

    private void ResetPendingChange()
    {
        _hasPendingChange = false;
        _pendingChangeIsDeletion = false;
        _pendingChangeOffset = 0;
        _pendingRemovalLength = 0;
        _pendingInsertionLength = 0;
    }

    private void MarkCurrentContextAccepted(TextDocument document)
    {
        if (!ReferenceEquals(_editor.Document, document) ||
            !_provider.TryGetContext(
                document.Text,
                _editor.TextArea.Caret.Offset,
                out InfoCompletionContext context))
        {
            _reopenPolicy.Cancel();
            return;
        }

        _reopenGeneration = Owner?.Generation ?? 0;
        _reopenPolicy.MarkAccepted(
            document,
            context.InvocationStartOffset,
            context.ArgumentStartOffset);
    }

    private sealed record CompletionSession(
        TextDocument Document,
        TextAnchor InvocationStart,
        IReadOnlyList<InfoCompletionData> AllItems);
}

public sealed class InfoCompletionData : ICompletionData
{
    private readonly Action? _completed;
    private bool RequestOwned { get; init; }
    internal static InfoCompletionData ForWindow(string text, string description) =>
        new(text, description) { RequestOwned = true };

    public InfoCompletionData(
        string text,
        string? description = null,
        Action? completed = null)
    {
        Text = text;
        Description = description ?? text;
        _completed = completed;
    }

    public IImage Image => null!;

    public string Text { get; }

    public object Content => Text;

    public object Description { get; }

    public double Priority => 0;

    public void Complete(
        TextArea textArea,
        ISegment completionSegment,
        EventArgs insertionRequestEventArgs)
    {
        // Workbench insertion is finalized by the original request's session handler.
        if (RequestOwned || textArea is WorkbenchTextArea) return;
        textArea.Document.Replace(completionSegment, Text);
        _completed?.Invoke();
    }
}
