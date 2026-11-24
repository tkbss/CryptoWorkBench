using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Collections.Immutable;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoScript.SecretSharing;
using CryptoWorkBenchAvalonia.Services.Automation;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

internal sealed record InspectorProgress(long Completed, long Total, TimeSpan Elapsed);

internal sealed class StaleInspectorSelectionException() : InvalidOperationException("Stale inspector selection.");

internal static class InspectorCapture
{
    internal static async Task<SnapshotPart> ReadAsync(WorkbenchAutomationService service, SelectionHandle handle,
        ReadSpec spec, Func<bool> current, CancellationToken token, Func<InspectorPayload>? createPayload = null,
        IProgress<InspectorProgress>? progress = null)
    {
        if (!Enum.IsDefined(spec.Member)) throw new ArgumentOutOfRangeException(nameof(spec));
        var payload = createPayload?.Invoke() ?? new InspectorPayload(cacheBudget: 0);
        InspectorPayload? owned = payload;
        void Abandon()
        {
            var staging = Interlocked.Exchange(ref owned, null);
            if (staging != null) service.RetireStaging(staging);
        }
        try
        {
            var part = await service.RunInspectorReadAsync(async cancellation =>
            {
                string? text = null; SecretShare? share = null; long total = 0;
                VariableDeclaration Resolve()
                {
                    if (InterpreterHostQueue.StateRevision != handle.Stamp.Revision ||
                        !handle.Stamp.Origin.TryGetTarget(out var origin) ||
                        !ReferenceEquals(service.Store.Get(handle.Stamp.Identifier), origin))
                        throw new StaleInspectorSelectionException();
                    return origin;
                }
                if (spec.Property is { } property && spec.Member is InspectorMember.PropertyPage or InspectorMember.PropertyValue)
                {
                    var projector = service.AccessJobState(() => new InspectorProjector(Resolve(), cancellation)
                    {
                        Index = spec.Member == InspectorMember.PropertyValue ? ImmutableDictionary.CreateBuilder<string,CapturedSlice>() : null
                    });
                    InspectorPage? page = null;
                    if (spec.Member == InspectorMember.PropertyPage)
                    {
                        using var steps = projector.DescribeSteps(property,result => page = result).GetEnumerator();
                        while (service.AccessJobState(() => { Resolve(); return steps.MoveNext(); }))
                            cancellation.ThrowIfCancellationRequested();
                        string? captured = page!.ExactScalar;
                        if (captured == null && page.StringLength.HasValue)
                            captured = string.Concat(page.Rows.Where(row => row.Step.Kind == StepKind.Entry).Select(row => row.ExactScalar));
                        if (captured is { } exact)
                            await payload.AppendAsync(System.Runtime.InteropServices.MemoryMarshal.AsBytes(exact.AsSpan()).ToArray(), cancellation).ConfigureAwait(false);
                    }
                    else
                    {
                        using var tokens = service.AccessJobState(() => projector.Encode(property).GetEnumerator());
                        var encoded = new byte[65536];
                        string remainder = "";
                        bool finished = false;
                        var progressInterval = Stopwatch.StartNew();
                        while (!finished || remainder.Length != 0)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            int used = service.AccessJobState(() =>
                            {
                                Resolve();
                                return projector.ReadChunk(tokens,encoded,ref remainder,out finished);
                            });
                            if (used > 0) await payload.AppendAsync(encoded.AsMemory(0, used), cancellation).ConfigureAwait(false);
                            if (progressInterval.ElapsedMilliseconds >= 250)
                            {
                                progress?.Report(new(payload.Length / 2, -1, progressInterval.Elapsed));
                                progressInterval.Restart();
                            }
                        }
                    }
                    await payload.SealAsync(cancellation).ConfigureAwait(false);
                    long length = payload.Length / 2;
                    return new SnapshotPart(handle.Stamp, spec.Member, spec.ShareIndex, 0, length, length, payload, Page: page, Request: property, Index: projector.Index?.ToImmutable());
                }
                service.AccessJobState(() =>
                {
                    var root = Resolve();
                    var type = root.GetType();
                    if (type != typeof(VariableDeclaration) && type != typeof(StringVariableDeclaration) &&
                        type != typeof(PathVariableDeclaration) && type != typeof(ParameterVariableDeclaration) &&
                        type != typeof(KeyVariableDeclaration) && type != typeof(ValVariableDeclaration) &&
                        type != typeof(SecretVariableDeclaration) && type != typeof(BlockHeaderVariableDeclaration))
                        throw new NotSupportedException("UnsupportedAccessor");
                    if (spec.Member == InspectorMember.ShareValue)
                    {
                        if (root.GetType() != typeof(SecretVariableDeclaration)) throw new NotSupportedException("UnsupportedAccessor");
                        var set = ((SecretVariableDeclaration)root).SecretSet;
                        if (spec.ShareIndex < 0 || spec.ShareIndex >= set.Shares.Count) throw new ArgumentOutOfRangeException(nameof(spec));
                        share = set.Shares[spec.ShareIndex]; total = set.Descriptor.KeySizeBits / 8;
                    }
                    else
                    {
                        if (spec.Member == InspectorMember.Text) text = root.Text;
                        else if (root.GetType() == typeof(VariableDeclaration) || root.GetType() == typeof(StringVariableDeclaration) ||
                            root.GetType() == typeof(PathVariableDeclaration) || root.GetType() == typeof(ParameterVariableDeclaration) ||
                            root.GetType() == typeof(KeyVariableDeclaration) || root.GetType() == typeof(ValVariableDeclaration) ||
                            root.GetType() == typeof(SecretVariableDeclaration)) text = root.Value;
                        else throw new NotSupportedException("UnsupportedAccessor");
                        total = text?.Length ?? 0;
                    }
                    return true;
                });
                long count = spec.Count ?? (total - spec.Offset);
                if (spec.Offset < 0 || spec.Offset > total || count < 0 || count > total - spec.Offset)
                    throw new ArgumentOutOfRangeException(nameof(spec));
                byte[] buffer = new byte[65536];
                long consumed = 0;
                var elapsed = Stopwatch.StartNew();
                long lastProgress = 0;
                while (consumed < count)
                {
                    cancellation.ThrowIfCancellationRequested();
                    int step = (int)Math.Min(count - consumed, share == null ? buffer.Length / 2 : buffer.Length);
                    service.AccessJobState(() =>
                    {
                        Resolve();
                        int offset = checked((int)(spec.Offset + consumed));
                        if (share != null) share.CopyValueRange(offset, buffer.AsSpan(0, step));
                        else for (int i = 0; i < step; i++)
                        {
                            char c = text![offset + i]; buffer[2 * i] = (byte)c; buffer[2 * i + 1] = (byte)(c >> 8);
                        }
                        return true;
                    });
                    await payload.AppendAsync(buffer.AsMemory(0, share == null ? step * 2 : step), cancellation).ConfigureAwait(false);
                    consumed += step;
                    if (elapsed.ElapsedMilliseconds >= 5000 && elapsed.ElapsedMilliseconds - lastProgress >= 250)
                    {
                        progress?.Report(new(consumed, count, elapsed.Elapsed));
                        lastProgress = elapsed.ElapsedMilliseconds;
                    }
                }
                await payload.SealAsync(cancellation).ConfigureAwait(false);
                return new SnapshotPart(handle.Stamp, spec.Member, spec.ShareIndex, spec.Offset, count, total, payload,
                    share == null && text == null);
            }, current, token, Abandon).ConfigureAwait(false);
            owned = null; // The returned part now owns the sealed payload.
            return part;
        }
        catch { Abandon(); throw; }
    }
}
