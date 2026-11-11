namespace CryptoScript.Model.Ast
{
    public sealed record FunctionCallExpressionNode(
        string Name,
        string CallText,
        IReadOnlyList<FunctionCallArgumentNode> Arguments) : ExpressionNode
    {
        public SourcePosition? Source { get; init; }
    }

    /// <summary>One-based parser token coordinates; null means no parser source.</summary>
    public sealed record SourcePosition(int Line, int Column);

    public abstract record FunctionCallArgumentNode;
    public sealed record SecureOptionArgumentNode(string Name, string RawValue) : FunctionCallArgumentNode;
    public sealed record HeaderAttributeArgumentNode(string Name, string RawValue) : FunctionCallArgumentNode;
    public sealed record MechanismArgumentNode(string RawText) : FunctionCallArgumentNode;
    public sealed record VariableArgumentNode(string Identifier) : FunctionCallArgumentNode;
    public sealed record LiteralArgumentNode(string RawText) : FunctionCallArgumentNode;
    public sealed record ParameterArgumentNode(string TypeName, string RawValue) : FunctionCallArgumentNode;
    public sealed record InfoArgumentNode(string RawText) : FunctionCallArgumentNode;
    public sealed record NestedCallArgumentNode(FunctionCallExpressionNode Call) : FunctionCallArgumentNode;
    public sealed record EmptyArgumentNode : FunctionCallArgumentNode;
}
