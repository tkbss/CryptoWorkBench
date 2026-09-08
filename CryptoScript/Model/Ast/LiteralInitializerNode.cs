namespace CryptoScript.Model.Ast
{
    /// <summary>
    /// Literal syntax text, preserved without decoding or runtime classification.
    /// </summary>
    public sealed record LiteralInitializerNode(string RawText);
}
