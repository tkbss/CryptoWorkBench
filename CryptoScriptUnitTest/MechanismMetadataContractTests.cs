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
                MechanismParameterDirection.Input, MechanismParameterDefaultKind.None, "PKCS-7"));
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDirection.Input, MechanismParameterDefaultKind.Literal, null));
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDirection.Input, MechanismParameterDefaultKind.Generated, null));
        });
    }

    [Test]
    public void FunctionMetadataRejectsCaseInsensitiveDuplicateParameterNames()
    {
        Assert.Throws<ArgumentException>(() => new MechanismFunctionMetadata(
            CryptoScriptFunction.Parameters, new[]
            {
                ParameterMetadata(MechanismParameterDirection.Input, name: "#IV"),
                ParameterMetadata(MechanismParameterDirection.Input, name: "#iv")
            }));
    }

    [Test]
    public void ParameterMetadataRejectsUndefinedInputForms()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MechanismParameterMetadata(
            "#IV", MechanismParameterKind.NamedParameter, MechanismParameterDirection.Input, false,
            new[] { MechanismParameterDataType.BinaryData }, "Test", "Test",
            acceptedInputForms: new[] { (MechanismParameterInputForm)int.MaxValue }));
    }

    [TestCase(MechanismParameterDirection.Input, true)]
    [TestCase(MechanismParameterDirection.Output, false)]
    [TestCase(MechanismParameterDirection.InOut, true)]
    public void ParameterMetadataStoresValidDirection(
        MechanismParameterDirection direction, bool isRequired)
    {
        MechanismParameterMetadata metadata = ParameterMetadata(direction, isRequired: isRequired);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.Direction, Is.EqualTo(direction));
            Assert.That(metadata.IsRequired, Is.EqualTo(isRequired));
            Assert.That(metadata.ResultingDataTypes,
                Is.EquivalentTo(new[] { MechanismParameterDataType.Padding }));
        });
    }

    [Test]
    public void ParameterMetadataRejectsUndefinedDirection()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ParameterMetadata((MechanismParameterDirection)int.MaxValue));
    }

    [Test]
    public void OutputParameterRejectsRequiredInputDefaultAndAcceptedInputForms()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDirection.Output, isRequired: true));
            Assert.Throws<ArgumentException>(() => ParameterMetadata(
                MechanismParameterDirection.Output,
                defaultKind: MechanismParameterDefaultKind.Literal,
                defaultValue: "PKCS-7"));
            Assert.Throws<ArgumentException>(() => new MechanismParameterMetadata(
                "#PAD", MechanismParameterKind.NamedParameter,
                MechanismParameterDirection.Output, false,
                new[] { MechanismParameterDataType.Padding }, "Test", "Test",
                acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral }));
        });
    }

    [Test]
    public void InOutParameterAllowsRequiredInputMetadata()
    {
        var metadata = new MechanismParameterMetadata(
            "#PAD", MechanismParameterKind.NamedParameter,
            MechanismParameterDirection.InOut, true,
            new[] { MechanismParameterDataType.Padding }, "Test", "Test",
            MechanismParameterDefaultKind.Literal, "PKCS-7",
            acceptedInputForms: new[] { MechanismParameterInputForm.StringLiteral });

        Assert.Multiple(() =>
        {
            Assert.That(metadata.Direction, Is.EqualTo(MechanismParameterDirection.InOut));
            Assert.That(metadata.IsRequired, Is.True);
            Assert.That(metadata.DefaultValue, Is.EqualTo("PKCS-7"));
            Assert.That(metadata.AcceptedInputForms,
                Is.EquivalentTo(new[] { MechanismParameterInputForm.StringLiteral }));
        });
    }

    [Test]
    public void DirectionPropertyIsReadOnly()
    {
        Assert.That(typeof(MechanismParameterMetadata)
            .GetProperty(nameof(MechanismParameterMetadata.Direction))!.SetMethod, Is.Null);
    }

    [Test]
    public void FunctionMetadataRejectsUndefinedAdditionalNamedParameterHandling()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MechanismFunctionMetadata(
            CryptoScriptFunction.Parameters, Array.Empty<MechanismParameterMetadata>(),
            (AdditionalNamedParameterHandling)int.MaxValue));
    }

    private static MechanismParameterMetadata ParameterMetadata(
        MechanismParameterDirection direction,
        MechanismParameterDefaultKind defaultKind = MechanismParameterDefaultKind.None,
        string? defaultValue = null,
        string name = "#PAD",
        bool isRequired = false) =>
        new(name, MechanismParameterKind.NamedParameter, direction, isRequired,
            new[] { MechanismParameterDataType.Padding }, "Test", "Test",
            defaultKind, defaultValue);
}
