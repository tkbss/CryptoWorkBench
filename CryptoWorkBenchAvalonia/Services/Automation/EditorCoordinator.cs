using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using CryptoScript.Model;
using CryptoWorkBenchAvalonia.Controls;

namespace CryptoWorkBenchAvalonia.Services.Automation;

internal enum EditorControlPhase { Unavailable, Gui, Mcp, Releasing, Faulted }
internal enum EditorTextEffect { None, Applied, Partial, Indeterminate }
internal enum EditorHistoryEffect { None, Applied, Indeterminate }
internal enum EditorFinalization { Complete, Faulted }
internal enum EditorOperationError { Busy, Stale, CallbackFailed, MutationFailed, FinalizationFailed }

// Internal D01 evidence only. No script text, exceptions or transport projection.
internal sealed record EditorOperationReceipt(Guid EditorId, string OriginalRevision, string? ObservedRevision,
    EditorTextEffect TextEffect, EditorHistoryEffect HistoryEffect, EditorFinalization Finalization,
    EditorOperationError? Error)
{
    internal bool RequiresStateRefresh => Error is not null || Finalization == EditorFinalization.Faulted;
    internal string RetryPolicy => "noAutomaticRetry";
}

/// <summary>
/// One UI-affine slot owned by the service. Identity/revision belong exclusively to EditorBridge.
/// MCP tickets and GUI tickets share the same registration and drain.
/// This drain covers registered editor obligations, not the service/provider shutdown drain.
/// </summary>
internal sealed class EditorCoordinator
{
    private readonly EditorBridge bridge;
    private readonly Func<bool>? stopping;
    internal EditorCoordinator(EditorBridge bridge, Func<bool>? stopping = null)
    {
        if (bridge.Coordinator is not null) throw new InvalidOperationException("Editor slot already coordinated.");
        this.bridge = bridge;
        this.stopping = stopping;
        bridge.Coordinator = this;
    }
    private const int MaxOutstanding = 64;
    private readonly HashSet<OperationTicket> tickets = new();
    private EditorBridge.Registration? registration;
    private OperationTicket? active;
    private DrainTicket? drain;
    private TextDocument? drainingDocument;
    private int callbackCount;
    internal EditorControlPhase Phase { get; private set; } = EditorControlPhase.Unavailable;
    internal long Generation { get; private set; }
    internal int ObserverDepth { get; private set; }
    internal int Pending => tickets.Count + callbackCount;
    internal bool Active => active is not null;
    internal EditorOperationReceipt? LastReceipt { get; private set; }
    internal long ObserverFailures { get; private set; }

    private static void Verify() => Dispatcher.UIThread.VerifyAccess();
    internal long WorkspaceGeneration { get; private set; }
    private long? firstAttachment;
    // The shell can start its own KeyMap before its first editor is attached.
    internal bool AcceptsWorkspacePresentation(long generation) =>
        (Phase == EditorControlPhase.Gui || (Phase == EditorControlPhase.Unavailable && Pending == 0)) &&
        (generation == WorkspaceGeneration || (generation == 0 && firstAttachment == WorkspaceGeneration));

    internal event Action? WorkspaceChanged;
    internal void NotifyWorkspaceChanged()
    {
        if (WorkspaceChanged is not { } changed) return;
        var generation = WorkspaceGeneration;
        Observe(() =>
        {
            foreach (var subscriber in Delegate.EnumerateInvocationList(changed))
            {
                // A reentrant transition publishes its own generation to the remaining views.
                if (WorkspaceGeneration != generation) break;
                Observe(subscriber);
            }
        });
    }

    private void Advance(bool workspace = true)
    {
        if (Generation == long.MaxValue) { Phase = EditorControlPhase.Faulted; throw new ExecutionBusyException(); }
        Generation++;
        if (workspace) WorkspaceGeneration = Generation;
    }

