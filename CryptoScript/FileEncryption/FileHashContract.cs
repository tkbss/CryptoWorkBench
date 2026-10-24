using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.FileEncryption;

internal static class FileHashContract
{
    internal static void Validate(FunctionCallExpressionNode call, List<SemanticError> errors,
        string? declarationType = null, bool nested = false)
    {
        bool file = call.Name.Equals("Hash", StringComparison.OrdinalIgnoreCase) &&
            call.Arguments.OfType<VariableArgumentNode>().Any(a =>
                VariableDictionary.Instance().Get(a.Identifier) is PathVariableDeclaration);
        if (file && (nested || declarationType is not null && declarationType != "VAR" ||
            call.Arguments.Count != 2 || call.Arguments[0] is not VariableArgumentNode parameter ||
            VariableDictionary.Instance().Get(parameter.Identifier) is not ParameterVariableDeclaration ||
            call.Arguments[1] is not VariableArgumentNode source ||
            VariableDictionary.Instance().Get(source.Identifier) is not PathVariableDeclaration))
        {
            var error = new SemanticError { Type = "FunctionContract", FunctionName = call.Name,
                DiagnosticCode = SemanticDiagnosticCode.FunctionContractViolation,
                Message = "Hash(PATH) returns VAR and requires a direct call with PARAM, PATH references and no nested arguments." };
            errors.Add(error);
            throw new SemanticErrorException { SemanticError = error };
        }
        foreach (var argument in call.Arguments.OfType<NestedCallArgumentNode>())
            Validate(argument.Call, errors, nested: true);
    }
}
