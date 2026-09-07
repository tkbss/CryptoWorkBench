namespace CryptoScript.Model.Ast
{
    /// <summary>
    /// Syntax model for a variable declaration, separate from its runtime value.
    /// Initializers temporarily retain their parse trees until they are migrated.
    /// </summary>
    public sealed record VariableDeclarationNode(
        string Identifier,
        string TypeName,
        CryptoScriptParser.ExpressionContext? Expression,
        CryptoScriptParser.FunctionCallContext? FunctionCall,
        IReadOnlyList<CryptoScriptParser.DeclareparamContext> Parameters,
        CryptoScriptParser.Tr31HeaderContext? Tr31Header);
}
