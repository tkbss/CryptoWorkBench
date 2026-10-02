using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;
using Org.BouncyCastle.Crypto.Paddings;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class Iso10126PaddingTests
{
    private const string AesKeyHex = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string AesIvHex = "000102030405060708090A0B0C0D0E0F";
    private const string Des3KeyHex = "0123456789ABCDEFFEDCBA98765432100011223344556677";
    private const string Des3IvHex = "1234567890ABCDEF";

    [TestCase("AES-CBC", 16, 0, 16)]
    [TestCase("AES-CBC", 16, 1, 15)]
    [TestCase("AES-CBC", 16, 15, 1)]
    [TestCase("AES-CBC", 16, 16, 16)]
    [TestCase("AES-CBC", 16, 17, 15)]
    [TestCase("DES3-CBC", 8, 0, 8)]
    [TestCase("DES3-CBC", 8, 1, 7)]
    [TestCase("DES3-CBC", 8, 7, 1)]
    [TestCase("DES3-CBC", 8, 8, 8)]
    [TestCase("DES3-CBC", 8, 9, 7)]
    public void Encrypt_UsesExpectedIso10126LayoutAndRoundtrips(
        string mechanism, int blockSize, int plaintextLength, int expectedPaddingLength)
    {
        byte[] plaintext = CreatePlaintext(plaintextLength);

        (byte[] ciphertext, byte[] cleartext) = EncryptAndDecrypt(mechanism, plaintext);
        byte[] paddedPlaintext = TransformNoPadding(mechanism, ciphertext, encrypt: false);

        cleartext.Should().Equal(plaintext);
        paddedPlaintext.Should().HaveCount(plaintext.Length + expectedPaddingLength);
        paddedPlaintext[..plaintext.Length].Should().Equal(plaintext);
        paddedPlaintext[^1].Should().Be((byte)expectedPaddingLength);
        paddedPlaintext[plaintext.Length..^1].Should().HaveCount(expectedPaddingLength - 1);
        expectedPaddingLength.Should().Be(blockSize - plaintext.Length % blockSize);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(7)]
    [TestCase(8)]
    [TestCase(9)]
    public void Des3Ecb_EncryptDecryptIso10126Roundtrip(int plaintextLength)
    {
        byte[] plaintext = CreatePlaintext(plaintextLength);

        (byte[] ciphertext, byte[] cleartext) = EncryptAndDecrypt("DES3-ECB", plaintext);

        ciphertext.Should().HaveCount(plaintext.Length + 8 - plaintext.Length % 8);
        cleartext.Should().Equal(plaintext);
    }

    private static readonly object[] ValidManualPaddingCases =
    {
        new object[] { "AES-CBC", 16, 1, "none" },
        new object[] { "AES-CBC", 16, 16, "mixed" },
        new object[] { "AES-CBC", 16, 5, "zero" },
        new object[] { "AES-CBC", 16, 5, "ff" },
        new object[] { "AES-CBC", 16, 5, "alternating" },
        new object[] { "AES-CBC", 16, 5, "same-as-length" },
        new object[] { "AES-CBC", 16, 5, "mixed" },
        new object[] { "DES3-CBC", 8, 1, "none" },
        new object[] { "DES3-CBC", 8, 8, "mixed" },
        new object[] { "DES3-CBC", 8, 5, "zero" },
        new object[] { "DES3-CBC", 8, 5, "ff" },
        new object[] { "DES3-CBC", 8, 5, "alternating" },
        new object[] { "DES3-CBC", 8, 5, "same-as-length" },
        new object[] { "DES3-CBC", 8, 5, "mixed" }
    };

    [TestCaseSource(nameof(ValidManualPaddingCases))]
    public void Decrypt_AcceptsValidLengthAndArbitraryFillerBytes(
        string mechanism, int blockSize, int paddingLength, string fillerPattern)
    {
        byte[] plaintext = CreatePlaintext(blockSize - paddingLength);
        byte[] paddedPlaintext = CreateManualPadding(
            plaintext, paddingLength, fillerPattern);
        byte[] ciphertext = TransformNoPadding(mechanism, paddedPlaintext, encrypt: true);

        byte[] cleartext = DecryptWithCryptoScript(mechanism, ciphertext);

        cleartext.Should().Equal(plaintext);
        paddedPlaintext[^1].Should().Be((byte)paddingLength);
        new ISO10126d2Padding().PadCount(paddedPlaintext[^blockSize..])
            .Should().Be(paddingLength);
    }

    [TestCase("AES-CBC", 16)]
    [TestCase("DES3-CBC", 8)]
    public void Decrypt_RejectsEmptyCiphertext(string mechanism, int blockSize)
    {
        Action act = () => DecryptWithCryptoScript(mechanism, Array.Empty<byte>());

        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("AES-CBC", 16)]
    [TestCase("DES3-CBC", 8)]
    public void Decrypt_RejectsNonBlockAlignedCiphertext(string mechanism, int blockSize)
    {
        byte[] ciphertext = Enumerable.Repeat((byte)0xA5, blockSize - 1).ToArray();

        Action act = () => DecryptWithCryptoScript(mechanism, ciphertext);

        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("AES-CBC", 16, 0)]
    [TestCase("AES-CBC", 16, 17)]
    [TestCase("AES-CBC", 16, 255)]
    [TestCase("DES3-CBC", 8, 0)]
    [TestCase("DES3-CBC", 8, 9)]
    [TestCase("DES3-CBC", 8, 255)]
    public void Decrypt_RejectsInvalidDeclaredPaddingLength(
        string mechanism, int blockSize, int declaredPaddingLength)
    {
        byte[] invalidPaddedPlaintext = Enumerable.Repeat((byte)0x5A, blockSize).ToArray();
        invalidPaddedPlaintext[^1] = (byte)declaredPaddingLength;
        byte[] ciphertext = TransformNoPadding(mechanism, invalidPaddedPlaintext, encrypt: true);

        Action act = () => DecryptWithCryptoScript(mechanism, ciphertext);

        act.Should().Throw<SemanticErrorException>();
    }

    [SetUp]
    public void Setup()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    private static (byte[] Ciphertext, byte[] Cleartext) EncryptAndDecrypt(
        string mechanism, byte[] plaintext)
    {
        string script =
            $"KEY k=GenerateKey({mechanism},0x({GetKeyHex(mechanism)})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{GetIvParameter(mechanism)},#PAD:ISO-10126) " +
            $"VAR c=Encrypt(p,k,{ToScriptValue(plaintext)}) " +
            "VAR clear=Decrypt(p,k,c)";
        CryptoScriptProgram result = Execute(script);

        return (
            GetBytes(result.Statements[2]),
            GetBytes(result.Statements[3]));
    }

    private static byte[] DecryptWithCryptoScript(string mechanism, byte[] ciphertext)
    {
        string script =
            $"KEY k=GenerateKey({mechanism},0x({GetKeyHex(mechanism)})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{GetIvParameter(mechanism)},#PAD:ISO-10126) " +
            $"VAR clear=Decrypt(p,k,{ToScriptValue(ciphertext)})";
        return GetBytes(Execute(script).Statements[2]);
    }

    private static CryptoScriptProgram Execute(string script)
    {
        CryptoScriptParser parser = ParserBuilder.StringBuild(script);
        CryptoScriptProgram result = new CryptoScriptRunner().Execute(parser.program());

        parser.NumberOfSyntaxErrors.Should().Be(0);
        SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
        LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        return result;
    }

    private static byte[] TransformNoPadding(string mechanism, byte[] input, bool encrypt)
    {
        using SymmetricAlgorithm algorithm = mechanism == "AES-CBC"
            ? Aes.Create()
            : TripleDES.Create();
        algorithm.Mode = mechanism == "DES3-ECB" ? CipherMode.ECB : CipherMode.CBC;
        algorithm.Padding = PaddingMode.None;
        algorithm.Key = Convert.FromHexString(GetKeyHex(mechanism));
        if (algorithm.Mode == CipherMode.CBC)
            algorithm.IV = Convert.FromHexString(mechanism == "AES-CBC" ? AesIvHex : Des3IvHex);
        using ICryptoTransform transform = encrypt
            ? algorithm.CreateEncryptor()
            : algorithm.CreateDecryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }

    private static byte[] CreateManualPadding(
        byte[] plaintext, int paddingLength, string fillerPattern)
    {
        byte[] padded = new byte[plaintext.Length + paddingLength];
        Buffer.BlockCopy(plaintext, 0, padded, 0, plaintext.Length);
        for (int index = 0; index < paddingLength - 1; index++)
        {
            padded[plaintext.Length + index] = fillerPattern switch
            {
                "zero" => 0x00,
                "ff" => 0xFF,
                "alternating" => index % 2 == 0 ? (byte)0xAA : (byte)0x55,
                "same-as-length" => (byte)paddingLength,
                "mixed" => new byte[] { 0x00, 0xFF, 0x7E, 0x13 }[index % 4],
                "none" => throw new InvalidOperationException("N=1 has no filler bytes."),
                _ => throw new ArgumentOutOfRangeException(nameof(fillerPattern))
            };
        }
        padded[^1] = (byte)paddingLength;
        return padded;
    }

    private static byte[] CreatePlaintext(int length) =>
        Enumerable.Range(1, length).Select(value => (byte)value).ToArray();

    private static byte[] GetBytes(Statement statement)
    {
        StringVariableDeclaration value = statement.Should()
            .BeOfType<StringVariableDeclaration>().Subject;
        return FormatConversions.ToByteArray(value.Value, value.ValueFormat);
    }

    private static string GetKeyHex(string mechanism) =>
        mechanism == "AES-CBC" ? AesKeyHex : Des3KeyHex;

    private static string GetIvParameter(string mechanism) => mechanism switch
    {
        "AES-CBC" => $",#IV:0x({AesIvHex})",
        "DES3-CBC" => $",#IV:0x({Des3IvHex})",
        _ => string.Empty
    };

    private static string ToScriptValue(byte[] value) =>
        value.Length == 0 ? "\"\"" : $"0x({Convert.ToHexString(value)})";
}
