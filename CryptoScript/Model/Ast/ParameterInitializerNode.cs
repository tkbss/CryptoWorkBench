namespace CryptoScript.Model.Ast
{
    /// <summary>Unevaluated parameter syntax, including the text used by runtime diagnostics.</summary>
    public sealed record ParameterInitializerNode(string? TypeName, string? RawValue, string RawText);
}
