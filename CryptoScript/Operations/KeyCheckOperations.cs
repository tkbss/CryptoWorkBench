using System.Security.Cryptography;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

public static class KeyCheckOperations
{
    public static VariableDeclaration Check(string[] args) => Check(new OperationInvocation(
        args.Select(value => new ResolvedCallArgument(value, ResolvedCallArgumentKind.Expression))));

    public static VariableDeclaration Check(OperationInvocation invocation)
    {
        var args = invocation.Values;
        if (args.Length is < 1 or > 2)
            throw new ArgumentException("Check expects a HEX value and an optional method (KCV, KCVC, KCVK).");
        string value = args[0];
        if (value is null || !value.StartsWith("0x(", StringComparison.Ordinal) || !value.EndsWith(')'))
            throw new ArgumentException("Check requires a HEX value containing at least one complete byte.");
        string hex = value[3..^1];
        if (hex.Length == 0 || hex.Length % 2 != 0 || hex.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Check requires a HEX value containing at least one complete byte.");
        var methods = Enum.GetValues<KeyCheckValueMethod>();
        if (args.Length == 2)
        {
            string name = FormatConversions.ToString(args[1]);
            var selected = methods.Where(method => method.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (selected.Length != 1)
                throw new ArgumentException("Unknown Check method; expected KCV, KCVC or KCVK.");
            methods = selected;
        }
        byte[] prefix = Convert.FromHexString(hex);
        try
        {
            var matches = VariableDictionary.Instance().GetVariables().OfType<KeyVariableDeclaration>()
                .Where(key => methods.Any(method => Matches(key, method, prefix))).ToArray();
            if (matches.Length != 1)
                throw new KeyCheckValueSearchException(new SemanticError
                {
                    Type = "FunctionCall", FunctionName = "Check", FunctionCall = "Check(<redacted>)",
                    DiagnosticCode = matches.Length == 0 ? SemanticDiagnosticCode.KeyCheckValueNotFound : SemanticDiagnosticCode.KeyCheckValueAmbiguous,
                    Message = matches.Length == 0 ? "No visible KEY matches the check value." : "Multiple visible KEY variables match the check value."
                });
            var source = matches[0];
            return new KeyVariableDeclaration
            {
                Type = new CryptoTypeKey(), Value = source.Value, ValueFormat = source.ValueFormat,
                KeyValue = source.KeyValue, KeySize = source.KeySize, KeySizeInBits = source.KeySizeInBits,
                KeyType = source.KeyType, Usage = source.Usage, DerivationMechanism = source.DerivationMechanism,
                KeyAttributes = source.KeyAttributes?.Select(block => new CryptoAlgorithm.WRAPPERS.OptionalBlock
                {
                    ID = block.ID, Length = block.Length, Data = block.Data
                }).ToList() ?? []
            };
        }
        finally { CryptographicOperations.ZeroMemory(prefix); }
    }

    private static bool Matches(KeyVariableDeclaration key, KeyCheckValueMethod method, byte[] prefix)
    {
        string? value = KeyCheckValueCalculator.Calculate(key, method);
        if (value is null) return false;
        byte[] full = FormatConversions.HexStringToByteArray(value);
        try { return prefix.Length <= full.Length && prefix.AsSpan().SequenceEqual(full.AsSpan(0, prefix.Length)); }
        finally { CryptographicOperations.ZeroMemory(full); }
    }
}
