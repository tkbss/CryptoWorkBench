using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;
using System.Security.Cryptography;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoDes3RetailTests
    {
        private const string Key16 = "0123456789ABCDEFFEDCBA9876543210";
        private const string Key24 = "0123456789ABCDEFFEDCBA987654321089ABCDEF01234567";

        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [Test]
        public void DES3_RETAIL_MatchesAnsiX919PublishedVector()
        {
            const string message = "4E6F77206973207468652074696D6520666F7220616C6C20";
            Mac(Key16, message, "ISO-9797-M1").Should().Be("0x(A1C72E74EA3FA9B6)");
        }

        [Test]
        public void DES3_RETAIL_With24ByteKey_UsesK3ForFinalEncryption()
        {
            const string message = "00112233445566778899AABBCC";
            string actual = Mac(Key24, message, "ISO-9797-M2");
            actual.Should().Be(ReferenceMac(Key24, message, true));
            actual.Should().NotBe(ReferenceMac(Key24, message, false));
        }

        [TestCase("0011223344", "ISO-9797-M1")]
        [TestCase("0011223344556677", "ISO-9797-M1")]
        [TestCase("0011223344", "ISO-9797-M2")]
        [TestCase("0011223344556677", "ISO-9797-M2")]
        public void DES3_RETAIL_PaddingMatchesIndependentReference(string message, string padding)
        {
            Mac(Key16, message, padding).Should().Be(ReferenceMac(Key16, message, true, padding));
        }

        [Test]
        public void DES3_RETAIL_M2_AllowsEmptyMessage()
        {
            Mac(Key16, string.Empty, "ISO-9797-M2")
                .Should().Be(ReferenceMac(Key16, string.Empty, true, "ISO-9797-M2"));
        }

        [Test]
        public void DES3_RETAIL_M1_RejectsEmptyMessage()
        {
            Action act = () => Mac(Key16, string.Empty, "ISO-9797-M1");
            act.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("non-empty"));
        }

        [TestCase("0011223344556677")]
        [TestCase("00112233445566778899AABB")]
        [TestCase("00112233445566778899AABBCCDDEEFF00112233")]
        public void DES3_RETAIL_RejectsInvalidKeyLengths(string key)
        {
            Action act = () => Execute($"KEY k=GenerateKey(DES3-RETAIL,0x({key}))");
            act.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("16 or 24 bytes"));
        }

        [Test]
        public void DES3_RETAIL_RejectsUnsupportedPadding()
        {
            Action act = () => Execute("PARAM p=Parameters(#MECH:DES3-RETAIL,#PAD:PKCS-7)");
            act.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("ISO-9797-M1 or ISO-9797-M2"));
        }

        [Test]
        public void DES3_RETAIL_RejectsCustomIv()
        {
            Action act = () => Execute("PARAM p=Parameters(#MECH:DES3-RETAIL,#PAD:ISO-9797-M2,#IV:0x(0000000000000000))");
            act.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("does not use an IV"));
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void DES3_RETAIL_ReturnsRequestedLeftmostMacBytes(int length)
        {
            string full = Mac(Key16, "001122334455667788", "ISO-9797-M2");
            string truncated = Mac(Key16, "001122334455667788", "ISO-9797-M2", length);
            truncated.Should().Be("0x(" + full.Substring(3, length * 2) + ")");
        }

        [TestCase(3)]
        [TestCase(9)]
        public void DES3_RETAIL_RejectsInvalidMacLength(int length)
        {
            Action act = () => Execute($"PARAM p=Parameters(#MECH:DES3-RETAIL,#MACLEN:\"{length}\")");
            act.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("between 4 and 8 bytes"));
        }

        [Test]
        public void DES3_RETAIL_DefaultParametersUseM2EightBytesAndNoIv()
        {
            var program = Execute("PARAM p=Parameters(DES3-RETAIL)");
            var parameter = (ParameterVariableDeclaration)program.Statements[0];
            parameter.GetParameter("PAD").Should().Be("ISO-9797-M2");
            parameter.GetParameter("MACLEN").Should().Be("8");
            parameter.GetParameter("IV").Should().BeEmpty();
        }

        [Test]
        public void DES3_RETAIL_IsMacOnly()
        {
            const string declarations = $"KEY k=GenerateKey(DES3-RETAIL,0x({Key16})) PARAM p=Parameters(DES3-RETAIL) ";
            Action encrypt = () => Execute(declarations + "VAR result=Encrypt(p,k,0x(0011223344556677))");
            Action decrypt = () => Execute(declarations + "VAR result=Decrypt(p,k,0x(0011223344556677))");
            encrypt.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("only be used in MAC"));
            decrypt.Should().Throw<SemanticErrorException>().Where(e => e.SemanticError!.Message.Contains("only be used in MAC"));
        }

        [Test]
        public void AlgorithmFactory_RecognizesDes3Retail()
        {
            AlgorithmFactory.Create("DES3-RETAIL").Should().BeOfType<DES3>();
        }

        private static string Mac(string key, string message, string padding, int length = 8)
        {
            string data = message.Length == 0 ? "\"\"" : $"0x({message})";
            var program = Execute(
                $"KEY k=GenerateKey(DES3-RETAIL,0x({key})) " +
                $"PARAM p=Parameters(#MECH:DES3-RETAIL,#PAD:{padding},#MACLEN:\"{length}\") " +
                $"VAR mac=Mac(p,k,{data})");
            return ((StringVariableDeclaration)program.Statements[2]).Value.ToUpperInvariant().Replace("0X", "0x");
        }

        private static string ReferenceMac(string keyHex, string messageHex, bool useK3, string padding = "ISO-9797-M2")
        {
            byte[] key = Convert.FromHexString(keyHex);
            byte[] message = Convert.FromHexString(messageHex);
            int length = padding == "ISO-9797-M1"
                ? ((message.Length + 7) / 8) * 8
                : ((message.Length + 8) / 8) * 8;
            byte[] padded = new byte[length];
            message.CopyTo(padded, 0);
            if (padding == "ISO-9797-M2")
                padded[message.Length] = 0x80;

            byte[] state = new byte[8];
            using DES des = DES.Create();
            des.Mode = CipherMode.ECB;
            des.Padding = PaddingMode.None;
            for (int offset = 0; offset < padded.Length; offset += 8)
            {
                for (int i = 0; i < 8; i++)
                    state[i] ^= padded[offset + i];
                des.Key = key[..8];
                state = des.EncryptEcb(state, PaddingMode.None);
            }
            des.Key = key[8..16];
            state = des.DecryptEcb(state, PaddingMode.None);
            des.Key = useK3 && key.Length == 24 ? key[16..24] : key[..8];
            state = des.EncryptEcb(state, PaddingMode.None);
            return "0x(" + Convert.ToHexString(state) + ")";
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
