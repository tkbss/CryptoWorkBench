using CryptoScript.Model.Ast;

namespace CryptoScript.Model;

public static class AntlrToAssignment
{
    public static AssignmentStatementNode Map(CryptoScriptParser.AssignmentContext context) =>
        new(context.ID().GetText(),
            context.expression() is { } expression ? new LiteralInitializerNode(expression.GetText()) :
                context.functionCall() is { } call ? AntlrToFunctionCallExpression.Map(call) : null,
            new ParameterListInitializerNode(Array.AsReadOnly(context.declareparam()
                .Select(AntlrToParameterInitializer.Map).ToArray())),
            context.tr31Header() is { } header ? new Tr31HeaderInitializerNode(header.GetText(),
                Array.AsReadOnly(header.tr31Field().Select(f => new HeaderAttributeArgumentNode(
                    f.headerAttribute().GetChild(0).GetText(), f.headerAttribute().GetChild(2).GetText())).ToArray())) : null,
            context.GetText());
}