    internal void Attach(EditorBridge.Registration value, Action? publish = null)
    {
        Verify();
        if (ReferenceEquals(registration, value) && Phase == EditorControlPhase.Gui) return;
        if (Active || Pending != 0 || ObserverDepth != 0 || Phase is EditorControlPhase.Faulted or EditorControlPhase.Releasing or EditorControlPhase.Mcp)
            throw new ExecutionBusyException();
        SelectRegistration(value);
        (publish ?? NotifyWorkspaceChanged)();
    }

    internal void BeforeAttach(TextEditor editor, bool retainCurrent = false)
    {
        Verify();
        if (stopping?.Invoke() == true || editor.Document?.IsInUpdate == true ||
            TransitionBlocked(allowPending: false) || OutgoingViewBusy(retainCurrent))
            throw new ExecutionBusyException();
        if (editor is WorkbenchTextEditor { Coordinator: { } other } && !ReferenceEquals(other, this) &&
            (other.bridge.Contains(editor) || other.Phase != EditorControlPhase.Unavailable || other.TransitionBlocked(allowPending: false)))
            throw new ExecutionBusyException();
        if (Generation == long.MaxValue) { Phase = EditorControlPhase.Faulted; throw new ExecutionBusyException(); }
    }

    internal bool CanTakeControl => stopping?.Invoke() != true && !TransitionBlocked(allowPending: false) &&
        !OutgoingViewBusy(retainCurrent: true);

    internal void TakeControl(EditorBridge.Registration target)
    {
        Verify();
        if (!CanTakeControl || target.RawDocument is not { IsInUpdate: false } ||
            target.Editor is not WorkbenchTextEditor { MutationLocked: false, IdentityLocked: false })
            throw new ExecutionBusyException();
        Advance();
        registration = target;
        Phase = EditorControlPhase.Mcp;
    }

    internal void SelectView(EditorBridge.Registration value)
    {
        Verify();
        if (!bridge.Owns(value) || value.Editor.Document?.IsInUpdate == true || TransitionBlocked(allowPending: true) ||
            OutgoingViewBusy(retainCurrent: true))
            throw new ExecutionBusyException();
        SelectRegistration(value);
    }

    private bool TransitionBlocked(bool allowPending)
    {
        if (Phase == EditorControlPhase.Unavailable) CheckDrain();
        return Active || ObserverDepth != 0 || Phase is EditorControlPhase.Faulted or EditorControlPhase.Releasing or EditorControlPhase.Mcp ||
            (Pending != 0 && (!allowPending || Phase == EditorControlPhase.Unavailable)) ||
            (Phase == EditorControlPhase.Unavailable && drain is { Completion.IsCompletedSuccessfully: false });
    }

    private bool OutgoingViewBusy(bool retainCurrent) => registration is { Attached: true } current &&
        (current.RawDocument?.IsInUpdate == true || current.Editor is WorkbenchTextEditor { IdentityLocked: true } ||
         (retainCurrent && current.Editor is WorkbenchTextEditor { MutationLocked: true }));

    private void SelectRegistration(EditorBridge.Registration value)
    {
        bool initial = WorkspaceGeneration == 0;
        Advance();
        if (initial) firstAttachment = WorkspaceGeneration;
        registration = value;
        Phase = EditorControlPhase.Gui;
        drain = null;
        if (value.Editor is WorkbenchTextEditor editor) editor.Coordinator = this;
    }

    internal bool Targets(WorkbenchTextEditor editor) => ReferenceEquals(registration?.Editor, editor) &&
        registration.Attached && ReferenceEquals(registration.RawDocument, editor.Document);
    internal bool Rejects(WorkbenchTextEditor editor) => RejectDocumentMutation ||
        (bridge.Contains(editor) && !Targets(editor));
    internal bool IsOtherDocument(WorkbenchTextEditor editor, TextDocument value) => bridge.UsedByAnotherEditor(editor, value);

