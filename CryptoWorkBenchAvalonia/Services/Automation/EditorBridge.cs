using CryptoScript.Model;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<TextEditor, Registration> open = new();
    private int openCount;
    // Published availability only: never dereference a control off the dispatcher.
    internal bool Available => System.Threading.Volatile.Read(ref current) is { Attached: true, HasDocument: true };
    internal bool Registered => System.Threading.Volatile.Read(ref openCount) != 0;
    internal Registration? Current
    {
        get
        {
            Dispatcher.UIThread.VerifyAccess();
            // Earlier subscribers may reenter a read from DocumentChanged/Changed
            // before our event handler runs. Observe the actual document/version.
            ObserveDocument(current);
            if (current is not { HasDocument: true }) return null;
            current.ObserveText();
            return current;
        }
    }

    internal Registration? ReplacementFor(TextEditor editor, bool retainCurrent)
    {
        if (retainCurrent || current == null || open.ContainsKey(editor)) return null;
        if (editor.Document is { } document && UsedByAnotherEditor(editor, document)) throw new ExecutionBusyException();
        Coordinator!.BeforeAttach(editor);
        return current;
    }

    internal bool IsSelected(Registration value) => ReferenceEquals(current, value) && Owns(value);

    internal Registration Attach(TextEditor editor, IEditorExecutionPresentation? presentation, bool retainCurrent, Action publish)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (open.TryGetValue(editor, out var known))
        {
            Activate(known, publish);
            if (!Owns(known)) throw new ExecutionBusyException();
            return known;
        }
        if (!retainCurrent && current != null) throw new ExecutionBusyException();
        Coordinator?.BeforeAttach(editor, retainCurrent);
        if (open.ContainsKey(editor) || editor.Document is { } live && UsedByAnotherEditor(editor, live))
            throw new ExecutionBusyException();
        var registration = new Registration(editor, presentation);
        open.Add(editor, registration);
        System.Threading.Volatile.Write(ref openCount, open.Count);
        current = registration;
        editor.DocumentChanged += DocumentChanged;
        if (registration.RawDocument is { } document) document.Changed += TextChanged;
        Coordinator!.Attach(registration, publish);
        if (!Owns(registration)) throw new ExecutionBusyException();
        return registration;
    }

    internal bool IsLiveRegistration(Guid id) => Current?.Id == id;

    internal Registration? Find(Guid id)
    {
        Dispatcher.UIThread.VerifyAccess();
        foreach (var candidate in open.Values)
        {
            ObserveDocument(candidate);
            if (candidate.Id == id && candidate.HasDocument) return candidate;
        }
        return null;
    }

    internal void TakeControl(Registration target)
    {
        if (!Owns(target)) throw new ExecutionBusyException();
        Coordinator!.TakeControl(target);
        current = target;
    }

    internal void Activate(Registration registration, Action publish)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!open.TryGetValue(registration.Editor, out var known) || !ReferenceEquals(known, registration))
            throw new ExecutionBusyException();
        if (ReferenceEquals(current, registration)) return;
        Coordinator?.SelectView(registration);
        current = registration;
        publish();
    }

    internal bool Owns(Registration registration) =>
        open.TryGetValue(registration.Editor, out var known) && ReferenceEquals(known, registration);
    internal bool Contains(TextEditor editor) => open.ContainsKey(editor);
    internal bool UsedByAnotherEditor(TextEditor editor, TextDocument document) =>
        open.Values.Any(r => !ReferenceEquals(r.Editor, editor) && ReferenceEquals(r.Editor.Document, document));

    internal void Detach(Registration registration)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!open.TryGetValue(registration.Editor, out var known) || !ReferenceEquals(known, registration)) return;
        if (registration.Editor is WorkbenchTextEditor { IdentityLocked: true } guarded)
        {
            guarded.AfterIdentity(() => Detach(registration));
            return;
        }
        bool selected = ReferenceEquals(current, registration);
        bool changed = false;
        Coordinator?.Detached(registration, () => changed = true);
        if (!selected && registration.Editor is WorkbenchTextEditor editor && ReferenceEquals(editor.Coordinator, Coordinator))
            editor.Coordinator = null;
        registration.Attached = false;
        open.Remove(registration.Editor);
        System.Threading.Volatile.Write(ref openCount, open.Count);
        registration.Editor.DocumentChanged -= DocumentChanged;
        if (registration.RawDocument is { } document) document.Changed -= TextChanged;
        if (selected)
        {
            current = null;
            Coordinator?.RetireDetachedRegistration();
        }
        if (changed) Coordinator!.NotifyWorkspaceChanged();
        // An outstanding save's independent mutation lease remains alive until
        // its finally. Detach never unlocks a document during DeleteAsync.
    }

    private void TextChanged(object? sender, DocumentChangeEventArgs args)
    {
        // An earlier subscriber may already have swapped/detached the document
        // while the old event's invocation list is still being dispatched.
        foreach (var registration in open.Values)
            if (ReferenceEquals(sender, registration.RawDocument)) registration.ObserveText();
    }

    private void DocumentChanged(object? sender, DocumentChangedEventArgs args)
    {
        if (sender is TextEditor editor && open.TryGetValue(editor, out var registration)) ObserveDocument(registration);
    }

    private void ObserveDocument(Registration? registration)
    {
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
        foreach (var registration in open.Values.ToArray()) Detach(registration);
    }

    internal bool Matches(Registration registration, EditorSnapshot snapshot) =>
        ReferenceEquals(Current, registration) && registration.Attached && registration.Id == snapshot.EditorId &&
        registration.Revision.ToString(CultureInfo.InvariantCulture) == snapshot.Revision;

    internal bool IsLive(Registration registration, Guid id) =>
        ReferenceEquals(Current, registration) && registration.Attached && registration.Id == id;

    internal static IDisposable Lock(Registration registration) => registration.Editor is WorkbenchTextEditor guarded
        ? guarded.LockMutations() : throw new InvalidOperationException("The registered editor does not support save locking.");
}
