using CryptoScript.ErrorListner;
using CryptoScript.Model.Ast;
using CryptoScript.Variables;

namespace CryptoScript.Model;

/// <summary>Shared PARAM validation without dictionary publication.</summary>
internal static class ParameterInitializerEvaluator
{
    internal static ParameterVariableDeclaration Prepare(string Id, string TypeName,
        IReadOnlyList<ParameterInitializerNode> declarParam, List<SemanticError> semanticErrors,
        Func<string, string, ArgumentParameter> evaluateParameter)
    {
        var mech=declarParam.FirstOrDefault(c => c.RawText.Contains("MECH"));
        if (mech == null)
        {
            SemanticError se = new SemanticError() { Type = "Declaration", Identifier = TypeName };
            se.Message = "Type PARAM does not contain element #MECH";
            semanticErrors.Add(se);
            throw new SemanticErrorException() { SemanticError = se };
        }
        var Parameter = new ParameterVariableDeclaration();
        Parameter.Mechanism = mech.RawText;
        for (int i = 0; i < declarParam.Count; i++)
        {
            try
            {
                // Preserve the former missing-child failure inside this runtime error boundary.
                var param = evaluateParameter(
                    declarParam[i].TypeName ?? throw new NullReferenceException(),
                    declarParam[i].RawValue ?? throw new NullReferenceException());
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
}
