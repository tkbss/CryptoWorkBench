using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScript.CryptoAlgorithm.PINBLOCK;

internal sealed class PinBlockTranslationException(SemanticDiagnosticCode code, string message)
    : Exception(message)
{
    internal SemanticDiagnosticCode Code { get; } = code;
}

public sealed partial class PinBlockAlgorithm
{
    // Instance-local fault boundaries, available only to internal tests through algorithm dispatch.
    internal Action<string>? TranslationCheckpoint { get; init; }
    internal Func<byte[], byte[], byte[]>? TranslationEncrypt { get; init; }

    internal static VariableDeclaration Translate(OperationInvocation invocation,
        Func<string, CryptoAlgorithm> algorithmFactory)
    {
        if (invocation.Arguments.Count != 5 ||
            invocation.Arguments.Any(a => a.Kind != ResolvedCallArgumentKind.Variable) ||
            invocation.Arguments[0].SourceVariable is not ParameterVariableDeclaration { Type: CryptoTypeParameters } source ||
            invocation.Arguments[1].SourceVariable is not { Type: CryptoTypeKey } ||
            invocation.Arguments[2].SourceVariable is not StringVariableDeclaration { Type: CryptoTypeVar } ||
            invocation.Arguments[3].SourceVariable is not ParameterVariableDeclaration { Type: CryptoTypeParameters } target ||
            invocation.Arguments[4].SourceVariable is not { Type: CryptoTypeKey })
            throw Failure(SemanticDiagnosticCode.FunctionContractViolation,
                "PinBlockTranslate requires exactly five genuine references: PARAM, KEY, VAR, PARAM, KEY.");
        if (ReferenceEquals(source, target))
            throw Failure(SemanticDiagnosticCode.FunctionContractViolation,
                "PinBlockTranslate requires two distinct PARAM objects.");

        var sourceAlgorithm = ForTranslation(source, algorithmFactory);
        var targetAlgorithm = ForTranslation(target, algorithmFactory);
        var rule = PinBlockTranslationRules.Get(sourceAlgorithm.Format, targetAlgorithm.Format);
        if (rule.Status == PinBlockTranslationStatus.Forbidden)
            throw Failure(SemanticDiagnosticCode.PinBlockTranslationForbidden,
                "This PIN-block format translation is forbidden by PCI PIN Requirement 3-3.");

        // Reject full-PAN differences before decoding or decrypting anything.
        ValidateTranslationContract(source, CryptoScriptFunction.Unwrap, "source", rule.SourcePanRequired);
        ValidateTranslationContract(target, CryptoScriptFunction.Wrap, "target", rule.TargetPanRequired);
        string? sourcePan = ValidateTranslationPan(source, rule.SourcePanRequired);
        string? targetPan = ValidateTranslationPan(target, rule.TargetPanRequired);
        if (rule.IdenticalPanRequired && !string.Equals(sourcePan, targetPan, StringComparison.Ordinal))
            throw Failure(SemanticDiagnosticCode.PinBlockPanMismatch,
                "Translation requires identical complete source and target PANs.");

        PinBlockArguments? resolvedSource = null;
        PinBlockArguments? to = null;
        try
        {
            resolvedSource = sourceAlgorithm.ResolveArguments(Arguments(0, 1), false, false, CryptoScriptFunction.Unwrap, "PinBlockTranslate");
            to = targetAlgorithm.ResolveArguments(Arguments(3, 4), false, false, CryptoScriptFunction.Wrap, "PinBlockTranslate");
            ValidateTargetField(target, targetAlgorithm.Format);
            var sourceStage = source.StagePinTranslation();
            var targetStage = target.StagePinTranslation();
            VariableDeclaration pin;
            try
            {
                pin = sourceAlgorithm.DecodeResolved(resolvedSource with { Parameters = sourceStage });
            }
            catch (Exception e) when (e is FormatException or ArgumentException or CryptographicException)
            {
                throw Failure(SemanticDiagnosticCode.InvalidPinBlock, "Encrypted PIN block or decoded PIN-block structure is invalid.");
            }
            // This temporary object never enters the script variable dictionary or output stream.
            targetAlgorithm.TranslationCheckpoint?.Invoke("after-source-decode");
            StringVariableDeclaration result;
            try
            {
                result = targetAlgorithm.EncodeResolved(to with { Parameters = targetStage },
                    GetPin((StringVariableDeclaration)pin));
            }
            catch (ArgumentException)
            {
                throw Failure(SemanticDiagnosticCode.InvalidParameter, "Invalid target PIN-block parameters for the decoded PIN length.");
            }
            targetAlgorithm.TranslationCheckpoint?.Invoke("before-publish");
            ParameterVariableDeclaration.CommitPinTranslation(source, sourceStage, target, targetStage);
            return result;
        }
        finally
        {
            if (resolvedSource is not null) CryptographicOperations.ZeroMemory(resolvedSource.Key);
            if (to is not null) CryptographicOperations.ZeroMemory(to.Key);
        }

        AlgorithmCallArguments Arguments(int parameter, int key) => new(
            new[] { invocation.Arguments[parameter], invocation.Arguments[key], invocation.Arguments[2] }
                .Select(a => new AlgorithmCallArgument(a.Value, a.SourceVariable, a.Kind)), invocation.DiagnosticSink);
    }

