using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.HMAC;
using CryptoScript.CryptoAlgorithm.WRAPPERS;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AlgorithmCallArgumentsTests
{
    private VariableDeclaration[] previousVariables = null!;

    [SetUp]
    public void SetUp()
    {
        previousVariables = VariableDictionary.Instance().GetVariables().ToArray();
        VariableDictionary.Instance().Clear();
    }

    [TearDown]
    public void TearDown()
    {
        VariableDictionary.Instance().Clear();
        foreach (VariableDeclaration variable in previousVariables)
            VariableDictionary.Instance().Add(variable);
    }

    [Test]
    public void PreservesPositionsValuesAndSourceVariableIdentity()
    {
        var first = Parameter("AES-CBC");
        var second = Parameter("AES-CBC");
        first.Value = "same";
        second.Value = "same";
        AlgorithmCallArgument[] source =
        {
            new("same", first),
            new(null),
            new("same", second)
        };

        var arguments = new AlgorithmCallArguments(source);
        source[0] = new AlgorithmCallArgument("changed");
        string[] values = arguments.Values;
        values[0] = "changed";

        Assert.Multiple(() =>
        {
            Assert.That(arguments.Values, Is.EqualTo(new string?[] { "same", null, "same" }));
            Assert.That(arguments.Arguments[0].SourceVariable, Is.SameAs(first));
            Assert.That(arguments.Arguments[2].SourceVariable, Is.SameAs(second));
            Assert.That(arguments.Arguments[0].SourceVariable,
                Is.Not.SameAs(arguments.Arguments[2].SourceVariable));
        });
    }

    [Test]
    public void ResolverUsesOriginalParameterOrCreatesAnUnregisteredTemporary()
    {
        var original = Parameter("AES-CBC");
        string serialized = original.Value;

        ParameterVariableDeclaration resolved = AlgorithmArgumentResolver.ResolveParameter(
            new AlgorithmCallArgument("not-a-parameter", original));
        ParameterVariableDeclaration temporary = AlgorithmArgumentResolver.ResolveParameter(
            new AlgorithmCallArgument(serialized));

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.SameAs(original));
            Assert.That(temporary, Is.Not.SameAs(original));
            Assert.That(temporary.Value, Is.EqualTo(serialized));
            Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
        });
    }

    [Test]
    public void SerializedParameterIsNotTreatedAsALegacyVariableIdentifier()
    {
        var serialized = Parameter("AES-CBC");
        var invalidIdentifierCollision = Parameter("AES-CBC");
        invalidIdentifierCollision.Id = serialized.Value;
        VariableDictionary.Instance().Add(invalidIdentifierCollision);

        ParameterVariableDeclaration resolved = AlgorithmArgumentResolver.ResolveParameter(
            new AlgorithmCallArgument(serialized.Value));

        Assert.That(resolved, Is.Not.SameAs(invalidIdentifierCollision));
        Assert.That(resolved.Value, Is.EqualTo(serialized.Value));
    }

    [Test]
    public void ObjectAwareAlgorithmEntryReceivesTheExactOriginalParameterInstance()
    {
        var original = Parameter("AES-CBC");
        var algorithm = new ParameterIdentitySpyAlgorithm();

        algorithm.Encrypt(Arguments(original, "second", "third"));

        Assert.That(algorithm.ReceivedParameter, Is.SameAs(original));
    }

    [Test]
    public void AesObjectAwarePathUsesSourceParameterAndLegacyPathStillWorks()
    {
        var algorithm = new AES();
        ParameterVariableDeclaration parameter = algorithm.GenerateParameters("AES-CBC", new[]
        {
            "#IV:0x(000102030405060708090A0B0C0D0E0F)", "#PAD:NONE"
        });
        const string key = "0x(000102030405060708090A0B0C0D0E0F)";
        const string data = "0x(00112233445566778899AABBCCDDEEFF)";

        StringVariableDeclaration objectResult = algorithm.Encrypt(
            Arguments(parameter, key, data));
        StringVariableDeclaration legacyResult = algorithm.Encrypt(
            new[] { parameter.Value, key, data });

        Assert.That(objectResult.Value, Is.EqualTo(legacyResult.Value));
    }

    [Test]
    public void Des3ObjectAwarePathUsesSourceParameterAndLegacyPathStillWorks()
    {
        var algorithm = new DES3();
        ParameterVariableDeclaration parameter = algorithm.GenerateParameters("DES3-CBC", new[]
        {
            "#IV:0x(0001020304050607)", "#PAD:NONE"
        });
        const string key = "0x(0123456789ABCDEFFEDCBA9876543210)";
        const string data = "0x(0011223344556677)";

        StringVariableDeclaration objectResult = algorithm.Encrypt(
            Arguments(parameter, key, data));
        StringVariableDeclaration legacyResult = algorithm.Encrypt(
            new[] { parameter.Value, key, data });

        Assert.That(objectResult.Value, Is.EqualTo(legacyResult.Value));
    }

    [Test]
    public void HmacObjectAwarePathUsesSourceParameterAndLegacyPathStillWorks()
    {
        var algorithm = new HMAC();
        ParameterVariableDeclaration parameter = algorithm.GenerateParameters("HMAC-SHA256");
        const string key = "0x(000102030405060708090A0B0C0D0E0F)";
        const string data = "\"data\"";

        StringVariableDeclaration objectResult = algorithm.Mac(
            Arguments(parameter, key, data));
        StringVariableDeclaration legacyResult = algorithm.Mac(
            new[] { parameter.Value, key, data });

        Assert.That(objectResult.Value, Is.EqualTo(legacyResult.Value));
    }

    [Test]
    public void AesTr31WrapAndUnwrapResolveTheSourceParameterBeforeLaterValidation()
    {
        var algorithm = new WrapAESTR31();
        ParameterVariableDeclaration parameter = Parameter("WRAP-AES-TR31");
        parameter.SetParameter("BLKH", "\"A0112D0AB00E0000\"");

        Assert.Throws<NotSupportedException>(() => algorithm.Wrap(
            Arguments(parameter, "missing-key", "missing-key")));
        Exception error = Assert.Throws<Exception>(() => algorithm.Unwrap(
            Arguments(parameter, "missing-key", "missing-block")))!;
        Assert.That(error.Message, Does.StartWith("Protection key variable not found"));
    }

    [Test]
    public void Des3Tr31WrapAndUnwrapResolveTheSourceParameterBeforeLaterValidation()
    {
        var algorithm = new WrapDES3TR31();
        ParameterVariableDeclaration parameter = Parameter("WRAP-DES3-TR31");
        parameter.SetParameter("BLKH", "\"B0096D0TB00E0000\"");

        Assert.That(Assert.Throws<ArgumentException>(() => algorithm.Wrap(
            Arguments(parameter, "missing-key", "missing-key")))!.Message,
            Is.EqualTo("Key variable not found."));
        Assert.That(Assert.Throws<ArgumentException>(() => algorithm.Unwrap(
            Arguments(parameter, "missing-key", "not-a-block")))!.Message,
            Does.StartWith("Expected a TR-31 string"));
    }

    [Test]
    public void CryptoOperationsMapsInvocationSourceIdentityToConcreteAlgorithm()
    {
        var algorithm = new AES();
        ParameterVariableDeclaration parameter = algorithm.GenerateParameters("AES-CBC", new[]
        {
            "#IV:0x(000102030405060708090A0B0C0D0E0F)", "#PAD:NONE"
        });
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument("not-a-serialized-parameter",
                ResolvedCallArgumentKind.Variable, parameter),
            new ResolvedCallArgument("0x(000102030405060708090A0B0C0D0E0F)",
                ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument("0x(00112233445566778899AABBCCDDEEFF)",
                ResolvedCallArgumentKind.Expression)
        });

        Assert.DoesNotThrow(() => new CryptoOperations().Encrypt(invocation));
        Assert.That(invocation.Values[0], Is.EqualTo("not-a-serialized-parameter"));
    }

    [Test]
    public void CryptoOperationsUsesATemporaryForSerializedParameterWithoutSourceVariable()
    {
        var algorithm = new AES();
        ParameterVariableDeclaration parameter = algorithm.GenerateParameters("AES-CBC", new[]
        {
            "#IV:0x(000102030405060708090A0B0C0D0E0F)", "#PAD:NONE"
        });
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameter.Value, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument("0x(000102030405060708090A0B0C0D0E0F)",
                ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument("0x(00112233445566778899AABBCCDDEEFF)",
                ResolvedCallArgumentKind.Expression)
        });

        Assert.DoesNotThrow(() => new CryptoOperations().Encrypt(invocation));
        Assert.That(VariableDictionary.Instance().GetVariables(), Is.Empty);
    }

    private static AlgorithmCallArguments Arguments(
        ParameterVariableDeclaration parameter,
        string second,
        string third) =>
        new(new[]
        {
            new AlgorithmCallArgument("not-a-serialized-parameter", parameter),
            new AlgorithmCallArgument(second),
            new AlgorithmCallArgument(third)
        });

    private static ParameterVariableDeclaration Parameter(string mechanism) =>
        new() { Mechanism = mechanism };

    private sealed class ParameterIdentitySpyAlgorithm : CryptoAlgorithm
    {
        internal ParameterVariableDeclaration? ReceivedParameter { get; private set; }

        public override StringVariableDeclaration Encrypt(AlgorithmCallArguments parameters)
        {
            ReceivedParameter = AlgorithmArgumentResolver.ResolveParameter(parameters.Arguments[0]);
            return new StringVariableDeclaration();
        }
    }
}
