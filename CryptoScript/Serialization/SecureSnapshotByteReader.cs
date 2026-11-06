using System.Security.Cryptography;
using CryptoScript.Variables;

namespace CryptoScript.Serialization;

internal static class SecureSnapshotByteReader
{
    // Same S02 path policy and same documented check/open race: ancestors must be trusted.
    // There is exactly one open and no access to PATH or CWD after this method returns.
    internal static byte[] Read(PathVariableDeclaration path, CancellationToken token)
    {
        byte[]? result = null;
        SnapshotByteFilePublisher.Guard(() =>
        {
            string source = SnapshotByteFilePublisher.ValidatePath(path);
            SnapshotByteFilePublisher.ValidateTarget(source, null);
            using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > SecureSaveOptions.FileLimit) throw new SecureLoadException("ResourceLimitExceeded");
            var chunks = new List<byte[]>();
            int length = 0;
            byte[] chunk = new byte[8192];
            try
            {
                int count;
                while ((count = stream.Read(chunk, 0, (int)Math.Min(chunk.Length, (long)SecureSaveOptions.FileLimit - length + 1))) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    if (length + count > SecureSaveOptions.FileLimit) throw new SecureLoadException("ResourceLimitExceeded");
                    chunks.Add(chunk.AsSpan(0, count).ToArray()); length += count;
                }
                token.ThrowIfCancellationRequested();
                result = new byte[length]; int offset = 0;
                foreach (var part in chunks) { part.CopyTo(result, offset); offset += part.Length; }
            }
            finally { CryptographicOperations.ZeroMemory(chunk); foreach (var part in chunks) CryptographicOperations.ZeroMemory(part); }
        }, writing: false);
        return result!;
    }
}
