using CryptoScript.Model;
using System;
using System.Collections.Generic;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoWorkBenchAvalonia.Services;

namespace CryptoWorkBenchAvalonia.Controls;

/// <summary>The real AvaloniaEdit control, with a pre-change document-swap guard.</summary>
public sealed class WorkbenchTextEditor : TextEditor
{
    protected override Type StyleKeyOverride => typeof(TextEditor);
    internal CryptoWorkBenchAvalonia.Services.Automation.EditorCoordinator? Coordinator { get; set; }

    public WorkbenchTextEditor() : base(new WorkbenchTextArea())
    {
        ((WorkbenchTextArea)TextArea).Host = this;
        Avalonia.Input.InputMethod.SetIsInputMethodEnabled(this, false);
        Avalonia.Input.InputMethod.SetIsInputMethodEnabled(TextArea, false);
        Options.EnableTextDragDrop = false;
        TextArea.TextEntering += (_, args) => { if (MutationLocked) args.Handled = true; };
        // Native Undo/Redo registrations and gestures are retained unchanged.
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
                if (!ReferenceEquals(editor.Document, value)) editor.Coordinator?.BeforeDocumentChange();
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
    private List<Action>? afterIdentity;
    private bool previousReadOnly;

    private void ObserveDocument(TextDocument? document)
    {
        if (ReferenceEquals(guardedDocument, document)) return;
        if (guardedDocument is not null)
        {
            guardedDocument.UpdateStarted -= BeforeUpdate;
            guardedDocument.Changing -= BeforeChange;
        }
        guardedDocument = document;
        if (document is not null)
        {
            document.UpdateStarted += BeforeUpdate;
            document.Changing += BeforeChange;
        }
    }

    private void BeforeUpdate(object? sender, EventArgs args)
    {
        if (!MutationLocked && !(IdentityLocked && editChangeSeen) && Coordinator?.RejectDocumentMutation != true) return;
        // BeginUpdate has already opened its own group, and raises this event
        // before its caller enters a try/finally. Balance that rejected update
        // before throwing. No replacement stack or repair group is introduced.
        // Our handler precedes TextArea's optional caret/history operations.
        try { ((TextDocument)sender!).EndUpdate(); }
        finally { throw new ExecutionBusyException(); }
    }

    private void BeforeChange(object? sender, DocumentChangeEventArgs args)
    {
        if (MutationLocked || Coordinator?.RejectDocumentMutation == true || (IdentityLocked && (!editWriteAllowed || editChangeSeen))) throw new ExecutionBusyException();
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
