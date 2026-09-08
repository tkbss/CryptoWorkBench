using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class AntlrToFunctionCallInitializer
    {
        public static FunctionCallInitializerNode Map(CryptoScriptParser.FunctionCallContext context)
        {
            return new FunctionCallInitializerNode(
                context.FN().GetText(), context.GetText(),
                Array.AsReadOnly(context.arguments()?.argument().Select(MapArgument).ToArray()
                    ?? Array.Empty<FunctionCallArgumentNode>()));
        }

        public static FunctionCallArgumentNode MapArgument(CryptoScriptParser.ArgumentContext context)
        {
            if (context.MECHANISM() is { } mechanism)
                return new MechanismArgumentNode(mechanism.GetText());
            if (context.functionCall() is { } call)
                return new NestedCallArgumentNode(Map(call));
            if (context.ID() is { } identifier)
                return new VariableArgumentNode(identifier.GetText());
            if (context.expression() is { } expression)
                return new LiteralArgumentNode(expression.GetText());
            if (context.declareparam() is { } parameter)
                return new ParameterArgumentNode(parameter.GetChild(0).GetText(), parameter.GetChild(2).GetText());
            if (context.INFO() is { } info)
                return new InfoArgumentNode(info.GetText());
            return new EmptyArgumentNode();
        }
    }
}
