using System.Globalization;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.SecretSharing;

internal sealed record SecretParameters(string Mechanism, int Parts, int MinParts, string? Profile, string Format)
{
    internal SecretDescriptor Descriptor(int bits) => new(Mechanism, Parts, MinParts, bits, Profile);
    internal static bool IsSecretMechanism(string mechanism) => mechanism is SecretDescriptor.Split or SecretDescriptor.Shamir;
    internal static SecretParameters Read(ParameterVariableDeclaration parameters)
    {
        string mechanism = parameters.Mechanism;
        if (!IsSecretMechanism(mechanism) || parameters.GetParameter("#MECH") != mechanism)
            throw new ArgumentException("A consistent secret sharing mechanism is required.");
        var allowed = mechanism == SecretDescriptor.Split
            ? new[] { "#MECH", "#PARTS", "#SHAREFORMAT" }
            : new[] { "#MECH", "#PARTS", "#MINPARTS", "#POLYNOM", "#SHAREFORMAT" };
        if (parameters.GetParameters().Keys.Any(n => !allowed.Contains(n)) ||
            parameters.ExplicitParameterNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parameters.ExplicitParameterNames.Count)
            throw new ArgumentException("Forbidden or duplicate secret sharing parameter.");
        int parts = Integer(parameters.GetParameter("#PARTS"));
        int min = mechanism == SecretDescriptor.Split ? parts : Integer(parameters.GetParameter("#MINPARTS"));
        string? profile = mechanism == SecretDescriptor.Split ? null : ReadString(parameters, "#POLYNOM", SecretDescriptor.Profile11B);
        string format = ReadString(parameters, "#SHAREFORMAT", "X-Y");
        SecretSet.ValidateFormat(format);
        _ = new SecretDescriptor(mechanism, parts, min, 8, profile);
        return new(mechanism, parts, min, profile, format);
    }
    private static string ReadString(ParameterVariableDeclaration parameters, string name, string fallback)
    {
        if (!parameters.GetParameters().ContainsKey(name)) return fallback;
        string text = parameters.GetParameter(name);
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"') throw new ArgumentException("Expected quoted secret sharing parameter.");
        return text[1..^1];
    }
    internal static int Integer(string? text)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int result))
            throw new ArgumentException("Expected decimal integer.");
        return result;
    }
}
