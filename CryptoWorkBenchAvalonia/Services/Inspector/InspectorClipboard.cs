using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace CryptoWorkBenchAvalonia.Services.Inspector;

internal static class InspectorClipboard
{
    internal static void ValidateWindowsText(string value)
    {
        if (value.Contains('\0'))
            throw new NotSupportedException("Clipboard text cannot preserve embedded NUL.");
    }

    internal static async Task SetAsync(IClipboard clipboard, string value, CancellationToken cancellation)
    {
        if (OperatingSystem.IsWindows())
            await Task.Run(() => ValidateWindowsText(value), cancellation);
        cancellation.ThrowIfCancellationRequested();
        await clipboard.SetTextAsync(value);
        // Read the platform format, including delayed OLE rendering. This comparison
        // needs only a bounded buffer in addition to the final clipboard string.
        if (OperatingSystem.IsWindows())
            await Task.Run(() => VerifyUnicodeText(value));
        else if (!string.Equals(await clipboard.TryGetTextAsync(), value, StringComparison.Ordinal))
            throw new IOException("Clipboard readback differs from the complete property value.");
    }

    private static void VerifyUnicodeText(string value)
    {
        if (!OpenClipboard(IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var handle = GetClipboardData(13); // CF_UNICODETEXT
            long size = ((long)value.Length + 1) * sizeof(char);
            if (handle == IntPtr.Zero || GlobalSize(handle) < (ulong)size)
                throw new IOException("Clipboard has no complete Unicode text value.");
            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var buffer = new char[Math.Min(32768, value.Length)];
                for (int i = 0; i < value.Length;)
                {
                    int count = Math.Min(buffer.Length, value.Length - i);
                    Marshal.Copy(pointer + checked((nint)((long)i * 2)), buffer, 0, count);
                    if (!value.AsSpan(i,count).SequenceEqual(buffer.AsSpan(0,count)))
                        throw new IOException("Clipboard text differs from the complete property value.");
                    i += count;
                }
                if (Marshal.ReadInt16(pointer + checked((nint)(size - 2))) != 0)
                    throw new IOException("Clipboard text lacks the expected terminator.");
            }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    [DllImport("user32", SetLastError = true)] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32")] private static extern bool CloseClipboard();
    [DllImport("user32")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32")] private static extern nuint GlobalSize(IntPtr memory);
    [DllImport("kernel32", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32")] private static extern bool GlobalUnlock(IntPtr memory);
}
