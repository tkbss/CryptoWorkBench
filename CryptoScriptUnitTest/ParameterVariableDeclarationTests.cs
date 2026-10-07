using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

public class ParameterVariableDeclarationTests
{
    [Test]
    public void SetParameterAcceptsCanonicalNameWithHash()
    {
        var parameter = new ParameterVariableDeclaration();

        parameter.SetParameter("#IV", "0x(01)");

        Assert.That(parameter.GetParameters()["#IV"], Is.EqualTo("0x(01)"));
    }

    [Test]
    public void SetParameterAcceptsNameWithoutHash()
    {
        var parameter = new ParameterVariableDeclaration();

        parameter.SetParameter("IV", "0x(02)");

        Assert.That(parameter.GetParameters()["#IV"], Is.EqualTo("0x(02)"));
    }

    [Test]
    public void SetParameterMatchesNameCaseInsensitivelyAndUsesCanonicalKey()
    {
        var parameter = new ParameterVariableDeclaration();

        parameter.SetParameter("#iV", "0x(03)");

        Assert.Multiple(() =>
        {
            Assert.That(parameter.GetParameters(), Does.ContainKey("#IV"));
            Assert.That(parameter.GetParameters()["#IV"], Is.EqualTo("0x(03)"));
        });
    }

    [Test]
    public void SetParameterRejectsUnknownNameWithDescriptiveArgumentException()
    {
        var parameter = new ParameterVariableDeclaration();

        var exception = Assert.Throws<ArgumentException>(() =>
            parameter.SetParameter("#UNKNOWN", "value"));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.ParamName, Is.EqualTo("type"));
            Assert.That(exception.Message, Does.Contain("Unknown parameter name '#UNKNOWN'."));
            Assert.That(parameter.GetParameters(), Is.Empty);
        });
    }

    [Test]
    public void SetParameterDistinguishesMacLengthFromOutputLength()
    {
        var parameter = new ParameterVariableDeclaration();

        parameter.SetParameter("MACLEN", "8");
        parameter.SetParameter("#OUTLEN", "256");

        Assert.Multiple(() =>
        {
            Assert.That(parameter.GetParameters()["#MACLEN"], Is.EqualTo("8"));
            Assert.That(parameter.GetParameters()["#OUTLEN"], Is.EqualTo("256"));
            Assert.That(parameter.GetParameters().Keys,
                Is.EquivalentTo(new[] { "#MECH", "#MACLEN", "#OUTLEN" }));
        });
    }

    [Test]
    public void SetParameterStillStoresKnownMechanismForeignParameter()
    {
        var parameter = new ParameterVariableDeclaration { Mechanism = "AES-CBC" };

        parameter.SetParameter("NONCE", "0x(01)");

        Assert.That(parameter.GetParameters()["#NONCE"], Is.EqualTo("0x(01)"));
    }

    [Test]
    public void SetParameterStillLetsLastDuplicateValueWin()
    {
        var parameter = new ParameterVariableDeclaration();

        parameter.SetParameter("IV", "0x(01)");
        parameter.SetParameter("#iv", "0x(02)");

        Assert.Multiple(() =>
        {
            Assert.That(parameter.GetParameters()["#IV"], Is.EqualTo("0x(02)"));
            Assert.That(parameter.GetParameters().Keys, Is.EquivalentTo(new[] { "#MECH", "#IV" }));
        });
    }

    [TestCase("IV")]
    [TestCase("#IV")]
    [TestCase("iv")]
    [TestCase("#iV")]
    public void GetParameterMatchesCompleteNameWithOptionalHashAndOrdinalIgnoreCase(string name)
    {
        var parameter = new ParameterVariableDeclaration();
        parameter.SetParameter("IV", "0x(01)");

        Assert.That(parameter.GetParameter(name), Is.EqualTo("0x(01)"));
    }

    [Test]
    public void GetParameterDistinguishesMacLengthFromOutputLength()
    {
        var parameter = new ParameterVariableDeclaration();
        parameter.SetParameter("MACLEN", "8");
        parameter.SetParameter("OUTLEN", "256");

        Assert.Multiple(() =>
        {
            Assert.That(parameter.GetParameter("MACLEN"), Is.EqualTo("8"));
            Assert.That(parameter.GetParameter("#OUTLEN"), Is.EqualTo("256"));
        });
    }

    [TestCase("LEN")]
    [TestCase("")]
    [TestCase("UNKNOWN")]
    [TestCase("#UNKNOWN")]
    public void GetParameterReturnsEmptyForPartialEmptyOrUnknownName(string name)
    {
        var parameter = new ParameterVariableDeclaration();
        parameter.SetParameter("MACLEN", "8");
        parameter.SetParameter("OUTLEN", "256");

        Assert.That(parameter.GetParameter(name), Is.Empty);
    }

    [Test]
    public void GetParameterPreservesAllExistingCompleteParameterNames()
    {
        var parameter = new ParameterVariableDeclaration();
        string[] names = ParameterTypeList.Instance.ParameterTypes.ToArray();

        foreach (string name in names)
            parameter.SetParameter(name, name + "-value");

        Assert.Multiple(() =>
        {
            foreach (string name in names)
            {
                Assert.That(parameter.GetParameter(name), Is.EqualTo(name + "-value"), name);
                Assert.That(parameter.GetParameter(name[1..].ToLowerInvariant()),
                    Is.EqualTo(name + "-value"), name);
            }
        });
    }
}
