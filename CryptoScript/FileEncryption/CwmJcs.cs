using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CryptoScript.FileEncryption;

// RFC 8785 for the CWM v1 data domain: objects, Unicode strings and safe integers.
// No floating point value is admitted by this profile (including in parameters).
// Do not substitute JsonSerializer output: its escaping and property order differ.
internal static class CwmJcs
{
    internal const long MaximumInteger = 9007199254740991;
    internal static byte[] Canonicalize(JsonElement value)
    {
        var output = new StringBuilder();
        Write(value, output);
        return new UTF8Encoding(false, true).GetBytes(output.ToString());
    }

    private static void Write(JsonElement value, StringBuilder output)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new FormatException("Duplicate CWM property.");
                output.Append('{');
                for (int i = 0; i < properties.Length; i++)
                {
                    if (i != 0) output.Append(',');
                    String(properties[i].Name, output); output.Append(':'); Write(properties[i].Value, output);
                }
                output.Append('}'); break;
            case JsonValueKind.String: String(value.GetString()!, output); break;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out long number) || number < -MaximumInteger || number > MaximumInteger)
                    throw new FormatException("CWM numbers must be safe integers.");
                output.Append(number.ToString(CultureInfo.InvariantCulture)); break;
            default: throw new FormatException("Unsupported CWM JSON value.");
        }
    }

    private static void String(string value, StringBuilder output)
    {
        // Strict UTF-8 encoding rejects lone UTF-16 surrogates rather than replacing them.
        _ = new UTF8Encoding(false, true).GetByteCount(value);
        output.Append('"');
        foreach (char c in value)
        {
            output.Append(c switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\b' => "\\b", '\t' => "\\t", '\n' => "\\n",
                '\f' => "\\f", '\r' => "\\r", < ' ' => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
                _ => c.ToString()
            });
        }
        output.Append('"');
    }
}
