namespace CryptoScript.Model
{
    public static class ParameterEvaluator
    {
        public static ArgumentParameter Evaluate(string type, string value)
        {
            var parameter = new ArgumentParameter();
            parameter.SetParameter(type, value);
            return parameter;
        }
    }
}
