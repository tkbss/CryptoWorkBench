using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.ViewModels;
using FluentAssertions;
using NUnit.Framework;

namespace CryptoWorkBenchGuiTest;

public class DataVariablesViewModelTests
{
    [Test]
    public void KeyRefreshDisplaysCurrentCanonicalValue()
    {
        var key = new CryptoScript.CryptoAlgorithm.AES.AES().GenerateKey("AES-ECB", "128");
        key.Id = "key";
        var sut = new DataVariablesViewModel();
        sut.Add(key);
        key.Value = "0x(000102030405060708090A0B0C0D0E0F)";

        // Execution refreshes the variable table by clearing and repopulating it.
        sut.Clear();
        sut.Add(key);

        var row = sut.DataVariables.Should().ContainSingle().Which;
        row.Type.Should().Be("KEY");
        row.Identifier.Should().Be("key");
        row.Value.Should().Be(key.Value);
        row.ValueFormat.Should().Be(FormatConversions.HEX);
        key.PrintOutput().Should().Contain("Value: " + row.Value).And.NotContain("KeyValue:");
    }

    [Test]
    public void Add_WithStringVariable_MapsVariableToVisibleCollection()
    {
        var sut = new DataVariablesViewModel();
        var dataVariable = new StringVariableDeclaration
        {
            Id = "message",
            Value = "\"Hello\"",
            ValueFormat = FormatConversions.STR
        };

        sut.Add(dataVariable);

        var result = sut.DataVariables.Should().ContainSingle().Which;
        result.Identifier.Should().Be("message");
        result.Type.Should().Be("VAR");
        result.Value.Should().Be("\"Hello\"");
        result.ValueFormat.Should().Be(FormatConversions.STR);
    }

    [Test]
    public void Add_WithSameIdentifierAndType_DoesNotAddDuplicate()
    {
        var sut = new DataVariablesViewModel();
        var firstVariable = new StringVariableDeclaration
        {
            Id = "message",
            Value = "\"First\"",
            ValueFormat = FormatConversions.STR
        };
        var secondVariable = new StringVariableDeclaration
        {
            Id = "message",
            Value = "\"Second\"",
            ValueFormat = FormatConversions.STR
        };

        sut.Add(firstVariable);
        sut.Add(secondVariable);

        sut.DataVariables.Should().ContainSingle();
    }

    [Test]
    public void Remove_WithStringVariable_RemovesMatchingVisibleEntry()
    {
        var sut = new DataVariablesViewModel();
        var dataVariable = new StringVariableDeclaration
        {
            Id = "message",
            Value = "\"Hello\"",
            ValueFormat = FormatConversions.STR
        };
        sut.Add(dataVariable);
        sut.DataVariables.Should().ContainSingle();

        sut.Remove(dataVariable);

        sut.DataVariables.Should().BeEmpty();
    }
}
