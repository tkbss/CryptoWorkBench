using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CryptoScript.Serialization;

// Fixed capacity: no resize can leave an unwiped copy of a plaintext/AAD buffer.
internal sealed class SecureBuffer(int capacity) : IDisposable
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly byte[] buffer = new byte[capacity];
    private int length;
    internal ReadOnlySpan<byte> Span => buffer.AsSpan(0, length);
    internal byte[] ToArray() => Span.ToArray();
    internal void Bytes(ReadOnlySpan<byte> value)
    {
        if (value.Length > buffer.Length - length) throw new SecureSaveException("SizeLimitExceeded");
        value.CopyTo(buffer.AsSpan(length)); length += value.Length;
    }
    internal void U8(byte value) { Span<byte> b = stackalloc byte[1]; b[0] = value; Bytes(b); }
    internal void U32(uint value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, value); Bytes(b); }
    internal void U64(ulong value) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteUInt64BigEndian(b, value); Bytes(b); }
    internal void B(ReadOnlySpan<byte> value) { U64((ulong)value.Length); Bytes(value); }
    internal void S(string value)
    {
        byte[] bytes = Utf8.GetBytes(value);
        try { B(bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal void Text(string value)
    {
        int count = Utf8.GetByteCount(value);
        if (count > buffer.Length - length) throw new SecureSaveException("SizeLimitExceeded");
        length += Utf8.GetBytes(value, buffer.AsSpan(length));
    }
    internal void Number(int value) => Text(value.ToString(CultureInfo.InvariantCulture));
    internal void Json(string value)
    {
        byte[] bytes = Utf8.GetBytes(value);
        try { Json(bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal static long EscapedSize(ReadOnlySpan<byte> bytes)
    {
        long result = 0;
        foreach (byte b in bytes) result += b < 32 ? 6 : b is 34 or 92 ? 2 : 1;
        return result;
    }
    internal void Json(ReadOnlySpan<byte> bytes)
    {
        U8(34);
        foreach (byte b in bytes)
        {
            if (b < 32) { Text("\\u00"); U8((byte)"0123456789abcdef"[b >> 4]); U8((byte)"0123456789abcdef"[b & 15]); }
            else { if (b is 34 or 92) U8(92); U8(b); }
        }
        U8(34);
    }
    public void Dispose() => CryptographicOperations.ZeroMemory(buffer);
}