    internal void BeforeDocumentChange()
    {
        Verify();
        bool Blocked() => stopping?.Invoke() == true || Active || ObserverDepth != 0 || Phase != EditorControlPhase.Gui;
        if (Blocked()) throw new ExecutionBusyException();
        // Publish invalidation before property/document callbacks can run.
        Advance();
        NotifyWorkspaceChanged();
        if (Blocked()) throw new ExecutionBusyException();
    }

    internal void InvalidateInputIntents() { Verify(); Advance(workspace: false); }

    internal void Detached(EditorBridge.Registration value, Action publish)
    {
        Verify();
        if (!ReferenceEquals(value, registration)) return;
        bool transitioning = Phase == EditorControlPhase.Releasing;
        BeginDrain(publish);
        if (!transitioning && Phase != EditorControlPhase.Faulted) Phase = EditorControlPhase.Unavailable;
        CheckDrain();
    }

    private bool CanBegin => CanBeginIn(EditorControlPhase.Gui);
    private bool CanBeginIn(EditorControlPhase phase) => stopping?.Invoke() != true && Phase == phase && !Active && ObserverDepth == 0 &&
        registration is { Attached: true, HasDocument: true } r && ReferenceEquals(bridge.Current, r) &&
        !r.Document.IsInUpdate && r.Editor is WorkbenchTextEditor { MutationLocked: false, IdentityLocked: false };

    internal bool CanStartGui { get { Verify(); return CanBegin; } }

    internal OperationTicket? TryBegin() => BeginIn(EditorControlPhase.Gui);
    internal OperationTicket? TryBeginMcp() => BeginIn(EditorControlPhase.Mcp);
    private OperationTicket? BeginIn(EditorControlPhase phase)
    {
        Verify();
        if (!CanBeginIn(phase) || Pending >= MaxOutstanding) return null;
        var r = registration!;
        var snapshot = r.Snapshot();
        var ticket = new OperationTicket(this, r, snapshot.EditorId, snapshot.Revision, Generation);
        tickets.Add(ticket);
        return ticket;
    }

    internal bool IsCurrent(OperationTicket ticket)
    {
        Verify();
        return stopping?.Invoke() != true && ticket.Owner == this && tickets.Contains(ticket) && !ticket.BodyDone &&
            (ticket.Phase == EditorControlPhase.Mcp ? ticket.Workspace == WorkspaceGeneration : ticket.Generation == Generation) && Phase == ticket.Phase &&
            bridge.IsLive(ticket.Registration, ticket.EditorId) &&
            ReferenceEquals(ticket.Document, ticket.Registration.RawDocument) &&
            ticket.OriginalRevision == ticket.Registration.Revision.ToString(CultureInfo.InvariantCulture);
    }

    internal bool RunGui(Action action, string? expectedText = null)
    {
        Verify();
        var ticket = TryBegin();
        if (ticket is null) return false;
        return Commit(ticket, action, expectedText);
    }

    // Validation only: never creates or upgrades an operation's authority.
    internal bool IsActiveGuiOperation(OperationTicket ticket)
    {
        Verify();
        return ReferenceEquals(active, ticket) && ObserverDepth == 0 && IsCurrent(ticket);
    }

    // A separate admission entry, never an upgrade of an ambient Active ticket.
    internal bool RunHistory(Action<HistoryCapability> action) => HistoryCapability.Run(this, action);

    internal sealed class HistoryCapability
    {
        private readonly OperationTicket operation;
        private bool valid = true;
        private bool consumed;
        private HistoryCapability(OperationTicket operation) => this.operation = operation;
        internal TextDocument Document => operation.Document;

        internal static bool Run(EditorCoordinator owner, Action<HistoryCapability> action)
        {
            Verify();
            using var ticket = owner.TryBegin();
            if (ticket is null) return false;
            IDisposable reservation;
            try { reservation = InterpreterHostQueue.Reserve(); }
            catch (ExecutionBusyException) { return false; }
            using (reservation)
                return owner.Commit(ticket, () =>
                {
                    var capability = new HistoryCapability(ticket);
                    try { action(capability); }
                    finally { capability.valid = false; }
                });
        }

