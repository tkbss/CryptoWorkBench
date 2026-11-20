using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

// All parts in a source belong to one detached snapshot. Even a slice of a formatted
// parent goes through the original segment digests and snapshot-wide reader lease.
internal sealed record InspectorValueSource(ImmutableArray<SnapshotPart> Parts, long Characters,
    CapturedSlice? Slice = null)
{
    internal static InspectorValueSource? Find(InspectorSnapshot snapshot, ImmutableArray<InspectorStep> path)
    {
        string key = InspectorProjector.Key(path);
        var pages = new SortedDictionary<int, SnapshotPart>();
        InspectorValueSource? nested = null;
        foreach (var part in snapshot.Parts)
        {
            if (InspectorProjector.Lookup(part,path) is { } slice)
                nested = new([part],slice.Count,slice);
            if (part.Request == null || !part.Request.Path.SequenceEqual(path)) continue;
            if (part.Member == InspectorMember.PropertyValue || part.Page?.ExactScalar != null)
                return new([part],part.Count,part.Index?.GetValueOrDefault(key));
            if (part.Page?.StringLength.HasValue == true)
            {
                // Empty windows beyond the end do not contribute coverage.
                if ((part.Count != 0 || part.Request.Page == 0) && !pages.TryAdd(part.Request.Page,part))
                    return null;
            }
        }
        if (nested != null || pages.Count == 0) return nested;
        int total = pages.First().Value.Page!.StringLength!.Value;
        long end = 0;
        foreach (var (page,part) in pages)
        {
            long start = (long)page * InspectorProjector.PageSize;
            if (part.Page!.StringLength != total || start != end ||
                part.Count != Math.Min(InspectorProjector.PageSize,total-start)) return null;
            end += part.Count;
        }
        return end == total ? new(pages.Values.ToImmutableArray(),total) : null;
    }

    internal async Task CopyAsync(InspectorSnapshot snapshot, Func<string,Task> use, CancellationToken token)
    {
        long bytes = checked(Characters * sizeof(char));
        if (Characters > int.MaxValue) throw new IOException("Value exceeds the platform string limit.");
        if (bytes > 262144 && new DriveInfo(Path.GetPathRoot(Path.GetTempPath())!).AvailableFreeSpace - bytes < 512L*1024*1024)
            throw new IOException("ResourceUnavailable: insufficient space to stage the complete value.");
        using Stream staging = bytes <= 262144 ? new MemoryStream((int)bytes) :
            new FileStream(Path.Combine(Path.GetTempPath(),"cwb-inspector-"+Guid.NewGuid().ToString("N")+".payload"),
                FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,65536,FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        using var range = new SliceWriter(staging,checked((Slice?.Offset ?? 0)*2),bytes);
        await snapshot.CopyPartsAsync(Parts,range,async () =>
        {
            if (staging.Length != bytes) throw new InvalidDataException("Incomplete property slice.");
            var text = await Task.Run(() => ReadText(staging,token),token);
            token.ThrowIfCancellationRequested();
            await use(text);
        },token);
    }

    private string ReadText(Stream input,CancellationToken token)
    {
        if (Slice?.HexByte == true)
        {
            Span<char> digits = stackalloc char[2];
            input.Position=0;
            input.ReadExactly(MemoryMarshal.AsBytes(digits));
            return byte.Parse(digits,System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (Slice?.PairColon is long colon)
        {
            input.Position=0;
            // A JSON object member has a quoted key and its encoded value. Preserve
            // both verbatim when adapting that member to the domain KeyValuePair.
            return string.Create(checked((int)Characters+16),input,(dest,stream) =>
            {
                const string prefix = "{\"Key\":";
                const string separator = ",\"Value\":";
                prefix.CopyTo(dest);
                dest=dest[prefix.Length..];
                stream.ReadExactly(MemoryMarshal.AsBytes(dest[..(int)colon]));
                dest=dest[(int)colon..];
                stream.Position+=2;
                separator.CopyTo(dest);
                dest=dest[separator.Length..];
                stream.ReadExactly(MemoryMarshal.AsBytes(dest[..^1]));
                dest[^1]='}';
                token.ThrowIfCancellationRequested();
            });
        }
        bool quoted = Slice?.Quoted == true;
        int length = checked((int)(quoted ? Slice!.DecodedLength ?? Characters-2 : Characters));
        input.Position = quoted ? 2 : 0;
        return string.Create(length,input,(destination,stream) =>
        {
            if (quoted && Characters-2 != length)
            {
                using var reader = new BinaryReader(stream,System.Text.Encoding.Unicode,true);
                for (int i=0;i<destination.Length;i++)
                {
                    if ((i & 8191)==0) token.ThrowIfCancellationRequested();
                    char c = (char)reader.ReadUInt16();
                    if (c == (char)92)
                    {
                        char escape = (char)reader.ReadUInt16();
                        c = escape switch
                        {
                            'b' => (char)8, 't' => (char)9, 'n' => (char)10, 'f' => (char)12, 'r' => (char)13,
                            '"' => '"', (char)92 => (char)92, 'u' => ReadUnicode(reader),
                            _ => throw new InvalidDataException("Invalid property escape.")
                        };
                    }
                    destination[i]=c;
                }
                if (stream.Position != stream.Length-2) throw new InvalidDataException("Invalid encoded length.");
            }
            else while (!destination.IsEmpty)
            {
                token.ThrowIfCancellationRequested();
                int count = Math.Min(destination.Length,32768);
                stream.ReadExactly(MemoryMarshal.AsBytes(destination[..count]));
                destination = destination[count..];
            }
        });
    }

    private static char ReadUnicode(BinaryReader reader)
    {
        int value=0;
        for (int i=0;i<4;i++)
        {
            char c=(char)reader.ReadUInt16();
            value = value*16 + (c switch { >= '0' and <= '9' => c-'0', >= 'a' and <= 'f' => c-'a'+10,
                _ => throw new InvalidDataException("Invalid Unicode escape.") });
        }
        return (char)value;
    }

    private sealed class SliceWriter(Stream destination,long start,long length) : Stream
    {
        private long consumed;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default)
        {
            long from = Math.Clamp(start-consumed,0,buffer.Length);
            long to = Math.Clamp(start+length-consumed,0,buffer.Length);
            consumed+=buffer.Length;
            return to==from ? ValueTask.CompletedTask : destination.WriteAsync(buffer[(int)from..(int)to],token);
        }
        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => consumed; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] b,int o,int n) => throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long n) => throw new NotSupportedException();
        public override void Write(byte[] b,int o,int n) => throw new NotSupportedException();
    }
}
