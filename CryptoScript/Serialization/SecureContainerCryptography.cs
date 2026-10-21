using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScript.Serialization;

internal static class SecureContainerCryptography
{
    internal const string Format = "CryptoScriptSecureVariables";
    internal static byte[] DeriveMaster(SecureSaveOptions options, byte[] password, byte[] salt)
    {
        options.Validate();
        if (password.Length is < 1 or > 1024 || salt.Length != 16) throw new SecureSaveException("InvalidCryptographicInput");
        if (options.Pbkdf)
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, options.PbkdfIterations, HashAlgorithmName.SHA256, 32);
        var parameters = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(Argon2Parameters.Version13).WithSecret([]).WithAdditional([]).WithSalt(salt).WithMemoryAsKB(options.Memory)
            .WithIterations(options.Iterations).WithParallelism(options.Lanes).Build();
        var generator = new Argon2BytesGenerator();
        byte[] result = new byte[32];
        try { generator.Init(parameters); generator.GenerateBytes(password, result); return result; }
        catch { CryptographicOperations.ZeroMemory(result); throw; }
        // BouncyCastle's working memory follows its own lifecycle (documented S05-A risk).
    }

    internal static byte[] KeyInfo(SecureSaveOptions options)
    {
        using var b = new SecureBuffer(256);
        b.S(Format + "/key"); b.U32(1); b.S(options.Mode); b.S(options.KdfId); return b.ToArray();
    }

    internal static byte[] Header(SecureSaveOptions o, byte[] salt, byte[] nonce, byte[] manifest, int length)
    {
        using var b = new SecureBuffer(SecureSaveOptions.ManifestLimit + 4096);
        b.S(Format + "/auth"); b.S(Format); b.U32(1); b.S(o.Mode); b.S(o.KdfId);
        if (!o.Pbkdf) b.U32(19);
        b.B(salt);
        if (!o.Pbkdf) { b.U32((uint)o.Memory); b.U32((uint)o.Iterations); b.U32((uint)o.Lanes); }
        else b.U32((uint)o.PbkdfIterations);
        b.U32(32); b.B(nonce); b.U32(16); b.Bytes(manifest); b.S(o.Encoding); b.U64((ulong)length);
        return b.ToArray();
    }

    // Internal deterministic primitive seam for independent vectors, never exposed by the script/public API.
    internal static byte[] Protect(SecureSaveOptions o, byte[] plaintext, byte[] manifestJson, byte[] manifestFrame,
        byte[] password, byte[] salt, byte[] nonce, CancellationToken token = default, Action? afterDerivation = null)
    {
        if (nonce.Length != 12 || plaintext.Length > SecureSaveOptions.PayloadLimit) throw new SecureSaveException("InvalidCryptographicInput");
        int size = EnvelopeSize(o, plaintext, manifestJson);
        byte[]? master = null, modeKey = null, header = null, ciphertext = null;
        try
        {
            token.ThrowIfCancellationRequested();
            // Protect consumes the caller-owned password bytes; no password is needed after KDF.
            try { master = DeriveMaster(o, password, salt); }
            finally { CryptographicOperations.ZeroMemory(password); }
            afterDerivation?.Invoke();
            token.ThrowIfCancellationRequested();
            modeKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, new byte[32], KeyInfo(o));
            CryptographicOperations.ZeroMemory(master);
            header = Header(o, salt, nonce, manifestFrame, plaintext.Length);
            byte[] tag = new byte[16];
            using var aes = new AesGcm(modeKey, 16);
            if (o.Gmac)
            {
                using var aad = new SecureBuffer(checked(header.Length + 8 + plaintext.Length));
                aad.Bytes(header); aad.B(plaintext);
                aes.Encrypt(nonce, ReadOnlySpan<byte>.Empty, Span<byte>.Empty, tag, aad.Span);
            }
            else
            {
                ciphertext = new byte[plaintext.Length];
                aes.Encrypt(nonce, plaintext, ciphertext, tag, header);
            }
            token.ThrowIfCancellationRequested();
            return Envelope(o, o.Gmac ? plaintext : ciphertext!, manifestJson, salt, nonce, tag, size);
        }
        finally
        {
            if (master is not null) CryptographicOperations.ZeroMemory(master);
            if (modeKey is not null) CryptographicOperations.ZeroMemory(modeKey);
            if (header is not null) CryptographicOperations.ZeroMemory(header);
            if (ciphertext is not null) CryptographicOperations.ZeroMemory(ciphertext);
        }
    }

    internal static int EnvelopeSize(SecureSaveOptions o, byte[] plaintext, byte[] manifest)
    {
        byte[] empty = Envelope(o, [], manifest, new byte[16], new byte[12], new byte[16], manifest.Length + 2048);
        long size = empty.Length + (o.Gmac ? SecureBuffer.EscapedSize(plaintext) : 4L * ((plaintext.Length + 2) / 3));
        if (size > SecureSaveOptions.FileLimit) throw new SecureSaveException("SizeLimitExceeded");
        return checked((int)size);
    }

    internal static byte[] Unprotect(SecureEnvelope envelope, byte[] password, CancellationToken token)
    {
        byte[]? master = null, key = null, plaintext = null;
        try
        {
            token.ThrowIfCancellationRequested();
            try { master = DeriveMaster(envelope.Options, password, envelope.Salt); }
            finally { CryptographicOperations.ZeroMemory(password); }
            token.ThrowIfCancellationRequested();
            key = HKDF.DeriveKey(HashAlgorithmName.SHA256, master, 32, new byte[32], KeyInfo(envelope.Options));
            CryptographicOperations.ZeroMemory(master);
            byte[] header = Header(envelope.Options, envelope.Salt, envelope.Nonce,
                SecureManifest.Frame(envelope.Manifest), envelope.Payload.Length);
            using var aes = new AesGcm(key, 16);
            if (envelope.Options.Gmac)
            {
                using var aad = new SecureBuffer(checked(header.Length + 8 + envelope.Payload.Length));
                aad.Bytes(header); aad.B(envelope.Payload);
                aes.Decrypt(envelope.Nonce, ReadOnlySpan<byte>.Empty, envelope.Tag, Span<byte>.Empty, aad.Span);
                plaintext = envelope.Payload.ToArray();
            }
            else
            {
                plaintext = new byte[envelope.Payload.Length];
                aes.Decrypt(envelope.Nonce, envelope.Payload, envelope.Tag, plaintext, header);
            }
            token.ThrowIfCancellationRequested();
            var result = plaintext; plaintext = null; return result;
        }
        catch (AuthenticationTagMismatchException) { throw new SecureLoadException("SecureAuthenticationFailed"); }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            if (master is not null) CryptographicOperations.ZeroMemory(master);
            if (key is not null) CryptographicOperations.ZeroMemory(key);
            if (plaintext is not null) CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] Envelope(SecureSaveOptions o, byte[] payload, byte[] manifest,
        byte[] salt, byte[] nonce, byte[] tag, int capacity)
    {
        using var b = new SecureBuffer(capacity);
        b.Text("{\"format\":"); b.Json(Format); b.Text(",\"version\":1,\"mode\":"); b.Json(o.Mode);
        b.Text(",\"kdf\":{\"id\":"); b.Json(o.KdfId);
        if (!o.Pbkdf) b.Text(",\"version\":19");
        b.Text(",\"salt\":"); b.Json(Convert.ToBase64String(salt));
        if (!o.Pbkdf)
        {
            b.Text(",\"memoryKiB\":"); b.Number(o.Memory); b.Text(",\"iterations\":"); b.Number(o.Iterations);
            b.Text(",\"parallelism\":"); b.Number(o.Lanes);
        }
        else { b.Text(",\"iterations\":"); b.Number(o.PbkdfIterations); }
        b.Text(",\"outputLength\":32},\"cipher\":{\"nonce\":"); b.Json(Convert.ToBase64String(nonce));
        b.Text(",\"tagLength\":16},\"manifest\":"); b.Bytes(manifest);
        b.Text(",\"payloadEncoding\":"); b.Json(o.Encoding); b.Text(",\"payload\":");
        if (o.Gmac) b.Json(payload); else b.Json(Convert.ToBase64String(payload));
        b.Text(",\"tag\":"); b.Json(Convert.ToBase64String(tag)); b.Text("}");
        return b.ToArray();
    }
}
