using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.FileEncryption;

internal static class FileEncryptCoordinator
{
    internal static PathVariableDeclaration Encrypt(AlgorithmCallArguments arguments,
        PathVariableDeclaration path, CancellationToken token = default,
        Action? beforePublish = null, int maximumCandidates = 1000000)
    {
        if (arguments.Arguments[0].SourceVariable is not ParameterVariableDeclaration parameters)
            throw new ArgumentException("Encrypt(PATH) requires a PARAM variable reference.");
        if (!parameters.Mechanism.Equals("AES-CBC", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Encrypt(PATH) supports only AES-CBC.");
        MechanismParameterContractValidator.Validate(parameters.Mechanism, CryptoScriptFunction.Encrypt, parameters);
        var key = AlgorithmArgumentResolver.ResolveKey(arguments.Arguments[1]);
        KeyMetadataValidator.Validate(key, KeyAlgorithm.Aes, KeyUsage.Encrypt,
            "Encrypt", parameters.Mechanism, arguments.DiagnosticSink);
        string padding = parameters.GetParameter("PAD").ToUpperInvariant();
        if (padding is not ("PKCS-7" or "NONE"))
            throw new ArgumentException("Encrypt(PATH) supports only PKCS-7 and NONE padding.");
        byte[] iv = AES_CBC.ValidateAndDecodeIv(parameters.GetParameter("IV"));
        if (!parameters.ExplicitParameterNames.Contains("#IV", StringComparer.OrdinalIgnoreCase))
            RandomNumberGenerator.Fill(iv);
        using var aes = AES_CBC.CreateCipher(key, iv);
        byte[] header = EncryptedFileCodec.Encode(new("AES-CBC", padding, Convert.ToBase64String(iv)));
        token.ThrowIfCancellationRequested();
        string sourcePath = SnapshotByteFilePublisher.ValidatePath(path);
        string directory = Path.GetDirectoryName(sourcePath)!;
        using var publication = new FilePublication(directory);
        using var source = publication.OpenSource(Path.GetFileName(sourcePath));
        string temporary = $".cwe-{Guid.NewGuid():N}.tmp";
        bool ownsTemporary = false;
        try
        {
            using var output = publication.CreateTemporary(temporary);
            ownsTemporary = true;
            output.Write(header);
            AES_CBC.EncryptStream(source, output, aes, parameters, token);
            output.Flush(flushToDisk: true);
            beforePublish?.Invoke();
            for (int number = 0; number < maximumCandidates; number++)
            {
                token.ThrowIfCancellationRequested();
                string name = Path.GetFileName(sourcePath) + (number == 0 ? "" : "." + number.ToString(System.Globalization.CultureInfo.InvariantCulture)) + ".cwe";
                // Prepare the return object before committing the file.
                var result = new PathVariableDeclaration { Type = new CryptoTypePath(), Value = Path.Combine(publication.CurrentDirectory(), name) };
                if (publication.TryPublish(output, temporary, name)) return result;
            }
            throw new IOException("CWE output name candidates exhausted.");
        }
        finally { if (ownsTemporary) publication.DeleteTemporary(temporary); }
    }
}
