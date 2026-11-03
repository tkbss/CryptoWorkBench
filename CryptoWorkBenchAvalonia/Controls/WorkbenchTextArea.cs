using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;
using CryptoWorkBenchAvalonia.Services.Automation;

namespace CryptoWorkBenchAvalonia.Controls;

/// <summary>Early native input boundary; original native command registrations stay intact.</summary>
internal sealed class WorkbenchTextArea : TextArea, IRoutedCommandBindable
{
    internal WorkbenchTextEditor? Host { get; set; }
    internal Task PendingClipboard { get; private set; } = Task.CompletedTask;
    private RoutedCommandBinding? nativeBinding;
    internal Action<TextInputEventArgs, Func<bool>, EditorCoordinator.OperationTicket>? AdmittedInput { private get; set; }
    private bool copying;
    private Action<TextInputEventArgs>? enterContinuation;
    protected override Type StyleKeyOverride => typeof(TextArea);

    static WorkbenchTextArea()
    {
        ((StyledProperty<bool>)InputMethod.IsInputMethodEnabledProperty).OverrideMetadata<WorkbenchTextArea>(
            new StyledPropertyMetadata<bool>(false, coerce: (_, _) => false));
    }

    public WorkbenchTextArea()
    {
        InputMethod.SetIsInputMethodEnabled(this, false);
        Options.EnableTextDragDrop = false;
        LostFocus += (_, _) => Host?.Coordinator?.InvalidateInputIntents();
    }

    protected override void OnOptionChanged(PropertyChangedEventArgs e)
    {
        // Runs before the native OptionChanged handlers can install a drag handler.
        if (Options is { EnableTextDragDrop: true }) Options.EnableTextDragDrop = false;
        base.OnOptionChanged(e);
    }

    private bool Allowed => Host is not { MutationLocked: true } && Host is not { IdentityLocked: true } &&
        Host is { Coordinator: { } c } editor && c.Targets(editor) && c.CanStartGui;

    private T Native<T>(RoutedCommandBinding binding, Func<T> action)
    {
        nativeBinding = binding;
        try { return action(); }
        finally { nativeBinding = null; }
    }

    IList<RoutedCommandBinding> IRoutedCommandBindable.CommandBindings
    {
        get
        {
            // Consume BEFORE native handlers/observers run. No callback inherits the bypass.
            if (nativeBinding is { } original) { nativeBinding = null; return new[] { original }; }
            var commands = DefaultInputHandler.Editing.CommandBindings.Select(b => b.Command).ToHashSet();
            commands.Add(ApplicationCommands.Undo);
            commands.Add(ApplicationCommands.Redo);
            commands.Add(ApplicationCommands.Copy);
            return base.CommandBindings.Select(binding =>
            {
                if (WorkbenchTextEditor.IsSearchMutation(binding.Command) && Host?.SearchPanel is { } panel)
                    return Host.SearchBinding(panel, binding.Command);
                return commands.Contains(binding.Command) ? Guard(binding) : binding;
            }).ToList();
        }
    }

