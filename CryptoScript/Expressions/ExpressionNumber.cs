namespace CryptoScript.Model
{
    public class ExpressionNumber : Expression
    {
        public string IntegerValue { get; set; } = string.Empty;

        public override string Value()
        {
            return IntegerValue;
        }
    }
}