        internal void Consume(WorkbenchTextEditor editor, TextDocument document)
        {
            Verify();
            var owner = operation.Owner;
            if (!valid || consumed || !ReferenceEquals(owner.active, operation) ||
                owner.ObserverDepth != 0 || !owner.IsCurrent(operation) ||
                !ReferenceEquals(editor.Coordinator, owner) ||
                !ReferenceEquals(operation.Registration.Editor, editor) ||
                !ReferenceEquals(Document, document) || !ReferenceEquals(editor.Document, document))
                throw new ExecutionBusyException();
            // Burn before RunUpdate can publish even its first native callback.
            consumed = true;
        }
    }

    internal bool Commit(OperationTicket ticket, WorkbenchTextEditor target, Action<TextDocument> action)
    {
        Verify();
        if (!ReferenceEquals(ticket.Registration.Editor, target) || !ReferenceEquals(ticket.Document, target.Document))
            return false;
        return Commit(ticket, () => action(ticket.Document));
    }

    internal bool Commit(OperationTicket ticket, Action action, string? expectedText = null) =>
        TryStartCommit(ticket) && FinishCommit(ticket, action, expectedText);

    internal bool TryStartCommit(OperationTicket ticket)
    {
        Verify();
        if (ticket.Owner != this || !tickets.Contains(ticket) || ticket.BodyDone) return false;
        if (Active || ObserverDepth != 0) return false;
        // Admission is only an intent: recheck live locks/update state immediately
        // before any native action. The action may then open its own atomic group.
        if (!IsCurrent(ticket) || !CanBeginIn(ticket.Phase))
        {
            ticket.Error = EditorOperationError.Stale;
            ticket.Dispose();
            return false;
        }
        active = ticket;
        ticket.MutationStarted = true;
        return true;
    }

    internal bool FinishCommit(OperationTicket ticket, Action action, string? expectedText)
    {
        Verify();
        if (!ReferenceEquals(active, ticket)) throw new InvalidOperationException("No matching editor commit.");
        bool returned = false;
        var history = ticket.Document.UndoStack;
        bool emptyBefore = !history.CanUndo && !history.CanRedo;
        bool historyObserved = false;
        PropertyChangedEventHandler historyChanged = (_, e) =>
        {
            if (!ticket.TemporaryHistoryLimit || e.PropertyName != nameof(UndoStack.SizeLimit)) historyObserved = true;
        };
        ticket.Document.UndoStack.PropertyChanged += historyChanged;
        try { action(); returned = true; }
        catch (Exception) { ticket.Error = EditorOperationError.MutationFailed; }
        finally
        {
            ticket.Document.UndoStack.PropertyChanged -= historyChanged;
            bool changed = !ReferenceEquals(ticket.Version, ticket.Document.Version);
            ticket.Text = changed
                ? returned || (expectedText is not null && ticket.Document.Text == expectedText)
                    ? EditorTextEffect.Applied : EditorTextEffect.Indeterminate
                : returned ? EditorTextEffect.None : EditorTextEffect.Indeterminate;
            // An escaped native exception can have popped/altered history even without text.
            // Optional caret actions can transiently enable Undo during an empty update.
            bool emptyAfter = !history.CanUndo && !history.CanRedo;
            bool historyEffect = !(emptyBefore && emptyAfter) && (changed || historyObserved);
            ticket.History = !returned ? EditorHistoryEffect.Indeterminate :
                historyEffect ? EditorHistoryEffect.Applied : EditorHistoryEffect.None;
            if (ticket.Phase == EditorControlPhase.Mcp &&
                ((changed && !history.CanUndo) || expectedText != ticket.Document.Text))
            {
                if (expectedText != ticket.Document.Text) ticket.Text = EditorTextEffect.Indeterminate;
                Fault(ticket);
            }
            if (!returned || ticket.Document.IsInUpdate) Fault(ticket);
            active = null;
            ticket.Dispose();
        }
        return returned;
    }

