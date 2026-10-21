using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

internal sealed class PreparedSecureSave(PreparedSnapshotDestination destination, SecureSaveOptions options,
    byte[] payload, byte[] manifestJson, byte[] manifestFrame) : IDisposable
{
    internal PreparedSnapshotDestination Destination { get; } = destination;
    internal SecureSaveOptions Options { get; } = options;
    internal byte[] Payload { get; } = payload;
    internal byte[] ManifestJson { get; } = manifestJson;
    internal byte[] ManifestFrame { get; } = manifestFrame;
    public void Dispose() => CryptographicOperations.ZeroMemory(Payload);
}

internal static class SecureSaveOperations
{
    internal static bool IsSecureSave(string name) => name.Equals("SecureSave", StringComparison.OrdinalIgnoreCase);
    internal static bool IsVoidSnapshot(string name) => IsSecureSave(name) || SnapshotOperations.IsSnapshot(name);
    internal static string CanonicalName(string name) => IsSecureSave(name) ? "SecureSave" : SnapshotOperations.CanonicalName(name);

    internal static PreparedSecureSave Prepare(FunctionCallExpressionNode call)
    {
        int optionStart = call.Arguments.TakeWhile(a => a is not SecureOptionArgumentNode).Count();
        if (call.Arguments.Take(optionStart).Any(a => a is not VariableArgumentNode) ||
            call.Arguments.Skip(optionStart).Any(a => a is not SecureOptionArgumentNode))
            throw new SecureSaveException("InvalidArguments");
        var options = SecureSaveOptions.Parse(call.Arguments.Skip(optionStart).Cast<SecureOptionArgumentNode>());
        var resolved = SnapshotOperations.ResolveSaveArguments(call with { Arguments = call.Arguments.Take(optionStart).ToArray() })
            .Cast<ArgumentVariable>().Select(a => a.Id!).ToArray();
        if (resolved.Length < 2 || resolved[0] is not PathVariableDeclaration { Type: CryptoTypePath } path)
            throw new SecureSaveException("InvalidPathReference");
        var roots = resolved.Skip(1).ToArray();
        if (roots.Length > SecureSaveOptions.RootLimit) throw new SecureSaveException("RootLimit");
        if (roots.Distinct(ReferenceEqualityComparer.Instance).Count() != roots.Length ||
            roots.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != roots.Length ||
            roots.Any(r => r.Id.Length > 256)) throw new SecureSaveException("InvalidSelection");
        if (!AesGcm.IsSupported) throw new SecureSaveException("UnsupportedPlatform");
        var destination = SnapshotByteFilePublisher.Prepare(path, VariableSnapshotWriteMode.CreateNew);
        var serializer = new VariableSnapshotJsonSerializer();
        string json = serializer.Serialize(roots); // One fixed S01 serialization; never re-read live roots after this.
        if (SecureBuffer.Utf8.GetByteCount(json) > SecureSaveOptions.PayloadLimit) throw new SecureSaveException("PayloadLimit");
        var detached = serializer.Deserialize(json);
        var manifest = SecureManifest.Create(detached);
        byte[] manifestJson = SecureManifest.Json(manifest), manifestFrame = SecureManifest.Frame(manifest);
        byte[] payload = SecureBuffer.Utf8.GetBytes(json);
        try
        {
            _ = SecureContainerCryptography.EnvelopeSize(options, payload, manifestJson);
            return new(destination, options, payload, manifestJson, manifestFrame);
        }
        catch { CryptographicOperations.ZeroMemory(payload); throw; }
    }

    internal static async Task ExecuteAsync(PreparedSecureSave prepared, AsyncOperationContext context)
    {
        byte[]? password = null, envelope = null;
        try
        {
            using (var response = await context.RequestPasswordAsync(PasswordPurpose.SecureSave).ConfigureAwait(false))
                password = PasswordValidation.Encode(response.Password!);
            context.CancellationToken.ThrowIfCancellationRequested();
            // KDF and filesystem work must not resume on the GUI dispatcher. The interpreter lease
            // spans this worker, so there is at most one active KDF in this interpreter process.
            await Task.Run(() =>
            {
                try
                {
                    envelope = SecureContainerCryptography.Protect(prepared.Options, prepared.Payload,
                        prepared.ManifestJson, prepared.ManifestFrame, password,
                        RandomNumberGenerator.GetBytes(16), RandomNumberGenerator.GetBytes(12), context.CancellationToken);
                }
                finally { CryptographicOperations.ZeroMemory(password); }
                SnapshotByteFilePublisher.Write(prepared.Destination, envelope, SecureSaveOptions.FileLimit,
                    VariableSnapshotWriteMode.CreateNew, context.CancellationToken);
            }, context.CancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (password is not null) CryptographicOperations.ZeroMemory(password);
            if (envelope is not null) CryptographicOperations.ZeroMemory(envelope);
        }
    }
}
