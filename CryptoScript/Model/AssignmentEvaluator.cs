using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.Model;

/// <summary>Prepare a complete replacement, then publish once under the runner's interpreter lease.</summary>
public static class AssignmentEvaluator
{
    public static VariableDeclaration Evaluate(AssignmentStatementNode assignment,
        List<SemanticError> errors, Func<FunctionCallExpressionNode, Statement> evaluateFunctionCall)
    {
        var variables = VariableDictionary.Instance();
        var target = variables.Get(assignment.Identifier);
        if (target is null)
            throw Error(errors, assignment, SemanticDiagnosticCode.UnknownVariable,
                "Assignment target is not declared.");

        int start = errors.Count;
        bool function = false;
        try
        {
            var type = target.Type ?? throw new ArgumentException("Missing declared type.");
            var initializerCount = (assignment.Initializer is null ? 0 : 1) +
                (assignment.Parameters.Items.Count == 0 ? 0 : 1) + (assignment.Tr31Header is null ? 0 : 1);
            if (initializerCount != 1)
                throw Error(errors, assignment, SemanticDiagnosticCode.InvalidAssignmentInitializer,
                    "Assignment requires exactly one initializer.");

            VariableDeclaration prepared;
            var call = assignment.Initializer as FunctionCallExpressionNode;
            if (assignment.Tr31Header is { } header)
            {
                if (type is not CryptoTypeTR31Header)
                    throw TypeMismatch(errors, assignment);
                call = new FunctionCallExpressionNode("CreateHeader", header.RawText,
                    header.Fields ?? throw new ArgumentException("Missing header fields."));
            }
            if (call is not null)
            {
                ValidateCallTree(call, type, assignment, errors);
                FileEncryption.FileEncryptionContract.Validate(call, errors, type.Name);
                FileEncryption.FileMacContract.Validate(call, errors, type.Name);
                FileEncryption.FileHashContract.Validate(call, errors, type.Name);
                function = true;
                var result = evaluateFunctionCall(call) as FunctionCall;
                if (result?.ReturnVariable?.Type is null)
                    throw Error(errors, assignment, SemanticDiagnosticCode.MissingReturnValue,
                        "Function has no usable return value.");
                prepared = result.ReturnVariable;
                // Script type compatibility is independent of the concrete result class.
                if (!string.Equals(prepared.Type.Name, type.Name, StringComparison.Ordinal))
                    throw TypeMismatch(errors, assignment);
                if (!ValidRepresentation(prepared))
                    throw Error(errors, assignment, SemanticDiagnosticCode.FunctionContractViolation,
                        "Function result does not satisfy the declared type contract.");
                // Audited operations return detached objects. Never rename/mutate a live alias.
                if (variables.GetVariables().Any(value => ReferenceEquals(value, prepared)))
                    throw Error(errors, assignment, SemanticDiagnosticCode.FunctionContractViolation,
                        "Assignment requires a detached function result.");
            }
            else if (assignment.Parameters.Items.Count != 0)
            {
                if (type is not CryptoTypeParameters) throw TypeMismatch(errors, assignment);
                prepared = ParameterInitializerEvaluator.Prepare(target.Id, type.Name,
                    assignment.Parameters.Items, errors, ParameterEvaluator.Evaluate, requireStructuredMechanism: true);
            }
            else if (assignment.Initializer is LiteralInitializerNode literal)
            {
                if (type is not (CryptoTypeVar or CryptoTypeKey or CryptoTypePath))
                    throw Error(errors, assignment, SemanticDiagnosticCode.InvalidAssignmentInitializer,
                        "This type does not accept a literal initializer.");
                // INT tokens contain ASCII digits. Reject overflow before the legacy
                // expression factory can silently fall back to an empty string.
                if (literal.RawText.Length != 0 && literal.RawText.All(char.IsAsciiDigit) &&
                    !int.TryParse(literal.RawText, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    throw Error(errors, assignment, SemanticDiagnosticCode.InvalidAssignmentInitializer,
                        "Integer assignment literal is outside the supported Int32 range.");
                var expression = type is CryptoTypePath
                    ? PathInitializerEvaluator.Prepare(literal.RawText, errors) : Expression.Create(literal.RawText);
                prepared = Expression.PrepareVariable(expression, target.Id, type) as VariableDeclaration
                    ?? throw new ArgumentException("Invalid literal.");
            }
            else throw Error(errors, assignment, SemanticDiagnosticCode.InvalidAssignmentInitializer,
                "Unsupported assignment initializer.");

            if (prepared is ParameterVariableDeclaration parameter)
            {
                // This boundary also covers detached Parameters(...) results.
                try { ParameterInitializerEvaluator.ValidateAssignmentSerialization(parameter); }
                catch (Exception)
                {
                    throw Error(errors, assignment, SemanticDiagnosticCode.InvalidAssignmentInitializer,
                        "Invalid assignment initializer.");
                }
            }

            prepared.Id = target.Id;
            prepared.Type = type;
            prepared.Text = assignment.RawText;
            // No callbacks, validation or allocation after publication.
            variables.Update(prepared);
            return prepared;
        }
        catch (SemanticErrorException exception)
        {
            // Preserve operation codes, locations and existing redaction. Shared initializer
            // helpers retain declaration diagnostics for their original callers only.
            foreach (var error in errors.Skip(start).Append(exception.SemanticError).OfType<SemanticError>())
            {
                if (error.Type == "FunctionCall" && error.DiagnosticCode is
                    SemanticDiagnosticCode.FunctionExecutionFailed or SemanticDiagnosticCode.Unclassified)
                {
                    error.DiagnosticCode = SemanticDiagnosticCode.FunctionExecutionFailed;
                    error.Message = "Assignment function failed.";
                    error.FunctionCall = error.FunctionName + "(<redacted>)";
                }
                if (error.Type != "Declaration") continue;
                error.Type = "Assignment";
                error.Identifier = assignment.Identifier;
                error.DiagnosticCode = SemanticDiagnosticCode.InvalidAssignmentInitializer;
                error.Message = "Invalid assignment initializer.";
            }
            throw;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            throw Error(errors, assignment, function ? SemanticDiagnosticCode.FunctionExecutionFailed :
                SemanticDiagnosticCode.InvalidAssignmentInitializer,
                function ? "Assignment function failed." : "Invalid assignment initializer.");
        }
    }

    private static bool ValidRepresentation(VariableDeclaration value) => value.Type switch
    {
        CryptoTypeVar => true,
        CryptoTypeKey => value is KeyVariableDeclaration key && key.KeySizeInBits.IsKnown,
        CryptoTypeVal => value is ValVariableDeclaration,
        CryptoTypeParameters => value is ParameterVariableDeclaration,
        CryptoTypePath => value is PathVariableDeclaration,
        // Definitions are immutable and can only be created through their validating factory.
        CryptoTypeTR31Header => value is BlockHeaderVariableDeclaration { Definition: not null },
        CryptoTypeSecret => value is SecretVariableDeclaration,
        _ => false
    };

    private static void ValidateCallTree(FunctionCallExpressionNode call, CryptoType targetType,
        AssignmentStatementNode assignment, List<SemanticError> errors, bool nested = false)
    {
        string name = call.Name.ToLowerInvariant();
        // Check the whole tree before evaluating any sibling, including output callbacks.
        if (name is "print" or "info" or "save" or "load" or "securesave" or "secureload" or "delete")
            throw Error(errors, assignment, SemanticDiagnosticCode.MissingReturnValue,
                "Function has no usable return value for assignment.");
        // Explicitly audited set: new operations must opt in after reviewing their mutations.
        if (name is not ("parameters" or "generatekey" or "derive" or "hash" or "mac" or
            "encrypt" or "decrypt" or "wrap" or "unwrap" or "blockheader" or "createheader" or
            "check" or "compare" or "share" or "secret" or "restore" or "pinblocktranslate" or
            "macpack" or "macverify"))
            throw Error(errors, assignment, SemanticDiagnosticCode.UnknownFunction,
                "Function is not supported as an assignment initializer.");
        // Cryptographic operations can update PARAM inputs (e.g. PIN-block outputs).
        // PARAM assignments accept only Parameters throughout the call tree, preventing
        // target mutation even through a nested operation or a legacy identifier lookup.
        if (targetType is CryptoTypeParameters && name != "parameters")
            throw TypeMismatch(errors, assignment);
        // Translation mutates its PARAM arguments, but its result/assignment target is VAR.
        if (!nested && name == "pinblocktranslate" && targetType is not CryptoTypeVar)
            throw TypeMismatch(errors, assignment);
        foreach (var child in call.Arguments.OfType<NestedCallArgumentNode>())
            ValidateCallTree(child.Call, targetType, assignment, errors, nested: true);
    }

    private static SemanticErrorException TypeMismatch(List<SemanticError> errors, AssignmentStatementNode node) =>
        Error(errors, node, SemanticDiagnosticCode.AssignmentTypeMismatch,
            "Initializer is incompatible with the declared CryptoScript type.");

    private static SemanticErrorException Error(List<SemanticError> errors, AssignmentStatementNode node,
        SemanticDiagnosticCode code, string message)
    {
        var error = new SemanticError { Type = "Assignment", Identifier = node.Identifier,
            DiagnosticCode = code, Message = message };
        errors.Add(error);
        return new SemanticErrorException { SemanticError = error };
    }
}