    internal void Observe(Action callback, bool propagate = false)
    {
        Verify();
        ObserverDepth++;
        InterpreterHostQueue.EnterObserver();
        try { callback(); }
        catch (Exception)
        {
            if (ObserverFailures != long.MaxValue) ObserverFailures++;
            if (active is { } ticket) ticket.Error ??= EditorOperationError.CallbackFailed;
            if (propagate) throw;
        }
        finally { InterpreterHostQueue.ExitObserver(); ObserverDepth--; CheckDrain(); }
    }

    // A detached, fully drained document is no longer the host's editor slot.
    // Keep native entry points unavailable, but do not freeze an orphan forever.
    internal bool RejectDocumentMutation => ObserverDepth != 0 || (!Active &&
        (Phase is EditorControlPhase.Mcp or EditorControlPhase.Releasing or EditorControlPhase.Faulted ||
         (Phase == EditorControlPhase.Unavailable && Pending != 0)));

    internal DrainTicket BeginDrain(Action? publish = null)
    {
        Verify();
        if (drain is not null) return drain;
        Advance();
        if (Phase != EditorControlPhase.Faulted) Phase = EditorControlPhase.Releasing;
        var certificate = drain = new DrainTicket(this);
        if (registration?.RawDocument is { IsInUpdate: true } original)
        {
            drainingDocument = original;
            original.UpdateFinished += NativeUpdateFinished;
        }
        (publish ?? NotifyWorkspaceChanged)();
        CheckDrain();
        return certificate;
    }

    internal bool ResumeGui(DrainTicket certificate, Action? publish = null)
    {
        Verify();
        // The current certificate is the transition identity. Focus invalidation
        // advances input generations, but cannot replace or authorize a transition.
        if (!ReferenceEquals(certificate, drain) || certificate.Owner != this ||
            !certificate.Completion.IsCompletedSuccessfully || Phase != EditorControlPhase.Releasing ||
            Pending != 0 || Active || ObserverDepth != 0) return false;
        if (registration is not { Attached: true } && bridge.Current == null)
        {
            Advance();
            drain = null;
            Phase = EditorControlPhase.Unavailable;
            RetireDetachedRegistration();
            return true;
        }
        if (registration is not { Attached: true, HasDocument: true } || !ReferenceEquals(bridge.Current, registration) ||
            registration.Document.IsInUpdate ||
            registration.Editor is not WorkbenchTextEditor { MutationLocked: false, IdentityLocked: false }) return false;
        Advance();
        drain = null;
        Phase = EditorControlPhase.Gui;
        (publish ?? NotifyWorkspaceChanged)();
        return true;
    }

    private void NativeUpdateFinished(object? sender, EventArgs args)
    {
        if (ReferenceEquals(sender, drainingDocument)) CheckDrain();
    }

    private void StopWatchingDrain()
    {
        if (drainingDocument is not { } original) return;
        original.UpdateFinished -= NativeUpdateFinished;
        drainingDocument = null;
    }

    private void CheckDrain()
    {
        if (drain != null && Pending == 0 && !Active && ObserverDepth == 0 && Phase == EditorControlPhase.Faulted)
        {
            StopWatchingDrain(); // faultedDocument retains the original identity and open-group evidence.
            drain.Quiescent.TrySetResult();
        }
        if (drain is not null && !drain.Completion.IsCompleted && Pending == 0 && !Active && ObserverDepth == 0 &&
            Phase != EditorControlPhase.Faulted && drainingDocument?.IsInUpdate != true && registration?.RawDocument?.IsInUpdate != true)
        {
            StopWatchingDrain();
            drain.Signal.TrySetResult();
            drain.Quiescent.TrySetResult();
        }
        RetireDetachedRegistration();
    }

