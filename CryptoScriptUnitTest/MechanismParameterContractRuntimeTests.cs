using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class MechanismParameterContractRuntimeTests
{
    private const string AesKey = "0x(000102030405060708090A0B0C0D0E0F)";
    private const string AesIv = "0x(000102030405060708090A0B0C0D0E0F)";
    private const string Data = "0x(00112233445566778899AABBCCDDEEFF)";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
    }

    [Test]
    public void DirectParameterIdentityIsUsedForValidationAndAlgorithmDispatch()
    {
        ParameterVariableDeclaration parameters = AesCbcParameters();
        parameters.Value = "not-a-serialized-parameter";

        StringVariableDeclaration result = (StringVariableDeclaration)new CryptoOperations().Encrypt(
            Invocation(parameters.Value, parameters, AesKey, Data));

        Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
    }

    [Test]
    public void SerializedParameterIsResolvedLocallyForValidationAndDispatch()
    {
        ParameterVariableDeclaration parameters = AesCbcParameters();
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameters.Value, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument(AesKey, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument(Data, ResolvedCallArgumentKind.Expression)
        });

        Assert.DoesNotThrow(() => new CryptoOperations().Encrypt(invocation));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    [Test]
    public void NestedParametersKeepsExistingArgumentReductionBehavior()
    {
        SemanticErrorException exception = ExecuteError(
            $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
            $"VAR encrypted=Encrypt(Parameters(AES-CBC,#IV:{AesIv},#PAD:NONE),k,{Data})");

        Assert.Multiple(() =>
        {
            Assert.That(exception.SemanticError!.Type, Is.EqualTo("FunctionCall"));
            Assert.That(exception.SemanticError.Message, Is.EqualTo("Missing argument of type PARAM"));
            Assert.That(exception.SemanticError.ErrorCode, Is.Null);
        });
    }

    [Test]
    public void AesCbcParametersSupportsBothMechanismFormsDefaultsAndStoredNonce()
    {
        Execute(
            "PARAM plain=Parameters(AES-CBC) " +
            "PARAM named=Parameters(#MECH:AES-CBC) " +
            "PARAM withNonce=Parameters(AES-CBC,#NONCE:0x(01))");

        ParameterVariableDeclaration plain = Parameters("plain");
        ParameterVariableDeclaration named = Parameters("named");
        ParameterVariableDeclaration withNonce = Parameters("withNonce");
        Assert.Multiple(() =>
        {
            Assert.That(plain.GetParameter("IV"), Is.Not.Empty);
            Assert.That(plain.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
            Assert.That(named.GetParameter("IV"), Is.Not.Empty);
            Assert.That(named.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
            Assert.That(withNonce.GetParameter("NONCE"), Is.EqualTo("0x(01)"));
        });
    }

    [Test]
    public void AesCbcIgnoreStoredAllowsKnownAdditionalParameterDuringEncryption()
    {
        Execute(
            $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
            $"PARAM p=Parameters(AES-CBC,#IV:{AesIv},#PAD:NONE,#NONCE:0x(01)) " +
            $"VAR encrypted=Encrypt(p,k,{Data})");

        Assert.That(VariableDictionary.Instance().Contains("encrypted"), Is.True);
    }

    [Test]
    public void AesCbcMacParametersDefaultsRemainValid()
    {
        Execute("PARAM p=Parameters(#MECH:AES-CBC-MAC)");

        ParameterVariableDeclaration parameters = Parameters("p");
        Assert.Multiple(() =>
        {
            Assert.That(parameters.GetParameter("PAD"), Is.EqualTo("PKCS-7"));
            Assert.That(parameters.GetParameter("MACLEN"), Is.EqualTo("16"));
        });
    }

    [Test]
    public void AesCbcMacNoneRejectsAdditionalNamedParameterBeforeGeneration()
    {
        SemanticErrorException exception = ExecuteError(
            $"PARAM p=Parameters(#MECH:AES-CBC-MAC,#IV:{AesIv})");

        AssertContractError(exception, FunctionContractError.ForbiddenAdditionalParameter,
            "Parameters", "#IV");
    }

    [Test]
    public void MissingRequiredInputIsAFunctionContractError()
    {
        SemanticErrorException exception = ExecuteError(
            $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
            "PARAM p=#MECH:AES-CBC #PAD:NONE " +
            $"VAR encrypted=Encrypt(p,k,{Data})");

        AssertContractError(exception, FunctionContractError.MissingRequiredParameter,
            "Encrypt", "#IV");
    }

    [Test]
    public void UnknownParameterIsRejectedAtTheRuntimeHook()
    {
        ParameterVariableDeclaration parameters = AesCbcParameters();
        parameters.GetParameters()["#UNKNOWN"] = "sensitive-parameter-value";

        FunctionContractException exception = Assert.Throws<FunctionContractException>(() =>
            new CryptoOperations().Encrypt(
                Invocation(parameters.Value, parameters, AesKey, Data)))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Error, Is.EqualTo(FunctionContractError.UnknownParameter));
            Assert.That(exception.ParameterName, Is.EqualTo("#UNKNOWN"));
            Assert.That(exception.Message, Does.Not.Contain("sensitive-parameter-value"));
        });
    }

    [Test]
    public void AesCbcMacRejectsForbiddenAdditionalParameterAtMacBoundary()
    {
        var parameters = new ParameterVariableDeclaration { Mechanism = "AES-CBC-MAC" };
        parameters.SetParameter("PAD", "NONE");
        parameters.SetParameter("MACLEN", "16");
        parameters.SetParameter("IV", AesIv);

        FunctionContractException exception = Assert.Throws<FunctionContractException>(() =>
            new CryptoOperations().Mac(
                Invocation(parameters.Value, parameters, AesKey, Data)))!;

        Assert.That(exception.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
        Assert.That(exception.ParameterName, Is.EqualTo("#IV"));
    }

    [Test]
    public void UnsupportedFunctionIsRejectedBySupportedFunctionsContract()
    {
        ParameterVariableDeclaration parameters = AesCbcParameters();

        FunctionContractException exception = Assert.Throws<FunctionContractException>(() =>
            new CryptoOperations().Mac(
                Invocation(parameters.Value, parameters, AesKey, Data)))!;

        Assert.That(exception.Error, Is.EqualTo(FunctionContractError.UnsupportedFunction));
    }

    [Test]
    public void MechanismWithoutFunctionMetadataKeepsExistingRuntimeBehavior()
    {
        Execute(
            $"KEY k=GenerateKey(AES-ECB,{AesKey}) " +
            "PARAM p=Parameters(AES-ECB) " +
            $"VAR encrypted=Encrypt(p,k,{Data})");

        Assert.That(VariableDictionary.Instance().Contains("encrypted"), Is.True);
    }

    [Test]
    public void ContractFailureCreatesOneStructuredValueFreeSemanticError()
    {
        var runner = new CryptoScriptRunner();
        const string sensitiveData = "0x(FFEEDDCCBBAA99887766554433221100)";
        SemanticErrorException exception = ExecuteError(runner,
            $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
            "PARAM p=#MECH:AES-CBC #PAD:NONE " +
            $"VAR encrypted=Encrypt(p,k,{sensitiveData})");

        Assert.That(runner.SemanticErrors, Has.Count.EqualTo(1));
        Assert.That(runner.SemanticErrors[0], Is.SameAs(exception.SemanticError));
        AssertContractError(exception, FunctionContractError.MissingRequiredParameter,
            "Encrypt", "#IV");
        Assert.Multiple(() =>
        {
            Assert.That(exception.SemanticError!.FunctionCall, Is.Empty);
            Assert.That(exception.SemanticError.Value, Is.Empty);
            Assert.That(exception.SemanticError.Message, Does.Not.Contain(AesKey));
            Assert.That(exception.SemanticError.Message, Does.Not.Contain(sensitiveData));
            Assert.That(exception.SemanticError.Message, Does.Not.Contain("NONE"));
        });
    }

    [Test]
    public void NestedContractFailureIsRegisteredOnlyOnce()
    {
        var runner = new CryptoScriptRunner();
        SemanticErrorException exception = ExecuteError(runner,
            $"KEY k=GenerateKey(AES-CBC,{AesKey}) " +
            "PARAM p=#MECH:AES-CBC #PAD:NONE " +
            $"Print(Encrypt(p,k,{Data}))");

        Assert.That(runner.SemanticErrors, Has.Count.EqualTo(1));
        Assert.That(runner.SemanticErrors[0], Is.SameAs(exception.SemanticError));
        AssertContractError(exception, FunctionContractError.MissingRequiredParameter,
            "Encrypt", "#IV");
    }

    private static ParameterVariableDeclaration AesCbcParameters()
    {
        var parameters = new ParameterVariableDeclaration { Mechanism = "AES-CBC" };
        parameters.SetParameter("IV", AesIv);
        parameters.SetParameter("PAD", "NONE");
        return parameters;
    }

    private static OperationInvocation Invocation(
        string parameterValue,
        ParameterVariableDeclaration parameters,
        string key,
        string data) =>
        new(new[]
        {
            new ResolvedCallArgument(parameterValue, ResolvedCallArgumentKind.Variable, parameters),
            new ResolvedCallArgument(key, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument(data, ResolvedCallArgumentKind.Expression)
        });

    private static ParameterVariableDeclaration Parameters(string id) =>
        (ParameterVariableDeclaration)VariableDictionary.Instance().Get(id);

    private static CryptoScriptProgram Execute(string script) =>
        new CryptoScriptRunner().Execute(ParserBuilder.StringBuild(script).program());

    private static SemanticErrorException ExecuteError(string script) =>
        ExecuteError(new CryptoScriptRunner(), script);

    private static SemanticErrorException ExecuteError(CryptoScriptRunner runner, string script) =>
        Assert.Throws<SemanticErrorException>(() =>
            runner.Execute(ParserBuilder.StringBuild(script).program()))!;

    private static void AssertContractError(
        SemanticErrorException exception,
        FunctionContractError error,
        string function,
        string identifier)
    {
        Assert.That(exception.SemanticError, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(exception.SemanticError!.Type, Is.EqualTo("FunctionContract"));
            Assert.That(exception.SemanticError.FunctionName, Is.EqualTo(function));
            Assert.That(exception.SemanticError.ErrorCode, Is.EqualTo(error));
            Assert.That(exception.SemanticError.Identifier, Is.EqualTo(identifier));
        });
    }
}
