using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScript.CryptoAlgorithm.PINBLOCK;

public enum PinBlockCipherFamily
{
    Des3,
    Aes
}

public sealed class PinBlockAlgorithm : CryptoAlgorithm
{
    private const int ArgumentCount = 3;
    private const int Des3PinBlockLength = 8;
    private const int AesPinBlockLength = 16;
    private const int Format4RandomByteCount = 8;
    private readonly Func<int, int> randomInt32;
    private readonly Func<int, byte[]> randomBytes;

    public PinBlockAlgorithm(string mechanismName, PinBlockCipherFamily cipherFamily)
        : this(mechanismName, cipherFamily, RandomNumberGenerator.GetInt32, RandomNumberGenerator.GetBytes)
    {
    }

    internal PinBlockAlgorithm(
        string mechanismName,
        PinBlockCipherFamily cipherFamily,
        Func<int, int> randomInt32)
        : this(mechanismName, cipherFamily, randomInt32, RandomNumberGenerator.GetBytes)
    {
    }

    internal PinBlockAlgorithm(
        string mechanismName,
        PinBlockCipherFamily cipherFamily,
        Func<int, int> randomInt32,
        Func<int, byte[]> randomBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanismName);
        ArgumentNullException.ThrowIfNull(randomInt32);
        ArgumentNullException.ThrowIfNull(randomBytes);
        MechanismName = mechanismName;
        CipherFamily = cipherFamily;
        this.randomInt32 = randomInt32;
        this.randomBytes = randomBytes;
    }

    public string MechanismName { get; }
    public PinBlockCipherFamily CipherFamily { get; }

    public override StringVariableDeclaration Wrap(string[] parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return WrapCore(AlgorithmCallArguments.FromValues(parameters), legacyCall: true);
    }

    public override StringVariableDeclaration Wrap(AlgorithmCallArguments parameters) =>
        WrapCore(parameters, legacyCall: false);

    public override VariableDeclaration Unwrap(string[] parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return UnwrapCore(AlgorithmCallArguments.FromValues(parameters), legacyCall: true);
    }

    public override VariableDeclaration Unwrap(AlgorithmCallArguments parameters) =>
        UnwrapCore(parameters, legacyCall: false);

    private StringVariableDeclaration WrapCore(AlgorithmCallArguments arguments, bool legacyCall)
    {
        EnsureImplemented(CryptoScriptFunction.Wrap);
        PinBlockArguments resolved = ResolveArguments(arguments, legacyCall, requiresParameterIdentity: MutatesParameters);
        string pin = GetPin(resolved.Data);

        if (IsFormat4)
            return WrapFormat4(resolved, pin);

        string? outputName = null;
        string? outputValue = null;
        byte[] clearBlock = Format switch
        {
            0 => PinBlockFieldCodec.EncodeFormat0(pin, GetHexNibbles(resolved.Parameters, "PAN")),
            1 => EncodeFormat1(resolved.Parameters, pin, out outputName, out outputValue),
            2 => PinBlockFieldCodec.EncodeFormat2(pin),
            3 => EncodeFormat3(resolved.Parameters, pin, out outputName, out outputValue),
            _ => throw NotImplemented(CryptoScriptFunction.Wrap)
        };

        byte[] ciphertext = DES3_ECB.EncryptNoPadding(resolved.Key, clearBlock);
        StringVariableDeclaration result = CreateBinaryResult(ciphertext);
        ApplyOutputParameter(resolved.Parameters, outputName, outputValue);
        return result;
    }

    private StringVariableDeclaration UnwrapCore(AlgorithmCallArguments arguments, bool legacyCall)
    {
        EnsureImplemented(CryptoScriptFunction.Unwrap);
        PinBlockArguments resolved = ResolveArguments(arguments, legacyCall, requiresParameterIdentity: MutatesParameters);
        if (IsFormat4)
            return UnwrapFormat4(resolved);

        byte[] ciphertext = GetPinBlock(resolved.Data, Des3PinBlockLength);
        byte[] clearBlock = DES3_ECB.DecryptNoPadding(resolved.Key, ciphertext);

        DecodedPinBlockFields decoded = Format switch
        {
            0 => PinBlockFieldCodec.DecodeFormat0(clearBlock, GetHexNibbles(resolved.Parameters, "PAN")),
            1 => PinBlockFieldCodec.DecodeFormat1(clearBlock),
            2 => PinBlockFieldCodec.DecodeFormat2(clearBlock),
            3 => PinBlockFieldCodec.DecodeFormat3(clearBlock, GetHexNibbles(resolved.Parameters, "PAN")),
            _ => throw NotImplemented(CryptoScriptFunction.Unwrap)
        };

        string? outputName = Format switch
        {
            1 => "TRANSACTION",
            3 => "FILL",
            _ => null
        };
        string? outputValue = Format switch
        {
            1 => decoded.TransactionField,
            3 => decoded.FillField,
            _ => null
        };
        StringVariableDeclaration result = CreatePinResult(decoded.Pin);
        ApplyOutputParameter(resolved.Parameters, outputName, outputValue);
        return result;
    }

    private PinBlockArguments ResolveArguments(
        AlgorithmCallArguments arguments,
        bool legacyCall,
        bool requiresParameterIdentity)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Arguments.Count != ArgumentCount)
            throw new ArgumentException("PIN-block operations require exactly three arguments.", nameof(arguments));

        AlgorithmCallArgument parameterArgument = arguments.Arguments[0];
        if (!legacyCall && parameterArgument.SourceVariable is not ParameterVariableDeclaration)
            throw new ArgumentException("PIN-block operations require a PARAM variable as the first argument.");

        ParameterVariableDeclaration parameters = AlgorithmArgumentResolver.ResolveParameter(parameterArgument);
        if (!parameters.Mechanism.Equals(MechanismName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("PIN-block parameters do not match the selected mechanism.");
        if (requiresParameterIdentity && !HasReferencableParameterIdentity(parameterArgument, parameters))
            throw new ArgumentException("This PIN-block operation requires a referencable PARAM variable.");

        KeyVariableDeclaration key = ResolveKey(arguments.Arguments[1], legacyCall);
        byte[] keyBytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
        if (CipherFamily == PinBlockCipherFamily.Aes)
        {
            if (key.KeyType.Algorithm is not (KeyAlgorithm.Aes or KeyAlgorithm.Unknown))
                throw new ArgumentException("AES PIN-block mechanisms require an AES key.");
            AES_ECB.ValidateKeyLength(keyBytes);
        }
        else
        {
            if (key.KeyType.Algorithm is not (KeyAlgorithm.Tdea or KeyAlgorithm.Unknown))
                throw new ArgumentException("DES3 PIN-block mechanisms require a DES3 key.");
            Des3Algorithm.ValidateKeyLength(keyBytes);
            Des3Algorithm.ValidateUsableKey(keyBytes);
        }

        StringVariableDeclaration data = ResolveData(arguments.Arguments[2], legacyCall);
        return new PinBlockArguments(parameters, keyBytes, data);
    }

    private static KeyVariableDeclaration ResolveKey(AlgorithmCallArgument argument, bool legacyCall)
    {
        if (argument.SourceVariable is not null)
        {
            if (argument.SourceVariable.Type is not CryptoTypeKey)
                throw new ArgumentException("PIN-block operations require a KEY variable as the second argument.");
            if (argument.SourceVariable is KeyVariableDeclaration sourceKey)
                return sourceKey;
            return new KeyVariableDeclaration
            {
                Value = argument.SourceVariable.Value,
                ValueFormat = argument.SourceVariable.ValueFormat,
                Type = new CryptoTypeKey()
            };
        }
        if (!legacyCall)
            throw new ArgumentException("PIN-block operations require a KEY variable as the second argument.");

        string value = argument.Value ?? throw new ArgumentException("Missing PIN-block key argument.");
        if (VariableDictionary.Instance().Get(value) is KeyVariableDeclaration declared)
            return declared;
        if (FormatConversions.ParseString(value) == FormatConversions.HEX)
        {
            return new KeyVariableDeclaration
            {
                Value = value,
                ValueFormat = FormatConversions.HEX,
                Type = new CryptoTypeKey()
            };
        }
        if (FormatConversions.ParseString(value) == FormatConversions.JSO)
            return KeyVariableDeclaration.Deserialize(value);
        throw new ArgumentException("PIN-block operations require a KEY argument.");
    }

    private static StringVariableDeclaration ResolveData(AlgorithmCallArgument argument, bool legacyCall)
    {
        if (argument.SourceVariable is not null)
        {
            if (argument.SourceVariable is not StringVariableDeclaration sourceData ||
                sourceData.Type is not CryptoTypeVar)
            {
                throw new ArgumentException("PIN-block operations require a VAR variable as the third argument.");
            }
            return sourceData;
        }
        if (!legacyCall)
            throw new ArgumentException("PIN-block operations require a VAR variable as the third argument.");

        string value = argument.Value ?? throw new ArgumentException("Missing PIN-block data argument.");
        if (VariableDictionary.Instance().Get(value) is StringVariableDeclaration declared &&
            declared.Type is CryptoTypeVar)
        {
            return declared;
        }

        string format = FormatConversions.ParseString(value);
        if (format == FormatConversions.STR || format == FormatConversions.HEX || format == FormatConversions.B64)
            return new StringVariableDeclaration { Value = value, ValueFormat = format, Type = new CryptoTypeVar() };
        throw new ArgumentException("PIN-block operations require a VAR argument.");
    }

    private static bool HasReferencableParameterIdentity(
        AlgorithmCallArgument argument,
        ParameterVariableDeclaration parameters)
    {
        if (argument.SourceVariable is ParameterVariableDeclaration source &&
            ReferenceEquals(source, parameters) &&
            !string.IsNullOrWhiteSpace(source.Id))
        {
            return true;
        }

        return argument.SourceVariable is null &&
               argument.Value is not null &&
               ReferenceEquals(VariableDictionary.Instance().Get(argument.Value), parameters);
    }

    private byte[] EncodeFormat1(
        ParameterVariableDeclaration parameters,
        string pin,
        out string? outputName,
        out string? outputValue)
    {
        outputName = "TRANSACTION";
        outputValue = GetOptionalHexNibbles(parameters, outputName);
        outputValue ??= GenerateNibbles(14 - pin.Length, 16, 0);
        return PinBlockFieldCodec.EncodeFormat1(pin, outputValue);
    }

    private byte[] EncodeFormat3(
        ParameterVariableDeclaration parameters,
        string pin,
        out string? outputName,
        out string? outputValue)
    {
        outputName = "FILL";
        outputValue = GetOptionalHexNibbles(parameters, outputName);
        outputValue ??= GenerateNibbles(14 - pin.Length, 6, 10);
        return PinBlockFieldCodec.EncodeFormat3(pin, GetHexNibbles(parameters, "PAN"), outputValue);
    }

    private StringVariableDeclaration WrapFormat4(PinBlockArguments arguments, string pin)
    {
        string pan = GetHexNibbles(arguments.Parameters, "PAN");
        string randomField = (GetOptionalHexNibbles(arguments.Parameters, "RANDOM") ?? GenerateRandomField())
            .ToUpperInvariant();
        byte[] pinField = PinBlockFormat4FieldCodec.EncodePinField(pin, randomField);
        byte[] panField = PinBlockFormat4FieldCodec.EncodePanField(pan);
        byte[] firstAesResult = AES_ECB.EncryptNoPadding(arguments.Key, pinField);
        byte[] xorResult = Xor(firstAesResult, panField);
        byte[] ciphertext = AES_ECB.EncryptNoPadding(arguments.Key, xorResult);
        StringVariableDeclaration result = CreateBinaryResult(ciphertext);
        ApplyOutputParameter(arguments.Parameters, "RANDOM", randomField);
        return result;
    }

    private StringVariableDeclaration UnwrapFormat4(PinBlockArguments arguments)
    {
        byte[] ciphertext = GetPinBlock(arguments.Data, AesPinBlockLength);
        string pan = GetHexNibbles(arguments.Parameters, "PAN");
        byte[] panField = PinBlockFormat4FieldCodec.EncodePanField(pan);
        byte[] xorResult = AES_ECB.DecryptNoPadding(arguments.Key, ciphertext);
        byte[] firstAesResult = Xor(xorResult, panField);
        byte[] pinField = AES_ECB.DecryptNoPadding(arguments.Key, firstAesResult);
        DecodedPinBlockFormat4Fields decoded = PinBlockFormat4FieldCodec.DecodePinField(pinField);
        StringVariableDeclaration result = CreatePinResult(decoded.Pin);
        ApplyOutputParameter(arguments.Parameters, "RANDOM", decoded.RandomField);
        return result;
    }

    private string GenerateRandomField()
    {
        byte[] generated = randomBytes(Format4RandomByteCount);
        if (generated is null || generated.Length != Format4RandomByteCount)
            throw new InvalidOperationException("The random byte source returned an invalid value.");
        return Convert.ToHexString(generated);
    }

    private string GenerateNibbles(int count, int exclusiveUpperBound, int offset)
    {
        char[] result = new char[count];
        for (int index = 0; index < result.Length; index++)
        {
            int value = randomInt32(exclusiveUpperBound);
            if (value < 0 || value >= exclusiveUpperBound)
                throw new InvalidOperationException("The random number source returned an out-of-range value.");
            value += offset;
            result[index] = (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }
        return new string(result);
    }

    private static string GetPin(StringVariableDeclaration data)
    {
        if (data.ValueFormat != FormatConversions.HEX)
            throw new ArgumentException("PIN input must be a hexadecimal nibble sequence stored in a VAR variable.");

        string pin = FormatConversions.HexStringToString(data.Value);
        if (pin.Length is < 4 or > 12 || pin.Any(character => character is < '0' or > '9'))
            throw new ArgumentException("PIN input must contain between 4 and 12 decimal nibbles.");
        return pin;
    }

    private static byte[] GetPinBlock(StringVariableDeclaration data, int expectedLength)
    {
        if (data.ValueFormat != FormatConversions.HEX && data.ValueFormat != FormatConversions.B64)
            throw new ArgumentException("Encrypted PIN-block input must be hexadecimal or Base64 binary data.");
        byte[] result = FormatConversions.ToByteArray(data.Value, data.ValueFormat);
        if (result.Length != expectedLength)
            throw new ArgumentException($"Encrypted PIN block must contain exactly {expectedLength} bytes.");
        return result;
    }

    private static byte[] Xor(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            throw new ArgumentException("PIN-block fields must have identical lengths.");
        byte[] result = new byte[left.Length];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)(left[index] ^ right[index]);
        return result;
    }

    private static string GetHexNibbles(ParameterVariableDeclaration parameters, string name)
    {
        string value = parameters.GetParameter(name);
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"PIN-block parameter #{name} is required.");
        return ParseHexNibbles(value, name);
    }

    private static string? GetOptionalHexNibbles(ParameterVariableDeclaration parameters, string name)
    {
        string value = parameters.GetParameter(name);
        return string.IsNullOrEmpty(value) ? null : ParseHexNibbles(value, name);
    }

    private static string ParseHexNibbles(string value, string name)
    {
        if (FormatConversions.ParseString(value) != FormatConversions.HEX)
            throw new ArgumentException($"PIN-block parameter #{name} must be hexadecimal.");
        return FormatConversions.HexStringToString(value);
    }

    private static void ApplyOutputParameter(
        ParameterVariableDeclaration parameters,
        string? outputName,
        string? outputValue)
    {
        if (outputName is null)
            return;
        if (outputValue is null)
            throw new InvalidOperationException("PIN-block output parameter was not produced.");
        parameters.SetParameter(outputName, $"0x({outputValue})");
    }

    private static StringVariableDeclaration CreateBinaryResult(byte[] value)
    {
        string encoded = FormatConversions.ByteArrayToHexString(value);
        return new StringVariableDeclaration
        {
            Value = encoded,
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeVar()
        };
    }

    private static StringVariableDeclaration CreatePinResult(string pin) => new()
    {
        Value = $"0x({pin})",
        ValueFormat = FormatConversions.HEX,
        Type = new CryptoTypeVar()
    };

    private int Format => MechanismName[^1] - '0';
    private bool IsFormat4 => CipherFamily == PinBlockCipherFamily.Aes && Format == 4;
    private bool MutatesParameters => Format is 1 or 3 || IsFormat4;

    private void EnsureImplemented(CryptoScriptFunction function)
    {
        bool implemented = CipherFamily == PinBlockCipherFamily.Des3 && Format is >= 0 and <= 3 || IsFormat4;
        if (!implemented)
            throw NotImplemented(function);
    }

    private NotImplementedException NotImplemented(CryptoScriptFunction function) =>
        new($"{MechanismName} {function} is not implemented.");

    private sealed record PinBlockArguments(
        ParameterVariableDeclaration Parameters,
        byte[] Key,
        StringVariableDeclaration Data);
}
