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
        RequireVersionB(header);
        byte[] kbpk = GetKey(parameters[1]);
        byte[] key = GetKey(parameters[2]);
        DES3.DES3.ValidateKeyLength(key);
        string random = p.GetParameter("#RND");
        byte[] data = Tr31ConfidentialData.Create(key, 8, 24 - key.Length,
            string.IsNullOrEmpty(random) ? null : FormatConversions.HexStringToByteArray(random)).ToArray();
        var wrapped = Tr31VersionBWrap.Wrap(header, kbpk, data);
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
        if (FormatConversions.ParseString(input) == FormatConversions.TR31)
        {
            var composite = TR31String.FromString(input);
            wire = composite.Block + Convert.ToHexString(composite.Cryptogram) + Convert.ToHexString(composite.Mac);
        }
        else if (FormatConversions.ParseString(input) == FormatConversions.STR)
            wire = FormatConversions.ToString(input);
        else
            throw new ArgumentException("Expected a TR-31 string or a quoted complete key block.");

        RequireVersionB(wire);
        var block = TR31Block.FromString(wire);
        byte[] key = Tr31VersionBUnwrap.Unwrap(Encoding.ASCII.GetString(block.HeaderDataToMac!),
            block.Cryptogram!, block.Mac!, GetKey(parameters[1]));
        string value = FormatConversions.ByteArrayToHexString(key);
        return new KeyVariableDeclaration
        {
            Value = value, KeyValue = value, KeySize = (key.Length * 8).ToString(),
            ValueFormat = FormatConversions.HEX, Type = new CryptoTypeKey(),
            Mechanism = MechanismName, KeyAttributes = block.HeaderOptionalBlocks()
        };
    }

    private static byte[] GetKey(string value)
    {
        // FunctionCall.Call passes variable.Value, as in the existing AES adapter.
        var key = VariableDictionary.Instance().GetVariables().OfType<KeyVariableDeclaration>()
            .FirstOrDefault(k => k.Value == value)
            ?? throw new ArgumentException("Key variable not found.");
        return FormatConversions.HexStringToByteArray(key.KeyValue);
    }

    private static void RequireVersionB(string header)
    {
        if (string.IsNullOrEmpty(header))
            throw new ArgumentException("TR-31 header is required.");
        if (header[0] != 'B')
            throw new NotSupportedException($"{MechanismName} does not yet support version {header[0]}; only version B is supported.");
    }
}
