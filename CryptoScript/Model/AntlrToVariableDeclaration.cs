using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class AntlrToVariableDeclaration
    {
        // Mapping only: do not evaluate initializers or resolve runtime types here.
        public static VariableDeclarationNode Map(CryptoScriptParser.DeclarationContext context)
        {
            var expression = context.expression();
            return new VariableDeclarationNode(
                context.ID().GetText(),
                context.type().GetText(),
                expression == null ? null : new LiteralInitializerNode(expression.GetText()),
                context.functionCall(),
                Array.AsReadOnly(context.declareparam()),
                context.tr31Header());
        }
    }
}
