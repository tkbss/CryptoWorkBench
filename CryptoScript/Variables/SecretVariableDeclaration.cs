using CryptoScript.Model;
using CryptoScript.SecretSharing;

namespace CryptoScript.Variables;

public sealed class SecretVariableDeclaration : VariableDeclaration
{
    public SecretVariableDeclaration(SecretSet secretSet)
    {
        SecretSet = secretSet ?? throw new ArgumentNullException(nameof(secretSet));
        Type = new CryptoTypeSecret();
    }
    public SecretSet SecretSet { get; }
    // General variable display and diagnostics must not accidentally disclose shares.
    public override string Value { get => "SECRET"; set { } }
    public override string PrintOutput()
    {
        var d = SecretSet.Descriptor;
        var lines = new List<string>
        {
            $"Mechanism: {d.Mechanism}", $"Polynomial: {d.Profile ?? "N/A"}",
            $"ShareFormat: {SecretSet.PreferredShareFormat}", $"Parts: {d.Parts}", $"MinParts: {d.MinParts}",
            $"AvailableShares: {SecretSet.Shares.Count}", $"KeySizeBits: {d.KeySizeBits}"
        };
        lines.AddRange(SecretSet.Shares.Select(s => $"Share {s.X}: 0x({Convert.ToHexString(s.Bytes)})"));
        string options = d.Mechanism == SecretDescriptor.Shamir ? $", #MINPARTS: {d.MinParts}, #POLYNOM: \"{d.Profile}\"" : "";
        lines.Add($"PARAM importedParameters = Parameters({d.Mechanism}, #PARTS: {d.Parts}{options}, #SHAREFORMAT: \"{SecretSet.PreferredShareFormat}\")");
        var shares = SecretSet.Shares.Select(s => SecretSet.PreferredShareFormat switch
        {
            "X-Y" => $"0x({s.X:X2}{Convert.ToHexString(s.Bytes)})",
            "Y-X" => $"0x({Convert.ToHexString(s.Bytes)}{s.X:X2})",
            _ => $"{s.X}, 0x({Convert.ToHexString(s.Bytes)})"
        });
        if (SecretSet.Shares.Count > 0)
            lines.Add($"SECRET importedSecret = Secret(importedParameters, {string.Join(", ", shares)})");
        return string.Join(Environment.NewLine, lines);
    }
}
