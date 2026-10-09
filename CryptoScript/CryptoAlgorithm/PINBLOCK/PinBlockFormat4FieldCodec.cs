namespace CryptoScript.CryptoAlgorithm.PINBLOCK;

internal sealed class DecodedPinBlockFormat4Fields
{
    internal DecodedPinBlockFormat4Fields(string pin, string randomField)
    {
        Pin = pin;
        RandomField = randomField;
    }

    internal string Pin { get; }
    internal string RandomField { get; }
}

internal static class PinBlockFormat4FieldCodec
{
    private const int FieldLength = 16;
    private const int NibbleCount = FieldLength * 2;
    private const int RandomNibbleCount = 16;
    private const int MinimumPinLength = 4;
    private const int MaximumPinLength = 12;
    private const int MinimumPanLength = 10;
    private const int MaximumPanLength = 19;
    private const byte FormatIdentifier = 4;
    private const byte FillNibble = 0x0A;

    internal static byte[] EncodePinField(string pin, string randomField)
    {
        ValidatePin(pin);
        ValidateRandomField(randomField);

        Span<byte> nibbles = stackalloc byte[NibbleCount];
        nibbles[..16].Fill(FillNibble);
        nibbles[0] = FormatIdentifier;
        nibbles[1] = (byte)pin.Length;
        WriteDecimalNibbles(pin, nibbles[2..]);
        WriteHexNibbles(randomField, nibbles[16..]);
        return Pack(nibbles);
    }

    internal static DecodedPinBlockFormat4Fields DecodePinField(ReadOnlySpan<byte> field)
    {
        Span<byte> nibbles = stackalloc byte[NibbleCount];
        Unpack(field, nibbles);

        if (nibbles[0] != FormatIdentifier)
            throw new FormatException("PIN field has an unexpected format identifier.");

        int pinLength = nibbles[1];
        if (pinLength is < MinimumPinLength or > MaximumPinLength)
            throw new FormatException("PIN field contains an invalid PIN length.");

        ReadOnlySpan<byte> pinNibbles = nibbles.Slice(2, pinLength);
        foreach (byte nibble in pinNibbles)
        {
            if (nibble > 9)
                throw new FormatException("PIN field contains a non-decimal PIN digit.");
        }

        foreach (byte nibble in nibbles.Slice(2 + pinLength, 14 - pinLength))
        {
            if (nibble != FillNibble)
                throw new FormatException("PIN field contains an invalid fill digit.");
        }

        return new DecodedPinBlockFormat4Fields(
            ToHexString(pinNibbles),
            ToHexString(nibbles[16..]));
    }

    internal static byte[] EncodePanField(string pan)
    {
        ValidatePan(pan);

        Span<byte> nibbles = stackalloc byte[NibbleCount];
        nibbles.Clear();
        nibbles[0] = (byte)Math.Max(0, pan.Length - 12);
        int destinationOffset = pan.Length < 12 ? 1 + 12 - pan.Length : 1;
        WriteDecimalNibbles(pan, nibbles[destinationOffset..]);
        return Pack(nibbles);
    }

    internal static void ValidatePanField(ReadOnlySpan<byte> field)
    {
        Span<byte> nibbles = stackalloc byte[NibbleCount];
        Unpack(field, nibbles);

        int lengthIndicator = nibbles[0];
        if (lengthIndicator > MaximumPanLength - 12)
            throw new FormatException("PAN field contains an invalid length indicator.");

        int encodedPanLength = 12 + lengthIndicator;
        foreach (byte nibble in nibbles.Slice(1, encodedPanLength))
        {
            if (nibble > 9)
                throw new FormatException("PAN field contains a non-decimal PAN digit.");
        }

        foreach (byte nibble in nibbles[(1 + encodedPanLength)..])
        {
            if (nibble != 0)
                throw new FormatException("PAN field contains an invalid pad digit.");
        }
    }

    private static void ValidatePin(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        if (pin.Length is < MinimumPinLength or > MaximumPinLength || !ContainsOnlyDecimalDigits(pin))
            throw new ArgumentException("PIN must contain between 4 and 12 decimal digits.", nameof(pin));
    }

    private static void ValidateRandomField(string randomField)
    {
        ArgumentNullException.ThrowIfNull(randomField);
        if (randomField.Length != RandomNibbleCount || !ContainsOnlyHexDigits(randomField))
            throw new ArgumentException("Random field must contain exactly 16 hexadecimal digits.", nameof(randomField));
    }

    private static void ValidatePan(string pan)
    {
        ArgumentNullException.ThrowIfNull(pan);
        if (pan.Length is < MinimumPanLength or > MaximumPanLength || !ContainsOnlyDecimalDigits(pan))
            throw new ArgumentException("PAN must contain between 10 and 19 decimal digits.", nameof(pan));
    }

    private static bool ContainsOnlyDecimalDigits(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character is < '0' or > '9')
                return false;
        }
        return true;
    }

    private static bool ContainsOnlyHexDigits(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (ParseHexNibble(character) < 0)
                return false;
        }
        return true;
    }

    private static void WriteDecimalNibbles(ReadOnlySpan<char> digits, Span<byte> destination)
    {
        for (int index = 0; index < digits.Length; index++)
            destination[index] = (byte)(digits[index] - '0');
    }

    private static void WriteHexNibbles(ReadOnlySpan<char> digits, Span<byte> destination)
    {
        for (int index = 0; index < digits.Length; index++)
            destination[index] = (byte)ParseHexNibble(digits[index]);
    }

    private static void Unpack(ReadOnlySpan<byte> field, Span<byte> destination)
    {
        if (field.Length != FieldLength)
            throw new ArgumentException("Format 4 field must contain exactly 16 bytes.", nameof(field));

        for (int index = 0; index < field.Length; index++)
        {
            destination[index * 2] = (byte)(field[index] >> 4);
            destination[index * 2 + 1] = (byte)(field[index] & 0x0F);
        }
    }

    private static byte[] Pack(ReadOnlySpan<byte> nibbles)
    {
        byte[] result = new byte[FieldLength];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)((nibbles[index * 2] << 4) | nibbles[index * 2 + 1]);
        return result;
    }

    private static int ParseHexNibble(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        >= 'A' and <= 'F' => character - 'A' + 10,
        >= 'a' and <= 'f' => character - 'a' + 10,
        _ => -1
    };

    private static string ToHexString(ReadOnlySpan<byte> nibbles)
    {
        char[] result = new char[nibbles.Length];
        for (int index = 0; index < nibbles.Length; index++)
            result[index] = (char)(nibbles[index] < 10 ? '0' + nibbles[index] : 'A' + nibbles[index] - 10);
        return new string(result);
    }
}
