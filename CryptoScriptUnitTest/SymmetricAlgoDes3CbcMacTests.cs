using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoDes3CbcMacTests
    {
        private const string Key16 = "0123456789ABCDEFFEDCBA9876543210";
        private const string Key24 = "0123456789ABCDEFFEDCBA98765432100011223344556677";

        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [Test]
        public void DES3_CBC_MAC_OneBlockWithoutPadding_MatchesKnownAnswer()
        {
            Mac(Key24, "4E6F772069732074", "NONE")
                .Should().BeEquivalentTo("0x(EECA43AEC1E4ED98)", options => options.IgnoringCase());
        }

        [Test]
        public void DES3_CBC_MAC_MultipleBlocksWithoutPadding_UsesLastCiphertextBlock()
        {
            const string message = "4E6F77206973207468652074696D6520666F7220616C6C20";
            Mac(Key24, message, "NONE").Should()
                .BeEquivalentTo("0x(1A9B83258830BC09)", options => options.IgnoringCase());
        }

        [TestCase("ISO-9797-M1", "00112233445566", "0x(6DF9E60DE45A60DB)")]
        [TestCase("ISO-9797-M1", "0011223344556677", "0x(51FB23DC603ADDD1)")]
        [TestCase("ISO-9797-M2", "", "0x(8667A2C7C9FA095A)")]
        [TestCase("ISO-9797-M2", "00112233445566", "0x(05D9292D20F1AB44)")]
        [TestCase("ISO-9797-M2", "0011223344556677", "0x(CEDDB5A6EC9B61D5)")]
        [TestCase("ISO-9797-M3", "", "0x(CBE6A76F9E351C6F)")]
        [TestCase("ISO-9797-M3", "00112233445566", "0x(22F1C499D6E0FB4B)")]
        [TestCase("ISO-9797-M3", "0011223344556677", "0x(D7865A42E85E2C3E)")]
        public void DES3_CBC_MAC_Iso9797Padding_MatchesFixedExpectedMac(
            string padding, string message, string expected)
        {
            Mac(Key24, message, padding).Should()
                .BeEquivalentTo(expected, options => options.IgnoringCase());
        }

        [TestCase(Key16, "0011223344556677", "NONE")]
        [TestCase(Key24, "00112233445566", "PKCS-7")]
        [TestCase(Key24, "0011223344556677", "PKCS-7")]
        [TestCase(Key24, "001122334455667788", "PKCS-7")]
        [TestCase(Key24, "", "PKCS-7")]
        [TestCase(Key24, "00112233445566", "ANSI-X923")]
        [TestCase(Key24, "0011223344556677", "ANSI-X923")]
        [TestCase(Key24, "", "ANSI-X923")]
        [TestCase(Key24, "00112233445566", "ISO-7816")]
        [TestCase(Key24, "0011223344556677", "ISO-7816")]
        [TestCase(Key24, "", "ISO-7816")]
        [TestCase(Key24, "00112233445566", "ISO-9797-M1")]
        [TestCase(Key24, "0011223344556677", "ISO-9797-M1")]
        [TestCase(Key24, "00112233445566", "ISO-9797-M2")]
        [TestCase(Key24, "0011223344556677", "ISO-9797-M2")]
        [TestCase(Key24, "", "ISO-9797-M2")]
        [TestCase(Key24, "00112233445566", "ISO-9797-M3")]
        [TestCase(Key24, "0011223344556677", "ISO-9797-M3")]
        [TestCase(Key24, "", "ISO-9797-M3")]
        public void DES3_CBC_MAC_SupportedDeterministicPadding_MatchesIndependentCbcEncryption(
            string key, string message, string padding)
        {
            Mac(key, message, padding).Should().BeEquivalentTo(
                ReferenceMac(key, message, ToPaddingMode(padding), padding),
                options => options.IgnoringCase());
        }

        [Test]
        public void DES3_CBC_MAC_TruncatesTheFullMacFromTheLeft()
        {
            Mac(Key24, "0011223344556677", "ISO-9797-M2")
                .Should().BeEquivalentTo("0x(CEDDB5A6EC9B61D5)", options => options.IgnoringCase());
            Mac(Key24, "0011223344556677", "ISO-9797-M2", 4)
                .Should().BeEquivalentTo("0x(CEDDB5A6)", options => options.IgnoringCase());
        }

        [TestCase(3)]
        [TestCase(9)]
        public void DES3_CBC_MAC_RejectsInvalidMacLength(int length)
        {
            Action act = () => Execute($"PARAM p=Parameters(#MECH:DES3-CBC,#PAD:PKCS-7,#MACLEN:\"{length}\")");
            act.Should().Throw<SemanticErrorException>()
                .Where(e => e.SemanticError!.Message.Contains("between 4 and 8 bytes"));
        }

        [Test]
        public void DES3_CBC_MAC_NoneRejectsNonAlignedMessage()
        {
            Action act = () => Mac(Key24, "00112233445566", "NONE");
            act.Should().Throw<SemanticErrorException>()
                .Where(e => e.SemanticError!.Message.Contains("multiple of 8 bytes"));
        }

        [TestCase("NONE")]
        [TestCase("ISO-9797-M1")]
        public void DES3_CBC_MAC_EmptyInputWithoutGeneratedBlock_IsRejected(string padding)
        {
            Action act = () => Mac(Key24, string.Empty, padding);
            act.Should().Throw<SemanticErrorException>()
                .Where(e => e.SemanticError!.Message.Contains("at least one 8-byte block"));
        }

        [Test]
        public void DES3_CBC_MAC_AlwaysUsesZeroIv()
        {
            Mac(Key24, "0011223344556677", "NONE", iv: "FFFFFFFFFFFFFFFF").Should()
                .BeEquivalentTo("0x(51FB23DC603ADDD1)", options => options.IgnoringCase());
        }

        private static string Mac(string key, string message, string padding, int? length = null, string? iv = null)
        {
            string macLength = length.HasValue ? $",#MACLEN:\"{length.Value}\"" : string.Empty;
            string parameterIv = iv is null ? string.Empty : $",#IV:0x({iv})";
            string input =
                $"KEY k=GenerateKey(DES3-CBC,0x({key})) " +
                $"PARAM p=Parameters(#MECH:DES3-CBC,#PAD:{padding}{parameterIv}{macLength}) " +
                $"VAR m=Mac(p,k,{(message.Length == 0 ? "\"\"" : $"0x({message})")})";
            return Execute(input).Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value;
        }

        private static string ReferenceMac(string keyHex, string messageHex, PaddingMode padding, string? customPadding = null)
        {
            byte[] input = Convert.FromHexString(messageHex);
            if (customPadding is "ISO-7816" or "ISO-9797-M2")
                input = PadIso7816(input);
            else if (customPadding == "ISO-9797-M3")
                input = PadIso9797M3(input);

            using TripleDES des3 = TripleDES.Create();
            des3.Mode = CipherMode.CBC;
            des3.Padding = padding;
            des3.Key = Convert.FromHexString(keyHex);
            des3.IV = new byte[8];
            byte[] ciphertext = des3.CreateEncryptor().TransformFinalBlock(input, 0, input.Length);
            return "0x(" + Convert.ToHexString(ciphertext[^8..]) + ")";
        }

        private static PaddingMode ToPaddingMode(string padding) => padding switch
        {
            "PKCS-7" => PaddingMode.PKCS7,
            "ANSI-X923" => PaddingMode.ANSIX923,
            "ISO-9797-M1" => PaddingMode.Zeros,
            _ => PaddingMode.None
        };

        private static byte[] PadIso7816(byte[] input)
        {
            byte[] output = new byte[((input.Length + 1 + 7) / 8) * 8];
            Buffer.BlockCopy(input, 0, output, 0, input.Length);
            output[input.Length] = 0x80;
            return output;
        }

        private static byte[] PadIso9797M3(byte[] input)
        {
            byte[] output = new byte[((input.Length + 8 + 7) / 8) * 8];
            Buffer.BlockCopy(input, 0, output, 0, input.Length);
            ulong bitLength = (ulong)input.Length * 8;
            for (int i = 0; i < 8; i++)
                output[output.Length - 1 - i] = (byte)(bitLength >> (8 * i));
            return output;
        }

        private static CryptoScript.Model.CryptoScriptProgram Execute(string input)
        {
            var parser = ParserBuilder.StringBuild(input);
            var context = parser.program();
            SyntaxErrorListner.SyntaxErrorOccured.Should().BeFalse();
            LexerErrorListener.LexerErrorOccured.Should().BeFalse();
            return new AntlrToProgram().Visit(context);
        }
    }
}
