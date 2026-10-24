using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.Model;
using CryptoScript.Serialization;
using CryptoScript.Variables;
using Org.BouncyCastle.Crypto;

namespace CryptoScript.FileEncryption;

internal static class FileHashCoordinator
{
    internal static StringVariableDeclaration Hash(ParameterVariableDeclaration parameters,
        PathVariableDeclaration path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var digest = Prepare(parameters);
        string sourcePath = SnapshotByteFilePublisher.ValidatePath(path);
        using var publication = new FilePublication(Path.GetDirectoryName(sourcePath)!);
        using var source = publication.OpenSource(Path.GetFileName(sourcePath));
        return Read(source, digest, token);
    }

    // Failure/cancellation seam; production always opens through FilePublication.
    internal static StringVariableDeclaration HashStream(ParameterVariableDeclaration parameters,
        Stream source, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        return Read(source, Prepare(parameters), token);
    }

    private static IDigest Prepare(ParameterVariableDeclaration parameters)
    {
        if (!MechanismRegistry.TryGet(parameters.Mechanism, out var entry) ||
            !entry!.Supports(CryptoScriptFunction.Hash))
            throw new ArgumentException("Unsupported Hash(PATH) mechanism.");
        MechanismParameterContractValidator.Validate(parameters.Mechanism, CryptoScriptFunction.Hash, parameters);
        if (parameters.GetParameters().Any(p => !p.Key.Equals("#MECH", StringComparison.OrdinalIgnoreCase)) ||
            parameters.ExplicitParameterNames.Any(p => !p.Equals("#MECH", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("HASH does not support additional parameters.");
        return DigestFactory.Create(entry.CanonicalName);
    }

    private static StringVariableDeclaration Read(Stream source, IDigest digest, CancellationToken token)
    {
        byte[] buffer = new byte[65536];
        byte[] result = new byte[digest.GetDigestSize()];
        try
        {
            long length = source.Length;
            long consumed = 0;
            void CheckLength(bool end = false)
            {
                if (source.Length != length || consumed > length || end && consumed != length)
                    throw new IOException("HASH source length changed during processing.");
            }
            CheckLength();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int count = source.Read(buffer, 0, buffer.Length);
                token.ThrowIfCancellationRequested();
                consumed = checked(consumed + count);
                CheckLength(count == 0);
                if (count == 0) break;
                digest.BlockUpdate(buffer, 0, count);
            }
            digest.DoFinal(result, 0);
            CheckLength(true);
            token.ThrowIfCancellationRequested();
            return new StringVariableDeclaration { Value = "0x(" + Convert.ToHexString(result) + ")",
                ValueFormat = FormatConversions.HEX, Type = new CryptoTypeVar() };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
            CryptographicOperations.ZeroMemory(result);
            digest.Reset();
        }
    }
}
