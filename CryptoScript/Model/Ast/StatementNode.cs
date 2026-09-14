namespace CryptoScript.Model.Ast
{
    public abstract record StatementNode;

    public sealed record VariableDeclarationStatementNode(
        VariableDeclarationNode Declaration, string RawText) : StatementNode;

    public sealed record FunctionCallStatementNode(
        FunctionCallExpressionNode Call) : StatementNode;
}
