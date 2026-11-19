using System;
using System.Collections.Generic;
using System.Text;

namespace CryptoWorkBenchAvalonia;

internal static class DisplayTextPreview
{
    private const int Limit = 128;

    internal static string Shorten(string value)
    {
        return value.Length <= Limit ? value : WithEllipsis(value, Limit - 1);
    }

    // The caller has established that at least one code unit must be omitted.
    internal static string WithEllipsis(string value, int end)
    {
        if (char.IsHighSurrogate(value[end - 1]) && char.IsLowSurrogate(value[end])) end--;
        return value[..end] + "…";
    }

    internal static string Concat(params string?[] parts) => Join("", parts);

    internal static string Join(string separator, IEnumerable<string?> parts)
    {
        // One extra code unit distinguishes an exact fit from overflow.
        var prefix = new StringBuilder(Limit + 1);
        bool first = true;
        foreach (var part in parts)
        {
            if (!first) Append(separator);
            Append(part);
            if (prefix.Length > Limit) break;
            first = false;
        }
        return Shorten(prefix.ToString());

        void Append(string? text)
        {
            if (text != null) prefix.Append(text, 0, Math.Min(text.Length, Limit + 1 - prefix.Length));
        }
    }
}
