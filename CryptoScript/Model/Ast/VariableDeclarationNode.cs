namespace CryptoScript.Model.Ast
{
    /// <summary>
    /// Syntax model for a variable declaration, separate from its runtime value.
    /// TR31 headers temporarily retain their parse trees until they are migrated.
    /// </summary>
    public sealed record VariableDeclarationNode(
        string Identifier,
        string TypeName,
        LiteralInitializerNode? Expression,
        FunctionCallInitializerNode? FunctionCall,
        IReadOnlyList<ParameterInitializerNode> Parameters,
        CryptoScriptParser.Tr31HeaderContext? Tr31Header);
}
