using System;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

internal sealed record CaptureStamp(string Identifier, WeakReference<VariableDeclaration> Origin,
    VariableModel Row, long Publication, long Workspace, long Revision, object? DocumentVersion = null);

internal sealed record SelectionHandle(CaptureStamp Stamp);

// I1 supports exact scalar text and share bytes. Additional audited property paths belong to I2.
internal enum InspectorMember { Text, Value, ShareValue, PropertyPage, PropertyValue }
internal sealed record ReadSpec(InspectorMember Member, int ShareIndex = 0, long Offset = 0, long? Count = null, PropertyRequest? Property = null);
internal readonly record struct CapturedMapEntry(string Key, long Offset, long KeyCount, long ValueCount, int? ValueLength);
// Append immutable pages incrementally. Publishing a large map never freezes or copies
// its entire index inside the last core step.
internal sealed class CapturedMapIndex
{
    private const int PageSize = 128;
    private readonly ImmutableList<ImmutableArray<CapturedMapEntry>> pages;
    private CapturedMapIndex(ImmutableList<ImmutableArray<CapturedMapEntry>> pages,int count)
    {
        this.pages=pages;
        Count=count;
    }
    internal int Count { get; }
    internal CapturedMapEntry this[int i] => pages[i/PageSize][i%PageSize];
    internal sealed class Builder
    {
        private ImmutableList<ImmutableArray<CapturedMapEntry>> pages = [];
        private ImmutableArray<CapturedMapEntry>.Builder pending = ImmutableArray.CreateBuilder<CapturedMapEntry>(PageSize);
        private int count;
        internal void Add(CapturedMapEntry entry)
        {
            pending.Add(entry);
            count++;
            if (pending.Count != PageSize) return;
            pages=pages.Add(pending.MoveToImmutable());
            pending=ImmutableArray.CreateBuilder<CapturedMapEntry>(PageSize);
        }
        internal CapturedMapIndex Finish() => new(pending.Count==0 ? pages : pages.Add(pending.ToImmutable()),count);
    }
}

internal sealed record CapturedSlice(long Offset, long Count, bool Quoted, Type? Type, ImmutableArray<InspectorStep> Path, int? DecodedLength, int Entries = 0,
    CapturedMapIndex? Map = null, long? PairColon = null, bool HexByte = false);
internal sealed record SnapshotPart(CaptureStamp Stamp, InspectorMember Member, int ShareIndex,
    long Offset, long Count, long Total, InspectorPayload Payload, bool IsNull = false, InspectorPage? Page = null, PropertyRequest? Request = null,
    ImmutableDictionary<string,CapturedSlice>? Index = null)
{
    internal DateTimeOffset CapturedAt { get; } = DateTimeOffset.Now;
}

