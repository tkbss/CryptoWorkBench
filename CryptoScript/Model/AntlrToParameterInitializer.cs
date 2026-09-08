using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class AntlrToParameterInitializer
    {
        public static ParameterInitializerNode Map(CryptoScriptParser.DeclareparamContext context)
        {
            return new ParameterInitializerNode(
                context.GetChild(0)?.GetText(), context.GetChild(2)?.GetText(), context.GetText());
        }
    }
}
