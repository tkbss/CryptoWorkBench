namespace CryptoScript.CryptoAlgorithm.PINBLOCK;

internal sealed class DecodedPinBlockFields
{
    internal DecodedPinBlockFields(string pin, string? transactionField = null, string? fillField = null)
    {
        Pin = pin;
        TransactionField = transactionField;
        FillField = fillField;
    }

    internal string Pin { get; }
    internal string? TransactionField { get; }
    internal string? FillField { get; }
}

internal static class PinBlockFieldCodec
{
    private const int BlockLength = 8;
    private const int NibbleCount = BlockLength * 2;
    private const int MinimumPinLength = 4;
    private const int MaximumPinLength = 12;
    private const int MinimumPanLength = 10;
    private const int MaximumPanLength = 19;

    internal static byte[] EncodeFormat0(string pin, string pan)
    {
        Span<byte> pinField = stackalloc byte[NibbleCount];
        BuildPinField(pin, 0, 0x0F, pinField);

        Span<byte> panField = stackalloc byte[NibbleCount];
        BuildPanField(pan, panField);
        return XorAndPack(pinField, panField);
    }

    internal static DecodedPinBlockFields DecodeFormat0(ReadOnlySpan<byte> block, string pan)
    {
        Span<byte> pinField = stackalloc byte[NibbleCount];
        Unpack(block, pinField);

        Span<byte> panField = stackalloc byte[NibbleCount];
        BuildPanField(pan, panField);
        XorInPlace(pinField, panField);
        return DecodePinField(pinField, 0, FixedFillValidator);
    }

    internal static byte[] EncodeFormat1(string pin, string transactionField)
    {
        Span<byte> field = stackalloc byte[NibbleCount];
        BuildVariableField(pin, transactionField, 1, AnyHexValidator,
            "Transaction field must contain the required number of hexadecimal digits.", field);
        return Pack(field);
    }

    internal static DecodedPinBlockFields DecodeFormat1(ReadOnlySpan<byte> block)
    {
        Span<byte> field = stackalloc byte[NibbleCount];
        Unpack(block, field);
        return DecodePinField(field, 1, AnyHexValidator, includeTransactionField: true);
    }

    internal static byte[] EncodeFormat2(string pin)
    {
        Span<byte> field = stackalloc byte[NibbleCount];
        BuildPinField(pin, 2, 0x0F, field);
        return Pack(field);
    }

    internal static DecodedPinBlockFields DecodeFormat2(ReadOnlySpan<byte> block)
    {
        Span<byte> field = stackalloc byte[NibbleCount];
        Unpack(block, field);
        return DecodePinField(field, 2, FixedFillValidator);
    }

    internal static byte[] EncodeFormat3(string pin, string pan, string fillField)
    {
        Span<byte> pinField = stackalloc byte[NibbleCount];
        BuildVariableField(pin, fillField, 3, Format3FillValidator,
            "Fill field must contain the required number of hexadecimal digits in the range A through F.",
            pinField);

        Span<byte> panField = stackalloc byte[NibbleCount];
        BuildPanField(pan, panField);
        return XorAndPack(pinField, panField);
    }

    internal static DecodedPinBlockFields DecodeFormat3(ReadOnlySpan<byte> block, string pan)
    {
        Span<byte> pinField = stackalloc byte[NibbleCount];
        Unpack(block, pinField);

        Span<byte> panField = stackalloc byte[NibbleCount];
        BuildPanField(pan, panField);
        XorInPlace(pinField, panField);
        return DecodePinField(pinField, 3, Format3FillValidator, includeFillField: true);
    }

    private static void BuildPinField(string pin, byte format, byte fillNibble, Span<byte> destination)
    {
        ValidatePin(pin);
        destination.Fill(fillNibble);
        destination[0] = format;
        destination[1] = (byte)pin.Length;
        WriteDecimalNibbles(pin, destination[2..]);
    }

    private static void BuildVariableField(
        string pin,
        string variableField,
        byte format,
        Func<byte, bool> variableNibbleValidator,
        string errorMessage,
        Span<byte> destination)
    {
        ValidatePin(pin);
        ArgumentNullException.ThrowIfNull(variableField);
        if (variableField.Length != 14 - pin.Length)
            throw new ArgumentException(errorMessage, nameof(variableField));

        destination[0] = format;
        destination[1] = (byte)pin.Length;
        WriteDecimalNibbles(pin, destination[2..]);

        for (int index = 0; index < variableField.Length; index++)
        {
            int nibble = ParseHexNibble(variableField[index]);
            if (nibble < 0 || !variableNibbleValidator((byte)nibble))
                throw new ArgumentException(errorMessage, nameof(variableField));
            destination[2 + pin.Length + index] = (byte)nibble;
        }
    }

