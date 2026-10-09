using Antlr4.Runtime;
using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockFormat4AlgorithmIntegrationTests
{
    private const string Mechanism = "WRAP-AES-PINBLOCK-4";
    private const string Key128 = "0x(000102030405060708090A0B0C0D0E0F)";
    private const string Key192 = "0x(000102030405060708090A0B0C0D0E0F1011121314151617)";
    private const string Key256 = "0x(000102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F)";
    private const string Pan = "0x(1234567890123456)";
    private const string Random = "0x(0011223344556677)";

    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorList.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
        SyntaxErrorListner.ErrorMessage.Clear();
    }

    [TearDown]
    public void TearDown() => VariableDictionary.Instance().Clear();

    [TestCase(
        Key128,
        "CB2C4B7CE2D3FB704F1FA957E1CF2C98",
        "8A0F0E1B6BD2D8352F1FA957E1CF2C98",
        "6685B016B0CEC63CB7E0F2663C9551EC")]
    [TestCase(
        Key192,
        "C9815BA161A7F29E42D678FD5E3E6236",
        "88A21EC6E8A6D1DB22D678FD5E3E6236",
        "3D82F10B6E406770B509275DE21981D2")]
    [TestCase(
        Key256,
        "E25EB71B0762A81911A03B73551BFA38",
        "A37DF27C8E638B5C71A03B73551BFA38",
        "E229CDA360D0A794E2B8846E0FC505B0")]
    public void Wrap_MatchesSyntheticOpenSslReferenceVector(
        string keyValue,
        string expectedFirstAes,
        string expectedXor,
        string expectedCiphertext)
    {
        const string pin = "01234";
        byte[] key = Convert.FromHexString(FormatConversions.HexStringToString(keyValue));
        byte[] pinField = PinBlockFormat4FieldCodec.EncodePinField(pin, "0011223344556677");
        byte[] panField = PinBlockFormat4FieldCodec.EncodePanField("1234567890123456");
        byte[] firstAes = AES_ECB.EncryptNoPadding(key, pinField);
        byte[] xor = Xor(firstAes, panField);
        byte[] ciphertext = AES_ECB.EncryptNoPadding(key, xor);

        ParameterVariableDeclaration parameters = Parameters("p", Pan, Random);
        StringVariableDeclaration result = Algorithm().Wrap(
            Arguments(parameters, Key(keyValue), Var("0x(01234)")));

        Assert.Multiple(() =>
        {
            Assert.That(Convert.ToHexString(pinField),
                Is.EqualTo("4501234AAAAAAAAA0011223344556677"));
            Assert.That(Convert.ToHexString(panField),
                Is.EqualTo("41234567890123456000000000000000"));
            Assert.That(Convert.ToHexString(firstAes), Is.EqualTo(expectedFirstAes));
            Assert.That(Convert.ToHexString(xor), Is.EqualTo(expectedXor));
            Assert.That(Convert.ToHexString(ciphertext), Is.EqualTo(expectedCiphertext));
            Assert.That(result.Value, Is.EqualTo($"0x({expectedCiphertext.ToLowerInvariant()})"));
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
        });
    }

    [TestCase(Key128, "0123", "0x(1234567890)")]
    [TestCase(Key192, "01234", "0x(123456789012)")]
    [TestCase(Key256, "012345678901", "0x(1234567890123)")]
    [TestCase(Key128, "00001234", "0x(1234567890123456789)")]
    public void WrapAndUnwrap_RoundTripKeyPinAndPanBoundaries(
        string keyValue,
        string pin,
        string pan)
    {
        ParameterVariableDeclaration parameters = Parameters("p", pan, Random);
        PinBlockAlgorithm algorithm = Algorithm();
        KeyVariableDeclaration key = Key(keyValue);

        StringVariableDeclaration wrapped = algorithm.Wrap(
            Arguments(parameters, key, Var($"0x({pin})")));
        StringVariableDeclaration unwrapped = (StringVariableDeclaration)algorithm.Unwrap(
            Arguments(parameters, key, wrapped));

        Assert.Multiple(() =>
        {
            Assert.That(wrapped.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(FormatConversions.ToByteArray(wrapped.Value, wrapped.ValueFormat), Has.Length.EqualTo(16));
            Assert.That(unwrapped.Value, Is.EqualTo($"0x({pin})"));
            Assert.That(unwrapped.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(parameters.GetParameter("RANDOM"), Is.EqualTo(Random));
            Assert.That(parameters.GetParameter("PAN"), Is.EqualTo(pan));
            Assert.That(parameters.GetParameter("MECH"), Is.EqualTo(Mechanism));
        });
    }

    [Test]
    public void Wrap_GeneratesAndStoresRandomFieldOnlyAfterSuccess()
    {
        byte[] generated = Convert.FromHexString("0001020304050607");
        ParameterVariableDeclaration parameters = Parameters("p", Pan);
        PinBlockAlgorithm algorithm = Algorithm(generated);

        StringVariableDeclaration result = algorithm.Wrap(
            Arguments(parameters, Key(Key128), Var("0x(0123)")));

        Assert.Multiple(() =>
        {
            Assert.That(result.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(parameters.GetParameter("RANDOM"), Is.EqualTo("0x(0001020304050607)"));
        });
    }

    [Test]
    public void Wrap_WithExplicitRandomDoesNotInvokeRandomSourceAndNormalizesHex()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, "0x(00abcdef01234567)");
        PinBlockAlgorithm algorithm = new(
            Mechanism,
            PinBlockCipherFamily.Aes,
            _ => throw new AssertionException("DES3 random source must not be used."),
            _ => throw new AssertionException("Explicit RANDOM must not invoke the random source."));

        algorithm.Wrap(Arguments(parameters, Key(Key128), Var("0x(0123)")));

        Assert.That(parameters.GetParameter("RANDOM"), Is.EqualTo("0x(00ABCDEF01234567)"));
    }

    [Test]
    public void Unwrap_OverwritesExistingRandomWithExtractedValue()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, Random);
        PinBlockAlgorithm algorithm = Algorithm();
        KeyVariableDeclaration key = Key(Key128);
        StringVariableDeclaration wrapped = algorithm.Wrap(
            Arguments(parameters, key, Var("0x(01234)")));
        parameters.SetParameter("RANDOM", "not-hex-and-not-an-input-condition");

        StringVariableDeclaration result = (StringVariableDeclaration)algorithm.Unwrap(
            Arguments(parameters, key, wrapped));

        Assert.Multiple(() =>
        {
            Assert.That(result.Value, Is.EqualTo("0x(01234)"));
            Assert.That(parameters.GetParameter("RANDOM"), Is.EqualTo(Random));
        });
    }

    [Test]
    public void ObjectAwareCallMutatesOnlyOriginalParameterInstanceWithIdenticalValue()
    {
        ParameterVariableDeclaration original = Parameters("original", Pan);
        ParameterVariableDeclaration sameValue = Parameters("other", Pan);
        Assert.That(original.Value, Is.EqualTo(sameValue.Value));
        string untouched = sameValue.Value;

        Algorithm(Convert.FromHexString("0011223344556677")).Wrap(
            Arguments(original, Key(Key128), Var("0x(1234)")));

        Assert.Multiple(() =>
        {
            Assert.That(original.GetParameter("RANDOM"), Is.EqualTo(Random));
            Assert.That(sameValue.Value, Is.EqualTo(untouched));
            Assert.That(sameValue.GetParameter("RANDOM"), Is.Empty);
        });
    }

    [Test]
    public void LegacyIdCallsRoundTripAndMutateReferencedParameter()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, Random);
        KeyVariableDeclaration key = Key(Key128, "k");
        StringVariableDeclaration pin = Var("0x(01234)", "pin");
        VariableDictionary.Instance().Add(parameters);
        VariableDictionary.Instance().Add(key);
        VariableDictionary.Instance().Add(pin);
        PinBlockAlgorithm algorithm = Algorithm();

        StringVariableDeclaration wrapped = algorithm.Wrap(new[] { "p", "k", "pin" });
        wrapped.Id = "block";
        VariableDictionary.Instance().Add(wrapped);
        parameters.SetParameter("RANDOM", "0x(FFFFFFFFFFFFFFFF)");
        StringVariableDeclaration unwrapped = (StringVariableDeclaration)algorithm.Unwrap(
            new[] { "p", "k", "block" });

        Assert.Multiple(() =>
        {
            Assert.That(unwrapped.Value, Is.EqualTo("0x(01234)"));
            Assert.That(parameters.GetParameter("RANDOM"), Is.EqualTo(Random));
        });
    }

    [Test]
    public void IdlessSerializedParameterIsRejectedWithoutMutation()
    {
        ParameterVariableDeclaration parameters = Parameters(string.Empty, Pan, Random);
        string before = parameters.Value;

        Assert.That(() => Algorithm().Wrap(new[] { parameters.Value, Key128, "0x(1234)" }),
            Throws.ArgumentException.With.Message.Contains("referencable PARAM"));
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [Test]
    public void WrongKeyAlgorithmAndMissingKeyMaterialDoNotMutateParameters()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, Random);
        string before = parameters.Value;
        KeyVariableDeclaration tdeaKey = Key(Key128);
        tdeaKey.KeyType = KeyType.Secret(KeyAlgorithm.Tdea);
        KeyVariableDeclaration missingKey = Key(string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(() => Algorithm().Wrap(
                Arguments(parameters, tdeaKey, Var("0x(1234)"))), Throws.ArgumentException);
            Assert.That(parameters.Value, Is.EqualTo(before));
            Assert.That(() => Algorithm().Wrap(
                Arguments(parameters, missingKey, Var("0x(1234)"))), Throws.Exception);
            Assert.That(parameters.Value, Is.EqualTo(before));
        });
    }

    [Test]
    public void InvalidRandomSourceResultDoesNotMutateParameters()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan);
        string before = parameters.Value;

        Assert.That(() => Algorithm(new byte[7]).Wrap(
            Arguments(parameters, Key(Key128), Var("0x(1234)"))),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [TestCase(null, Random, Key128, "0x(1234)")]
    [TestCase("0x(123456789)", Random, Key128, "0x(1234)")]
    [TestCase("0x(123456789A)", Random, Key128, "0x(1234)")]
    [TestCase(Pan, "0x(001122334455667)", Key128, "0x(1234)")]
    [TestCase(Pan, "0x(00112233445566GG)", Key128, "0x(1234)")]
    [TestCase(Pan, Random, "0x(00112233445566778899AABBCCDDEE)", "0x(1234)")]
    [TestCase(Pan, Random, Key128, "0x(123)")]
    [TestCase(Pan, Random, Key128, "0x(12A4)")]
    [TestCase(Pan, Random, Key128, "\"1234\"")]
    public void InvalidWrapInputsDoNotMutateParameters(
        string? pan,
        string random,
        string keyValue,
        string pinValue)
    {
        ParameterVariableDeclaration parameters = Parameters("p", pan, random);
        string before = parameters.Value;

        Assert.That(() => Algorithm().Wrap(
            Arguments(parameters, Key(keyValue), Var(pinValue))), Throws.Exception);
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [TestCase(15)]
    [TestCase(17)]
    public void InvalidCiphertextLengthDoesNotMutateParameters(int length)
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, "0x(FFFFFFFFFFFFFFFF)");
        string before = parameters.Value;
        StringVariableDeclaration ciphertext = Var($"0x({new string('0', length * 2)})");

        Assert.That(() => Algorithm().Unwrap(
            Arguments(parameters, Key(Key128), ciphertext)), Throws.ArgumentException);
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [Test]
    public void InvalidRecoveredPinFieldDoesNotMutateParameters()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, "0x(FFFFFFFFFFFFFFFF)");
        string before = parameters.Value;
        byte[] key = Convert.FromHexString(FormatConversions.HexStringToString(Key128));
        byte[] invalidPinField = Convert.FromHexString("441234AAAAAAAAAB0011223344556677");
        byte[] panField = PinBlockFormat4FieldCodec.EncodePanField("1234567890123456");
        byte[] firstAes = AES_ECB.EncryptNoPadding(key, invalidPinField);
        byte[] ciphertext = AES_ECB.EncryptNoPadding(key, Xor(firstAes, panField));

        Assert.That(() => Algorithm().Unwrap(Arguments(
            parameters, Key(Key128), Var(FormatConversions.ByteArrayToHexString(ciphertext)))),
            Throws.TypeOf<FormatException>());
        Assert.That(parameters.Value, Is.EqualTo(before));
    }

    [Test]
    public void RuntimeContractRejectsAdditionalNamedParameterWithoutMutation()
    {
        ParameterVariableDeclaration parameters = Parameters("p", Pan, Random);
        parameters.SetParameter("IV", "0x(00000000000000000000000000000000)");
        string before = parameters.Value;
        var invocation = new OperationInvocation(new[]
        {
            new ResolvedCallArgument(parameters.Value, ResolvedCallArgumentKind.Variable, parameters),
            new ResolvedCallArgument(Key128, ResolvedCallArgumentKind.Expression),
            new ResolvedCallArgument("0x(1234)", ResolvedCallArgumentKind.Expression)
        });

        FunctionContractException error = Assert.Throws<FunctionContractException>(() =>
            new CryptoOperations().Wrap(invocation))!;

        Assert.Multiple(() =>
        {
            Assert.That(error.Error, Is.EqualTo(FunctionContractError.ForbiddenAdditionalParameter));
            Assert.That(error.ParameterName, Is.EqualTo("#IV"));
            Assert.That(parameters.Value, Is.EqualTo(before));
        });
    }

    [TestCase(Key128, "001234")]
    [TestCase(Key192, "01234")]
    [TestCase(Key256, "000012345678")]
    public void ScriptEndToEndWrapAndUnwrapPreserveHexPinAndParameterMutation(
        string keyValue,
        string pin)
    {
        string script =
            $"PARAM p=#MECH:{Mechanism} #PAN:{Pan} #RANDOM:{Random} " +
            $"KEY k={keyValue} " +
            $"VAR pin=0x({pin}) " +
            "VAR block=Wrap(p,k,pin) " +
            "VAR recovered=Unwrap(p,k,block)";

        Execute(script);

        var parameters = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p");
        VariableDeclaration block = VariableDictionary.Instance().Get("block");
        VariableDeclaration recovered = VariableDictionary.Instance().Get("recovered");
        Assert.Multiple(() =>
        {
            Assert.That(block.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(FormatConversions.ToByteArray(block.Value, block.ValueFormat), Has.Length.EqualTo(16));
            Assert.That(recovered.Value, Is.EqualTo($"0x({pin})"));
            Assert.That(recovered.ValueFormat, Is.EqualTo(FormatConversions.HEX));
            Assert.That(parameters.GetParameter("RANDOM"), Is.EqualTo(Random));
            Assert.That(parameters.GetParameter("PAN"), Is.EqualTo(Pan));
            Assert.That(parameters.GetParameter("MECH"), Is.EqualTo(Mechanism));
        });
    }

    private static PinBlockAlgorithm Algorithm(byte[]? generatedRandom = null) => new(
        Mechanism,
        PinBlockCipherFamily.Aes,
        _ => throw new AssertionException("DES3 random source must not be used."),
        count => generatedRandom is null
            ? RandomNumberGenerator.GetBytes(count)
            : (byte[])generatedRandom.Clone());

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
        string id,
        string? pan,
        string? random = null)
    {
        var result = new ParameterVariableDeclaration { Id = id, Mechanism = Mechanism };
        if (pan is not null)
            result.SetParameter("PAN", pan);
        if (random is not null)
            result.SetParameter("RANDOM", random);
        return result;
    }

    private static KeyVariableDeclaration Key(string value, string id = "k") => new()
    {
        Id = id,
        Value = value,
        ValueFormat = FormatConversions.ParseString(value),
        Type = new CryptoTypeKey(),
        KeyType = KeyType.Secret(KeyAlgorithm.Aes)
    };

    private static StringVariableDeclaration Var(string value, string id = "data") => new()
    {
        Id = id,
        Value = value,
        ValueFormat = FormatConversions.ParseString(value),
        Type = new CryptoTypeVar()
    };

    private static byte[] Xor(byte[] left, byte[] right)
    {
        byte[] result = new byte[left.Length];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)(left[index] ^ right[index]);
        return result;
    }

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
