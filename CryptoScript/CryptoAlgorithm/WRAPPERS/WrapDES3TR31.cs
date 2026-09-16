using System.Text;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>CryptoScript adapter; binding operations remain in the version-specific cores.</summary>
public class WrapDES3TR31 : CryptoAlgorithm
{
    private const string MechanismName = "WRAP-DES3-TR31";

    public override StringVariableDeclaration Wrap(string[] parameters)
    {
        var p = new ParameterVariableDeclaration();
        p.SetInstance(parameters[0]);
        string header = FormatConversions.ToString(p.GetParameter("#BLKH"));
        char version = RequireSupportedVersion(header);
        byte[] kbpk = GetKey(parameters[1]);
        byte[] key = GetKey(parameters[2]);
        DES3.DES3.ValidateKeyLength(key);
        string random = p.GetParameter("#RND");
        byte[] data = Tr31ConfidentialData.Create(key, 8, 24 - key.Length,
            string.IsNullOrEmpty(random) ? null : FormatConversions.HexStringToByteArray(random)).ToArray();
        var wrapped = version == 'B'
            ? Tr31VersionBWrap.Wrap(header, kbpk, data)
            : Tr31TdeaVariantWrap.Wrap(header, kbpk, data);
        return new StringVariableDeclaration
        {
            Value = new TR31String(header, wrapped.Ciphertext, wrapped.Mac).ToString(),
            ValueFormat = FormatConversions.TR31
        };
    }

    public override KeyVariableDeclaration Unwrap(string[] parameters)
    {
        string input = parameters[2];
        string wire;
        char version;
        if (FormatConversions.ParseString(input) == FormatConversions.TR31)
        {
            var composite = TR31String.FromString(input);
            version = RequireSupportedVersion(composite.Block);
            RequireAuthenticationValueLength(version, composite.Mac);
            wire = composite.Block + Convert.ToHexString(composite.Cryptogram) + Convert.ToHexString(composite.Mac);
        }
        else if (FormatConversions.ParseString(input) == FormatConversions.STR)
        {
            wire = FormatConversions.ToString(input);
            version = RequireSupportedVersion(wire);
        }
        else
            throw new ArgumentException("Expected a TR-31 string or a quoted complete key block.");

        var block = TR31Block.FromString(wire);
        string header = Encoding.ASCII.GetString(block.HeaderDataToMac!);
        byte[] kbpk = GetKey(parameters[1]);
        byte[] key = version == 'B'
            ? Tr31VersionBUnwrap.Unwrap(header, block.Cryptogram!, block.Mac!, kbpk)
            : Tr31TdeaVariantUnwrap.Unwrap(header, block.Cryptogram!, block.Mac!, kbpk);
        string value = FormatConversions.ByteArrayToHexString(key);
        return new KeyVariableDeclaration
        {
            Value = value, KeyValue = value, KeySize = (key.Length * 8).ToString(),
            ValueFormat = FormatConversions.HEX, Type = new CryptoTypeKey(),
            Mechanism = MechanismName, KeyAttributes = block.HeaderOptionalBlocks()
        };
    }

    private static void RequireAuthenticationValueLength(char version, byte[] authenticationValue)
    {
        int expectedLength = TR31Block.GetAuthenticationValueLength(version);
        if (authenticationValue.Length != expectedLength)
            throw new ArgumentException(
                $"TR-31 version {version} authentication value must contain exactly {expectedLength} bytes.",
                nameof(authenticationValue));
    }

    private static byte[] GetKey(string value)
    {
        // FunctionCall.Call passes variable.Value, as in the existing AES adapter.
        var key = VariableDictionary.Instance().GetVariables().OfType<KeyVariableDeclaration>()
            .FirstOrDefault(k => k.Value == value)
            ?? throw new ArgumentException("Key variable not found.");
        return FormatConversions.HexStringToByteArray(key.KeyValue);
    }

    private static char RequireSupportedVersion(string header)
    {
        if (string.IsNullOrEmpty(header))
            throw new ArgumentException("TR-31 header is required.");
        if (header[0] != 'A' && header[0] != 'B' && header[0] != 'C')
            throw new NotSupportedException($"{MechanismName} does not support version {header[0]}; only versions A, B and C are supported.");
        return header[0];
    }
}
