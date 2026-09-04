using CryptoScript.CryptoAlgorithm;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoDes3EcbTests
    {
        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [Test]
        public void DES3_ECB_KnownAnswer_EncryptsExpectedCiphertext()
        {
            const string input =
                "KEY k=GenerateKey(DES3-ECB,0x(0123456789ABCDEFFEDCBA98765432100011223344556677)) " +
                "PARAM p=Parameters(#MECH:DES3-ECB,#PAD:NONE) " +
                "VAR c=Encrypt(p,k,0x(4E6F772069732074))";

            var result = Execute(input);

            result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject.Value
                .Should().BeEquivalentTo("0x(EECA43AEC1E4ED98)", options => options.IgnoringCase());
        }

        [TestCase(128, 16)]
        [TestCase(192, 24)]
        public void DES3_ECB_GenerateKey_UsesRequestedRepresentation(int keyBits, int expectedBytes)
        {
            var result = Execute($"KEY k=GenerateKey(DES3-ECB,{keyBits})");

            var key = result.Statements[0].Should().BeOfType<KeyVariableDeclaration>().Subject;
            key.Mechanism.Should().Be("DES3-ECB");
            key.KeySize.Should().Be(keyBits.ToString());
            FormatConversions.HexStringToByteArray(key.Value).Should().HaveCount(expectedBytes);
        }

        [TestCase(64)]
        [TestCase(112)]
        [TestCase(168)]
        [TestCase(256)]
        public void DES3_ECB_GenerateKey_RejectsUnsupportedSizes(int keyBits)
        {
            Action act = () => Execute($"KEY k=GenerateKey(DES3-ECB,{keyBits})");

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("128 or 192"));
        }

        [Test]
        public void DES3_ECB_DefaultParameters_UsePkcs7AndNoIv()
        {
            var result = Execute("PARAM p=Parameters(DES3-ECB)");

            var parameter = result.Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;
            parameter.Mechanism.Should().Be("DES3-ECB");
            parameter.GetParameter("PAD").Should().Be("PKCS-7");
            parameter.GetParameter("IV").Should().BeEmpty();
        }

        [TestCase("PKCS-7")]
        [TestCase("ANSI-X923")]
        [TestCase("ISO-7816")]
        [TestCase("ISO-9797-M2")]
        [TestCase("ISO-9797-M3")]
        [TestCase("TLS-CBC")]
        public void DES3_ECB_SupportedPadding_RoundtripReturnsPlaintext(string padding)
        {
            const string plaintext = "0x(00112233445566778899AABBCC)";
            string input =
                "KEY k=GenerateKey(DES3-ECB,0x(0123456789ABCDEFFEDCBA9876543210)) " +
                $"PARAM p=Parameters(#MECH:DES3-ECB,#PAD:{padding}) " +
                $"VAR c=Encrypt(p,k,{plaintext}) VAR clear=Decrypt(p,k,c)";

            var result = Execute(input);

            result.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject.Value
                .Should().BeEquivalentTo(plaintext, options => options.IgnoringCase());
        }

        [Test]
        public void DES3_ECB_RejectsIvParameter()
        {
            Action act = () => Execute(
                "PARAM p=Parameters(#MECH:DES3-ECB,#IV:0x(1234567890ABCDEF),#PAD:PKCS-7)");

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("does not use an IV"));
        }

        [Test]
        public void DES3_ECB_RejectsIvFromDirectParameterDeclaration()
        {
            const string input =
                "KEY k=GenerateKey(DES3-ECB,0x(0123456789ABCDEFFEDCBA9876543210)) " +
                "PARAM p= #MECH:DES3-ECB #IV:0x(1234567890ABCDEF) #PAD:PKCS-7 " +
                "VAR c=Encrypt(p,k,0x(001122))";

            Action act = () => Execute(input);

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("does not use an IV"));
        }

        [Test]
        public void DES3_ECB_NoPadding_RejectsNonAlignedPlaintext()
        {
            const string input =
                "KEY k=GenerateKey(DES3-ECB,0x(0123456789ABCDEFFEDCBA9876543210)) " +
                "PARAM p=Parameters(#MECH:DES3-ECB,#PAD:NONE) " +
                "VAR c=Encrypt(p,k,0x(00112233445566))";

            Action act = () => Execute(input);

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("multiple of 8 bytes"));
        }

        [Test]
        public void AlgorithmFactory_RecognizesDes3EcbWithoutChangingDes3Cbc()
        {
            AlgorithmFactory.Create("DES3-ECB").Should().BeOfType<CryptoScript.CryptoAlgorithm.DES3.DES3>();
            AlgorithmFactory.Create("DES3-CBC").Should().BeOfType<CryptoScript.CryptoAlgorithm.DES3.DES3>();
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