    private RoutedCommandBinding Guard(RoutedCommandBinding binding)
    {
        var command = binding.Command;
        return new RoutedCommandBinding(command, (_, args) =>
        {
            args.Handled = true;
            void ExecuteNative() => Native(binding, () => { command.Execute(args.Parameter, this); return true; });
            var owner = Host?.Coordinator;
            if (command == ApplicationCommands.Copy)
            {
                if (copying) return;
                copying = true;
                try
                {
                    if (owner != null) owner.Observe(ExecuteNative);
                    else ExecuteNative();
                }
                finally { copying = false; }
                return;
            }
            if (owner == null || !Allowed) return;
            if (command == EditingCommands.EnterParagraphBreak || command == EditingCommands.EnterLineBreak)
                ExecuteEnter(owner);
            else if (command == ApplicationCommands.Paste || command == ApplicationCommands.Cut)
                PendingClipboard = ClipboardAsync(owner, command);
            else
                owner.RunGui(ExecuteNative);
        }, (_, args) =>
        {
            bool allowed = command == ApplicationCommands.Copy ? !copying : Allowed;
            bool available = false;
            if (allowed)
            {
                void Query() => available = Native(binding, () => command.CanExecute(args.Parameter, this));
                if (Host?.Coordinator is { } owner) owner.Observe(Query, propagate: true);
                else Query();
            }
            args.CanExecute = available;
            args.Handled = true;
        });
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!Allowed) { e.Handled = true; return; }
        base.OnTextInput(e);
    }

    protected override void OnTextEntering(TextInputEventArgs e)
    {
        if (enterContinuation is { } continuation)
        {
            // Consume before *any* TextEntering subscriber sees the event.
            enterContinuation = null;
            continuation(e);
            return;
        }
        // Also covers public PerformTextInput, which bypasses OnTextInput.
        // Re-dispatch once inside the admission scope; suppress only the outer call.
        if (Allowed && Host?.Coordinator is { } coordinator)
            ExecuteTextInput(coordinator, e);
        e.Handled = true;
    }

    protected override void OnTextEntered(TextInputEventArgs e)
    {
        if (Host?.Coordinator is { } owner) owner.Observe(() => base.OnTextEntered(e), propagate: true);
        else base.OnTextEntered(e);
    }

    private void ExecuteEnter(EditorCoordinator coordinator)
    {
        // Native Enter requires focus and then delegates exactly one newline.
        if (!IsFocused)
        {
            coordinator.RunGui(() => { });
            return;
        }
        ExecuteTextInput(coordinator, new TextInputEventArgs { Text = "\n", RoutedEvent = TextInputEvent });
    }

    private void ExecuteTextInput(EditorCoordinator coordinator, TextInputEventArgs input)
    {
        using var ticket = coordinator.TryBegin();
        if (ticket is null) return;
        string? expected = input.Text;
        coordinator.Commit(ticket, () =>
        {
            var editor = Host;
            var document = Document;
            bool valid = true;
            bool IsOriginal() => coordinator.IsActiveGuiOperation(ticket) &&
                ReferenceEquals(Host, editor) && ReferenceEquals(editor?.Coordinator, coordinator) &&
                ReferenceEquals(ticket.Registration.Editor, editor) &&
                ReferenceEquals(Document, document) && ReferenceEquals(ticket.Document, document) &&
                editor is { MutationLocked: false, IdentityLocked: false };
            enterContinuation = actual =>
            {
                bool admitted = valid && ReferenceEquals(actual, input) && actual.Text == expected &&
                    !actual.Handled && IsOriginal();
                valid = false;
                if (!admitted) { actual.Handled = true; return; }
                var caret = Caret.Position;
                var selection = Selection;
                coordinator.Observe(() => base.OnTextEntering(actual), propagate: true);
                if (actual.Handled || actual.Text != expected || !IsOriginal() ||
                    !Caret.Position.Equals(caret) || !ReferenceEquals(Selection, selection))
                {
                    actual.Handled = true;
                    return;
                }
                AdmittedInput?.Invoke(actual, IsOriginal, ticket);
                if (!IsOriginal()) actual.Handled = true;
            };
            try
            {
                // No routed command, observer or mutation is called between
                // installing the event-bound delegation and this native entry.
                base.PerformTextInput(input);
            }
            finally { valid = false; enterContinuation = null; }
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // TextArea handles Tab directly; history shortcuts still use native registrations.
        if (e.Key == Key.Tab && Options.AcceptsTab && IsFocused)
        {
            if (Allowed) Host!.Coordinator!.RunGui(() => base.OnKeyDown(e));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private sealed record ClipboardEdit(SimpleSegment Range, string Text);

    // Materialize value types before RunUpdate. No native callback can substitute
    // a provider, selection, mutable segment or a different document for these edits.
    private Action<TextDocument>? PlanClipboard(IReadOnlySectionProvider provider, Selection selection,
        TextViewPosition caret, bool paste, ISegment? wholeLine, string text)
    {
        var document = Document;
        var revision = Host!.PermissionRevision(provider);
        int offset = document.GetOffset(caret.Location);
        if (paste && !provider.CanInsert(offset)) return null;
        var edits = new List<ClipboardEdit>();
        bool rectangle = selection is RectangleSelection;
        var rows = selection.Segments.ToArray();
        // Independent grants from an unversioned policy cannot be combined across rows.
        if (wholeLine == null && rows.Length > 1 && revision == null) return null;
        string[]? lines = rectangle && text.IndexOfAny(['\r', '\n']) >= 0
            ? text.Split(["\r\n", "\r", "\n"], rows.Length, StringSplitOptions.None) : null;
        var start = selection.StartPosition;
        var end = selection.EndPosition;
        int firstOffset = rows.FirstOrDefault()?.StartOffset ?? offset;
        int firstInsertion = 0;
        TextViewPosition? caretBefore = null;
        int? caretOffsetBefore = null;

        string Pad(string value, TextViewPosition from, TextViewPosition to)
        {
            if (!selection.EnableVirtualSpace || value is "\r" or "\n" or "\r\n") return value;
            var line = document.GetLineByNumber(from.Line);
            int spaces = from.VisualColumn - TextView.GetOrConstructVisualLine(line).VisualLength;
            if (spaces <= 0 || value.Length == 0 && to.VisualColumn >
                TextView.GetOrConstructVisualLine(document.GetLineByNumber(to.Line)).VisualLength) return value;
            int tabs = Options.ConvertTabsToSpaces || document.GetText(line).Trim('\t').Length != 0
                ? 0 : spaces / Options.IndentationSize;
            return new string('\t', tabs) + new string(' ', spaces - tabs * Options.IndentationSize) + value;
        }

        void PlanRange(ISegment range, string value, TextViewPosition from, TextViewPosition to)
        {
            var parts = new List<SimpleSegment>();
            int next = range.Offset;
            foreach (var part in provider.GetDeletableSegments(range))
            {
                var frozen = new SimpleSegment(part.Offset, part.Length);
                if (frozen.Offset < next || frozen.Length < 0 || frozen.EndOffset > range.EndOffset)
                    throw new InvalidOperationException("Invalid clipboard protection segment.");
                if (frozen.Length != 0 || range.Length == 0 && paste) parts.Add(frozen);
                next = frozen.EndOffset;
            }
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var part = parts[i];
                string insert = i == parts.Count - 1 ? value : string.Empty;
                if (wholeLine == null && i == parts.Count - 1)
                {
                    var surround = selection.SurroundingSegment;
                    if (part.Offset == surround.Offset && part.Length == surround.Length)
                        insert = Pad(insert, from, to);
                    firstInsertion = insert.Length;
                    if (!rectangle)
                    {
                        if (insert.Length == 0) caretBefore = from.CompareTo(to) <= 0 ? from : to;
                        else caretOffsetBefore = part.EndOffset;
                    }
                }
                if (part.Length != 0 || insert.Length != 0) edits.Add(new(part, insert));
            }
        }

        if (wholeLine != null) PlanRange(wholeLine, string.Empty, caret, caret);
        else if (!rectangle && selection.IsEmpty)
        {
            if (paste) edits.Add(new(new SimpleSegment(offset, 0), Pad(text, caret, caret)));
        }
        else
        {
            for (int i = (lines?.Length ?? rows.Length) - 1; i >= 0; i--)
            {
                var row = rows[i];
                string value = lines?[i] ?? text;
                firstInsertion = value.Length;
                var from = rectangle ? new TextViewPosition(document.GetLocation(row.StartOffset), row.StartVisualColumn) : start;
                var to = rectangle ? new TextViewPosition(document.GetLocation(row.EndOffset), row.EndVisualColumn) : end;
                if (rectangle && row.Length == 0)
                {
                    if (value.Length != 0 && provider.CanInsert(row.StartOffset))
                    {
                        value = Pad(value, from, to);
                        firstInsertion = value.Length;
                        edits.Add(new(new SimpleSegment(row.StartOffset, 0), value));
                    }
                }
                else PlanRange(row, value, from, to);
            }
        }
        // A single requested range can also enumerate several disjoint grants.
        if (edits.Count == 0 || (edits.Count > 1 && revision == null) ||
            Host.PermissionRevision(provider) != revision) return null;
        return original =>
        {
            using (original.RunUpdate())
            {
                if (caretBefore.HasValue) Caret.Position = caretBefore.Value;
                if (caretOffsetBefore.HasValue) Caret.Offset = caretOffsetBefore.Value;
                foreach (var edit in edits) original.Replace(edit.Range.Offset, edit.Range.Length, edit.Text);
                if (rectangle && wholeLine == null)
                {
                    int lastLine = Math.Max(start.Line, end.Line);
                    var pos = new TextViewPosition(original.GetLocation(firstOffset + firstInsertion));
                    if (lines == null)
                    {
                        var top = TextView.GetOrConstructVisualLine(original.GetLineByNumber(pos.Line));
                        int column = top.ValidateVisualColumn(pos, true);
                        double x = top.GetTextLineVisualXPosition(top.GetTextLine(column, pos.IsAtEndOfLine), column);
                        var bottom = TextView.GetOrConstructVisualLine(original.GetLineByNumber(lastLine));
                        int visual = bottom.GetVisualColumn(new Point(x, 0), true);
                        var location = original.GetLocation(bottom.FirstDocumentLine.Offset + bottom.GetRelativeOffset(visual));
                        Selection = new RectangleSelection(this, pos, new TextViewPosition(location, visual));
                    }
                    else ClearSelection();
                    Caret.Position = new TextViewPosition(lastLine, pos.Column);
                }
                else if (wholeLine == null && !selection.IsEmpty) ClearSelection();
            }
            Caret.BringCaretToView();
        };
    }

    private async Task ClipboardAsync(EditorCoordinator coordinator, RoutedCommand command)
    {
        var ticket = coordinator.TryBegin();
        if (ticket is null) return;
        using (ticket)
        {
            var document = ticket.Document;
            var selection = Selection;
            int caret = Caret.Offset;
            var position = Caret.Position;
            var start = selection.StartPosition;
            var end = selection.EndPosition;
            bool SameInput() => ReferenceEquals(Selection, selection) && Caret.Position.Equals(position) &&
                Selection.StartPosition.Equals(start) && Selection.EndPosition.Equals(end) && coordinator.IsCurrent(ticket);
            bool wholeLine = selection.IsEmpty && Options.CutCopyWholeLine;
            var line = document.GetLineByOffset(caret);
            int lineOffset = line.Offset, lineLength = line.TotalLength;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            try
            {
                string? text;
                if (command == ApplicationCommands.Paste) text = await clipboard.TryGetTextAsync();
                else
                {
                    text = wholeLine ? document.GetText(lineOffset, lineLength) : selection.GetText();
                    if (text.Length == 0) return;
                    await clipboard.SetTextAsync(TextUtilities.NormalizeNewLines(text, Environment.NewLine));
                }
                // No successor lookup after await. Selection/caret and bridge revision must still match.
                if (!SameInput())
                { ticket.Error = EditorOperationError.Stale; return; }
                if (string.IsNullOrEmpty(text) || Host!.IsReadOnly) return;
                var provider = ReadOnlySectionProvider;
                bool paste = command == ApplicationCommands.Paste;
                ISegment? lineRange = !paste && wholeLine ? new SimpleSegment(lineOffset, lineLength) : null;
                var normalized = paste ? TextUtilities.NormalizeNewLines(text,
                    TextUtilities.GetNewLineFromDocument(document, position.Line)) : string.Empty;
                if (Options.ConvertTabsToSpaces) normalized = normalized.Replace("\t", new string(' ', Options.IndentationSize));
                Action<TextDocument>? apply = null;
                coordinator.Observe(() => apply = PlanClipboard(provider, selection, position, paste, lineRange, normalized));
                if (!SameInput() || !ReferenceEquals(ReadOnlySectionProvider, provider) || Host.IsReadOnly)
                { ticket.Error = EditorOperationError.Stale; return; }
                if (apply != null) coordinator.Commit(ticket, Host!, apply);
            }
            catch (Exception) { ticket.Error ??= EditorOperationError.CallbackFailed; }
        }
    }
}
