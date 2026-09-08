namespace CryptoScript.Model.Ast
{
    public sealed record FunctionCallInitializerNode(
        string Name,
        string CallText,
        IReadOnlyList<FunctionCallArgumentNode> Arguments);

    public abstract record FunctionCallArgumentNode;
    public sealed record MechanismArgumentNode(string RawText) : FunctionCallArgumentNode;
    public sealed record VariableArgumentNode(string Identifier) : FunctionCallArgumentNode;
    public sealed record LiteralArgumentNode(string RawText) : FunctionCallArgumentNode;
    public sealed record ParameterArgumentNode(string TypeName, string RawValue) : FunctionCallArgumentNode;
    public sealed record InfoArgumentNode(string RawText) : FunctionCallArgumentNode;
    public sealed record NestedCallArgumentNode(FunctionCallInitializerNode Call) : FunctionCallArgumentNode;
    public sealed record EmptyArgumentNode : FunctionCallArgumentNode;
}
