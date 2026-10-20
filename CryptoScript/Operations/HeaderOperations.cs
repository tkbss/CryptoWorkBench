using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.Operations;

internal static class HeaderOperations
{
    internal static VariableDeclaration CreateHeader(OperationInvocation invocation)
    {
        if (invocation.Arguments.Any(a => a.Kind != ResolvedCallArgumentKind.HeaderAttribute))
            throw new ArgumentException("CreateHeader accepts only named TR-31 attributes.");
        return new BlockHeaderVariableDeclaration
        {
            Definition = Tr31HeaderDefinition.Create(invocation.Values.Select(value =>
            {
                string[] parts = value.Split(':', 2);
                return KeyValuePair.Create(parts[0], parts[1]);
            }))
        };
    }
}
