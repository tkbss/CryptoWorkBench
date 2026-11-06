namespace CryptoScript.Model.Ast;

/// <summary>Syntax only; the target type is resolved at execution time.</summary>
public sealed record AssignmentStatementNode(
    string Identifier,
    ExpressionNode? Initializer,
    ParameterListInitializerNode Parameters,
    Tr31HeaderInitializerNode? Tr31Header,
    string RawText) : StatementNode;
