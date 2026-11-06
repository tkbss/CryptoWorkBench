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
        var (parameters, key) = FileCipherParameters.Resolve(arguments, encrypt: false);
        token.ThrowIfCancellationRequested();
        string sourcePath = SnapshotByteFilePublisher.ValidatePath(path);
        using var publication = new FilePublication(Path.GetDirectoryName(sourcePath)!);
        using var source = publication.OpenSource(Path.GetFileName(sourcePath));
        var (header, frame) = EncryptedFileCodec.Read(source);
        FileCipherParameters.Match(parameters, header);
        string sourceName = Path.GetFileName(sourcePath);
        string baseName = sourceName.Length > 4 && sourceName.EndsWith(".cwe", StringComparison.OrdinalIgnoreCase)
            ? sourceName[..^4] : sourceName + ".decrypted";
        string temporary = $".cwe-{Guid.NewGuid():N}.tmp";
        bool ownsTemporary = false;
        try
        {
            using var output = publication.CreateTemporary(temporary);
            ownsTemporary = true;
            FileCipherStream.Process(source, output, key, header, frame, encrypt: false, token);
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
