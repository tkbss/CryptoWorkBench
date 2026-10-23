using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.FileEncryption;

internal static class FileMacCoordinator
{
    internal static bool Supports(MechanismRegistryEntry entry) => entry.Supports(CryptoScriptFunction.Mac) &&
        (entry.GeneratedKeyAlgorithm == KeyAlgorithm.Hmac || entry.CanonicalName is
            "AES-CMAC" or "DES3-CMAC" or "AES-GMAC" or "AES-CBC-MAC" or "DES3-CBC" or "DES3-RETAIL");

    internal static StringVariableDeclaration Mac(AlgorithmCallArguments arguments,
        PathVariableDeclaration path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var macState = Prepare(arguments);
        string sourcePath = SnapshotByteFilePublisher.ValidatePath(path);
        using var publication = new FilePublication(Path.GetDirectoryName(sourcePath)!);
        using var source = publication.OpenSource(Path.GetFileName(sourcePath));
        return Read(source, macState, token);
    }

    // Stream seam tests failures and cancellation without replacing secure production opening.
    internal static StringVariableDeclaration MacStream(AlgorithmCallArguments arguments, Stream source,
        CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var state = Prepare(arguments);
        return Read(source, state, token);
    }

    private static MacState Prepare(AlgorithmCallArguments arguments)
    {
        if (arguments.Arguments[0].SourceVariable is not ParameterVariableDeclaration parameters)
            throw new ArgumentException("Mac(PATH) requires a PARAM variable reference.");
        if (!MechanismRegistry.TryGet(parameters.Mechanism, out var entry) || !Supports(entry!))
            throw new ArgumentException("Unsupported Mac(PATH) mechanism.");
        MechanismParameterContractValidator.Validate(parameters.Mechanism, CryptoScriptFunction.Mac, parameters);
        var key = AlgorithmArgumentResolver.ResolveKey(arguments.Arguments[1]);
        byte[] bytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
        try
        {
            var algorithm = entry!.GeneratedKeyAlgorithm!.Value;
            if (algorithm == KeyAlgorithm.Tdea) DES3.ValidateKeyLength(bytes);
            KeyMetadataValidator.Validate(key, algorithm, KeyUsage.MacGenerate,
                "Mac", parameters.Mechanism, arguments.DiagnosticSink);
            if (algorithm == KeyAlgorithm.Tdea)
            {
                Des3KeyQuality.Report(bytes, key.Id, "Mac", parameters.Mechanism, arguments.DiagnosticSink);
                DES3.ValidateUsableKey(bytes);
            }
            if (algorithm == KeyAlgorithm.Aes) AES_ECB.ValidateKeyLength(bytes);
            if (entry.CanonicalName is "AES-CBC-MAC" or "DES3-CBC" or "DES3-RETAIL")
                return new MacState(new StreamingBlockMac(parameters, bytes));
            if (entry.CanonicalName == "AES-GMAC")
            {
                string value = parameters.GetParameter("NONCE");
                byte[] nonce = FormatConversions.ToByteArray(value, FormatConversions.ParseString(value));
                try
                {
                    var gmac = new GMac(new GcmBlockCipher(new AesEngine()));
                    gmac.Init(new ParametersWithIV(new KeyParameter(bytes), nonce));
                    return new MacState(gmac);
                }
                finally { CryptographicOperations.ZeroMemory(nonce); }
            }
            IMac mac = algorithm switch
            {
                KeyAlgorithm.Aes => new CMac(new AesEngine()),
                KeyAlgorithm.Tdea => new CMac(new DesEdeEngine()),
                _ => new HMac(DigestFactory.Create(entry.CanonicalName))
            };
            mac.Init(new KeyParameter(bytes));
            return new MacState(mac);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static StringVariableDeclaration Read(Stream source, MacState mac, CancellationToken token)
    {
        byte[] buffer = new byte[65536];
        byte[] result = new byte[mac.Size];
        try
        {
            mac.Begin(source);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int count = source.Read(buffer, 0, buffer.Length);
                token.ThrowIfCancellationRequested();
                if (count == 0) { mac.CheckLength(source, true); break; }
                mac.Update(buffer, count);
                mac.CheckLength(source, false);
            }
            mac.Finish(result);
            mac.CheckLength(source, true);
            token.ThrowIfCancellationRequested();
            string value = FormatConversions.ByteArrayToHexString(result);
            return new StringVariableDeclaration { Value = value,
                ValueFormat = FormatConversions.ParseString(value), Type = new CryptoTypeVar() };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(result);
        }
    }

    // Bouncy Castle does not expose key schedule erasure; Reset is not an erasure guarantee.
    private sealed class MacState : IDisposable
    {
        private readonly IMac? mac;
        private readonly StreamingBlockMac? block;
        internal MacState(IMac mac) => this.mac = mac;
        internal MacState(StreamingBlockMac block) => this.block = block;
        internal int Size => mac?.GetMacSize() ?? block!.MacSize;
        internal void Begin(Stream source) => block?.Begin(source);
        internal void CheckLength(Stream source, bool end) => block?.CheckLength(source, end);
        internal void Update(byte[] bytes, int count)
        {
            if (mac is not null) mac.BlockUpdate(bytes, 0, count); else block!.Update(bytes, count);
        }
        internal void Finish(byte[] result)
        {
            if (mac is not null) mac.DoFinal(result, 0); else block!.Finish(result);
        }
        public void Dispose() { mac?.Reset(); block?.Dispose(); }
    }
}
