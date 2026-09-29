using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
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
        CloseWindow();
        AttachDocument(null);
        _reopenPolicy.Dispose();
        _editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
        _editor.TextArea.DocumentChanged -= TextArea_DocumentChanged;
        _editor.TextArea.RemoveHandler(InputElement.KeyDownEvent, TextArea_KeyDown);
    }

    private bool OpenOrUpdateForCurrentContext()
    {
        TextDocument? document = _editor.Document;
        if (document == null || !ReferenceEquals(document, _document))
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
            .Select(item => new InfoCompletionData(
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
        window.Closed += Window_Closed;

        ApplyMatches(window, _session, context.Prefix);
        window.Show();
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

        UpdateOrCloseActiveWindow();
    }

    private void TextArea_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            CancelAndClose();
    }

    private void Document_UpdateStarted(object? sender, EventArgs e)
    {
        _hasPendingChange = false;
        _pendingChangeIsDeletion = true;
        _pendingChangeOffset = int.MaxValue;
        _pendingRemovalLength = 0;
        _pendingInsertionLength = 0;
    }

    private void Document_Changed(object? sender, DocumentChangeEventArgs e)
    {
        _hasPendingChange = true;
        _pendingChangeIsDeletion &=
            e.RemovalLength > 0 && e.InsertionLength == 0;
        _pendingChangeOffset = Math.Min(_pendingChangeOffset, e.Offset);
        _pendingRemovalLength += e.RemovalLength;
        _pendingInsertionLength += e.InsertionLength;
    }

    private void Document_UpdateFinished(object? sender, EventArgs e)
    {
        if (!_hasPendingChange)
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

        if (!isDeletion)
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
        AttachDocument(_editor.Document);

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

    private void CancelAndClose()
    {
        _reopenPolicy.Cancel();
        CloseWindow();
    }

    private void CloseWindow()
    {
        CompletionWindow? window = _window;
        _window = null;
        _session = null;
        if (window == null)
            return;

        window.Closed -= Window_Closed;
        window.Hide();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(_window, sender))
        {
            _window = null;
            _session = null;
        }
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
        textArea.Document.Replace(completionSegment, Text);
        _completed?.Invoke();
    }
}
