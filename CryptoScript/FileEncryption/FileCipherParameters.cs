using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.FileEncryption;

internal static class FileCipherParameters
{
    internal static (ParameterVariableDeclaration Parameters, KeyVariableDeclaration Key) Resolve(
        AlgorithmCallArguments arguments, bool encrypt)
    {
        string function = encrypt ? "Encrypt" : "Decrypt";
        if (arguments.Arguments[0].SourceVariable is not ParameterVariableDeclaration p)
            throw new ArgumentException(function + "(PATH) requires a PARAM variable reference.");
        string mechanism = p.Mechanism.ToUpperInvariant();
        if (mechanism is not ("AES-CBC" or "AES-ECB" or "AES-CTR" or "AES-GCM" or "AES-CCM" or "DES3-CBC" or "DES3-ECB"))
            throw new ArgumentException("Unsupported file encryption mechanism.");
        MechanismParameterContractValidator.Validate(mechanism,
            encrypt ? CryptoScriptFunction.Encrypt : CryptoScriptFunction.Decrypt, p);
        if (p.OriginalExplicitPadding is not null &&
            mechanism is "AES-ECB" or "AES-GCM" &&
            !p.OriginalExplicitPadding.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Explicit padding is not supported for this file mechanism.");
        var key = AlgorithmArgumentResolver.ResolveKey(arguments.Arguments[1]);
        bool des = mechanism.StartsWith("DES3-", StringComparison.Ordinal);
        KeyMetadataValidator.Validate(key, des ? KeyAlgorithm.Tdea : KeyAlgorithm.Aes,
            encrypt ? KeyUsage.Encrypt : KeyUsage.Decrypt, function, mechanism, arguments.DiagnosticSink);
        byte[] bytes = FormatConversions.ToByteArray(key.Value, key.ValueFormat);
        try
        {
            if (des) Des3KeyQuality.Report(bytes, key.Id, function, mechanism, arguments.DiagnosticSink);
            else if (bytes.Length is not (16 or 24 or 32)) throw new CryptographicException("AES key must contain 16, 24 or 32 bytes.");
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "#MECH" };
        if (mechanism.EndsWith("CBC") || mechanism.EndsWith("ECB")) allowed.Add("#PAD");
        if (mechanism.EndsWith("CBC")) allowed.Add("#IV");
        if (mechanism is "AES-CTR" or "AES-GCM" or "AES-CCM") allowed.Add("#NONCE");
        if (mechanism == "AES-CTR") allowed.Add("#COUNTER");
        if (mechanism is "AES-GCM" or "AES-CCM") allowed.Add("#ADATA");
        // The existing AES parameter factory emits inert IV/PAD placeholders for ECB/GCM.
        // Preserve those defaults without permitting actual IVs or padding on stream modes.
        if (p.GetParameters().Any(item => !allowed.Contains(item.Key) &&
            !(item.Key == "#IV" && item.Value.Length == 0 && !Explicit(p, "IV")) &&
            !(item.Key == "#PAD" && item.Value.Equals("NONE", StringComparison.OrdinalIgnoreCase) && !Explicit(p, "PAD"))))
            throw new ArgumentException("Parameter is not allowed for this file mechanism.");
        if (p.ExplicitParameterNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != p.ExplicitParameterNames.Count)
            throw new ArgumentException("Duplicate file encryption parameter.");
        if (allowed.Contains("#PAD") && (!PaddingRegistry.TryGet(p.GetParameter("PAD").ToUpperInvariant(), out _) ||
            mechanism == "AES-ECB" && !p.GetParameter("PAD").Equals("NONE", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Unsupported file padding.");
        return (p, key);
    }

    internal static byte[] Decode(string value) => value.Length == 0 ? [] :
        FormatConversions.ToByteArray(value, FormatConversions.ParseString(value));

    internal static EncryptedFileHeader Create(ParameterVariableDeclaration p, long length)
    {
        string m = p.Mechanism.ToUpperInvariant();
        bool cbc = m.EndsWith("CBC"), aead = m is "AES-GCM" or "AES-CCM", ctr = m == "AES-CTR";
        string pad = cbc || m.EndsWith("ECB") ? p.GetParameter("PAD").ToUpperInvariant() : "NONE";
        byte[] iv = cbc ? m == "AES-CBC" ? AES_CBC.ValidateAndDecodeIv(p.GetParameter("IV")) : Decode(p.GetParameter("IV")) : [];
        byte[]? nonce = aead || ctr ? Decode(p.GetParameter("NONCE")) : null;
        if (cbc && !Explicit(p, "IV")) RandomNumberGenerator.Fill(iv);
        if (nonce is not null && !Explicit(p, "NONCE")) nonce = RandomNumberGenerator.GetBytes(12);
        var h = new EncryptedFileHeader(m, pad, Convert.ToBase64String(iv),
            Authentication: aead ? m[4..] : "none", TagLength: aead ? 16 : 0,
            Nonce: nonce is null ? null : Convert.ToBase64String(nonce),
            Counter: ctr ? Convert.ToBase64String(Decode(p.GetParameter("COUNTER"))) : null,
            CounterEncoding: ctr ? "uint32-be" : null,
            AssociatedData: aead && p.GetParameter("ADATA").Length > 0 ? Convert.ToBase64String(Decode(p.GetParameter("ADATA"))) : null,
            PlaintextLength: pad == "ISO-9797-M1" ? length.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
        EncryptedFileCodec.Validate(h);
        return h;
    }

    internal static void Match(ParameterVariableDeclaration p, EncryptedFileHeader h)
    {
        if (!p.Mechanism.Equals(h.Mechanism, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Decrypt(PATH) mechanism conflicts with CWE header.");
        if (Explicit(p, "PAD") && !p.GetParameter("PAD").Equals(h.Padding, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Decrypt(PATH) explicit #PAD conflicts with CWE header.");
        foreach (var (name, value) in new[] { ("IV", h.Iv), ("NONCE", h.Nonce), ("COUNTER", h.Counter), ("ADATA", h.AssociatedData) })
        {
            if (!Explicit(p, name)) continue;
            byte[] declared = name == "IV" && h.Mechanism == "AES-CBC"
                ? AES_CBC.ValidateAndDecodeIv(p.GetParameter(name)) : Decode(p.GetParameter(name));
            if (!declared.AsSpan().SequenceEqual(value is null ? [] : Convert.FromBase64String(value)))
                throw new ArgumentException("Decrypt(PATH) explicit #" + name + " conflicts with CWE header.");
        }
    }

    private static bool Explicit(ParameterVariableDeclaration p, string name) =>
        p.ExplicitParameterNames.Contains("#" + name, StringComparer.OrdinalIgnoreCase);
}