    private static void ValidateTranslationContract(ParameterVariableDeclaration parameters,
        CryptoScriptFunction function, string side, bool usesPan)
    {
        // Preserve the existing PAN-specific validation for unused PANs.
        if (!usesPan)
            ValidateTranslationPan(parameters, false);
        try { MechanismParameterContractValidator.Validate(parameters.Mechanism, function, parameters); }
        catch (FunctionContractException cause) { throw new FunctionContractException(cause, side); }
    }

    private static PinBlockAlgorithm ForTranslation(ParameterVariableDeclaration parameters,
        Func<string, CryptoAlgorithm> algorithmFactory)
    {
        if (parameters.GetParameter("MECH") != parameters.Mechanism ||
            parameters.ExplicitParameterNames.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            throw Failure(SemanticDiagnosticCode.InvalidMechanism, "PIN-block PARAM requires a consistent, unique mechanism declaration.");
        for (int format = 0; format < 5; format++)
            if (parameters.Mechanism == PinBlockTranslationRules.Mechanism(format))
                return (PinBlockAlgorithm)algorithmFactory(parameters.Mechanism);
        throw Failure(SemanticDiagnosticCode.InvalidMechanism, "Translation requires a supported PIN-block mechanism on each PARAM.");
    }

    private static string? ValidateTranslationPan(ParameterVariableDeclaration parameters, bool required)
    {
        try
        {
            if (!required)
            {
                if (parameters.GetParameters().Keys.Any(k => k.TrimStart('#').Equals("PAN", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException();
                return null;
            }
            string pan = GetHexNibbles(parameters, "PAN");
            PinBlockFieldCodec.ValidatePan(pan);
            return pan;
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            throw Failure(SemanticDiagnosticCode.InvalidParameter,
                "PAN must be present only for formats 0, 3 and 4, as 10 to 19 decimal HEX nibbles including the check digit.");
        }
    }

    private static void ValidateTargetField(ParameterVariableDeclaration target, int format)
    {
        string? name = format switch { 1 => "TRANSACTION", 3 => "FILL", 4 => "RANDOM", _ => null };
        if (name is null) return;
        string? value;
        try { value = GetOptionalHexNibbles(target, name); }
        catch (Exception e) when (e is ArgumentException or FormatException)
        {
            throw Failure(SemanticDiagnosticCode.InvalidParameter, "Target PIN-block fields must be HEX nibble sequences.");
        }
        if (value is null) return;
        bool valid = format == 4 ? value.Length == 16 && value.All(Uri.IsHexDigit)
            : value.Length is >= 2 and <= 10 && value.All(c => format == 3
                ? char.ToUpperInvariant(c) is >= 'A' and <= 'F' : Uri.IsHexDigit(c));
        if (!valid)
            throw Failure(SemanticDiagnosticCode.InvalidParameter, "Invalid explicit target PIN-block field.");
        // Exact field length for formats 1 and 3 additionally depends on the decoded PIN length.
    }

    private static PinBlockTranslationException Failure(SemanticDiagnosticCode code, string message) => new(code, message);
}
