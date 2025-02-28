namespace CryptoScript.Model
{
    public class ExpressionHex : Expression
    {
        public string HexValue { get; set; } = string.Empty;

        public override string Value()
        {
            return HexValue;
        }
    }
}
