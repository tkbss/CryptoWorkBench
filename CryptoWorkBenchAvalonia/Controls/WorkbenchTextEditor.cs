using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using AvaloniaEdit.Search;
using AvaloniaEdit.Utils;
using CryptoWorkBenchAvalonia.Services;

namespace CryptoWorkBenchAvalonia.Controls;

internal interface IRevisionedSectionProvider : AvaloniaEdit.Editing.IReadOnlySectionProvider
{
    // Monotonic for the provider lifetime, including changes made by its own callbacks.
    long PermissionRevision { get; }
}

/// <summary>The real AvaloniaEdit control, with a pre-change document-swap guard.</summary>
public sealed class WorkbenchTextEditor : TextEditor
{
    protected override Type StyleKeyOverride => typeof(TextEditor);
    internal CryptoWorkBenchAvalonia.Services.Automation.EditorCoordinator? Coordinator { get; set; }

    private readonly AvaloniaEdit.Editing.IReadOnlySectionProvider unrestricted;

    internal long? PermissionRevision(AvaloniaEdit.Editing.IReadOnlySectionProvider provider) =>
        ReferenceEquals(provider, unrestricted) ? 0L : (provider as IRevisionedSectionProvider)?.PermissionRevision;

    public WorkbenchTextEditor() : base(new WorkbenchTextArea())
    {
        unrestricted = TextArea.ReadOnlySectionProvider;
        ((WorkbenchTextArea)TextArea).Host = this;
        Avalonia.Input.InputMethod.SetIsInputMethodEnabled(this, false);
        Avalonia.Input.InputMethod.SetIsInputMethodEnabled(TextArea, false);
        Options.EnableTextDragDrop = false;
        DocumentChanged += (_, _) =>
        {
            if (SearchPanel is { IsOpened: true } panel) panel.Close();
        };
        TextArea.TextEntering += (_, args) => { if (MutationLocked) args.Handled = true; };
        // Native Undo/Redo registrations and gestures are retained unchanged.
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        SearchPanel?.Uninstall();
        base.OnApplyTemplate(e);
        var panel = SearchPanel!;
        // If native DoCanExecute declines an obsolete panel's binding, its
        // Execute event would otherwise bubble to the new TextArea fallback.
        panel.AddHandler(RoutedCommand.ExecutedEvent, (_, args) =>
        {
            if (args.Command is RoutedCommand command && IsSearchMutation(command)) args.Handled = true;
        });
        for (int i = 0; i < panel.CommandBindings.Count; i++)
        {
            var command = panel.CommandBindings[i].Command;
            if (IsSearchMutation(command))
                panel.CommandBindings[i] = SearchBinding(panel, command);
        }
    }

    internal static bool IsSearchMutation(RoutedCommand command) =>
        command == SearchCommands.ReplaceNext || command == SearchCommands.ReplaceAll;

    internal RoutedCommandBinding SearchBinding(SearchPanel panel, RoutedCommand command)
    {
        bool Available() => ReferenceEquals(SearchPanel, panel) && panel.IsOpened && panel.IsReplaceMode &&
            !IsReadOnly && Coordinator is { CanStartGui: true } owner && owner.Targets(this);

        return new RoutedCommandBinding(command, (_, args) =>
        {
            args.Handled = true;
            if (!Available() || string.IsNullOrEmpty(panel.SearchPattern)) return;
            var owner = Coordinator!;
            using var ticket = owner.TryBegin();
            if (ticket == null) return;
            var document = ticket.Document;
            var pattern = panel.SearchPattern;
            var replacement = panel.ReplacePattern ?? string.Empty;
            var options = (panel.MatchCase, panel.WholeWords, panel.UseRegex);
            var provider = TextArea.ReadOnlySectionProvider;
            bool Current() => ReferenceEquals(SearchPanel, panel) && panel.IsOpened && panel.IsReplaceMode &&
                !IsReadOnly && owner.Targets(this) && owner.Generation == ticket.Generation &&
                ReferenceEquals(provider, TextArea.ReadOnlySectionProvider) && panel.SearchPattern == pattern &&
                (panel.ReplacePattern ?? string.Empty) == replacement && options == (panel.MatchCase, panel.WholeWords, panel.UseRegex);

            ISearchStrategy strategy;
            try { strategy = SearchStrategyFactory.Create(pattern, !panel.MatchCase, panel.WholeWords,
                panel.UseRegex ? SearchMode.RegEx : SearchMode.Normal); }
            catch (SearchPatternException) { return; }
            var matches = strategy.FindAll(document, 0, document.TextLength).ToArray();
            if (matches.Length == 0) return;
            if (command == SearchCommands.ReplaceNext)
            {
                int start = Math.Max(0, CaretOffset - TextArea.Selection.Length);
                var match = matches.FirstOrDefault(r => r.Offset >= start) ?? matches[0];
                if (document.GetText(match) == replacement)
                    owner.Observe(() => panel.FindNext(match.Offset + Math.Max(1, match.Length)));
                else
                    owner.Commit(ticket, this, _ => panel.ReplaceNext());
                return;
            }

            // Freeze offsets before invoking a protection provider. The native panel
            // rereads its mutable renderer inside ReplaceAll, after authorization.
            var ranges = matches.Reverse().Select(m => new SimpleSegment(m.Offset, m.Length)).ToArray();
            if (ranges.All(r => document.GetText(r) == replacement)) return;
            long? Revision()
            {
                long? revision = null;
                owner.Observe(() => revision = PermissionRevision(provider));
                return revision;
            }
            var policyRevision = Revision();
            if (policyRevision == null || !Current()) return;
            bool Writable(SimpleSegment range)
            {
                bool allowed = false;
                owner.Observe(() => allowed = CanReplaceSegment(range, replacement));
                return allowed && Current();
            }
            if (!ranges.All(Writable)) return;
            owner.Commit(ticket, this, original =>
            {
                // This is the acceptance boundary. Even an empty native update can
                // clear redo. Finish provider callbacks before opening the group.
                if (!Current() || !ranges.All(Writable) || Revision() != policyRevision ||
                    !Current() || !owner.IsActiveGuiOperation(ticket)) return;
                using (original.RunUpdate())
                {
                    foreach (var range in ranges)
                        original.Replace(range.Offset, range.Length, replacement);
                }
            });
        }, (_, args) => { args.CanExecute = Available(); args.Handled = true; });
    }

