using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

// A payload is private staging until Seal. No global registry, no live interpreter references.
internal sealed class InspectorPayload : IDisposable
{
    private readonly object sync = new();
    private readonly List<byte[]> memory = new();
    private readonly long cacheBudget;
    private readonly string directory;
    private readonly long diskReserve;
    private FileStream? file;
    private string? path;
    private readonly List<string> paths = new();
    private bool sealedValue, disposed;
    private int readers;
    private IncrementalHash? hash;
    private readonly List<(int Length, byte[] Digest)> segments = new();
    private async Task FinishSegmentAsync()
    {
        if (file == null) return;
        segments.Add((checked((int)file.Length), hash!.GetHashAndReset()));
        await file.DisposeAsync().ConfigureAwait(false);
        file = null;
    }
    internal long Length { get; private set; }
    internal bool IsComplete => sealedValue && !disposed;
    internal InspectorPayload(long cacheBudget = 32 * 1024 * 1024, string? directory = null,
        long diskReserve = 512 * 1024 * 1024)
    {
        if (cacheBudget < 0 || diskReserve < 0) throw new ArgumentOutOfRangeException(nameof(cacheBudget));
        this.cacheBudget = cacheBudget; this.directory = directory ?? Path.GetTempPath(); this.diskReserve = diskReserve;
    }
    internal async Task AppendAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        if (disposed || sealedValue) throw new InvalidOperationException("Payload is not writable.");
        if (bytes.Length > 256 * 1024) throw new ArgumentOutOfRangeException(nameof(bytes));
        token.ThrowIfCancellationRequested();
        if (file == null && Length + bytes.Length <= cacheBudget) memory.Add(bytes.ToArray());
        else
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))!);
            if (drive.AvailableFreeSpace < diskReserve + bytes.Length + (file == null ? Length : 0))
                throw new IOException("ResourceUnavailable: insufficient temporary storage.");
            if (file == null && paths.Count == 0)
            {
                foreach (var segment in memory) await WriteFileAsync(segment, token).ConfigureAwait(false);
                memory.Clear();
            }
            await WriteFileAsync(bytes, token).ConfigureAwait(false);
        }
        Length = checked(Length + bytes.Length);
    }
    private async Task WriteFileAsync(ReadOnlyMemory<byte> bytes, CancellationToken token)
    {
        while (!bytes.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            if (file == null || file.Length == 1024 * 1024)
            {
                await FinishSegmentAsync().ConfigureAwait(false);
                hash ??= IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                path = Path.Combine(directory, "cwb-inspector-" + Guid.NewGuid().ToString("N") + ".payload");
                file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read,
                    4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                paths.Add(path);
            }
            int step = (int)Math.Min(bytes.Length, 1024 * 1024 - file.Length);
            await file.WriteAsync(bytes[..step], token).ConfigureAwait(false);
            hash!.AppendData(bytes.Span[..step]);
            bytes = bytes[step..];
        }
    }
    internal async Task SealAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        token.ThrowIfCancellationRequested();
        await FinishSegmentAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        sealedValue = true;
    }
    internal Reader AcquireReader()
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!sealedValue) throw new InvalidOperationException("Incomplete payload.");
            var reader = new Reader(this);
            readers++;
            return reader;
        }
    }
    internal sealed class Reader(InspectorPayload payload) : IDisposable
    {
        private InspectorPayload? owner = payload;
        internal Task StageAsync(Stream staging, CancellationToken token) =>
            (owner ?? throw new ObjectDisposedException(nameof(Reader))).StageAsync(staging, token);
        public void Dispose()
        {
            var value = Interlocked.Exchange(ref owner, null);
            if (value == null) return;
            lock (value.sync) if (--value.readers == 0 && value.disposed) value.Cleanup();
        }
    }
    internal async Task CopyToAsync(Stream staging, CancellationToken token = default)
    {
        using var reader = AcquireReader();
        await reader.StageAsync(staging, token).ConfigureAwait(false);
    }
    private async Task StageAsync(Stream staging, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        foreach (var segment in memory) await staging.WriteAsync(segment, token).ConfigureAwait(false);
        if (paths.Count == 0) return;
        var buffer = new byte[segments[0].Length];
        for (int index = 0; index < paths.Count; index++)
        {
            var (length, digest) = segments[index];
            await using var source = new FileStream(paths[index], FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var data = buffer.AsMemory(0, length);
            await source.ReadExactlyAsync(data, token).ConfigureAwait(false);
            if (source.Length != length || !SHA256.HashData(data.Span).AsSpan().SequenceEqual(digest))
                throw new InvalidDataException("Inspector payload segment changed.");
            await staging.WriteAsync(data, token).ConfigureAwait(false);
        }
    }
    public void Dispose()
    {
        lock (sync) { disposed = true; if (readers == 0) Cleanup(); }
    }
    private void Cleanup()
    {
        List<Exception>? errors = null;
        try { file?.Dispose(); } catch (Exception error) { (errors ??= new()).Add(error); }
        file = null; hash?.Dispose(); hash = null; memory.Clear();
        for (int index = paths.Count - 1; index >= 0; index--)
        {
            try { File.Delete(paths[index]); paths.RemoveAt(index); }
            catch (Exception error) { (errors ??= new()).Add(error); }
        }
        if (paths.Count == 0) path = null;
        if (errors != null) throw new AggregateException("Inspector payload cleanup failed.", errors);
    }
}