    internal void RetireDetachedRegistration()
    {
        if (Phase != EditorControlPhase.Unavailable || Pending != 0 || Active || ObserverDepth != 0 ||
            registration is not { Attached: false } old || drain is { Completion.IsCompletedSuccessfully: false }) return;
        if (old.Editor is WorkbenchTextEditor editor && ReferenceEquals(editor.Coordinator, this))
            editor.Coordinator = null;
        registration = null;
        drain = null;
    }

    // A faulted native group belongs to the original document even after detach.
    private TextDocument? faultedDocument;
    internal bool EpochWorkFinalized => Pending == 0 && !Active && ObserverDepth == 0 &&
        faultedDocument?.IsInUpdate != true && drainingDocument?.IsInUpdate != true &&
        registration?.RawDocument?.IsInUpdate != true &&
        registration?.Editor is not WorkbenchTextEditor { IdentityLocked: true } and not WorkbenchTextEditor { MutationLocked: true };

    private void Fault(OperationTicket ticket)
    {
        Phase = EditorControlPhase.Faulted;
        faultedDocument = ticket.Document;
        StopWatchingDrain();
        ticket.Finalization = EditorFinalization.Faulted;
        ticket.History = EditorHistoryEffect.Indeterminate;
        ticket.Error = EditorOperationError.FinalizationFailed;
        // No EndUpdate recovery, no unlock on a successor, no automatic retry.
    }

    private void Finish(OperationTicket ticket)
    {
        if (!ticket.BodyDone || !ticket.Sealed || ticket.Callbacks != 0 || !tickets.Contains(ticket)) return;
        string? observed = null;
        try
        {
            if (ReferenceEquals(ticket.Registration.RawDocument, ticket.Document) && ticket.Registration.Id == ticket.EditorId)
            {
                ticket.Registration.ObserveText();
                observed = ticket.Registration.Revision.ToString(CultureInfo.InvariantCulture);
            }
            // A rejected intent did not own a foreign update group. Its receipt
            // remains no-effect; only an executed action has finalization duties.
            if (ticket.MutationStarted && ticket.Document.IsInUpdate) Fault(ticket);
        }
        catch (Exception) { Fault(ticket); }
        var receipt = new EditorOperationReceipt(ticket.EditorId, ticket.OriginalRevision, observed,
            ticket.Text, ticket.History, ticket.Finalization, ticket.Error);
        tickets.Remove(ticket);
        LastReceipt = receipt;
        ticket.Signal.TrySetResult(receipt);
        CheckDrain();
    }

    internal sealed class DrainTicket
    {
        internal readonly EditorCoordinator Owner;
        internal readonly TaskCompletionSource Signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Quiescent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Completion => Signal.Task;
        internal DrainTicket(EditorCoordinator owner) => Owner = owner;
    }

    internal sealed class OperationTicket : IDisposable
    {
        internal readonly EditorControlPhase Phase;
        internal readonly EditorCoordinator Owner;
        internal readonly EditorBridge.Registration Registration;
        internal readonly TextDocument Document;
        internal readonly ITextSourceVersion Version;
        internal readonly Guid EditorId;
        internal readonly string OriginalRevision;
        internal readonly long Generation, Workspace;
        internal readonly TaskCompletionSource<EditorOperationReceipt> Signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<EditorOperationReceipt> Completion => Signal.Task;
        internal bool BodyDone, Sealed, MutationStarted;
        internal bool TemporaryHistoryLimit;
        internal int Callbacks;
        internal EditorTextEffect Text;
        internal EditorHistoryEffect History;
        internal EditorFinalization Finalization;
        internal EditorOperationError? Error;
        internal OperationTicket(EditorCoordinator owner, EditorBridge.Registration registration, Guid id, string revision, long generation)
        {
            Owner = owner; Phase = owner.Phase; Workspace = owner.WorkspaceGeneration; Registration = registration; Document = registration.Document; Version = Document.Version;
            EditorId = id; OriginalRevision = revision; Generation = generation;
        }
        internal CallbackTicket RegisterCallback()
        {
            Verify();
            if (Sealed || BodyDone || !Owner.tickets.Contains(this) || Owner.Pending >= MaxOutstanding)
                throw new ExecutionBusyException();
            Callbacks++; Owner.callbackCount++;
            return new CallbackTicket(this);
        }
        internal void SealProducer() { Verify(); Sealed = true; Owner.Finish(this); }
        public void Dispose()
        {
            Verify();
            if (BodyDone) return;
            if (ReferenceEquals(Owner.active, this)) throw new ExecutionBusyException();
            Sealed = true; BodyDone = true; Owner.Finish(this);
        }
    }