internal sealed class InspectorSnapshot : IDisposable
{
    private bool disposed;
    internal CaptureStamp Stamp { get; }
    internal ImmutableArray<SnapshotPart> Parts { get; private set; } = [];
    private readonly Func<IDisposable>? beginCopy;
    private readonly Action<InspectorSnapshot>? finishRetirement;
    private int activeCopies;
    internal InspectorSnapshot(CaptureStamp stamp, Func<IDisposable>? beginCopy = null,
        Action<InspectorSnapshot>? finishRetirement = null)
    {
        Stamp = stamp;
        this.beginCopy = beginCopy;
        this.finishRetirement = finishRetirement;
    }
    internal void Add(SnapshotPart part)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (part.Stamp != Stamp) throw new InvalidOperationException("Snapshot revision conflict.");
        if (part.Offset < 0 || part.Count < 0 || part.Total < 0 || part.Offset > part.Total || part.Count > part.Total - part.Offset)
            throw new ArgumentOutOfRangeException(nameof(part));
        if (!part.Payload.IsComplete || part.Payload.Length != checked(part.Count * (part.Member == InspectorMember.ShareValue ? 1 : 2)) ||
            part.IsNull && (part.Total != 0 || part.Member == InspectorMember.ShareValue))
            throw new InvalidOperationException("Incomplete or inconsistent payload.");
        if (part.Member is InspectorMember.PropertyPage or InspectorMember.PropertyValue &&
            (part.Request == null || part.Offset != 0 || part.Count != part.Total))
            throw new InvalidOperationException("A published property part must contain its complete requested window.");
        if (part.Request is { } request && part.Page?.StringLength is int length)
        {
            long start = (long)request.Page*InspectorProjector.PageSize;
            long expected = part.Page.ExactScalar != null ? length : Math.Clamp(length-start,0,InspectorProjector.PageSize);
            if (start < 0 || length < 0 || part.Count != expected)
                throw new InvalidOperationException("Inconsistent property page coverage.");
            foreach (var prior in Parts)
                if (prior.Page?.StringLength is int previous && prior.Request is { } priorRequest &&
                    priorRequest.Path.SequenceEqual(request.Path) &&
                    (previous != length || priorRequest.Page == request.Page))
                    throw new InvalidOperationException("Overlapping or conflicting property pages.");
        }
        foreach (var old in Parts)
            if (old.Member == part.Member && old.ShareIndex == part.ShareIndex)
            {
                if (old.Total != part.Total) throw new InvalidOperationException("Conflicting total length.");
                // Reject overlap rather than silently overwrite already published immutable data.
                if (old.IsNull != part.IsNull) throw new InvalidOperationException("Conflicting null metadata.");
                if (part.Count == 0) { part.Payload.Dispose(); return; }
                if (old.Count > 0 && old.Offset < part.Offset + part.Count && part.Offset < old.Offset + old.Count)
                    throw new InvalidOperationException("Overlapping snapshot ranges.");
            }
        Parts = Parts.Add(part);
    }
    internal bool Complete(InspectorMember member, int shareIndex = 0)
    {
        var selected = Parts.Where(p => p.Member == member && p.ShareIndex == shareIndex)
            .OrderBy(p => p.Offset).ToImmutableArray();
        if (selected.IsEmpty) return false;
        long next = 0;
        foreach (var part in selected.Where(p => p.Count != 0)) { if (part.Offset != next) return false; next += part.Count; }
        return next == selected[0].Total;
    }
    // UI-owned acquisition. The caller owns private staging and publishes it only in commit.
    internal async Task CopyAsync(InspectorMember member, int shareIndex, Stream staging,
        Func<Task> commit, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Complete(member, shareIndex)) throw new InvalidOperationException("Incomplete coverage.");
        await CopyPartsAsync(Parts.Where(p => p.Member == member && p.ShareIndex == shareIndex).OrderBy(p => p.Offset),
            staging,commit,token);
    }

    internal async Task CopyPartsAsync(IEnumerable<SnapshotPart> selected, Stream staging, Func<Task> commit, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        using var operation = beginCopy?.Invoke();
        var readers = new List<InspectorPayload.Reader>();
        activeCopies++;
        try
        {
            foreach (var part in selected)
                readers.Add(part.Payload.AcquireReader());
            foreach (var reader in readers) await reader.StageAsync(staging, token);
            token.ThrowIfCancellationRequested();
            await commit();
        }
        finally
        {
            List<Exception>? failures = null;
            foreach (var reader in readers)
                try { reader.Dispose(); } catch (Exception error) { (failures ??= new()).Add(error); }
            if (--activeCopies == 0 && disposed)
            {
                if (finishRetirement != null) finishRetirement(this); else Dispose();
            }
            if (failures != null) throw new AggregateException(failures);
        }
    }
    public void Dispose()
    {
        disposed = true;
        if (activeCopies != 0) return;
        System.Collections.Generic.List<Exception>? errors = null;
        foreach (var part in Parts)
            try { part.Payload.Dispose(); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        if (errors != null) throw new AggregateException("Inspector snapshot cleanup failed.", errors);
        Parts = [];
    }
}
