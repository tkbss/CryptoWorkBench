using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.FileEncryption;

internal static class FileEncryptionContract
{
    // Walk before evaluating any sibling: nested file operations must never become strings.
    internal static void Validate(FunctionCallExpressionNode call, List<SemanticError> errors,
        string? declarationType = null, bool nested = false)
    {
        bool file = (call.Name.Equals("Encrypt", StringComparison.OrdinalIgnoreCase) ||
            call.Name.Equals("Decrypt", StringComparison.OrdinalIgnoreCase)) &&
            call.Arguments.Count == 3 && call.Arguments[2] is VariableArgumentNode variable &&
            VariableDictionary.Instance().Get(variable.Identifier) is PathVariableDeclaration;
        if (file && (nested || declarationType is not null && declarationType != "PATH" ||
            call.Arguments.Any(a => a is NestedCallArgumentNode)))
        {
            var error = new SemanticError { Type = "FunctionContract", FunctionName = call.Name,
                DiagnosticCode = SemanticDiagnosticCode.FunctionContractViolation,
                Message = call.Name + "(PATH) returns PATH and requires a direct call with no nested arguments." };
            errors.Add(error);
            throw new SemanticErrorException { SemanticError = error };
        }
        foreach (var argument in call.Arguments.OfType<NestedCallArgumentNode>())
            Validate(argument.Call, errors, nested: true);
    }
}
