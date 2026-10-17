using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.EMV;

public sealed class MAC_EMV_ARPC_METHOD_1 : CryptoAlgorithm
{
    internal const string MechanismName = "MAC-EMV-ARPC-METHOD-1";

    public override ParameterVariableDeclaration GenerateParameters(string mechanism) =>
        GenerateParameters(mechanism, Array.Empty<string>());

    public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters) =>
        EmvAcArgumentContract.GenerateParameters(MechanismName, mechanism, parameters);

    public override StringVariableDeclaration Mac(AlgorithmCallArguments parameters) =>
        EmvArpcArgumentContract.Execute(MechanismName, parameters, method2: false);

    public override StringVariableDeclaration Mac(string[] parameters) =>
        throw new ArgumentException("EMV ARPC requires structured arguments with genuine PARAM and KEY references.");
}

internal static class EmvArpcArgumentContract
{
    internal static StringVariableDeclaration Execute(string mechanism, AlgorithmCallArguments arguments, bool method2)
    {
        var (parameters, key) = EmvAcArgumentContract.ResolveReferences(mechanism, arguments);
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Unknown, KeyUsage.MacGenerate,
            "Mac", mechanism, arguments.DiagnosticSink);
        string arqcValue = EmvAcArgumentContract.ResolveData(arguments.Arguments[2], "ARQC");
        byte[]? keyBytes = null;
        byte[]? arqc = null;
        byte[]? control = null;
        byte[]? proprietaryData = null;
        byte[]? arpc = null;
        byte[]? iad = null;
        try
        {
            arqc = EmvAcArgumentContract.DecodeHex(arqcValue, "ARQC");
            if (arqc.Length != 8)
                throw new ArgumentException("EMV ARQC must contain exactly eight bytes.");
            control = EmvAcArgumentContract.DecodeHex(parameters.GetParameter(method2 ? "#CSU" : "#ARC"),
                method2 ? "CSU" : "ARC");
            if (method2 && parameters.GetParameters().ContainsKey("#ADATA"))
                proprietaryData = EmvAcArgumentContract.DecodeHex(parameters.GetParameter("#ADATA"), "Proprietary data");
            keyBytes = EmvAcArgumentContract.DecodeSessionKey(key);
            if (key.KeyType.Algorithm == KeyAlgorithm.Tdea)
                CryptoScript.CryptoAlgorithm.DES3.Des3KeyQuality.Report(keyBytes, key.Id,
                    "Mac", mechanism, arguments.DiagnosticSink);

            // The validated core alone implements XOR, ECB, MAC and IAD concatenation.
            if (method2)
            {
                arpc = EmvApplicationCryptogram.ComputeArpcMethod2ForAnalysis(key.KeyType.Algorithm, keyBytes, arqc, control, proprietaryData);
                if (parameters.GetParameter("#VARIANT") == "\"IAD\"")
                    iad = EmvApplicationCryptogram.BuildIssuerAuthenticationData(arpc, control, proprietaryData);
            }
            else
                arpc = EmvApplicationCryptogram.ComputeArpcMethod1ForAnalysis(key.KeyType.Algorithm, keyBytes, arqc, control);

            return new StringVariableDeclaration
            {
                Type = new CryptoTypeVar(), ValueFormat = FormatConversions.HEX,
                Value = FormatConversions.ByteArrayToHexString(iad ?? arpc)
            };
        }
        finally
        {
            if (keyBytes is not null) CryptographicOperations.ZeroMemory(keyBytes);
            if (arqc is not null) CryptographicOperations.ZeroMemory(arqc);
            if (control is not null) CryptographicOperations.ZeroMemory(control);
            if (proprietaryData is not null) CryptographicOperations.ZeroMemory(proprietaryData);
            if (arpc is not null) CryptographicOperations.ZeroMemory(arpc);
            if (iad is not null) CryptographicOperations.ZeroMemory(iad);
        }
    }
}
