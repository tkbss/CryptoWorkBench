namespace CryptoScript.Model.Ast
{
    public sealed record ParameterListInitializerNode(
        IReadOnlyList<ParameterInitializerNode> Items);
}
