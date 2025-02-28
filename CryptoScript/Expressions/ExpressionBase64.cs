namespace CryptoScript.Model
{
    public class ExpressionBase64 : Expression
    {
        public string Base64Value { get; set; } = string.Empty;

        public override string Value()
        {
            return Base64Value;
        }
    }
}
