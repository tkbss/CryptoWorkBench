using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class AntlrToStatement
    {
        public static StatementNode? Map(CryptoScriptParser.StatementContext context)
        {
            if (context.functionCall() is { } call)
                return new FunctionCallStatementNode(AntlrToFunctionCallInitializer.Map(call));
            if (context.declaration() is { } declaration)
                return new VariableDeclarationStatementNode(
                    AntlrToVariableDeclaration.Map(declaration), context.GetText());

            // An empty recovery context previously produced no runtime statement.
            return null;
        }
    }
}
