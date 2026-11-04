using System;
using CryptoWorkBench.Automation.Abstractions;

namespace CryptoWorkBenchAvalonia.Services.Automation;

// A terminal slot contains only bounded identity fields. There is no reference back to a
// service, control, job, caller or timer. Slots are never removed in the fallback.
internal sealed class ProcessAcquisitions
{
    internal static readonly ProcessAcquisitions Shared = new();
    private readonly Slot?[] slots = new Slot[4096];
    private int used;

    internal sealed class Slot(Guid instance, Guid epoch, string principal, AcquireEditorV2 request, int ordinal)
    {
        internal readonly Guid Instance = instance, Epoch = epoch, Acquisition = request.AcquisitionId, Editor = request.EditorId;
        internal readonly string Principal = principal, Revision = request.ExpectedRevision;
        // The process slot ordinal never repeats. The instance and authenticated owner are also required.
        internal readonly Guid AssignedId = new(ordinal, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        internal Guid LeaseId;
        internal AutomationErrorCode? Error = AutomationErrorCode.Busy;
        internal ReleaseReason? Reason;
    }

    internal (Slot? Slot, bool Replay, AutomationErrorCode? Error) Reserve(Guid instance, AutomationCaller caller, AcquireEditorV2 request)
    {
        lock (slots)
        {
            int principalCount = 0;
            for (int i = 0; i < used; i++)
            {
                var entry = slots[i]!;
                if (entry.Principal != caller.PrincipalId) continue;
                principalCount++;
                if (entry.Instance == instance && entry.Epoch == caller.ActivationEpoch && entry.Acquisition == request.AcquisitionId)
                    return (entry, true, entry.Editor == request.EditorId && entry.Revision == request.ExpectedRevision
                        ? null : AutomationErrorCode.IdempotencyConflict);
            }
            if (used == slots.Length || principalCount >= 256) return (null, false, AutomationErrorCode.CapacityExceeded);
            var slot = new Slot(instance, caller.ActivationEpoch, caller.PrincipalId, request, used + 1);
            slots[used++] = slot;
            return (slot, false, null);
        }
    }

    internal Slot? Find(Guid instance, AutomationCaller caller, ReleaseEditorV2 request)
    {
        lock (slots)
        {
            for (int i = 0; i < used; i++)
            {
                var entry = slots[i]!;
                if (entry.Instance == instance && entry.Epoch == caller.ActivationEpoch && entry.Principal == caller.PrincipalId &&
                    entry.Editor == request.EditorId && entry.LeaseId == request.LeaseId) return entry;
            }
            return null;
        }
    }
}