    // A job borrows the native Return operation, or owns an intent captured at admission.
    // Only edits made by this intent may advance its expected document version.
    internal sealed class ExecutionIntent : IDisposable
    {
        private readonly OperationTicket origin;
        private readonly CallbackTicket? nativeCallback;
        private readonly long workspace;
        private ITextSourceVersion? expected;
        private bool disposed;

        internal ExecutionIntent(EditorCoordinator owner, OperationTicket? native)
        {
            workspace = owner.WorkspaceGeneration;
            if (native != null && !owner.IsActiveGuiOperation(native)) throw new ExecutionBusyException();
            origin = native ?? owner.TryBegin() ?? throw new ExecutionBusyException();
            if (native != null) nativeCallback = native.RegisterCallback();
            else expected = origin.Version;
        }

        internal Guid EditorId => origin.EditorId;
        internal string SourceRevision => origin.OriginalRevision;

        internal bool DestinationLive
        {
            get
            {
                Verify();
                if (disposed || origin.Owner.WorkspaceGeneration != workspace || !origin.Owner.CanBegin ||
                    !origin.Owner.bridge.IsLive(origin.Registration, origin.EditorId) ||
                    !ReferenceEquals(origin.Document, origin.Registration.RawDocument)) return false;
                if (nativeCallback != null && (!origin.BodyDone || origin.Error != null ||
                    origin.Text != EditorTextEffect.Applied)) return false;
                // Interactive Return addresses a surviving original anchor and allows subsequent typing.
                return true;
            }
        }

        internal bool Current => DestinationLive && (nativeCallback != null || ReferenceEquals(expected, origin.Document.Version));

        internal bool Write(Action action)
        {
            if (!Current || !origin.Owner.RunGui(action)) return false;
            expected = origin.Document.Version;
            return true;
        }

        public void Dispose()
        {
            Verify();
            disposed = true;
            if (nativeCallback != null) nativeCallback.Dispose();
            else origin.Dispose();
        }
    }

    internal sealed class CallbackTicket : IDisposable
    {
        private readonly OperationTicket operation;
        private bool done, disposeRequested;
        private int executions;
        internal CallbackTicket(OperationTicket ticket) => operation = ticket;
        internal void Observe(Action callback)
        {
            Verify();
            if (done || disposeRequested || operation.Generation != operation.Owner.Generation ||
                !operation.Owner.bridge.IsLive(operation.Registration, operation.EditorId)) return;
            var owner = operation.Owner;
            executions++;
            long before = owner.ObserverFailures;
            try
            {
                owner.Observe(() =>
                {
                    try { callback(); }
                    catch (Exception)
                    {
                        // Record before any terminal publication, even if the
                        // diagnostic observer-failure counter has saturated.
                        operation.Error ??= EditorOperationError.CallbackFailed;
                        throw;
                    }
                });
                if (owner.ObserverFailures != before) operation.Error ??= EditorOperationError.CallbackFailed;
            }
            finally
            {
                executions--;
                if (disposeRequested && executions == 0) Complete();
            }
        }
        public void Dispose()
        {
            Verify();
            if (done) return;
            disposeRequested = true;
            if (executions == 0) Complete();
        }
        private void Complete()
        {
            if (done) return;
            done = true; operation.Callbacks--; operation.Owner.callbackCount--;
            operation.Owner.Finish(operation);
        }
    }
}