    private static void BuildPanField(string pan, Span<byte> destination)
    {
        ValidatePan(pan);
        destination.Clear();

        ReadOnlySpan<char> accountDigits = pan.AsSpan(0, pan.Length - 1);
        int digitCount = Math.Min(accountDigits.Length, 12);
        ReadOnlySpan<char> selectedDigits = accountDigits[^digitCount..];
        int destinationOffset = 4 + (12 - digitCount);
        WriteDecimalNibbles(selectedDigits, destination[destinationOffset..]);
    }

    private static DecodedPinBlockFields DecodePinField(
        ReadOnlySpan<byte> field,
        byte expectedFormat,
        Func<byte, bool> variableNibbleValidator,
        bool includeTransactionField = false,
        bool includeFillField = false)
    {
        if (field[0] != expectedFormat)
            throw new FormatException("PIN block has an unexpected format identifier.");

        int pinLength = field[1];
        if (pinLength is < MinimumPinLength or > MaximumPinLength)
            throw new FormatException("PIN block contains an invalid PIN length.");

        char[] pin = new char[pinLength];
        for (int index = 0; index < pinLength; index++)
        {
            byte nibble = field[index + 2];
            if (nibble > 9)
                throw new FormatException("PIN block contains a non-decimal PIN digit.");
            pin[index] = (char)('0' + nibble);
        }

        int variableLength = 14 - pinLength;
        ReadOnlySpan<byte> variableNibbles = field.Slice(2 + pinLength, variableLength);
        foreach (byte nibble in variableNibbles)
        {
            if (!variableNibbleValidator(nibble))
                throw new FormatException(expectedFormat == 3
                    ? "PIN block contains an invalid fill field."
                    : "PIN block contains an invalid fixed fill field.");
        }

        string? variableField = includeTransactionField || includeFillField
            ? ToHexString(variableNibbles)
            : null;
        return new DecodedPinBlockFields(
            new string(pin),
            includeTransactionField ? variableField : null,
            includeFillField ? variableField : null);
    }

    private static void ValidatePin(string pin)
    {
        ArgumentNullException.ThrowIfNull(pin);
        if (pin.Length is < MinimumPinLength or > MaximumPinLength || !ContainsOnlyDecimalDigits(pin))
            throw new ArgumentException("PIN must contain between 4 and 12 decimal digits.", nameof(pin));
    }

    internal static void ValidatePan(string pan)
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

    private static void WriteDecimalNibbles(ReadOnlySpan<char> digits, Span<byte> destination)
    {
        for (int index = 0; index < digits.Length; index++)
            destination[index] = (byte)(digits[index] - '0');
    }

    private static void Unpack(ReadOnlySpan<byte> block, Span<byte> destination)
    {
        if (block.Length != BlockLength)
            throw new ArgumentException("PIN block must contain exactly 8 bytes.", nameof(block));

        for (int index = 0; index < block.Length; index++)
        {
            destination[index * 2] = (byte)(block[index] >> 4);
            destination[index * 2 + 1] = (byte)(block[index] & 0x0F);
        }
    }

    private static byte[] Pack(ReadOnlySpan<byte> nibbles)
    {
        byte[] result = new byte[BlockLength];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)((nibbles[index * 2] << 4) | nibbles[index * 2 + 1]);
        return result;
    }

    private static byte[] XorAndPack(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        Span<byte> result = stackalloc byte[NibbleCount];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)(left[index] ^ right[index]);
        return Pack(result);
    }

    private static void XorInPlace(Span<byte> destination, ReadOnlySpan<byte> other)
    {
        for (int index = 0; index < destination.Length; index++)
            destination[index] ^= other[index];
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

    private static bool AnyHexValidator(byte nibble) => nibble <= 0x0F;
    private static bool FixedFillValidator(byte nibble) => nibble == 0x0F;
    private static bool Format3FillValidator(byte nibble) => nibble is >= 0x0A and <= 0x0F;
}