    internal bool CanReplaceSegment(ISegment segment, string replacement)
    {
        var provider = TextArea.ReadOnlySectionProvider;
        if (replacement.Length != 0 && !provider.CanInsert(segment.Offset)) return false;
        if (segment.Length == 0) return true;
        int next = segment.Offset;
        foreach (var part in provider.GetDeletableSegments(segment))
        {
            if (part.Offset != next || part.EndOffset > segment.EndOffset) return false;
            next = part.EndOffset;
        }
        return next == segment.EndOffset;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (Coordinator is { } owner) owner.Observe(() => base.OnTextChanged(e), propagate: true);
        else base.OnTextChanged(e);
    }

    static WorkbenchTextEditor()
    {
        ((StyledProperty<bool>)Avalonia.Input.InputMethod.IsInputMethodEnabledProperty).OverrideMetadata<WorkbenchTextEditor>(
            new StyledPropertyMetadata<bool>(false, coerce: (_, _) => false));
        DocumentProperty.OverrideMetadata<WorkbenchTextEditor>(new StyledPropertyMetadata<TextDocument>(
            coerce: (owner, value) =>
            {
                var editor = (WorkbenchTextEditor)owner;
                if ((editor.MutationLocked || editor.IdentityLocked) && !ReferenceEquals(editor.Document, value))
                    throw new ExecutionBusyException();
                if (!ReferenceEquals(editor.Document, value))
                {
                    var original = editor.Document;
                    var coordinator = editor.Coordinator;
                    bool Invalid() => original?.IsInUpdate == true || value?.IsInUpdate == true || editor.IdentityLocked || editor.MutationLocked ||
                        !ReferenceEquals(editor.Document, original) || !ReferenceEquals(editor.Coordinator, coordinator) ||
                        value != null && coordinator?.IsOtherDocument(editor, value) == true;
                    if (Invalid()) throw new ExecutionBusyException();
                    coordinator?.BeforeDocumentChange();
                    if (Invalid()) throw new ExecutionBusyException();
                }
                // Install before TextArea and host listeners attach to the new
                // document. TextEditor.Text/Clear can clear history even when
                // Changing is never raised for an empty replacement.
                editor.ObserveDocument(value);
                return value;
            }));
    }

    internal bool MutationLocked { get; private set; }
    internal bool IdentityLocked { get; private set; }
    private TextDocument? guardedDocument;
    private bool editChangeSeen;
    private bool editWriteAllowed;
    private bool historyStepsActive;
    private List<Action>? afterIdentity;
    private bool previousReadOnly;
    private bool rejectingUpdate;

    private void ObserveDocument(TextDocument? document)
    {
        if (ReferenceEquals(guardedDocument, document)) return;
        if (guardedDocument is not null)
        {
            guardedDocument.UpdateStarted -= BeforeUpdate;
            guardedDocument.UpdateFinished -= OnUpdateFinished;
            guardedDocument.Changing -= BeforeChange;
        }
        guardedDocument = document;
        if (document is not null)
        {
            document.UpdateStarted += BeforeUpdate;
            document.UpdateFinished += OnUpdateFinished;
            document.Changing += BeforeChange;
        }
    }

