using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;
using FluentAssertions;
using Org.BouncyCastle.Crypto.Paddings;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class AnsiX923PaddingTests
{
    private const string AesKeyHex = "2B7E151628AED2A6ABF7158809CF4F3C";
    private const string AesIvHex = "000102030405060708090A0B0C0D0E0F";
    private const string Des3KeyHex = "0123456789ABCDEFFEDCBA98765432100011223344556677";
    private const string Des3IvHex = "1234567890ABCDEF";

    [TestCase("DES3-CBC", 8, "", "0000000000000008")]
    [TestCase("DES3-CBC", 8, "01", "0100000000000007")]
    [TestCase("DES3-CBC", 8, "01020304050607", "0102030405060701")]
    [TestCase("DES3-CBC", 8, "0102030405060708", "01020304050607080000000000000008")]
    [TestCase("DES3-CBC", 8, "010203040506070809", "01020304050607080900000000000007")]
    [TestCase("AES-CBC", 16, "", "00000000000000000000000000000010")]
    [TestCase("AES-CBC", 16, "01", "0100000000000000000000000000000F")]
    [TestCase("AES-CBC", 16, "0102030405060708090A0B0C0D0E0F", "0102030405060708090A0B0C0D0E0F01")]
    [TestCase("AES-CBC", 16, "0102030405060708090A0B0C0D0E0F10",
        "0102030405060708090A0B0C0D0E0F1000000000000000000000000000000010")]
    [TestCase("AES-CBC", 16, "0102030405060708090A0B0C0D0E0F1011",
        "0102030405060708090A0B0C0D0E0F101100000000000000000000000000000F")]
    public void Encrypt_UsesExactZeroFilledLayoutAndRoundtrips(
        string mechanism, int blockSize, string plaintextHex, string expectedPaddedHex)
    {
        byte[] plaintext = Convert.FromHexString(plaintextHex);
        byte[] expectedPadded = Convert.FromHexString(expectedPaddedHex);

        (byte[] ciphertext, byte[] cleartext) = EncryptAndDecrypt(mechanism, plaintext);
        byte[] paddedPlaintext = TransformNoPadding(mechanism, ciphertext, encrypt: false);

        cleartext.Should().Equal(plaintext);
        paddedPlaintext.Should().Equal(expectedPadded);

        int remainder = plaintext.Length % blockSize;
        byte[] bouncyCastleFinalBlock = new byte[blockSize];
        if (remainder > 0)
        {
            Buffer.BlockCopy(plaintext, plaintext.Length - remainder,
                bouncyCastleFinalBlock, 0, remainder);
        }
        var bouncyCastlePadding = new X923Padding();
        bouncyCastlePadding.Init(null);
        bouncyCastlePadding.AddPadding(bouncyCastleFinalBlock, remainder);
        expectedPadded[^blockSize..].Should().Equal(bouncyCastleFinalBlock);
    }

    [TestCase("")]
    [TestCase("01")]
    [TestCase("01020304050607")]
    [TestCase("0102030405060708")]
    [TestCase("010203040506070809")]
    public void Des3Ecb_EncryptDecryptRoundtrip(string plaintextHex)
    {
        byte[] plaintext = Convert.FromHexString(plaintextHex);

        (byte[] ciphertext, byte[] cleartext) = EncryptAndDecrypt("DES3-ECB", plaintext);

        ciphertext.Should().HaveCount(plaintext.Length + 8 - plaintext.Length % 8);
        cleartext.Should().Equal(plaintext);
    }

    [Test]
    public void Des3Ecb_EncryptUsesExactZeroFilledLayout()
    {
        byte[] ciphertext = EncryptWithCryptoScript(
            "DES3-ECB", Convert.FromHexString("010203"));

        byte[] paddedPlaintext = TransformNoPadding(
            "DES3-ECB", ciphertext, encrypt: false);

        paddedPlaintext.Should().Equal(Convert.FromHexString("0102030000000005"));
    }

    [Test]
    public void Des3Ecb_DecryptRejectsNonZeroFillerByte()
    {
        byte[] invalidPaddedPlaintext = Convert.FromHexString("010203A500000005");
        byte[] ciphertext = TransformNoPadding(
            "DES3-ECB", invalidPaddedPlaintext, encrypt: true);

        Action act = () => DecryptWithCryptoScript("DES3-ECB", ciphertext);

        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("AES-CBC", "0102030405060708090A0B0C0D0E0F", "0102030405060708090A0B0C0D0E0F01")]
    [TestCase("AES-CBC", "", "00000000000000000000000000000010")]
    [TestCase("AES-CBC", "010203", "0102030000000000000000000000000D")]
    [TestCase("DES3-CBC", "01020304050607", "0102030405060701")]
    [TestCase("DES3-CBC", "", "0000000000000008")]
    [TestCase("DES3-CBC", "010203", "0102030000000005")]
    public void Decrypt_AcceptsManuallyConstructedValidPadding(
        string mechanism, string plaintextHex, string paddedPlaintextHex)
    {
        byte[] paddedPlaintext = Convert.FromHexString(paddedPlaintextHex);
        byte[] ciphertext = TransformNoPadding(mechanism, paddedPlaintext, encrypt: true);

        byte[] cleartext = DecryptWithCryptoScript(mechanism, ciphertext);

        cleartext.Should().Equal(Convert.FromHexString(plaintextHex));
    }

    [TestCase("AES-CBC", 16, "first")]
    [TestCase("AES-CBC", 16, "middle")]
    [TestCase("AES-CBC", 16, "last")]
    [TestCase("DES3-CBC", 8, "first")]
    [TestCase("DES3-CBC", 8, "middle")]
    [TestCase("DES3-CBC", 8, "last")]
    public void Decrypt_RejectsNonZeroFillerByte(
        string mechanism, int blockSize, string fillerPosition)
    {
        const int plaintextLength = 3;
        int paddingLength = blockSize - plaintextLength;
        byte[] invalidPaddedPlaintext = new byte[blockSize];
        invalidPaddedPlaintext[0] = 0x01;
        invalidPaddedPlaintext[1] = 0x02;
        invalidPaddedPlaintext[2] = 0x03;
        invalidPaddedPlaintext[^1] = (byte)paddingLength;
        int fillerIndex = fillerPosition switch
        {
            "first" => plaintextLength,
            "middle" => plaintextLength + (paddingLength - 2) / 2,
            "last" => blockSize - 2,
            _ => throw new ArgumentOutOfRangeException(nameof(fillerPosition))
        };
        invalidPaddedPlaintext[fillerIndex] = 0xA5;
        byte[] ciphertext = TransformNoPadding(mechanism, invalidPaddedPlaintext, encrypt: true);

        Action act = () => DecryptWithCryptoScript(mechanism, ciphertext);

        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("AES-CBC")]
    [TestCase("DES3-CBC")]
    [TestCase("DES3-ECB")]
    public void Decrypt_RejectsEmptyCiphertext(string mechanism)
    {
        Action act = () => DecryptWithCryptoScript(mechanism, Array.Empty<byte>());

        act.Should().Throw<SemanticErrorException>();
    }

    [TestCase("AES-CBC", 16)]
    [TestCase("DES3-CBC", 8)]
    [TestCase("DES3-ECB", 8)]
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

    [Test]
    public void NonCanonicalAnsiX923_IsRejectedByThePublicScriptPath()
    {
        const string script =
            $"PARAM p=Parameters(#MECH:AES-CBC,#IV:0x({AesIvHex}),#PAD:ANSIX923)";
        CryptoScriptParser parser = ParserBuilder.StringBuild(script);
        var context = parser.program();

        Assert.Multiple(() =>
        {
            parser.NumberOfSyntaxErrors.Should().Be(0);
            SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
            LexerErrorListener.LexerErrorOccured.Should().BeFalse();
        });

        Action act = () => new CryptoScriptRunner().Execute(context);
        act.Should().Throw<SemanticErrorException>()
            .Where(exception => exception.SemanticError!.Message.Contains(
                "Unknown parameter value : ANSIX923"));
    }

    [TestCase("", "0x(A69BD201F4D1FA9F)")]
    [TestCase("00112233445566", "0x(47DEE12C68B103CC)")]
    [TestCase("0011223344556677", "0x(ED02660DDD234F19)")]
    [TestCase("001122334455667788", "0x(5E3F66E1629BFBEA)")]
    public void Des3CbcMac_MatchesIndependentKnownAnswer(
        string messageHex, string expectedMac)
    {
        byte[] message = Convert.FromHexString(messageHex);

        MacWithCryptoScript(message).Should().BeEquivalentTo(
            expectedMac, options => options.IgnoringCase());
        ManualAnsiX923Mac(message).Should().BeEquivalentTo(
            expectedMac, options => options.IgnoringCase());
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
            $"PARAM p=Parameters(#MECH:{mechanism}{GetIvParameter(mechanism)},#PAD:ANSI-X923) " +
            $"VAR c=Encrypt(p,k,{ToScriptValue(plaintext)}) " +
            "VAR clear=Decrypt(p,k,c)";
        CryptoScriptProgram result = Execute(script);
        return (GetBytes(result.Statements[2]), GetBytes(result.Statements[3]));
    }

    private static byte[] EncryptWithCryptoScript(string mechanism, byte[] plaintext)
    {
        string script =
            $"KEY k=GenerateKey({mechanism},0x({GetKeyHex(mechanism)})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{GetIvParameter(mechanism)},#PAD:ANSI-X923) " +
            $"VAR c=Encrypt(p,k,{ToScriptValue(plaintext)})";
        return GetBytes(Execute(script).Statements[2]);
    }

    private static byte[] DecryptWithCryptoScript(string mechanism, byte[] ciphertext)
    {
        string script =
            $"KEY k=GenerateKey({mechanism},0x({GetKeyHex(mechanism)})) " +
            $"PARAM p=Parameters(#MECH:{mechanism}{GetIvParameter(mechanism)},#PAD:ANSI-X923) " +
            $"VAR clear=Decrypt(p,k,{ToScriptValue(ciphertext)})";
        return GetBytes(Execute(script).Statements[2]);
    }

    private static string MacWithCryptoScript(byte[] message)
    {
        string script =
            $"KEY k=GenerateKey(DES3-CBC,0x({Des3KeyHex})) " +
            "PARAM p=Parameters(#MECH:DES3-CBC,#PAD:ANSI-X923) " +
            $"VAR mac=Mac(p,k,{ToScriptValue(message)})";
        return Execute(script).Statements[2]
            .Should().BeOfType<StringVariableDeclaration>().Subject.Value;
    }

    private static string ManualAnsiX923Mac(byte[] message)
    {
        int paddingLength = 8 - message.Length % 8;
        byte[] padded = new byte[message.Length + paddingLength];
        Buffer.BlockCopy(message, 0, padded, 0, message.Length);
        padded[^1] = (byte)paddingLength;
        byte[] ciphertext = TransformNoPadding(
            "DES3-CBC", padded, encrypt: true, ivOverride: new byte[8]);
        return "0x(" + Convert.ToHexString(ciphertext[^8..]) + ")";
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

    private static byte[] TransformNoPadding(
        string mechanism, byte[] input, bool encrypt, byte[]? ivOverride = null)
    {
        using SymmetricAlgorithm algorithm = mechanism == "AES-CBC"
            ? Aes.Create()
            : TripleDES.Create();
        algorithm.Mode = mechanism == "DES3-ECB" ? CipherMode.ECB : CipherMode.CBC;
        algorithm.Padding = PaddingMode.None;
        algorithm.Key = Convert.FromHexString(GetKeyHex(mechanism));
        if (algorithm.Mode == CipherMode.CBC)
        {
            algorithm.IV = ivOverride ?? Convert.FromHexString(
                mechanism == "AES-CBC" ? AesIvHex : Des3IvHex);
        }
        using ICryptoTransform transform = encrypt
            ? algorithm.CreateEncryptor()
            : algorithm.CreateDecryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }

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
