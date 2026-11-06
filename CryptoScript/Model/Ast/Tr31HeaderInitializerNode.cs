namespace CryptoScript.Model.Ast
{
    public sealed record Tr31HeaderInitializerNode(string RawText,
        IReadOnlyList<HeaderAttributeArgumentNode>? Fields = null);
}
