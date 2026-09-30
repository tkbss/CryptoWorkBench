using CryptoScript.Model;

namespace CryptoScriptUnitTest;

public class MechanismMetadataContractTests
{
    [Test]
    public void RegistryEntryRejectsMetadataForUnsupportedFunctions()
    {
        var metadata = new MechanismFunctionMetadata(
            CryptoScriptFunction.Decrypt, Array.Empty<MechanismParameterMetadata>());

        Assert.Throws<ArgumentException>(() => new MechanismRegistryEntry(
            "TEST", "Test", "test.md", new[] { CryptoScriptFunction.Encrypt },
            new[] { metadata }));
    }

    [Test]
    public void ParameterMetadataRejectsInconsistentDefaultKindAndValueCombinations()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDefaultKind.None, "PKCS-7"));
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDefaultKind.Literal, null));
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDefaultKind.Generated, null));
        });
    }

    [Test]
    public void FunctionMetadataRejectsCaseInsensitiveDuplicateParameterNames()
    {
        Assert.Throws<ArgumentException>(() => new MechanismFunctionMetadata(
            CryptoScriptFunction.Parameters, new[]
            {
                ParameterMetadata(name: "#IV"),
                ParameterMetadata(name: "#iv")
            }));
    }

    [Test]
    public void ParameterMetadataRejectsUndefinedInputForms()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MechanismParameterMetadata(
            "#IV", MechanismParameterKind.NamedParameter, false,
            new[] { MechanismParameterDataType.BinaryData }, "Test", "Test",
            acceptedInputForms: new[] { (MechanismParameterInputForm)int.MaxValue }));
    }

    [Test]
    public void FunctionMetadataRejectsUndefinedAdditionalNamedParameterHandling()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MechanismFunctionMetadata(
            CryptoScriptFunction.Parameters, Array.Empty<MechanismParameterMetadata>(),
            (AdditionalNamedParameterHandling)int.MaxValue));
    }

    private static MechanismParameterMetadata ParameterMetadata(
        MechanismParameterDefaultKind defaultKind = MechanismParameterDefaultKind.None,
        string? defaultValue = null,
        string name = "#PAD") =>
        new(name, MechanismParameterKind.NamedParameter, false,
            new[] { MechanismParameterDataType.Padding }, "Test", "Test",
            defaultKind, defaultValue);
}