    private void BeforeUpdate(object? sender, EventArgs args)
    {
        if (!MutationLocked && !(IdentityLocked && editChangeSeen) && Coordinator?.Rejects(this) != true) return;
        // BeginUpdate has already opened its own group, and raises this event
        // before its caller enters a try/finally. Balance that rejected update
        // before throwing. No replacement stack or repair group is introduced.
        // Our handler precedes TextArea's optional caret/history operations.
        rejectingUpdate = true;
        try { ((TextDocument)sender!).EndUpdate(); }
        finally { rejectingUpdate = false; }
        throw new ExecutionBusyException();
    }

    private void OnUpdateFinished(object? sender, EventArgs args)
    {
        // AvaloniaEdit has closed the group before publishing this event. Stop
        // dispatch for the rejected BeginUpdate, whose later subscribers never started.
        if (rejectingUpdate) throw new ExecutionBusyException();
    }

    private void BeforeChange(object? sender, DocumentChangeEventArgs args)
    {
        if (MutationLocked || Coordinator?.Rejects(this) == true || (IdentityLocked && (!editWriteAllowed || editChangeSeen))) throw new ExecutionBusyException();
        if (IdentityLocked) { editChangeSeen = true; editWriteAllowed = false; }
    }

    internal IDisposable LockIdentity()
    {
        VerifyAccess();
        if (MutationLocked || IdentityLocked || Document.IsInUpdate) throw new ExecutionBusyException();
        IdentityLocked = true;
        editChangeSeen = false;
        editWriteAllowed = false;
        return new IdentityLease(this);
    }

    internal void AllowSingleEditChange()
    {
        VerifyAccess();
        if (!IdentityLocked || editChangeSeen) throw new ExecutionBusyException();
        // Called only after the outer BeginUpdate and its observers returned.
        editWriteAllowed = true;
    }

    // Exactly the two native history steps, never a callback-supplied delegate.
    // Each permission is consumed by BeforeChange before native observers run.
    internal void ReplaceHistoryLine(TextDocument original, int offset, int length, string text,
        CryptoWorkBenchAvalonia.Services.Automation.EditorCoordinator.HistoryCapability? capability = null)
    {
        VerifyAccess();
        if (historyStepsActive || !IdentityLocked || editChangeSeen || !ReferenceEquals(Document, original) ||
            Coordinator is not { Active: true, ObserverDepth: 0 }) throw new ExecutionBusyException();
        if (capability is null) throw new ExecutionBusyException();
        capability.Consume(this, original);
        historyStepsActive = true;
        try
        {
        using (original.RunUpdate())
        {
            try
            {
                AllowSingleEditChange();
                original.Remove(offset, length);
                // Only reached after Remove and all its synchronous callbacks return.
                editChangeSeen = false;
                editWriteAllowed = true;
                original.Insert(offset, text);
            }
            finally { editWriteAllowed = false; editChangeSeen = true; }
        }
        }
        finally { historyStepsActive = false; }
    }

    internal void AfterIdentity(Action action)
    {
        VerifyAccess();
        if (!IdentityLocked) { action(); return; }
        (afterIdentity ??= new()).Add(action);
    }

    private sealed class IdentityLease(WorkbenchTextEditor editor) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            editor.IdentityLocked = false;
            editor.editChangeSeen = false;
            editor.editWriteAllowed = false;
            var actions = editor.afterIdentity;
            editor.afterIdentity = null;
            if (actions is null) return;
            List<Exception>? errors = null;
            foreach (var action in actions)
                try { action(); } catch (Exception error) { (errors ??= new()).Add(error); }
            if (errors is not null) throw new AggregateException(errors);
        }
    }

    internal IDisposable LockMutations()
    {
        VerifyAccess();
        if (MutationLocked || IdentityLocked || Document.IsInUpdate) throw new ExecutionBusyException();
        MutationLocked = true;
        previousReadOnly = IsReadOnly;
        var lease = new MutationLease(this);
        try { IsReadOnly = true; return lease; }
        catch { lease.Dispose(); throw; }
    }

    private sealed class MutationLease(WorkbenchTextEditor editor) : IDisposable
    {
        private bool disposed;
        public void Dispose()
        {
            editor.VerifyAccess();
            if (disposed) return;
            disposed = true;
            editor.MutationLocked = false;
            editor.IsReadOnly = editor.previousReadOnly;
        }
    }
}
