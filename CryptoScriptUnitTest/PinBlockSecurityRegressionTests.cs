using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.AES;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.CryptoAlgorithm.PINBLOCK;
using CryptoScript.Model;
using CryptoScript.Variables;
using AesAlgorithm = CryptoScript.CryptoAlgorithm.AES.AES;
using Des3Algorithm = CryptoScript.CryptoAlgorithm.DES3.DES3;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class PinBlockSecurityRegressionTests
{
    private const string Des3Key = "0x(0123456789ABCDEFFEDCBA9876543210)";
    private const string AesKey = "0x(000102030405060708090A0B0C0D0E0F)";
    private const string Pan = "0x(1234567890123456)";
    private const string PublicDecodeError = "Decrypted PIN block is invalid.";
    private static readonly string[] Mechanisms =
    {
        "WRAP-AES-PINBLOCK-4",
        "WRAP-DES3-PINBLOCK-0",
        "WRAP-DES3-PINBLOCK-1",
        "WRAP-DES3-PINBLOCK-2",
        "WRAP-DES3-PINBLOCK-3"
    };

    [SetUp]
    public void SetUp() => VariableDictionary.Instance().Clear();

    [TearDown]
    public void TearDown() => VariableDictionary.Instance().Clear();

    [TestCase("WRAP-DES3-PINBLOCK-2", KeyAlgorithm.Tdea, Des3Key)]
    [TestCase("WRAP-AES-PINBLOCK-4", KeyAlgorithm.Aes, AesKey)]
    public void PinBlocksRequirePinEncryptButAcceptPinEncryptAndUnspecified(
        string mechanism,
        KeyAlgorithm keyAlgorithm,
        string keyValue)
    {
        ParameterVariableDeclaration parameters = PinParameters(mechanism);
        PinBlockAlgorithm algorithm = (PinBlockAlgorithm)AlgorithmFactory.Create(mechanism);
        KeyVariableDeclaration pinKey = Key(keyValue, keyAlgorithm,
            KeyUsagePolicy.Restricted(KeyUsage.PinEncrypt));
        KeyVariableDeclaration unrestrictedKey = Key(keyValue, keyAlgorithm, KeyUsagePolicy.Unspecified);
        KeyVariableDeclaration wrongUsageKey = Key(keyValue, keyAlgorithm,
            KeyUsagePolicy.Restricted(KeyUsage.Encrypt));

        StringVariableDeclaration pinWrapped = algorithm.Wrap(Arguments(
            parameters, pinKey, Var("0x(01234)")));
        StringVariableDeclaration unrestrictedWrapped = algorithm.Wrap(Arguments(
            parameters, unrestrictedKey, Var("0x(01234)")));

        Assert.Multiple(() =>
        {
            Assert.That(((StringVariableDeclaration)algorithm.Unwrap(Arguments(
                parameters, pinKey, pinWrapped))).Value, Is.EqualTo("0x(01234)"));
            Assert.That(((StringVariableDeclaration)algorithm.Unwrap(Arguments(
                parameters, unrestrictedKey, unrestrictedWrapped))).Value, Is.EqualTo("0x(01234)"));
            Assert.That(() => algorithm.Wrap(Arguments(
                parameters, wrongUsageKey, Var("0x(01234)"))), Throws.ArgumentException);
            Assert.That(() => algorithm.Unwrap(Arguments(
                parameters, wrongUsageKey, pinWrapped)), Throws.ArgumentException);
        });

        var legacyPin = Var("0x(01234)");
        VariableDictionary.Instance().Add(parameters);
        VariableDictionary.Instance().Add(pinKey);
        VariableDictionary.Instance().Add(legacyPin);
        Assert.DoesNotThrow(() => algorithm.Wrap(new[] { "p", "k", "data" }));
    }

    [TestCase("AES-ECB", "Encrypt")]
    [TestCase("AES-ECB", "Decrypt")]
    [TestCase("AES-CMAC", "Mac")]
    [TestCase("DES3-ECB", "Encrypt")]
    [TestCase("DES3-ECB", "Decrypt")]
    [TestCase("DES3-CMAC", "Mac")]
    public void PinEncryptOnlyKeysAreRejectedByGeneralAesAndDes3Operations(
        string mechanism,
        string function)
    {
        bool aes = mechanism.StartsWith("AES", StringComparison.Ordinal);
        CryptoAlgorithm algorithm = aes ? new AesAlgorithm() : new Des3Algorithm();
        ParameterVariableDeclaration parameters = algorithm.GenerateParameters(mechanism);
        parameters.Id = "p";
        KeyVariableDeclaration key = Key(
            aes ? AesKey : Des3Key,
            aes ? KeyAlgorithm.Aes : KeyAlgorithm.Tdea,
            KeyUsagePolicy.Restricted(KeyUsage.PinEncrypt));
        StringVariableDeclaration data = Var(
            aes ? "0x(00112233445566778899AABBCCDDEEFF)" : "0x(0011223344556677)");
        AlgorithmCallArguments arguments = Arguments(parameters, key, data);

        Action operation = function switch
        {
            "Encrypt" => () => algorithm.Encrypt(arguments),
            "Decrypt" => () => algorithm.Decrypt(arguments),
            _ => () => algorithm.Mac(arguments)
        };

        Assert.That(operation, Throws.ArgumentException.With.Message.Contains("usage policy"));

        VariableDictionary.Instance().Add(parameters);
        VariableDictionary.Instance().Add(key);
        VariableDictionary.Instance().Add(data);
        Action legacyOperation = function switch
        {
            "Encrypt" => () => algorithm.Encrypt(new[] { "p", "k", "data" }),
            "Decrypt" => () => algorithm.Decrypt(new[] { "p", "k", "data" }),
            _ => () => algorithm.Mac(new[] { "p", "k", "data" })
        };
        Assert.That(legacyOperation, Throws.ArgumentException.With.Message.Contains("usage policy"));
    }

    [Test]
    public void Des3DecodeFailuresHaveOnePublicErrorForEveryFormat()
    {
        var cases = new[]
        {
            ("WRAP-DES3-PINBLOCK-0", XorHex("141234FFFFFFFFFF", "0000456789012345")),
            ("WRAP-DES3-PINBLOCK-1", "0412340123456789"),
            ("WRAP-DES3-PINBLOCK-2", "141234FFFFFFFFFF"),
            ("WRAP-DES3-PINBLOCK-3", XorHex("241234ABCDEFABCD", "0000456789012345"))
        };
        byte[] keyBytes = FormatConversions.ToByteArray(Des3Key, FormatConversions.HEX);

        foreach ((string mechanism, string clearHex) in cases)
        {
            ParameterVariableDeclaration parameters = PinParameters(mechanism);
            string before = parameters.Value;
            byte[] ciphertext = DES3_ECB.EncryptNoPadding(keyBytes, Convert.FromHexString(clearHex));

            FormatException error = Assert.Throws<FormatException>(() =>
                ((PinBlockAlgorithm)AlgorithmFactory.Create(mechanism)).Unwrap(Arguments(
                    parameters,
                    Key(Des3Key, KeyAlgorithm.Tdea, KeyUsagePolicy.Unspecified),
                    Var(FormatConversions.ByteArrayToHexString(ciphertext)))))!;

            Assert.Multiple(() =>
            {
                Assert.That(error.Message, Is.EqualTo(PublicDecodeError), mechanism);
                Assert.That(parameters.Value, Is.EqualTo(before), mechanism);
            });
        }
    }

    [TestCase("141234FFFFFFFFFF")]
    [TestCase("2D1234FFFFFFFFFF")]
    [TestCase("241A34FFFFFFFFFF")]
    [TestCase("241234FFFFFFFFFE")]
    public void DistinctDes3ClearFieldFailuresArePubliclyIndistinguishable(string clearHex)
    {
        const string mechanism = "WRAP-DES3-PINBLOCK-2";
        byte[] keyBytes = FormatConversions.ToByteArray(Des3Key, FormatConversions.HEX);
        byte[] ciphertext = DES3_ECB.EncryptNoPadding(keyBytes, Convert.FromHexString(clearHex));

        FormatException error = Assert.Throws<FormatException>(() =>
            ((PinBlockAlgorithm)AlgorithmFactory.Create(mechanism)).Unwrap(Arguments(
                PinParameters(mechanism),
                Key(Des3Key, KeyAlgorithm.Tdea, KeyUsagePolicy.Unspecified),
                Var(FormatConversions.ByteArrayToHexString(ciphertext)))))!;

        Assert.That(error.Message, Is.EqualTo(PublicDecodeError));
        Assert.That(error.Message, Does.Not.Contain(clearHex));
    }

    [TestCase("541234AAAAAAAAAA0011223344556677")]
    [TestCase("4D1234AAAAAAAAAA0011223344556677")]
    [TestCase("441A34AAAAAAAAAA0011223344556677")]
    [TestCase("441234AAAAAAAAAB0011223344556677")]
    public void DistinctFormat4ClearFieldFailuresArePubliclyIndistinguishable(string pinFieldHex)
    {
        byte[] keyBytes = FormatConversions.ToByteArray(AesKey, FormatConversions.HEX);
        byte[] pinField = Convert.FromHexString(pinFieldHex);
        byte[] panField = PinBlockFormat4FieldCodec.EncodePanField("1234567890123456");
        byte[] firstAes = AES_ECB.EncryptNoPadding(keyBytes, pinField);
        byte[] ciphertext = AES_ECB.EncryptNoPadding(keyBytes, Xor(firstAes, panField));

        FormatException error = Assert.Throws<FormatException>(() =>
            ((PinBlockAlgorithm)AlgorithmFactory.Create("WRAP-AES-PINBLOCK-4")).Unwrap(Arguments(
                PinParameters("WRAP-AES-PINBLOCK-4"),
                Key(AesKey, KeyAlgorithm.Aes, KeyUsagePolicy.Unspecified),
                Var(FormatConversions.ByteArrayToHexString(ciphertext)))))!;

        Assert.That(error.Message, Is.EqualTo(PublicDecodeError));
        Assert.That(error.Message, Does.Not.Contain(pinFieldHex));
    }

    [Test]
    public void AllPinBlockMechanismsEnforceClosedContractsOnBothPublicPaths()
    {
        foreach (string mechanism in Mechanisms)
        {
            foreach (CryptoScriptFunction function in new[]
                     {
                         CryptoScriptFunction.Wrap,
                         CryptoScriptFunction.Unwrap
                     })
            {
                AssertBothPaths(
                    mechanism,
                    function,
                    parameters => parameters.GetParameters()["#UNKNOWN"] = "secret",
                    FunctionContractError.UnknownParameter,
                    "#UNKNOWN");
                AssertBothPaths(
                    mechanism,
                    function,
                    parameters => parameters.SetParameter("IV", "0x(0000000000000000)"),
                    FunctionContractError.ForbiddenAdditionalParameter,
                    "#IV");
            }
        }
    }

    [TestCase("WRAP-DES3-PINBLOCK-0")]
    [TestCase("WRAP-DES3-PINBLOCK-3")]
    [TestCase("WRAP-AES-PINBLOCK-4")]
    public void RequiredPanIsEnforcedOnBothPublicPaths(string mechanism)
    {
        foreach (CryptoScriptFunction function in new[]
                 {
                     CryptoScriptFunction.Wrap,
                     CryptoScriptFunction.Unwrap
                 })
        {
            AssertBothPaths(
                mechanism,
                function,
                _ => { },
                FunctionContractError.MissingRequiredParameter,
                "#PAN",
                includeRequiredParameters: false);
        }
    }

    private static void AssertBothPaths(
        string mechanism,
        CryptoScriptFunction function,
        Action<ParameterVariableDeclaration> arrange,
        FunctionContractError expectedError,
        string expectedParameter,
        bool includeRequiredParameters = true)
    {
        foreach (bool legacy in new[] { true, false })
        {
            VariableDictionary.Instance().Clear();
            ParameterVariableDeclaration parameters = includeRequiredParameters
                ? PinParameters(mechanism)
                : new ParameterVariableDeclaration { Id = "p", Mechanism = mechanism };
            arrange(parameters);

            FunctionContractException error;
            if (legacy)
            {
                VariableDictionary.Instance().Add(parameters);
                var operations = new CryptoOperations();
                error = Assert.Throws<FunctionContractException>(() =>
                {
                    if (function == CryptoScriptFunction.Wrap)
                        operations.Wrap(new[] { "p", "missing-key", "missing-data" });
                    else
                        operations.Unwrap(new[] { "p", "missing-key", "missing-data" });
                })!;
            }
            else
            {
                var invocation = new OperationInvocation(new[]
                {
                    new ResolvedCallArgument(parameters.Value, ResolvedCallArgumentKind.Variable, parameters),
                    new ResolvedCallArgument("missing-key", ResolvedCallArgumentKind.Expression),
                    new ResolvedCallArgument("missing-data", ResolvedCallArgumentKind.Expression)
                });
                var operations = new CryptoOperations();
                error = Assert.Throws<FunctionContractException>(() =>
                {
                    if (function == CryptoScriptFunction.Wrap)
                        operations.Wrap(invocation);
                    else
                        operations.Unwrap(invocation);
                })!;
            }

            Assert.Multiple(() =>
            {
                Assert.That(error.Error, Is.EqualTo(expectedError), $"{mechanism} {function} legacy={legacy}");
                Assert.That(error.ParameterName, Is.EqualTo(expectedParameter), $"{mechanism} {function} legacy={legacy}");
                Assert.That(error.Message, Does.Not.Contain("secret"));
            });
        }
    }

    private static ParameterVariableDeclaration PinParameters(string mechanism)
    {
        var result = new ParameterVariableDeclaration { Id = "p", Mechanism = mechanism };
        if (mechanism.EndsWith("-0", StringComparison.Ordinal) ||
            mechanism.EndsWith("-3", StringComparison.Ordinal) ||
            mechanism.EndsWith("-4", StringComparison.Ordinal))
        {
            result.SetParameter("PAN", Pan);
        }
        if (mechanism.EndsWith("-1", StringComparison.Ordinal))
            result.SetParameter("TRANSACTION", "0x(0123456789)");
        if (mechanism.EndsWith("-3", StringComparison.Ordinal))
            result.SetParameter("FILL", "0x(ABCDEFABCD)");
        if (mechanism.EndsWith("-4", StringComparison.Ordinal))
            result.SetParameter("RANDOM", "0x(0011223344556677)");
        return result;
    }

    private static AlgorithmCallArguments Arguments(
        ParameterVariableDeclaration parameters,
        VariableDeclaration key,
        VariableDeclaration data) => new(new[]
        {
            new AlgorithmCallArgument(parameters.Value, parameters),
            new AlgorithmCallArgument(key.Value, key),
            new AlgorithmCallArgument(data.Value, data)
        });

    private static KeyVariableDeclaration Key(
        string value,
        KeyAlgorithm algorithm,
        KeyUsagePolicy usage) => new()
        {
            Id = "k",
            Value = value,
            KeyValue = value,
            ValueFormat = FormatConversions.HEX,
            Type = new CryptoTypeKey(),
            KeyType = KeyType.Secret(algorithm),
            Usage = usage
        };

    private static StringVariableDeclaration Var(string value) => new()
    {
        Id = "data",
        Value = value,
        ValueFormat = FormatConversions.ParseString(value),
        Type = new CryptoTypeVar()
    };

    private static string XorHex(string left, string right) =>
        Convert.ToHexString(Xor(Convert.FromHexString(left), Convert.FromHexString(right)));

    private static byte[] Xor(byte[] left, byte[] right)
    {
        byte[] result = new byte[left.Length];
        for (int index = 0; index < result.Length; index++)
            result[index] = (byte)(left[index] ^ right[index]);
        return result;
    }
}
