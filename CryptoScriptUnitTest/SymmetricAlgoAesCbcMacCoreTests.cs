using CryptoScript.CryptoAlgorithm;
using CryptoScript.Variables;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest;

public class SymmetricAlgoAesCbcMacCoreTests
{
    private const string Message =
        "6BC0BCE12A459991E134741A7F9E1925" +
        "AE2D8A571E03AC9C9EB76FAC45AF8E51" +
        "30C81C46A35CE411E5FBC1191A0A52EF" +
        "F69F2445DF4F9B17AD2B417BE66C3710";

    private static readonly object[] DerivedNistCbcCases =
    {
        new object[] { "2B7E151628AED2A6ABF7158809CF4F3C", "3FF1CAA1681FAC09120ECA307586E1A7" },
        new object[] { "8E73B0F7DA0E6452C810F32B809079E562F8EAD2522C6B7B", "08B0E27988598881D920A9E64F5615CD" },
        new object[] { "603DEB1015CA71BE2B73AEF0857D77811F352C073B6108D72D9810A30914DFF4", "B2EB05E2C39BE9FCDA6C19078C6A9D1B" }
    };

    [TestCaseSource(nameof(DerivedNistCbcCases))]
    public void ModeMac_DerivedNistCbcReferenceValuesMatch(string key, string expected)
    {
        Mac(key, Message, "NONE").Should().BeEquivalentTo(
            "0x(" + expected + ")", options => options.IgnoringCase());
        IndependentCbcMac(key, Message).Should().Be(expected);
    }

    [Test]
    public void ModeMac_TruncatesFromTheLeft()
    {
        const string key = "2B7E151628AED2A6ABF7158809CF4F3C";
        Mac(key, Message, "NONE", 16).Should().BeEquivalentTo(
            "0x(3FF1CAA1681FAC09120ECA307586E1A7)", options => options.IgnoringCase());
        Mac(key, Message, "NONE", 8).Should().BeEquivalentTo(
            "0x(3FF1CAA1681FAC09)", options => options.IgnoringCase());
    }

    [TestCase(7)]
    [TestCase(17)]
    public void ModeMac_RejectsInvalidMacLength(int macLength)
    {
        Action act = () => Mac(Key(16), "00112233445566778899AABBCCDDEEFF", "NONE", macLength);
        act.Should().Throw<ArgumentException>().WithMessage("*between 8 and 16 bytes*");
    }

    [Test]
    public void ModeMac_ValidatesMacLengthBeforePadding()
    {
        Action act = () => Mac(Key(16), "001122", "NOT-A-PADDING", 7);
        act.Should().Throw<ArgumentException>()
            .WithMessage("AES-CBC-MAC length must be between 8 and 16 bytes.");
    }

    [Test]
    public void ModeMac_DefaultsToFullBlock()
    {
        Mac(Key(16), "00112233445566778899AABBCCDDEEFF", "NONE")
            .Should().MatchRegex("^0x\\([0-9A-Fa-f]{32}\\)$");
    }

    [TestCase(16)]
    [TestCase(24)]
    [TestCase(32)]
    public void ModeMac_AcceptsAllAesKeyLengths(int keyLength)
    {
        Mac(Key(keyLength), "00112233445566778899AABBCCDDEEFF", "NONE")
            .Should().MatchRegex("^0x\\([0-9A-Fa-f]{32}\\)$");
    }

    [Test]
    public void ModeMac_RejectsInvalidAesKeyLength()
    {
        Action act = () => Mac(Key(15), "00112233445566778899AABBCCDDEEFF", "NONE");
        act.Should().Throw<ArgumentException>().WithMessage("*16, 24, or 32 bytes*");
    }

    [TestCase("00112233445566778899AABBCCDDEEFF")]
    [TestCase("00112233445566778899AABBCCDDEEFF00112233445566778899AABBCCDDEEFF")]
    public void ModeMac_NoneAcceptsOneOrMoreCompleteBlocks(string message)
    {
        Mac(Key(16), message, "NONE").Should().MatchRegex("^0x\\([0-9A-Fa-f]{32}\\)$");
    }

    [Test]
    public void ModeMac_NoneRejectsEmptyInput()
    {
        Action act = () => Mac(Key(16), string.Empty, "NONE");
        act.Should().Throw<ArgumentException>().WithMessage("*non-empty input*at least one 16-byte block*");
    }

    [Test]
    public void ModeMac_NoneRejectsPartialBlock()
    {
        Action act = () => Mac(Key(16), "001122", "NONE");
        act.Should().Throw<ArgumentException>().WithMessage("*multiple of 16 bytes*");
    }

