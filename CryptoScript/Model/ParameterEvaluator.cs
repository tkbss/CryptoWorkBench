namespace CryptoScript.Model
{
    public static class ParameterEvaluator
    {
        public static ArgumentParameter Evaluate(string type, string value) =>
            Evaluate(type, value, Variables.VariableDictionary.Instance());

        internal static ArgumentParameter Evaluate(string type, string value, Variables.VariableDictionary store)
        {
            var parameter = new ArgumentParameter();
            parameter.SetParameter(type, value, store);
            return parameter;
        }
    }
}
