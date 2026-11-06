using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.Model;

/// <summary>Shared PARAM validation without dictionary publication.</summary>
internal static class ParameterInitializerEvaluator
{
    internal static ParameterVariableDeclaration Prepare(string Id, string TypeName,
        IReadOnlyList<ParameterInitializerNode> declarParam, List<SemanticError> semanticErrors,
        Func<string, string, ArgumentParameter> evaluateParameter, bool requireStructuredMechanism = false)
    {
        // Keep the historical declaration path; assignments require the actual AST field.
        var mech = declarParam.FirstOrDefault(c => requireStructuredMechanism
            ? c.TypeName == "#MECH" : c.RawText.Contains("MECH"));
        if (mech == null)
        {
            SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
            se.Message = "Type PARAM does not contain element #MECH";
            semanticErrors.Add(se);
            throw new SemanticErrorException() { SemanticError = se };
        }
        // Resolve and compare all actual MECH values before constructing the replacement.
        string? effectiveMechanism = requireStructuredMechanism
            ? ResolveAssignmentMechanism(declarParam, evaluateParameter) : null;
        var Parameter = new ParameterVariableDeclaration();
        Parameter.Mechanism = effectiveMechanism ?? mech.RawText;
        bool mechanismRecorded = false;
        for (int i = 0; i < declarParam.Count; i++)
        {
            if (requireStructuredMechanism && declarParam[i].TypeName == "#MECH")
            {
                // Equivalent repetitions describe one effective parameter, including
                // its provenance. Other duplicate parameters retain their validators.
                if (!mechanismRecorded) Parameter.RecordExplicitParameter("#MECH");
                mechanismRecorded = true;
                continue;
            }
            try
            {
                // Preserve the former missing-child failure inside this runtime error boundary.
                var param = evaluateParameter(
                    declarParam[i].TypeName ?? throw new NullReferenceException(),
                    declarParam[i].RawValue ?? throw new NullReferenceException());
                if (requireStructuredMechanism) ValidateAssignmentValue(param.Value);
                Parameter.SetParameter(param);
                Parameter.RecordExplicitParameter(param.Type);
            }
            catch
            {
                SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
                se.Message = declarParam[i].TypeName == "#PSN"
                    ? "Error in parameter declaration: #PSN:<redacted>"
                    : "Error in  parameter declaration : " + declarParam[i].RawText;
                semanticErrors.Add(se);
                throw new SemanticErrorException() { SemanticError = se };
            }

        }
        if (requireStructuredMechanism && (Parameter.Mechanism != effectiveMechanism ||
            Parameter.GetParameter("MECH") != effectiveMechanism))
            throw new ArgumentException("Inconsistent assignment mechanism.");
        try
        {
            if (SecretSharing.SecretParameters.IsSecretMechanism(Parameter.Mechanism))
                _ = SecretSharing.SecretParameters.Read(Parameter);
            MechanismParameterContractValidator.ValidatePsnSupport(Parameter.Mechanism, Parameter);
            MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(Parameter.Mechanism, Parameter);
            if (Parameter.Mechanism is "EP2-DATA-ENCRYPTION" or "EP2-PAN-RECEIPT-ENCRYPTION" or "EP2-TR31-KEY-BLOCK")
                MechanismParameterContractValidator.Validate(Parameter.Mechanism, CryptoScriptFunction.Parameters, Parameter);
        }
        catch (Exception exception) when (exception is FunctionContractException ||
            SecretSharing.SecretParameters.IsSecretMechanism(Parameter.Mechanism) && exception is ArgumentException ||
            (Parameter.Mechanism is "EP2-DATA-ENCRYPTION" or "EP2-PAN-RECEIPT-ENCRYPTION" or "EP2-TR31-KEY-BLOCK") && exception is ArgumentException)
        {
            var error = new SemanticError { Type = "Declaration", Identifier = TypeName, Message = exception.Message };
            semanticErrors.Add(error);
            throw new SemanticErrorException { SemanticError = error };
        }
        Parameter.Id = Id;
        Parameter.Type = CryptoType.Parse(TypeName);
        return Parameter;
    }

    private static string ResolveAssignmentMechanism(IReadOnlyList<ParameterInitializerNode> parameters,
        Func<string, string, ArgumentParameter> evaluateParameter)
    {
        string? effective = null;
        foreach (var parameter in parameters.Where(p => p.TypeName == "#MECH"))
        {
            string value = evaluateParameter("#MECH", parameter.RawValue
                ?? throw new ArgumentException("Missing mechanism value.")).Value;
            // Match the existing selector-prefix and case normalization for known
            // mechanisms, without adding registry validation to legacy PARAM literals.
            if (value.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase))
                value = value["#MECH:".Length..];
            ValidateAssignmentValue(value);
            if (MechanismRegistry.TryGet(value.ToUpperInvariant(), out var entry))
                value = entry!.CanonicalName;
            if (effective is not null && !string.Equals(effective, value, StringComparison.Ordinal))
                throw new ArgumentException("Conflicting assignment mechanisms.");
            effective = value;
        }
        return effective ?? throw new ArgumentException("Missing assignment mechanism.");
    }

    private static void ValidateAssignmentValue(string value)
    {
        // The legacy wire format has no escape for '#', even inside quoted strings.
        // Reject before setters can reinterpret a selector or discard value segments.
        if (value.Contains('#'))
            throw new ArgumentException("Ambiguous assignment parameter value.");
    }

    internal static void ValidateAssignmentSerialization(ParameterVariableDeclaration prepared)
    {
        ValidateAssignmentValue(prepared.Mechanism);
        foreach (string value in prepared.GetParameters().Values)
            ValidateAssignmentValue(value);

        // Use the actual consumer parser on a detached object. Do not replace the
        // prepared object: its header reference and explicit provenance must survive.
        var consumed = new ParameterVariableDeclaration();
        consumed.SetInstance(prepared.Value);
        var parameters = prepared.GetParameters();
        var roundtrip = consumed.GetParameters();
        if (prepared.Mechanism != prepared.GetParameter("MECH") ||
            consumed.Mechanism != prepared.Mechanism ||
            consumed.Value != prepared.Value || roundtrip.Count != parameters.Count ||
            parameters.Any(pair => !roundtrip.TryGetValue(pair.Key, out var value) || value != pair.Value))
            throw new ArgumentException("Lossy assignment parameter serialization.");
    }
}
