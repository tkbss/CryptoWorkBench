using CryptoScript.CryptoAlgorithm;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.FileEncryption;

internal static class FileDecryptCoordinator
{
    internal static PathVariableDeclaration Decrypt(AlgorithmCallArguments arguments,
        PathVariableDeclaration path, CancellationToken token = default,
        Action? beforePublish = null, int maximumCandidates = 1000000)
    {
        if (arguments.Arguments[0].SourceVariable is not ParameterVariableDeclaration parameters)
            throw new ArgumentException("Decrypt(PATH) requires a PARAM variable reference.");
        if (!parameters.Mechanism.Equals("AES-CBC", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Decrypt(PATH) supports only AES-CBC.");
        MechanismParameterContractValidator.Validate(parameters.Mechanism, CryptoScriptFunction.Decrypt, parameters);
        var key = AlgorithmArgumentResolver.ResolveKey(arguments.Arguments[1]);
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Aes, KeyUsage.Decrypt,
            "Decrypt", parameters.Mechanism, arguments.DiagnosticSink);
        token.ThrowIfCancellationRequested();
        string sourcePath = SnapshotByteFilePublisher.ValidatePath(path);
        using var publication = new FilePublication(Path.GetDirectoryName(sourcePath)!);
        using var source = publication.OpenSource(Path.GetFileName(sourcePath));
        var (header, _) = EncryptedFileCodec.Read(source);
        byte[] iv = Convert.FromBase64String(header.Iv);
        if (parameters.ExplicitParameterNames.Contains("#IV", StringComparer.OrdinalIgnoreCase) &&
            !AES_CBC.ValidateAndDecodeIv(parameters.GetParameter("IV")).AsSpan().SequenceEqual(iv))
            throw new ArgumentException("Decrypt(PATH) explicit #IV conflicts with the CWE header.");
        if (parameters.ExplicitParameterNames.Contains("#PAD", StringComparer.OrdinalIgnoreCase) &&
            !parameters.GetParameter("PAD").Equals(header.Padding, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Decrypt(PATH) explicit #PAD conflicts with the CWE header.");
        AES_CBC.ValidateCiphertextLength(source.Length - source.Position);

        // Header values are authoritative. Never mutate the caller's PARAM or its provenance.
        var effective = new ParameterVariableDeclaration { Mechanism = header.Mechanism };
        effective.SetParameter("PAD", header.Padding);
        using var aes = AES_CBC.CreateCipher(key, iv);
        string sourceName = Path.GetFileName(sourcePath);
        string baseName = sourceName.Length > 4 && sourceName.EndsWith(".cwe", StringComparison.OrdinalIgnoreCase)
            ? sourceName[..^4] : sourceName + ".decrypted";
        string temporary = $".cwe-{Guid.NewGuid():N}.tmp";
        bool ownsTemporary = false;
        try
        {
            using var output = publication.CreateTemporary(temporary);
            ownsTemporary = true;
            AES_CBC.DecryptStream(source, output, aes, effective, token);
            output.Flush(flushToDisk: true);
            beforePublish?.Invoke();
            for (int number = 0; number < maximumCandidates; number++)
            {
                token.ThrowIfCancellationRequested();
                string name = baseName + (number == 0 ? "" : "." + number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                var result = new PathVariableDeclaration { Type = new CryptoTypePath(), Value = Path.Combine(publication.CurrentDirectory(), name) };
                if (publication.TryPublish(output, temporary, name)) return result;
            }
            throw new IOException("CWE output name candidates exhausted.");
        }
        finally { if (ownsTemporary) publication.DeleteTemporary(temporary); }
    }
}
