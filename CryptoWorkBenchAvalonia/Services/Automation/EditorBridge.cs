using CryptoScript.Model;
using System;
using System.Globalization;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoWorkBench.Automation.Abstractions;
using CryptoWorkBenchAvalonia.Controls;
using CryptoWorkBenchAvalonia.Models;

namespace CryptoWorkBenchAvalonia.Services.Automation;

internal interface IEditorExecutionPresentation
{
    void BeginExecution();
    void Begin(string line);
    bool Present(ExecutionPresentation result, int number);
    string Output { get; }
}

/// <summary>UI-thread-owned registration. A lease never resolves to a successor editor.</summary>
internal sealed class EditorBridge
{
    internal sealed class Registration(TextEditor editor, IEditorExecutionPresentation? presentation)
    {
        internal readonly TextEditor Editor = editor;
        internal readonly IEditorExecutionPresentation? Presentation = presentation;
        internal Guid Id = Guid.NewGuid();
        internal TextDocument? RawDocument = editor.Document;
        internal TextDocument Document => RawDocument ?? throw new InvalidOperationException("Editor has no document.");
        internal bool HasDocument => RawDocument is not null;
        private ITextSourceVersion? observedVersion = editor.Document?.Version;
        internal long Revision;
        internal bool Changed;
        internal bool Attached = true;
        internal void ObserveText()
        {
            if (ReferenceEquals(observedVersion, Document.Version)) return;
            observedVersion = Document.Version;
            checked { Revision++; }
            Changed = true;
        }
        internal void ResetVersion() => observedVersion = RawDocument?.Version;
        internal EditorSnapshot Snapshot()
        {
            ObserveText();
            return new(Id, Revision.ToString(CultureInfo.InvariantCulture),
                Document.Text, Document.TextLength, Editor.IsModified,
                Changed || Editor.IsModified ? DirtyState.Dirty : DirtyState.Unknown);
        }
    }

    internal EditorCoordinator? Coordinator { get; set; }
    private Registration? current;
    // Published availability only: never dereference a control off the dispatcher.
    internal bool Available => System.Threading.Volatile.Read(ref current) is { Attached: true, HasDocument: true };
    internal bool Registered => System.Threading.Volatile.Read(ref current) is not null;
    internal Registration? Current
    {
        get
        {
            Dispatcher.UIThread.VerifyAccess();
            // Earlier subscribers may reenter a read from DocumentChanged/Changed
            // before our event handler runs. Observe the actual document/version.
            ObserveDocument();
            if (current is not { HasDocument: true }) return null;
            current.ObserveText();
            return current;
        }
    }

    internal Registration Attach(TextEditor editor, IEditorExecutionPresentation? presentation)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (current is { } existing)
        {
            if (ReferenceEquals(existing.Editor, editor)) return existing;
            if (existing.Editor is WorkbenchTextEditor { IdentityLocked: true }) throw new ExecutionBusyException();
            Coordinator?.BeforeAttach(editor);
            Detach(existing);
        }
        Coordinator?.BeforeAttach(editor);
        var registration = new Registration(editor, presentation);
        current = registration;
        editor.DocumentChanged += DocumentChanged;
        if (registration.RawDocument is { } document) document.Changed += TextChanged;
        Coordinator?.Attach(registration);
        return registration;
    }

    internal void Detach(Registration registration)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!registration.Attached) return;
        if (registration.Editor is WorkbenchTextEditor { IdentityLocked: true } guarded)
        {
            guarded.AfterIdentity(() => Detach(registration));
            return;
        }
        Coordinator?.Detached(registration);
        registration.Attached = false;
        registration.Editor.DocumentChanged -= DocumentChanged;
        if (registration.RawDocument is { } document) document.Changed -= TextChanged;
        if (ReferenceEquals(current, registration)) current = null;
        // An outstanding save's independent mutation lease remains alive until
        // its finally. Detach never unlocks a document during DeleteAsync.
    }

    private void TextChanged(object? sender, DocumentChangeEventArgs args)
    {
        // An earlier subscriber may already have swapped/detached the document
        // while the old event's invocation list is still being dispatched.
        if (current is { HasDocument: true } registration && ReferenceEquals(sender, registration.RawDocument))
            registration.ObserveText();
    }

    private void DocumentChanged(object? sender, DocumentChangedEventArgs args)
        => ObserveDocument();

    private void ObserveDocument()
    {
        var registration = current;
        if (registration is null || ReferenceEquals(registration.RawDocument, registration.Editor.Document)) return;
        if (registration.RawDocument is { } previous) previous.Changed -= TextChanged;
        registration.RawDocument = registration.Editor.Document;
        registration.Id = Guid.NewGuid();
        checked { registration.Revision++; }
        registration.Changed = false;
        registration.ResetVersion();
        if (registration.RawDocument is { } document) document.Changed += TextChanged;
    }

    internal void DetachCurrent()
    {
        Dispatcher.UIThread.VerifyAccess();
        if (current is { } registration) Detach(registration);
    }

    internal bool Matches(Registration registration, EditorSnapshot snapshot) =>
        ReferenceEquals(Current, registration) && registration.Attached && registration.Id == snapshot.EditorId &&
        registration.Revision.ToString(CultureInfo.InvariantCulture) == snapshot.Revision;

    internal bool IsLive(Registration registration, Guid id) =>
        ReferenceEquals(Current, registration) && registration.Attached && registration.Id == id;

    internal static IDisposable Lock(Registration registration) => registration.Editor is WorkbenchTextEditor guarded
        ? guarded.LockMutations() : throw new InvalidOperationException("The registered editor does not support save locking.");
}
