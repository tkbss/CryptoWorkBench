namespace CryptoScript.Model
{
    public class ExpressionString : Expression
    {
        public string StringValue { get; set; } = string.Empty;
        public override string Value()
        {
            return StringValue;
        }
    }
}
