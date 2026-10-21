using CryptoScript.Model.Ast;

namespace CryptoScript.Model
{
    public static class AntlrToFunctionCallExpression
    {
        public static FunctionCallExpressionNode Map(CryptoScriptParser.FunctionCallContext context)
        {
            if (context.loadArguments() is { } load)
                return new FunctionCallExpressionNode(context.FN().GetText(), context.GetText(),
                    Array.AsReadOnly(load.loadArgument().Select(argument =>
                        argument.FN() is { } name
                            ? (FunctionCallArgumentNode)new VariableArgumentNode(name.GetText())
                            : MapArgument(argument.argument())).ToArray()));
            if (context.functionTopic() is { } topic)
                return new FunctionCallExpressionNode(context.FN().GetText(), context.GetText(),
                    Array.AsReadOnly(new FunctionCallArgumentNode[] { new InfoArgumentNode(topic.FN().GetText()) }
                        .Concat(topic.arguments()?.argument().Select(MapArgument) ?? []).ToArray()));
            return new FunctionCallExpressionNode(
                context.FN().GetText(), context.GetText(),
                Array.AsReadOnly(context.headerArguments()?.headerAttribute().Select(field =>
                    (FunctionCallArgumentNode)new HeaderAttributeArgumentNode(field.GetChild(0).GetText(), field.GetChild(2).GetText())).ToArray()
                    ?? context.arguments()?.argument().Select(MapArgument).ToArray()
                    ?? Array.Empty<FunctionCallArgumentNode>()));
        }

        public static FunctionCallArgumentNode MapArgument(CryptoScriptParser.ArgumentContext context)
        {
            if (context.onConflictParameter() is { } conflict)
                return new ParameterArgumentNode(conflict.GetChild(0).GetText(), conflict.GetChild(2).GetText());
            if (context.MECHANISM() is { } mechanism)
                return new MechanismArgumentNode(mechanism.GetText());
            if (context.functionCall() is { } call)
                return new NestedCallArgumentNode(Map(call));
            if (context.ID() is { } identifier)
            {
                if (context.Parent.Parent is CryptoScriptParser.FunctionCallContext parent &&
                    parent.FN().GetText() == "Info" &&
                    identifier.GetText() is "tr31" or "tr31attributes")
                    return new InfoArgumentNode(identifier.GetText());
                return new VariableArgumentNode(identifier.GetText());
            }
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