    [TestCase("PKCS-7", "")]
    [TestCase("PKCS-7", "001122")]
    [TestCase("PKCS-7", "00112233445566778899AABBCCDDEEFF")]
    [TestCase("ANSI-X923", "001122")]
    [TestCase("ISO-7816", "001122")]
    [TestCase("ISO-9797-M1", "")]
    [TestCase("ISO-9797-M1", "00112233445566778899AABBCCDDEEFF")]
    [TestCase("ISO-9797-M2", "001122")]
    [TestCase("ISO-9797-M3", "")]
    [TestCase("ISO-9797-M3", "001122")]
    [TestCase("TLS-CBC", "001122")]
    public void ModeMac_SupportedPaddingProducesDeterministicMac(string padding, string message)
    {
        string first = Mac(Key(16), message, padding);
        Mac(Key(16), message, padding).Should().Be(first);
        first.Should().BeEquivalentTo(
            "0x(" + IndependentPaddedCbcMac(Key(16), message, padding) + ")",
            options => options.IgnoringCase());
    }

    [Test]
    public void ModeMac_RejectsIso10126()
    {
        Action act = () => Mac(Key(16), "001122", "ISO-10126");
        act.Should().Throw<ArgumentException>()
            .WithMessage("AES-CBC-MAC does not support ISO-10126 padding.");
    }

    private static string Mac(string keyHex, string messageHex, string padding, int? macLength = null)
    {
        var parameter = new ParameterVariableDeclaration { Mechanism = "AES-CBC-MAC" };
        parameter.SetParameter("MECH", "AES-CBC-MAC");
        parameter.SetParameter("PAD", padding);
        if (macLength.HasValue)
            parameter.SetParameter("MACLEN", macLength.Value.ToString());

        var key = new KeyVariableDeclaration
        {
            Value = "0x(" + keyHex + ")",
            ValueFormat = FormatConversions.HEX
        };
        var data = new StringVariableDeclaration
        {
            Value = messageHex.Length == 0 ? string.Empty : "0x(" + messageHex + ")",
            ValueFormat = messageHex.Length == 0 ? FormatConversions.STR : FormatConversions.HEX
        };
        return new AES_CBC_MAC().ModeMac(parameter, key, data).Value;
    }

    private static string IndependentCbcMac(string keyHex, string messageHex)
        => IndependentCbcMac(keyHex, Convert.FromHexString(messageHex), PaddingMode.None);

    private static string IndependentPaddedCbcMac(string keyHex, string messageHex, string padding)
    {
        byte[] input = Convert.FromHexString(messageHex);
        PaddingMode frameworkPadding = PaddingMode.None;
        switch (padding)
        {
            case "PKCS-7":
                frameworkPadding = PaddingMode.PKCS7;
                break;
            case "ANSI-X923":
                frameworkPadding = PaddingMode.ANSIX923;
                break;
            case "ISO-7816":
            case "ISO-9797-M2":
                input = PadIso7816(input);
                break;
            case "ISO-9797-M1":
                input = PadIso9797M1(input);
                break;
            case "ISO-9797-M3":
                input = PadIso9797M3(input);
                break;
            case "TLS-CBC":
                input = PadTlsCbc(input);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(padding));
        }

        return IndependentCbcMac(keyHex, input, frameworkPadding);
    }

    private static string IndependentCbcMac(string keyHex, byte[] message, PaddingMode padding)
    {
        using Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = padding;
        aes.Key = Convert.FromHexString(keyHex);
        aes.IV = new byte[16];
        byte[] encrypted = aes.CreateEncryptor().TransformFinalBlock(message, 0, message.Length);
        return Convert.ToHexString(encrypted[^16..]);
    }

    private static byte[] PadIso7816(byte[] input)
    {
        int paddingLength = 16 - input.Length % 16;
        byte[] output = new byte[input.Length + paddingLength];
        Buffer.BlockCopy(input, 0, output, 0, input.Length);
        output[input.Length] = 0x80;
        return output;
    }

    private static byte[] PadIso9797M1(byte[] input)
    {
        int outputLength = input.Length == 0 ? 16 : (input.Length + 15) / 16 * 16;
        byte[] output = new byte[outputLength];
        Buffer.BlockCopy(input, 0, output, 0, input.Length);
        return output;
    }

    private static byte[] PadIso9797M3(byte[] input)
    {
        byte[] dataBlocks = PadIso9797M1(input);
        byte[] output = new byte[16 + dataBlocks.Length];
        ulong bitLength = (ulong)input.Length * 8;
        for (int index = 0; index < sizeof(ulong); index++)
            output[15 - index] = (byte)(bitLength >> (8 * index));
        Buffer.BlockCopy(dataBlocks, 0, output, 16, dataBlocks.Length);
        return output;
    }

    private static byte[] PadTlsCbc(byte[] input)
    {
        int paddingLength = 16 - input.Length % 16;
        byte[] output = new byte[input.Length + paddingLength];
        Buffer.BlockCopy(input, 0, output, 0, input.Length);
        Array.Fill(output, (byte)(paddingLength - 1), input.Length, paddingLength);
        return output;
    }

    private static string Key(int byteLength) => Convert.ToHexString(
        Enumerable.Range(0, byteLength).Select(value => (byte)value).ToArray());
}
