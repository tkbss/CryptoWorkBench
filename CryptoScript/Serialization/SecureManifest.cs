using System.Security.Cryptography;
using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoScript.CryptoAlgorithm;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace CryptoScript.Serialization;

internal sealed record SecureKeyManifest(string Algorithm, ulong BitLength, string? Kcv);
internal sealed record SecureManifestEntry(string Identifier, string Type, SecureKeyManifest? Key);

internal static class SecureManifest
{
    internal static SecureManifestEntry[] Create(IReadOnlyList<VariableDeclaration> roots)
    {
        if (roots.Count > SecureSaveOptions.RootLimit) throw new SecureSaveException("RootLimit");
        return roots.Select(root =>
        {
            if (root.Id.Length is < 1 or > 256 || !char.IsAsciiLetter(root.Id[0]) ||
                root.Id.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new SecureSaveException("InvalidIdentifier");
            string type = root switch
            {
                KeyVariableDeclaration => "KEY", ParameterVariableDeclaration => "PARAM",
                PathVariableDeclaration => "PATH", BlockHeaderVariableDeclaration => "TR31H",
                SecretVariableDeclaration => "SECRET", StringVariableDeclaration => "VAR",
                _ => throw new SecureSaveException("InvalidSnapshot")
            };
            return new SecureManifestEntry(root.Id, type, root is KeyVariableDeclaration key
                ? new SecureKeyManifest(key.KeyType.Algorithm.ToString(), Bits(key),
                    KeyCheckValueCalculator.Calculate(key, KeyCheckValueMethod.KCV)) : null);
        }).ToArray();
    }

    private static ulong Bits(KeyVariableDeclaration key)
    {
        string format = key.ValueFormat.Length == 0 ? FormatConversions.ParseString(key.Value) : key.ValueFormat;
        byte[] bytes = FormatConversions.ToByteArray(key.Value, format);
        try
        {
            ulong bits = checked((ulong)bytes.Length * 8);
            if (key.KeyType.Algorithm is KeyAlgorithm.Rsa or KeyAlgorithm.Ec)
            {
                var decoded = key.KeyType.MaterialKind == KeyMaterialKind.Private
                    ? PrivateKeyFactory.CreateKey(bytes) : PublicKeyFactory.CreateKey(bytes);
                bits = decoded switch
                {
                    RsaKeyParameters rsa => (ulong)rsa.Modulus.BitLength,
                    ECKeyParameters ec => (ulong)ec.Parameters.N.BitLength,
                    _ => throw new SecureSaveException("InvalidSnapshot")
                };
            }
            if (bits is < 1 or > 134217728) throw new SecureSaveException("InvalidSnapshot");
            return bits;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static byte[] Frame(IReadOnlyList<SecureManifestEntry> entries)
    {
        using var b = new SecureBuffer(SecureSaveOptions.ManifestLimit);
        b.U32((uint)entries.Count);
        foreach (var entry in entries)
        {
            b.S(entry.Identifier); b.S(entry.Type); b.U8(entry.Key is null ? (byte)0 : (byte)1);
            if (entry.Key is not { } key) continue;
            b.S(key.Algorithm); b.U64(key.BitLength); b.U8(key.Kcv is null ? (byte)0 : (byte)1);
            if (key.Kcv is not null) b.S(key.Kcv);
        }
        return b.ToArray();
    }

    internal static byte[] Json(IReadOnlyList<SecureManifestEntry> entries)
    {
        using var b = new SecureBuffer(SecureSaveOptions.ManifestLimit);
        b.Text("[");
        bool first = true;
        foreach (var entry in entries)
        {
            if (!first) b.Text(","); first = false;
            b.Text("{\"identifier\":"); b.Json(entry.Identifier); b.Text(",\"type\":"); b.Json(entry.Type);
            b.Text(",\"key\":");
            if (entry.Key is not { } key) b.Text("null");
            else
            {
                b.Text("{\"algorithm\":"); b.Json(key.Algorithm); b.Text(",\"bitLength\":");
                b.Text(key.BitLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
                b.Text(",\"kcv\":"); if (key.Kcv is null) b.Text("null"); else b.Json(key.Kcv);
                b.Text("}");
            }
            b.Text("}");
        }
        b.Text("]"); return b.ToArray();
    }
}
