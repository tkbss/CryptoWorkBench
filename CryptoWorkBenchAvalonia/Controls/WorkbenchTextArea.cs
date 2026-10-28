using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private bool nativeDispatch;
    private bool nativeTextDispatch;
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
        (Host?.Coordinator is not { } c || c.CanStartGui);

    private T Native<T>(Func<T> action)
    {
        nativeDispatch = true;
        try { return action(); }
        finally { nativeDispatch = false; }
    }

    IList<RoutedCommandBinding> IRoutedCommandBindable.CommandBindings
    {
        get
        {
            // Consume BEFORE native handlers/observers run. No callback inherits the bypass.
            if (nativeDispatch) { nativeDispatch = false; return base.CommandBindings; }
            var commands = DefaultInputHandler.Editing.CommandBindings.Select(b => b.Command).ToHashSet();
            commands.Add(ApplicationCommands.Undo);
            commands.Add(ApplicationCommands.Redo);
            commands.Add(ApplicationCommands.Copy);
            return base.CommandBindings.Select(binding => !commands.Contains(binding.Command) ? binding :
                new RoutedCommandBinding(binding.Command, (_, e) =>
                {
                    e.Handled = true;
                    if (!Allowed) return;
                    var coordinator = Host?.Coordinator;
                    if (coordinator is not null && (binding.Command == ApplicationCommands.Paste ||
                        binding.Command == ApplicationCommands.Cut || binding.Command == ApplicationCommands.Copy))
                    {
                        PendingClipboard = ClipboardAsync(coordinator, binding.Command);
                        return;
                    }
                    void Execute() => Native(() => { binding.Command.Execute(e.Parameter, this); return true; });
                    if (coordinator is null) Execute();
                    else coordinator.RunGui(Execute, binding.Command == ApplicationCommands.Undo || binding.Command == ApplicationCommands.Redo);
                }, (_, e) =>
                {
                    e.CanExecute = Allowed && Native(() => binding.Command.CanExecute(e.Parameter, this));
                    e.Handled = true;
                })).ToList();
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!Allowed) { e.Handled = true; return; }
        base.OnTextInput(e);
    }

    protected override void OnTextEntering(TextInputEventArgs e)
    {
        // Also covers public PerformTextInput, which bypasses OnTextInput.
        // Re-dispatch once inside the admission scope; suppress only the outer call.
        if (nativeTextDispatch) { nativeTextDispatch = false; base.OnTextEntering(e); return; }
        if (!Allowed) { e.Handled = true; return; }
        if (Host?.Coordinator is not { } coordinator) { base.OnTextEntering(e); return; }
        coordinator.RunGui(() =>
        {
            nativeTextDispatch = true;
            try { base.PerformTextInput(e); }
            finally { nativeTextDispatch = false; }
        });
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // TextArea handles Tab directly; history shortcuts still use native registrations.
        if (e.Key == Key.Tab && Options.AcceptsTab && IsFocused && Host?.Coordinator is { } coordinator)
        {
            if (Allowed) coordinator.RunGui(() => base.OnKeyDown(e));
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
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
            bool wholeLine = selection.IsEmpty && Options.CutCopyWholeLine;
            var line = document.GetLineByOffset(caret);
            int lineOffset = line.Offset, lineLength = line.TotalLength;
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            try
            {
                string? text;
                if (command == ApplicationCommands.Paste) text = await clipboard.GetTextAsync();
                else
                {
                    text = wholeLine ? document.GetText(lineOffset, lineLength) : selection.GetText();
                    if (text.Length == 0) return;
                    await clipboard.SetTextAsync(TextUtilities.NormalizeNewLines(text, Environment.NewLine));
                    if (command == ApplicationCommands.Copy) return;
                }
                // No successor lookup after await. Selection/caret and bridge revision must still match.
                if (text is null || !ReferenceEquals(Selection, selection) || Caret.Offset != caret || !coordinator.IsCurrent(ticket)) return;
                coordinator.Commit(ticket, () =>
                {
                    using (document.RunUpdate())
                    {
                        if (command == ApplicationCommands.Cut && wholeLine)
                        {
                            var segments = ReadOnlySectionProvider.GetDeletableSegments(new SimpleSegment(lineOffset, lineLength)).ToArray();
                            for (int i = segments.Length - 1; i >= 0; i--) document.Remove(segments[i]);
                        }
                        else if (command == ApplicationCommands.Cut) selection.ReplaceSelectionWithText(string.Empty);
                        else if (text.Length != 0)
                        {
                            var normalized = TextUtilities.NormalizeNewLines(text, TextUtilities.GetNewLineFromDocument(document, Caret.Line));
                            if (Options.ConvertTabsToSpaces) normalized = normalized.Replace("\t", new string(' ', Options.IndentationSize));
                            selection.ReplaceSelectionWithText(normalized);
                        }
                        Caret.BringCaretToView();
                    }
                });
            }
            catch (Exception) { ticket.Error ??= EditorOperationError.CallbackFailed; }
        }
    }
}
