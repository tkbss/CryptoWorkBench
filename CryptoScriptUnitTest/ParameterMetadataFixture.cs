using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

internal static class ParameterMetadataFixture
{
    // These fixtures deliberately install malformed raw metadata to exercise downstream contracts.
    internal static void EditMetadata(this ParameterVariableDeclaration parameter, Action<Dictionary<string, string>> edit)
    {
        var metadata = new Dictionary<string, string>(parameter.GetParameters(), StringComparer.Ordinal);
        edit(metadata);
        parameter.ReplaceParameters(metadata);
    }
}
