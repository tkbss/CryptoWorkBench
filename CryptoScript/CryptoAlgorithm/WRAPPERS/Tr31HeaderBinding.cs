using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.WRAPPERS;

/// <summary>Per-call binding. Never modifies PARAM or its header definition.</summary>
internal static class Tr31HeaderBinding
{
    internal static Tr31HeaderDefinition? Definition(ParameterVariableDeclaration p) => p.HeaderReference?.Definition;

    internal static string Resolve(ParameterVariableDeclaration p, int dataLength)
    {
        if (Definition(p) is { } definition) return definition.SerializeForWrap(dataLength);
        string raw = FormatConversions.ToString(p.GetParameter("#BLKH"));
        if (raw.Length < 16 || raw.AsSpan(1, 4).IndexOfAnyExcept("0123456789") >= 0)
            throw new ArgumentException("TR-31 requires a complete raw header or a TR31H reference.");
        int actual = checked(raw.Length + 2 * (dataLength + TR31Block.GetAuthenticationValueLength(raw[0])));
        if (int.Parse(raw.AsSpan(1, 4)) != actual)
            throw new ArgumentException($"TR-31 header declares total length {int.Parse(raw.AsSpan(1, 4))}, but wrap produces {actual} characters.");
        return raw;
    }

    internal static void CompareAfterAuthentication(ParameterVariableDeclaration p, string header, int wireLength)
    {
        // Existing general adapters did not compare raw #BLKH on Unwrap. Preserve that contract.
        // EP2 has its own stricter, exact raw-header comparison and does not accept TR31H.
        if (Definition(p) is { } definition)
        {
            definition.ValidateAuthenticatedHeader(header);
            if (int.Parse(header.AsSpan(1, 4)) != wireLength)
                throw new ArgumentException("Authenticated KBLEN does not match the complete received wire block.");
        }
    }
}
