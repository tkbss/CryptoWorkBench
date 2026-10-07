using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class MechanismParameterContractValidatorTests
{
    [Test]
    public void MissingFunctionMetadataSkipsDetailedParameterValidation()
    {
        var parameters = Parameters(("#UNKNOWN", "value"));

        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            "AES-ECB", CryptoScriptFunction.Encrypt, parameters));
    }

    [Test]
    public void UnsupportedFunctionIsDistinctFromMissingMetadata()
    {
        FunctionContractException error = Assert.Throws<FunctionContractException>(() =>
            MechanismParameterContractValidator.Validate(
                "AES-CBC", CryptoScriptFunction.Mac, Parameters()))!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Error, Is.EqualTo(FunctionContractError.UnsupportedFunction));
            Assert.That(error.Mechanism, Is.EqualTo("AES-CBC"));
            Assert.That(error.Function, Is.EqualTo(CryptoScriptFunction.Mac));
            Assert.That(error.ParameterName, Is.Null);
        });
    }

    [Test]
    public void RequiredInputMustBePresent()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#IV", MechanismParameterDirection.Input, required: true));

        Assert.DoesNotThrow(() => Validate(entry, Parameters(("#IV", "present"))));
        AssertContractError(
            () => Validate(entry, Parameters()),
            FunctionContractError.MissingRequiredParameter,
            "#IV");
    }

    [Test]
    public void RequiredInOutMustBePresent()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#IV", MechanismParameterDirection.InOut, required: true));

        Assert.DoesNotThrow(() => Validate(entry, Parameters(("#IV", "present"))));
        AssertContractError(
            () => Validate(entry, Parameters()),
            FunctionContractError.MissingRequiredParameter,
            "#IV");
    }

    [Test]
    public void OptionalInputMayBeAbsent()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#IV", MechanismParameterDirection.Input, required: false));

        Assert.DoesNotThrow(() => Validate(entry, Parameters()));
    }

    [Test]
    public void OutputMayBeAbsentOrAlreadyPresent()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#RND", MechanismParameterDirection.Output, required: false));

        Assert.DoesNotThrow(() => Validate(entry, Parameters()));
        Assert.DoesNotThrow(() => Validate(entry, Parameters(("#RND", "existing"))));
    }

    [Test]
    public void NoneRejectsUnlistedGloballyKnownParameter()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#IV", MechanismParameterDirection.Input, required: false));

        AssertContractError(
            () => Validate(entry, Parameters(("#NONCE", "present"))),
            FunctionContractError.ForbiddenAdditionalParameter,
            "#NONCE");
    }

    [Test]
    public void ParametersPositionalMechanismCoversStoredMechWithoutRequiringIt()
    {
        MechanismRegistryEntry entry = Entry(
            CryptoScriptFunction.Parameters,
            PositionalMechanism());

        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            entry, CryptoScriptFunction.Parameters, Parameters(("#MECH", "TEST"))));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            entry, CryptoScriptFunction.Parameters, Parameters()));
    }

    [Test]
    public void PositionalMechanismDoesNotGenerallyPermitStoredMech()
    {
        MechanismRegistryEntry entry = Entry(
            CryptoScriptFunction.GenerateKey,
            PositionalMechanism());

        AssertContractError(
            () => MechanismParameterContractValidator.Validate(
                entry,
                CryptoScriptFunction.GenerateKey,
                Parameters(("#MECH", "TEST"))),
            FunctionContractError.ForbiddenAdditionalParameter,
            "#MECH");
    }

    [TestCase(AdditionalNamedParameterHandling.IgnoreStored)]
    [TestCase(AdditionalNamedParameterHandling.StoreGloballyKnown)]
    public void PermissiveAdditionalHandlingAllowsGloballyKnownParameter(
        AdditionalNamedParameterHandling handling)
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#IV", MechanismParameterDirection.Input, required: false),
            handling);

        Assert.DoesNotThrow(() => Validate(entry, Parameters(("#NONCE", "present"))));
    }

    [Test]
    public void UnknownParameterIsRejectedEvenWhenAdditionalParametersArePermitted()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#IV", MechanismParameterDirection.Input, required: false),
            AdditionalNamedParameterHandling.IgnoreStored);

        AssertContractError(
            () => Validate(entry, Parameters(("#UNKNOWN", "present"))),
            FunctionContractError.UnknownParameter,
            "#UNKNOWN");
    }

    [Test]
    public void ParameterNamesUseExactOrdinalIgnoreCaseMatching()
    {
        MechanismRegistryEntry entry = Entry(
            Parameter("#MACLEN", MechanismParameterDirection.Input, required: true));

        Assert.DoesNotThrow(() => Validate(entry, Parameters(("maclen", "8"))));
        AssertContractError(
            () => Validate(entry, Parameters(("#OUTLEN", "8"))),
            FunctionContractError.MissingRequiredParameter,
            "#MACLEN");
        AssertContractError(
            () => Validate(entry, Parameters(("LEN", "8"))),
            FunctionContractError.UnknownParameter,
            "#LEN");
    }

    [Test]
    public void AesCbcContractsAllowGloballyKnownStoredParameters()
    {
        var creation = Parameters(("#MECH", "AES-CBC"), ("#NONCE", "present"));
        var operation = Parameters(
            ("#MECH", "AES-CBC"),
            ("#IV", "present"),
            ("#PAD", "NONE"),
            ("#NONCE", "present"));

        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            "AES-CBC", CryptoScriptFunction.Parameters, creation));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            "AES-CBC", CryptoScriptFunction.Encrypt, operation));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            "AES-CBC", CryptoScriptFunction.Decrypt, operation));

        AssertContractError(
            () => MechanismParameterContractValidator.Validate(
                "AES-CBC",
                CryptoScriptFunction.Encrypt,
                Parameters(("#MECH", "AES-CBC"), ("#PAD", "NONE"))),
            FunctionContractError.MissingRequiredParameter,
            "#IV");
    }

    [Test]
    public void AesCbcMacContractsRejectUnlistedGloballyKnownParameters()
    {
        var valid = Parameters(
            ("#MECH", "AES-CBC-MAC"),
            ("#PAD", "NONE"),
            ("#MACLEN", "8"));
        var creation = Parameters(
            ("#MECH", "AES-CBC-MAC"),
            ("#PAD", "NONE"),
            ("#MACLEN", "8"),
            ("#IV", "present"));

        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            "AES-CBC-MAC", CryptoScriptFunction.Parameters, valid));
        Assert.DoesNotThrow(() => MechanismParameterContractValidator.Validate(
            "AES-CBC-MAC", CryptoScriptFunction.Mac, valid));

        AssertContractError(
            () => MechanismParameterContractValidator.Validate(
                "AES-CBC-MAC", CryptoScriptFunction.Parameters, creation),
            FunctionContractError.ForbiddenAdditionalParameter,
            "#IV");
        AssertContractError(
            () => MechanismParameterContractValidator.Validate(
                "AES-CBC-MAC", CryptoScriptFunction.Mac, creation),
            FunctionContractError.ForbiddenAdditionalParameter,
            "#IV");
    }

    private static void Validate(
        MechanismRegistryEntry entry,
        ParameterVariableDeclaration parameters) =>
        MechanismParameterContractValidator.Validate(
            entry, CryptoScriptFunction.Encrypt, parameters);

    private static MechanismRegistryEntry Entry(
        MechanismParameterMetadata parameter,
        AdditionalNamedParameterHandling handling = AdditionalNamedParameterHandling.None) =>
        Entry(CryptoScriptFunction.Encrypt, parameter, handling);

    private static MechanismRegistryEntry Entry(
        CryptoScriptFunction function,
        MechanismParameterMetadata parameter,
        AdditionalNamedParameterHandling handling = AdditionalNamedParameterHandling.None) =>
        new("TEST", "Test", "test.md", new[] { function },
            new[]
            {
                new MechanismFunctionMetadata(
                    function, new[] { parameter }, handling)
            });

    private static MechanismParameterMetadata Parameter(
        string name,
        MechanismParameterDirection direction,
        bool required) =>
        new(name, MechanismParameterKind.NamedParameter, direction, required,
            new[] { MechanismParameterDataType.Data }, "Test", "Test");

    private static MechanismParameterMetadata PositionalMechanism() =>
        new("mechanism", MechanismParameterKind.PositionalArgument,
            MechanismParameterDirection.Input, true,
            new[] { MechanismParameterDataType.Mechanism }, "Test", "Test");

    private static ParameterVariableDeclaration Parameters(
        params (string Name, string Value)[] values)
    {
        var parameters = new ParameterVariableDeclaration();
        foreach ((string name, string value) in values)
            parameters.GetParameters()[name] = value;
        return parameters;
    }

    private static void AssertContractError(
        Action action,
        FunctionContractError expected,
        string parameterName)
    {
        FunctionContractException error = Assert.Throws<FunctionContractException>(action)!;
        Assert.Multiple(() =>
        {
            Assert.That(error.Error, Is.EqualTo(expected));
            Assert.That(error.ParameterName, Is.EqualTo(parameterName));
            Assert.That(error.Message, Does.Not.Contain("present").And.Not.Contain("value"));
        });
    }
}
