namespace CryptoScript.Model.Ast
{
    /// <summary>
    /// Syntax model for a variable declaration, separate from its runtime value.
    /// </summary>
    public sealed record VariableDeclarationNode(
        string Identifier,
        string TypeName,
        LiteralInitializerNode? Expression,
        FunctionCallInitializerNode? FunctionCall,
        IReadOnlyList<ParameterInitializerNode> Parameters,
        Tr31HeaderInitializerNode? Tr31Header);
}
