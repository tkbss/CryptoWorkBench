using static CryptoScript.Serialization.SnapshotByteFilePublisher;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using CryptoScript.Variables;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CryptoScript.Serialization;

/// <summary>
/// Plaintext S01 snapshots of explicitly selected, detached variables. No script execution
/// or VariableDictionary access. Callers must control the destination directory and ancestors.
/// See docs/architecture/S02-validation.md for filesystem guarantees and race limitations.
/// </summary>
public sealed class VariableSnapshotFileStore
{
    public const int DefaultMaxFileSizeBytes = 16 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly VariableSnapshotJsonSerializer serializer = new();

    /// <summary>Maximum encoded bytes on both read and write, including any input UTF-8 BOM.</summary>
    public int MaxFileSizeBytes { get; }

    public VariableSnapshotFileStore(int maxFileSizeBytes = DefaultMaxFileSizeBytes)
    {
        if (maxFileSizeBytes <= 0)
            throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidArgument);
        MaxFileSizeBytes = maxFileSizeBytes;
    }

    /// <summary>
    /// Serializes completely before creating a private temporary file, flushes it to disk,
    /// closes it, then publishes it. ReplaceExisting also permits a previously absent target.
    /// </summary>
    public void Write(PathVariableDeclaration path, IReadOnlyList<VariableDeclaration> variables,
        VariableSnapshotWriteMode mode = VariableSnapshotWriteMode.CreateNew)
    {
        if (variables is null || !Enum.IsDefined(mode))
            throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidArgument);
        Guard(() =>
        {
            var destination = SnapshotByteFilePublisher.Prepare(path, mode);

            byte[] bytes;
            try
            {
                string json = serializer.Serialize(variables);
                if (Utf8.GetByteCount(json) > MaxFileSizeBytes)
                    throw new VariableSnapshotFileException(VariableSnapshotFileError.SizeLimitExceeded);
                bytes = Utf8.GetBytes(json);
            }
            catch (Exception error) when (error is JsonException or EncoderFallbackException)
            {
                throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidSnapshot);
            }

            try { SnapshotByteFilePublisher.Write(destination, bytes, MaxFileSizeBytes, mode); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
        }, writing: true);
    }

    /// <summary>Returns the entire reconstructed root list only after S01 validation succeeds.</summary>
    public IReadOnlyList<VariableDeclaration> Read(PathVariableDeclaration path)
    {
        IReadOnlyList<VariableDeclaration>? result = null;
        Guard(() =>
        {
            string source = ValidatePath(path);
            ValidateTarget(source, null);
            using var stream = new FileStream(source, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete);
            if (stream.Length > MaxFileSizeBytes)
                throw new VariableSnapshotFileException(VariableSnapshotFileError.SizeLimitExceeded);
            using var buffer = new MemoryStream();
            byte[] chunk = new byte[8192];
            int count;
            // Also enforce the limit while reading, in case another process grows the file.
            while ((count = stream.Read(chunk, 0, (int)Math.Min(chunk.Length,
                       (long)MaxFileSizeBytes - buffer.Length + 1))) != 0)
            {
                if (buffer.Length + count > MaxFileSizeBytes)
                    throw new VariableSnapshotFileException(VariableSnapshotFileError.SizeLimitExceeded);
                buffer.Write(chunk, 0, count);
            }
            try
            {
                var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
                // Accept UTF-8 BOM on import, never auto-detect UTF-16/32.
                if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) bytes = bytes[3..];
                string json = Utf8.GetString(bytes);
                // S01 intentionally collapses all its errors. Inspect its strict envelope
                // here to expose an unsupported-version category without changing S01.
                var envelope = SnapshotJson.Parse(json) as JObject;
                SnapshotJson.Require(envelope?["format"]?.Type == JTokenType.String &&
                    envelope["version"]?.Type == JTokenType.Integer, "Invalid snapshot envelope.");
                if (envelope!["format"]!.Value<string>() != "CryptoScriptVariables" ||
                    !JToken.DeepEquals(envelope["version"], new JValue(1)))
                    throw new VariableSnapshotFileException(VariableSnapshotFileError.UnsupportedFormat);
                result = serializer.Deserialize(json);
            }
            catch (Exception error) when (error is JsonException or System.Text.Json.JsonException or
                DecoderFallbackException or OverflowException)
            {
                throw new VariableSnapshotFileException(VariableSnapshotFileError.InvalidSnapshot);
            }
        }, writing: false);
        return result!;
    }

}
