namespace CryptoScript.Model
{
    public class ExpressionJSON : Expression
    {
        public string JSONValue { get; set; } = string.Empty;

        public override string Value()
        {
            return JSONValue;
        }
    }
}
