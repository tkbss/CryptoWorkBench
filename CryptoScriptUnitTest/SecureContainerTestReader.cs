using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;

namespace CryptoScriptUnitTest;

// Test-only reader: independently assembles framing from decoded envelope fields.
// No reference to production serialization/framing/derivation helpers or runtime registration.
internal static class SecureContainerTestReader
{
    internal static string Read(string json, string password)
    {
        var e = JObject.Parse(json); var k = e["kdf"]!; var c = e["cipher"]!;
        string mode = (string)e["mode"]!, id = (string)k["id"]!;
        byte[] salt = Convert.FromBase64String((string)k["salt"]!);
        byte[] nonce = Convert.FromBase64String((string)c["nonce"]!);
        byte[] p = mode == "AES-256-GMAC" ? Encoding.UTF8.GetBytes((string)e["payload"]!) : Convert.FromBase64String((string)e["payload"]!);
        using var ms = new MemoryStream();
        void U32(uint n) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(b, n); ms.Write(b); }
        void U64(ulong n) { Span<byte> b = stackalloc byte[8]; BinaryPrimitives.WriteUInt64BigEndian(b, n); ms.Write(b); }
        void B(byte[] b) { U64((ulong)b.Length); ms.Write(b); }
        void S(string s) => B(Encoding.UTF8.GetBytes(s));
        S("CryptoScriptSecureVariables/key"); U32((uint)e["version"]!); S(mode); S(id);
        byte[] info = ms.ToArray(); ms.SetLength(0);
        S("CryptoScriptSecureVariables/auth"); S((string)e["format"]!); U32((uint)e["version"]!); S(mode); S(id);
        if (id == "ARGON2ID") U32((uint)k["version"]!);
        B(salt);
        if (id == "ARGON2ID") U32((uint)k["memoryKiB"]!);
        U32((uint)k["iterations"]!);
        if (id == "ARGON2ID") U32((uint)k["parallelism"]!);
        U32((uint)k["outputLength"]!); B(nonce); U32((uint)c["tagLength"]!);
        var manifest = (JArray)e["manifest"]!; U32((uint)manifest.Count);
        foreach (var entry in manifest)
        {
            S((string)entry["identifier"]!); S((string)entry["type"]!);
            var key = entry["key"]!; bool present = key.Type != JTokenType.Null;
            ms.WriteByte(present ? (byte)1 : (byte)0);
            if (!present) continue;
            S((string)key["algorithm"]!); U64((ulong)key["bitLength"]!);
            bool hasKcv = key["kcv"]!.Type != JTokenType.Null; ms.WriteByte(hasKcv ? (byte)1 : (byte)0);
            if (hasKcv) S((string)key["kcv"]!);
        }
        S((string)e["payloadEncoding"]!); U64((ulong)p.Length);
        if (mode == "AES-256-GMAC") B(p);
        byte[] master;
        if (id == "ARGON2ID")
        {
            var a = new Argon2BytesGenerator();
            a.Init(new Argon2Parameters.Builder(Argon2Parameters.Argon2id).WithVersion((int)k["version"]!)
                .WithSalt(salt).WithMemoryAsKB((int)k["memoryKiB"]!).WithIterations((int)k["iterations"]!)
                .WithParallelism((int)k["parallelism"]!).Build());
            master = new byte[32]; a.GenerateBytes(Encoding.UTF8.GetBytes(password), master);
        }
        else master = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, (int)k["iterations"]!, HashAlgorithmName.SHA256, 32);
        // Independent HKDF extract/one-block expand, not the production HKDF API.
        byte[] prk = HMACSHA256.HashData(new byte[32], master);
        byte[] aesKey = HMACSHA256.HashData(prk, info.Concat(new byte[] { 1 }).ToArray());
        try
        {
            using var aes = new AesGcm(aesKey, 16);
            byte[] clear = mode == "AES-256-GMAC" ? [] : new byte[p.Length];
            aes.Decrypt(nonce, mode == "AES-256-GMAC" ? [] : p,
                Convert.FromBase64String((string)e["tag"]!), clear, ms.ToArray());
            return Encoding.UTF8.GetString(mode == "AES-256-GMAC" ? p : clear);
        }
        finally { CryptographicOperations.ZeroMemory(master); CryptographicOperations.ZeroMemory(prk); CryptographicOperations.ZeroMemory(aesKey); }
    }
}
