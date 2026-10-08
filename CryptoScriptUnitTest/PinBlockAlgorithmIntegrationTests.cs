using Antlr4.Runtime;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockAlgorithmIntegrationTests
{
    private const string Key16 = "0x(0123456789ABCDEFFEDCBA9876543210)";
    private const string Key24 = "0x(0123456789ABCDEFFEDCBA98765432100011223344556677)";
    private const string Pan = "0x(1234567890123456)";
    private static readonly string[] Mechanisms =
    {
        "WRAP-DES3-PINBLOCK-0",
        "WRAP-DES3-PINBLOCK-1",
        "WRAP-DES3-PINBLOCK-2",
        "WRAP-DES3-PINBLOCK-3"
    };

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TearDown]
    public void TearDown() => VariableDictionary.Instance().Clear();

    [TestCase("WRAP-DES3-PINBLOCK-0", "0x(E5639CBC7EC0B4CA)")]
    [TestCase("WRAP-DES3-PINBLOCK-1", "0x(646855A2370347D8)")]
    [TestCase("WRAP-DES3-PINBLOCK-2", "0x(9859240AE52820C3)")]
    [TestCase("WRAP-DES3-PINBLOCK-3", "0x(16650ADB004873B9)")]
    public void Wrap_MatchesIndependentOpenSslCiphertextVectors(string mechanism, string expected)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        if (mechanism.EndsWith("-1", StringComparison.Ordinal))
            parameters.SetParameter("TRANSACTION", "0x(0123456789)");
        if (mechanism.EndsWith("-3", StringComparison.Ordinal))
            parameters.SetParameter("FILL", "0x(ABCDEFABCD)");
        PinBlockAlgorithm algorithm = Algorithm(mechanism, _ =>
            throw new AssertionException("Explicit fields must not invoke the random source."));

        StringVariableDeclaration result = algorithm.Wrap(Arguments(
            parameters, Key(Key16), Var("0x(1234)")));

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo(expected).IgnoreCase);
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(result.Type, Is.TypeOf<CryptoTypeVar>());
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-0", "0123", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-0", "01234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-0", "012345678901", Key24)]
    [TestCase("WRAP-DES3-PINBLOCK-0", "00001234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-1", "0123", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-1", "01234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-1", "012345678901", Key24)]
    [TestCase("WRAP-DES3-PINBLOCK-1", "00001234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-2", "0123", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-2", "01234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-2", "012345678901", Key24)]
    [TestCase("WRAP-DES3-PINBLOCK-2", "00001234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-3", "0123", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-3", "01234", Key16)]
    [TestCase("WRAP-DES3-PINBLOCK-3", "012345678901", Key24)]
    [TestCase("WRAP-DES3-PINBLOCK-3", "00001234", Key16)]
    public void WrapAndUnwrap_RoundTripPinsAndKeySizes(string mechanism, string pin, string keyValue)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, pin, explicitVariableField: true);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);
        KeyVariableDeclaration key = Key(keyValue);

        StringVariableDeclaration wrapped = algorithm.Wrap(Arguments(parameters, key, Var($"0x({pin})")));
        StringVariableDeclaration unwrapped = (StringVariableDeclaration)algorithm.Unwrap(
            Arguments(parameters, key, Var(wrapped.Value)));

        Assert.Multiple(() =>
        {
            Assert.That(Convert.FromHexString(FormatConversions.HexStringToString(wrapped.Value)).Length, Is.EqualTo(8));
            Assert.That(unwrapped.Value, Is.EqualTo($"0x({pin})"));
            Assert.That(unwrapped.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(unwrapped.Type, Is.TypeOf<CryptoTypeVar>());
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-0", "0x(12345678)")]
    [TestCase("WRAP-DES3-PINBLOCK-0", "0x(1234567890123456789)")]
    [TestCase("WRAP-DES3-PINBLOCK-3", "0x(12345678)")]
    [TestCase("WRAP-DES3-PINBLOCK-3", "0x(1234567890123456789)")]
    public void PanBoundaryLengths_RoundTrip(string mechanism, string pan)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p", ("PAN", pan));
        if (mechanism.EndsWith("-3", StringComparison.Ordinal))
            parameters.SetParameter("FILL", "0x(ABCDEFABCD)");
        PinBlockAlgorithm algorithm = Algorithm(mechanism);
        KeyVariableDeclaration key = Key(Key16);

        StringVariableDeclaration wrapped = algorithm.Wrap(Arguments(parameters, key, Var("0x(1234)")));
        StringVariableDeclaration unwrapped = (StringVariableDeclaration)algorithm.Unwrap(
            Arguments(parameters, key, Var(wrapped.Value)));

        Assert.That(unwrapped.Value, Is.EqualTo("0x(1234)"));
    }

    [Test]
    public void Format1GeneratedTransaction_IsStoredOnTheOriginalParameter()
    {
        ParameterVariableDeclaration original = Parameters("WRAP-DES3-PINBLOCK-1", "original");
        ParameterVariableDeclaration sameValue = Parameters("WRAP-DES3-PINBLOCK-1", "other");
        var values = new Queue<int>(Enumerable.Range(0, 10));
        PinBlockAlgorithm algorithm = Algorithm("WRAP-DES3-PINBLOCK-1", upperBound =>
        {
            Assert.That(upperBound, Is.EqualTo(16));
            return values.Dequeue();
        });

        algorithm.Wrap(Arguments(original, Key(Key16), Var("0x(1234)")));

        Assert.Multiple(() =>
        {
            Assert.That(original.GetParameter("TRANSACTION"), Is.EqualTo("0x(0123456789)"));
            Assert.That(sameValue.GetParameter("TRANSACTION"), Is.Empty);
            Assert.That(values, Is.Empty);
        });
    }

    [Test]
    public void Format3GeneratedFill_IsUniformlyMappedAndStoredOnTheOriginalParameter()
    {
        ParameterVariableDeclaration original = Parameters(
            "WRAP-DES3-PINBLOCK-3", "original", ("PAN", Pan));
        ParameterVariableDeclaration sameValue = Parameters(
            "WRAP-DES3-PINBLOCK-3", "other", ("PAN", Pan));
        var values = new Queue<int>(new[] { 0, 1, 2, 3, 4, 5, 0, 1, 2, 3 });
        PinBlockAlgorithm algorithm = Algorithm("WRAP-DES3-PINBLOCK-3", upperBound =>
        {
            Assert.That(upperBound, Is.EqualTo(6));
            return values.Dequeue();
        });

        algorithm.Wrap(Arguments(original, Key(Key16), Var("0x(1234)")));

        Assert.Multiple(() =>
        {
            Assert.That(original.GetParameter("FILL"), Is.EqualTo("0x(ABCDEFABCD)"));
            Assert.That(sameValue.GetParameter("FILL"), Is.Empty);
            Assert.That(values, Is.Empty);
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-1", "TRANSACTION", "0x(G)")]
    [TestCase("WRAP-DES3-PINBLOCK-3", "FILL", "0x(0)")]
    public void Unwrap_OverwritesOutputOnTheOriginalParameterOnly(
        string mechanism,
        string outputName,
        string replacement)
    {
        ParameterVariableDeclaration source = Parameters(mechanism, "source");
        AddFormatInputs(source, "1234", explicitVariableField: true);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);
        KeyVariableDeclaration key = Key(Key16);
        StringVariableDeclaration wrapped = algorithm.Wrap(Arguments(source, key, Var("0x(1234)")));
        string extracted = source.GetParameter(outputName);
        source.SetParameter(outputName, replacement);

        ParameterVariableDeclaration other = Parameters(mechanism, "other");
        AddFormatInputs(other, "1234", explicitVariableField: true);
        string otherBefore = other.Value;
        StringVariableDeclaration result = (StringVariableDeclaration)algorithm.Unwrap(
            Arguments(source, key, Var(wrapped.Value)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo("0x(1234)"));
            Assert.That(source.GetParameter(outputName), Is.EqualTo(extracted));
            Assert.That(other.Value, Is.EqualTo(otherBefore));
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-0")]
    [TestCase("WRAP-DES3-PINBLOCK-2")]
    public void Formats0And2_DoNotMutateParameters(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        string before = parameters.Value;
        PinBlockAlgorithm algorithm = Algorithm(mechanism);
        KeyVariableDeclaration key = Key(Key16);

        StringVariableDeclaration wrapped = algorithm.Wrap(Arguments(parameters, key, Var("0x(1234)")));
        algorithm.Unwrap(Arguments(parameters, key, Var(wrapped.Value)));

        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [Test]
    public void ExplicitVariableFields_DoNotUseRandomSource()
    {
        foreach (string mechanism in new[] { "WRAP-DES3-PINBLOCK-1", "WRAP-DES3-PINBLOCK-3" })
        {
            ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
            AddFormatInputs(parameters, "1234", explicitVariableField: true);
            PinBlockAlgorithm algorithm = Algorithm(mechanism, _ =>
                throw new AssertionException("Explicit fields must not invoke the random source."));

            Assert.DoesNotThrow(() => algorithm.Wrap(
                Arguments(parameters, Key(Key16), Var("0x(1234)"))));
        }
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void GeneratedVariableFields_RoundTripAndHaveRequiredRange(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, "1234", explicitVariableField: false);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);
        KeyVariableDeclaration key = Key(Key16);

        StringVariableDeclaration wrapped = algorithm.Wrap(Arguments(parameters, key, Var("0x(1234)")));
        string name = mechanism.EndsWith("-1", StringComparison.Ordinal) ? "TRANSACTION" : "FILL";
        string generated = FormatConversions.HexStringToString(parameters.GetParameter(name));
        StringVariableDeclaration unwrapped = (StringVariableDeclaration)algorithm.Unwrap(
            Arguments(parameters, key, Var(wrapped.Value)));

        Assert.Multiple(() =>
        {
            Assert.That(generated, Has.Length.EqualTo(10));
            Assert.That(generated.All(character => mechanism.EndsWith("-1", StringComparison.Ordinal)
                ? Uri.IsHexDigit(character)
                : character is >= 'A' and <= 'F'), Is.True);
            Assert.That(unwrapped.Value, Is.EqualTo("0x(1234)"));
            Assert.That(FormatConversions.HexStringToString(parameters.GetParameter(name)), Is.EqualTo(generated));
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void SerializedLegacyParameters_AreRejectedWhenOutputMutationIsRequired(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, string.Empty);
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        Assert.That(() => algorithm.Wrap(new[] { parameters.Value, Key16, "0x(1234)" }),
            Throws.ArgumentException.With.Message.Contains("referencable PARAM"));
    }

    [TestCase("WRAP-DES3-PINBLOCK-0")]
    [TestCase("WRAP-DES3-PINBLOCK-2")]
    public void NonMutatingLegacyOverloads_AcceptSerializedParameters(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, string.Empty);
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        StringVariableDeclaration wrapped = algorithm.Wrap(new[] { parameters.Value, Key16, "0x(1234)" });
        StringVariableDeclaration unwrapped = (StringVariableDeclaration)algorithm.Unwrap(
            new[] { parameters.Value, Key16, wrapped.Value });

        Assert.That(unwrapped.Value, Is.EqualTo("0x(1234)"));
    }

    [TestCase("WRAP-DES3-PINBLOCK-1", "TRANSACTION")]
    [TestCase("WRAP-DES3-PINBLOCK-3", "FILL")]
    public void LegacyVariableIds_MutateTheReferencedParameter(string mechanism, string outputName)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        KeyVariableDeclaration key = Key(Key16, "k");
        StringVariableDeclaration pin = Var("0x(1234)", "pin");
        VariableDictionary.Instance().Add(parameters);
        VariableDictionary.Instance().Add(key);
        VariableDictionary.Instance().Add(pin);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        StringVariableDeclaration wrapped = algorithm.Wrap(new[] { "p", "k", "pin" });

        Assert.Multiple(() =>
        {
            Assert.That(wrapped.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(parameters.GetParameter(outputName), Is.Not.Empty);
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void FailureBeforeCompletion_DoesNotMutateParameters(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        string before = parameters.Value;
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        Assert.Multiple(() =>
        {
            Assert.That(() => algorithm.Wrap(Arguments(parameters, Key(Key16), Var("0x(12A4)"))),
                Throws.ArgumentException);
            Assert.That(parameters.Value, Is.EqualTo(before));
            Assert.That(() => algorithm.Wrap(Arguments(parameters, Key("0x(0011)"), Var("0x(1234)"))),
                Throws.ArgumentException);
            Assert.That(parameters.Value, Is.EqualTo(before));
            Assert.That(() => algorithm.Unwrap(Arguments(parameters, Key(Key16), Var("0x(0011)"))),
                Throws.ArgumentException);
            Assert.That(parameters.Value, Is.EqualTo(before));
        });
    }

    [TestCase("\"0123\"", "0123")]
    [TestCase("0x(123)", "123")]
    [TestCase("0x(1234567890123)", "1234567890123")]
    [TestCase("0x(12A4)", "12A4")]
    public void InvalidPinInputs_AreRejectedWithoutExposingPinOrMutatingParameters(
        string value, string sensitivePin)
    {
        foreach (string mechanism in Mechanisms)
        {
            ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
            AddFormatInputs(parameters, "1234", explicitVariableField: true);
            string before = parameters.Value;

            ArgumentException? error = Assert.Throws<ArgumentException>(() =>
                Algorithm(mechanism).Wrap(Arguments(parameters, Key(Key16), Var(value))));

            Assert.Multiple(() =>
            {
                Assert.That(error!.Message, Does.Not.Contain(sensitivePin));
                Assert.That(parameters.Value, Is.EqualTo(before));
            });
        }
    }

    [Test]
    public void InvalidPanTransactionAndFill_DoNotMutateParameters()
    {
        AssertInvalidNamedInputIsAtomic("WRAP-DES3-PINBLOCK-0", ("PAN", "0x(1234567)"));
        AssertInvalidNamedInputIsAtomic("WRAP-DES3-PINBLOCK-1", ("TRANSACTION", "0x(012345678G)"));
        AssertInvalidNamedInputIsAtomic("WRAP-DES3-PINBLOCK-3",
            ("PAN", "0x(1234567)"), ("FILL", "0x(ABCDEFABCD)"));
        AssertInvalidNamedInputIsAtomic("WRAP-DES3-PINBLOCK-3",
            ("PAN", Pan), ("FILL", "0x(ABCDEFABC9)"));
    }

    [Test]
    public void ObjectAwareCalls_ValidateArgumentCountMechanismAndVariableTypes()
    {
        ParameterVariableDeclaration parameters = Parameters("WRAP-DES3-PINBLOCK-2", "p");
        PinBlockAlgorithm algorithm = Algorithm("WRAP-DES3-PINBLOCK-2");
        KeyVariableDeclaration key = Key(Key16);
        StringVariableDeclaration pin = Var("0x(1234)");

        ParameterVariableDeclaration wrongMechanism = Parameters("WRAP-DES3-PINBLOCK-1", "wrong");
        var wrongKeyType = new StringVariableDeclaration
        {
            Id = "notKey",
            Value = Key16,
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeVar()
        };
        var wrongDataType = new KeyVariableDeclaration
        {
            Id = "notVar",
            Value = "0x(1234)",
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey()
        };

        Assert.Multiple(() =>
        {
            Assert.That(() => algorithm.Wrap(new AlgorithmCallArguments(new[]
            {
                new AlgorithmCallArgument(parameters.Value, parameters),
                new AlgorithmCallArgument(key.Value, key)
            })), Throws.ArgumentException);
            Assert.That(() => algorithm.Wrap(Arguments(wrongMechanism, key, pin)), Throws.ArgumentException);
            Assert.That(() => algorithm.Wrap(Arguments(parameters, wrongKeyType, pin)), Throws.ArgumentException);
            Assert.That(() => algorithm.Wrap(Arguments(parameters, key, wrongDataType)), Throws.ArgumentException);
        });
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void MalformedDecryptedBlock_DoesNotMutateParameters(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p");
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        string before = parameters.Value;
        byte[] keyBytes = FormatConversions.ToByteArray(Key16, FormatConversions.HEX);
        byte[] malformed = DES3_ECB.EncryptNoPadding(keyBytes, new byte[8]);
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        Assert.That(() => algorithm.Unwrap(Arguments(
            parameters, Key(Key16), Var(FormatConversions.ByteArrayToHexString(malformed)))),
            Throws.TypeOf<FormatException>());
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void MissingReferencableParameterIdentity_IsRejectedWithoutMutation(string mechanism)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, string.Empty);
        AddFormatInputs(parameters, "1234", explicitVariableField: true);
        string before = parameters.Value;
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        Assert.That(() => algorithm.Wrap(Arguments(parameters, Key(Key16), Var("0x(1234)"))),
            Throws.ArgumentException.With.Message.Contains("referencable PARAM"));
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void RuntimeReconstructedParameters_AreNotMutatedOrReportedAsSuccessful(string mechanism)
    {
        ParameterVariableDeclaration serialized = Parameters(mechanism, string.Empty);
        AddFormatInputs(serialized, "1234", explicitVariableField: true);
        string before = serialized.Value;
        KeyVariableDeclaration key = Key(Key16);
        StringVariableDeclaration pin = Var("0x(1234)");
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(serialized.Value, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument(key.Value, ResolvedCallArgumentKind.Variable, key),
            new ResolvedCallArgument(pin.Value, ResolvedCallArgumentKind.Variable, pin)
        });

        Assert.That(() => new CryptoOperations().Wrap(invocation),
            Throws.ArgumentException.With.Message.Contains("referencable PARAM"));
        Assert.That(serialized.Value, Is.EqualTo(before));
    }

    [TestCase("WRAP-DES3-PINBLOCK-0")]
    [TestCase("WRAP-DES3-PINBLOCK-1")]
    [TestCase("WRAP-DES3-PINBLOCK-2")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    public void ScriptEndToEnd_WrapsAndUnwrapsAsVar(string mechanism)
    {
        string namedParameters = mechanism switch
        {
            "WRAP-DES3-PINBLOCK-0" => $" #PAN:{Pan}",
            "WRAP-DES3-PINBLOCK-3" => $" #PAN:{Pan}",
            _ => string.Empty
        };
        string pin = mechanism.EndsWith("-1", StringComparison.Ordinal) ? "01234" : "001234";
        string script =
            $"PARAM p=#MECH:{mechanism}{namedParameters} " +
            $"KEY k={Key16} " +
            $"VAR pin=0x({pin}) " +
            "VAR block=Wrap(p,k,pin) " +
            "VAR recovered=Unwrap(p,k,block)";

        Execute(script);

        VariableDeclaration block = VariableDictionary.Instance().Get("block");
        VariableDeclaration recovered = VariableDictionary.Instance().Get("recovered");
        Assert.Multiple(() =>
        {
            Assert.That(block.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(block.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(recovered.Type, Is.TypeOf<CryptoTypeVar>());
            Assert.That(recovered.Value, Is.EqualTo($"0x({pin})"));
            Assert.That(recovered.ValueFormat, Is.EqualTo(FormatConversions.HEX));
        });
    }

    [Test]
    public void Format4_RemainsNotImplemented()
    {
        PinBlockAlgorithm algorithm = Algorithm("WRAP-AES-PINBLOCK-4");
        ParameterVariableDeclaration parameters = Parameters(
            "WRAP-AES-PINBLOCK-4", "p", ("PAN", Pan));

        Assert.Multiple(() =>
        {
            Assert.That(() => algorithm.Wrap(Arguments(parameters, Key(Key16), Var("0x(1234)"))),
                Throws.TypeOf<NotImplementedException>());
            Assert.That(() => algorithm.Unwrap(Arguments(parameters, Key(Key16), Var("0x(0000000000000000)"))),
                Throws.TypeOf<NotImplementedException>());
        });
    }

    private static void AssertInvalidNamedInputIsAtomic(
        string mechanism,
        params (string Name, string Value)[] namedParameters)
    {
        ParameterVariableDeclaration parameters = Parameters(mechanism, "p", namedParameters);
        string before = parameters.Value;
        PinBlockAlgorithm algorithm = Algorithm(mechanism);

        Assert.That(() => algorithm.Wrap(Arguments(parameters, Key(Key16), Var("0x(1234)"))),
            Throws.ArgumentException);
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    private static void AddFormatInputs(
        ParameterVariableDeclaration parameters,
        string pin,
        bool explicitVariableField)
    {
        if (parameters.Mechanism.EndsWith("-0", StringComparison.Ordinal) ||
            parameters.Mechanism.EndsWith("-3", StringComparison.Ordinal))
        {
            parameters.SetParameter("PAN", Pan);
        }
        if (!explicitVariableField)
            return;

        string variableField = new('A', 14 - pin.Length);
        if (parameters.Mechanism.EndsWith("-1", StringComparison.Ordinal))
            parameters.SetParameter("TRANSACTION", $"0x({variableField})");
        if (parameters.Mechanism.EndsWith("-3", StringComparison.Ordinal))
            parameters.SetParameter("FILL", $"0x({variableField})");
    }

    private static PinBlockAlgorithm Algorithm(string mechanism, Func<int, int>? random = null) =>
        random is null
            ? (PinBlockAlgorithm)AlgorithmFactory.Create(mechanism)
            : new PinBlockAlgorithm(mechanism,
                mechanism.StartsWith("WRAP-AES-", StringComparison.Ordinal)
                    ? PinBlockCipherFamily.Aes
                    : PinBlockCipherFamily.Des3,
                random);

    private static AlgorithmCallArguments Arguments(
        ParameterVariableDeclaration parameters,
        VariableDeclaration key,
        VariableDeclaration data) => new(new[]
        {
            new AlgorithmCallArgument(parameters.Value, parameters),
            new AlgorithmCallArgument(key.Value, key),
            new AlgorithmCallArgument(data.Value, data)
        });

    private static ParameterVariableDeclaration Parameters(
        string mechanism,
        string id,
        params (string Name, string Value)[] values)
    {
        var result = new ParameterVariableDeclaration { Id = id, Mechanism = mechanism };
        foreach ((string name, string value) in values)
            result.SetParameter(name, value);
        return result;
    }

    private static KeyVariableDeclaration Key(string value, string id = "k") => new()
    {
        Id = id,
        Value = value,
        ValueFormat = FormatConversions.HEX,
        Type = new CryptoTypeKey(),
        KeyType = KeyType.Secret(KeyAlgorithm.Tdea)
    };

    private static StringVariableDeclaration Var(string value, string id = "data") => new()
    {
        Id = id,
        Value = value,
        ValueFormat = FormatConversions.ParseString(value),
        Type = new CryptoTypeVar()
    };

    private static void Execute(string script)
    {
        CryptoScriptParser parser = ParserBuilder.StringBuild(script);
        CryptoScriptParser.ProgramContext context = parser.program();
        Assert.Multiple(() =>
        {
            Assert.That(parser.NumberOfSyntaxErrors, Is.Zero);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
        });
        new CryptoScriptRunner().Execute(context);
    }
}
