using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;
using static CryptoScript.Serialization.SnapshotJson;

namespace CryptoScript.Serialization;

internal static class KeySnapshotCodec
{
    internal static KeyTypeDto WriteType(KeyType type) => new(type.Algorithm.ToString(), type.MaterialKind.ToString());
    internal static UsageDto WriteUsage(KeyUsagePolicy usage) => new(usage.Mode.ToString(), (int)usage.AllowedUsages);
    internal static T EnumValue<T>(string value, string error = "Invalid enum name.") where T : struct, Enum
    {
        Require(Enum.TryParse<T>(value, false, out var parsed) && Enum.IsDefined(parsed) &&
            parsed.ToString() == value, error);
        return parsed;
    }
    internal static KeyType ReadType(KeyTypeDto type) =>
        new(EnumValue<KeyAlgorithm>(type.Algorithm, "Unsupported key algorithm in CryptoScript v1."),
            EnumValue<KeyMaterialKind>(type.MaterialKind, "Unsupported key material kind in CryptoScript v1."));
    internal static KeyUsagePolicy ReadUsage(UsageDto usage) =>
        new(EnumValue<KeyUsageMode>(usage.Mode), (KeyUsage)usage.AllowedUsages);

    internal static (KeyMetadataDto, KeyPayloadDto) Encode(KeyVariableDeclaration key) =>
        (new("KEY", key.ValueFormat, "structured", key.Type?.Id, WriteType(key.KeyType),
            key.KeySizeInBits.Bits, key.KeySize, WriteUsage(key.Usage), key.DerivationMechanism,
            key.Kcv, key.Kcvc, key.Kcvk),
         new(key.Value, key.KeyAttributes.Select(a => new AttributeDto(a.ID, a.Length, a.Data)).ToList(), SourceDto.Capture(key)));

    internal static KeyVariableDeclaration Decode(KeyMetadataDto meta, KeyPayloadDto payload)
    {
        var key = new KeyVariableDeclaration
        {
            Type = new CryptoTypeKey(), ValueFormat = meta.Format, Value = payload.Material,
            KeyType = ReadType(meta.KeyType), Usage = ReadUsage(meta.Usage),
            KeySizeInBits = new KeySize(meta.KeySizeInBits), KeySize = meta.KeySize,
            DerivationMechanism = meta.DerivationMechanism,
            KeyAttributes = payload.Attributes.Select(a =>
                new CryptoAlgorithm.WRAPPERS.OptionalBlock { ID = a.Id, Length = a.Length, Data = a.Data }).ToList()
        };
        Require(key.KeySizeInBits.Bits == meta.KeySizeInBits, "Contradictory key sizes.");
        ValidateMaterial(key);
        Require(meta.Kcv == key.Kcv && meta.Kcvc == key.Kcvc && meta.Kcvk == key.Kcvk,
            "Key check values do not match the key.");
        return key;
    }

    private static void ValidateMaterial(KeyVariableDeclaration key)
    {
        string format = key.ValueFormat.Length == 0 ? FormatConversions.ParseString(key.Value) : key.ValueFormat;
        byte[] bytes;
        if (format == FormatConversions.HEX)
        {
            string hex = key.Value;
            if (hex.StartsWith("0x(", StringComparison.Ordinal))
            {
                Require(hex.EndsWith(')'), "Invalid HEX key.");
                hex = hex[3..^1];
            }
            bytes = Convert.FromHexString(hex);
        }
        else
        {
            Require(format == FormatConversions.B64 || format == FormatConversions.STR, "Unsupported key encoding.");
            if (format == FormatConversions.B64 && key.Value.StartsWith("b64(", StringComparison.Ordinal))
                Require(key.Value.EndsWith(')'), "Invalid Base64 key.");
            if (format == FormatConversions.STR && key.Value.StartsWith('"'))
                Require(key.Value.Length >= 2 && key.Value.EndsWith('"'), "Invalid string key.");
            bytes = FormatConversions.ToByteArray(key.Value, format);
        }
        try
        {
            Require(bytes.Length > 0, "Empty key material.");
            ulong bits = checked((ulong)bytes.Length * 8);
            switch (key.KeyType.Algorithm)
            {
                case KeyAlgorithm.Aes:
                    Require(bytes.Length is 16 or 24 or 32, "Invalid AES key size.");
                    break;
                case KeyAlgorithm.Tdea:
                    Require(bytes.Length is 16 or 24, "Invalid TDEA key size.");
                    break;
            }
            Require(!key.KeySizeInBits.IsKnown || key.KeySizeInBits.Bits == bits, "Key size does not match material.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
