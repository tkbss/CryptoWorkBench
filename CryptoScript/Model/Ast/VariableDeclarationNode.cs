namespace CryptoScript.Model.Ast
{
    /// <summary>
    /// Syntax model for a variable declaration, separate from its runtime value.
    /// </summary>
    public sealed record VariableDeclarationNode(
        string Identifier,
        string TypeName,
        ExpressionNode? Initializer,
        IReadOnlyList<ParameterInitializerNode> Parameters,
        Tr31HeaderInitializerNode? Tr31Header);
}
