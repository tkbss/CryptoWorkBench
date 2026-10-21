using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Serialization;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

internal sealed record PreparedSecureLoad(SecureEnvelope Envelope, string[] Names, bool Replace) : IDisposable
{
    public void Dispose() => Envelope.Dispose();
}

internal static class SecureLoadOperations
{
    internal static bool IsSecureLoad(string name) => name.Equals("SecureLoad", StringComparison.OrdinalIgnoreCase);

    internal static PreparedSecureLoad Prepare(FunctionCallExpressionNode call, CancellationToken token)
    {
        if (call.Arguments.Count == 0 || call.Arguments[0] is not VariableArgumentNode source)
            throw new SecureLoadException("InvalidSelection");
        var names = new List<string>(); bool replace = false;
        for (int i = 1; i < call.Arguments.Count; i++)
        {
            if (call.Arguments[i] is VariableArgumentNode selector) names.Add(selector.Identifier);
            else if (i == call.Arguments.Count - 1 && call.Arguments[i] is ParameterArgumentNode
                { TypeName: "#ONCONFLICT", RawValue: "REJECT" or "REPLACE" } option) replace = option.RawValue == "REPLACE";
            else throw new SecureLoadException("InvalidSelection");
        }
        if (names.Count > SecureSaveOptions.RootLimit || names.Distinct(StringComparer.Ordinal).Count() != names.Count ||
            names.Contains("ALL", StringComparer.Ordinal) && names.Count != 1) throw new SecureLoadException("InvalidSelection");
        if (VariableDictionary.Instance().Get(source.Identifier) is not PathVariableDeclaration { Type: CryptoTypePath } path)
            throw new SecureLoadException("InvalidPathReference");
        if (!AesGcm.IsSupported) throw new SecureLoadException("UnsupportedPlatform");
        byte[] bytes = SecureSnapshotByteReader.Read(path, token);
        try { return new(SecureEnvelope.Parse(bytes), names.ToArray(), replace); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static async Task ExecuteAsync(PreparedSecureLoad prepared, AsyncOperationContext context)
    {
        byte[]? password = null;
        try
        {
            using (var response = await context.RequestPasswordAsync(PasswordPurpose.SecureLoad).ConfigureAwait(false))
                password = PasswordValidation.Encode(response.Password!);
            var roots = await Task.Run(() =>
            {
                byte[] payload = SecureContainerCryptography.Unprotect(prepared.Envelope, password, context.CancellationToken);
                try
                {
                    // No S01 parser or key projection is reached before successful authentication.
                    var restored = new VariableSnapshotJsonSerializer().Deserialize(SecureBuffer.Utf8.GetString(payload));
                    if (!SecureManifest.Create(restored).SequenceEqual(prepared.Envelope.Manifest))
                        throw new SecureLoadException("InvalidSecureSnapshot");
                    return restored;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                { throw new SecureLoadException("InvalidSecureSnapshot"); }
                finally { CryptographicOperations.ZeroMemory(payload); }
            }, context.CancellationToken).ConfigureAwait(false);
            using (InterpreterExecution.EnterState(runner: true))
                SnapshotOperations.PublishLoadedRoots(roots, prepared.Names, prepared.Replace, context.CancellationToken, secure: true);
        }
        finally { if (password is not null) CryptographicOperations.ZeroMemory(password); }
    }
}
