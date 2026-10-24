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
        var (parameters, key) = FileCipherParameters.Resolve(arguments, encrypt: true);
        token.ThrowIfCancellationRequested();
        string sourcePath = SnapshotByteFilePublisher.ValidatePath(path);
        string directory = Path.GetDirectoryName(sourcePath)!;
        using var publication = new FilePublication(directory);
        using var source = publication.OpenSource(Path.GetFileName(sourcePath));
        var metadata = FileCipherParameters.Create(parameters, source.Length);
        byte[] header = EncryptedFileCodec.Encode(metadata);
        string temporary = $".cwe-{Guid.NewGuid():N}.tmp";
        bool ownsTemporary = false;
        try
        {
            using var output = publication.CreateTemporary(temporary);
            ownsTemporary = true;
            output.Write(header);
            FileCipherStream.Process(source, output, key, metadata, header, encrypt: true, token);
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
