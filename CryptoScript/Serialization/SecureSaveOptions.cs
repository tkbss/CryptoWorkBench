using System.Globalization;
using CryptoScript.Model.Ast;

namespace CryptoScript.Serialization;

internal sealed class SecureSaveException(string category) : Exception("SecureSave failed: " + category + ".");

internal sealed record SecureSaveOptions(bool Gmac = false, bool Pbkdf = false,
    int Memory = 65536, int Iterations = 3, int Lanes = 4, int PbkdfIterations = 600000)
{
    internal const int PayloadLimit = 16 * 1024 * 1024, FileLimit = 96 * 1024 * 1024,
        ManifestLimit = 1024 * 1024, RootLimit = 4096;
    internal string Mode => Gmac ? "AES-256-GMAC" : "AES-256-GCM";
    internal string KdfId => Pbkdf ? "PBKDF2-HMAC-SHA256" : "ARGON2ID";
    internal string Encoding => Gmac ? "utf8" : "base64";

    internal static SecureSaveOptions Parse(IEnumerable<SecureOptionArgumentNode> options)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var option in options)
            if (!values.TryAdd(option.Name, option.RawValue)) throw new SecureSaveException("InvalidOptions");
        if (values.Keys.Any(k => k is not ("#SECUREMODE" or "#SECUREKDF" or "#ARGONMEM" or "#ARGONITER" or "#ARGONLANES" or "#PBKDFITER")))
            throw new SecureSaveException("InvalidOptions");
        string mode = values.GetValueOrDefault("#SECUREMODE", "GCM");
        string kdf = values.GetValueOrDefault("#SECUREKDF", "ARGON2ID");
        if (mode is not ("GCM" or "GMAC") || kdf is not ("ARGON2ID" or "PBKDF2") ||
            kdf == "ARGON2ID" && values.ContainsKey("#PBKDFITER") ||
            kdf == "PBKDF2" && values.Keys.Any(k => k.StartsWith("#ARGON", StringComparison.Ordinal)))
            throw new SecureSaveException("InvalidOptions");
        int Number(string name, int fallback) => !values.TryGetValue(name, out var text) ? fallback :
            int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value :
            throw new SecureSaveException("InvalidOptions");
        var result = new SecureSaveOptions(mode == "GMAC", kdf == "PBKDF2", Number("#ARGONMEM", 65536),
            Number("#ARGONITER", 3), Number("#ARGONLANES", 4), Number("#PBKDFITER", 600000));
        result.Validate();
        return result;
    }

    internal void Validate()
    {
        if (Memory is < 65536 or > 262144 || Iterations is < 3 or > 6 || Lanes is < 1 or > 4 ||
            (long)Memory * Iterations > 786432 || Memory % (4 * Lanes) != 0 || Memory < 8 * Lanes ||
            PbkdfIterations is < 600000 or > 2000000)
            throw new SecureSaveException("KdfCostLimit");
    }
}
