using CryptoScript.CryptoAlgorithm;
using CryptoScript.CryptoAlgorithm.DES3;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoDes3CmacTests
    {
        private const string Key = "0123456789ABCDEFFEDCBA98765432100011223344556677";

        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [TestCase("\"\"", "2FE74386EC482D13")]
        [TestCase("0x(0011223344)", "2AA85B6D01226B00")]
        [TestCase("0x(0011223344556677)", "1760A50875016DCF")]
        [TestCase("0x(00112233445566778899AABBCCDDEEFF)", "B82ACE9CA9B7721F")]
        [TestCase("0x(00112233445566778899AABBCC)", "017C32BBE4A4A828")]
        public void DES3_CMAC_MatchesOpenSslReferenceValues(string message, string expectedMac)
        {
            var result = Execute(
                $"KEY k=GenerateKey(DES3-CMAC,0x({Key})) " +
                "PARAM p=Parameters(DES3-CMAC) " +
                $"VAR mac=Mac(p,k,{message})");

            var mac = result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
            mac.Value.Should().BeEquivalentTo($"0x({expectedMac})", options => options.IgnoringCase());
            FormatConversions.HexStringToByteArray(mac.Value).Should().HaveCount(8);
        }

        [Test]
        public void DES3_CMAC_IsDeterministicAndChangesWithMessage()
        {
            var result = Execute(
                $"KEY k=GenerateKey(DES3-CMAC,0x({Key})) " +
                "PARAM p=Parameters(DES3-CMAC) " +
                "VAR first=Mac(p,k,0x(0011223344556677)) " +
                "VAR second=Mac(p,k,0x(0011223344556677)) " +
                "VAR changed=Mac(p,k,0x(0011223344556678))");

            var first = (StringVariableDeclaration)result.Statements[2];
            var second = (StringVariableDeclaration)result.Statements[3];
            var changed = (StringVariableDeclaration)result.Statements[4];
            second.Value.Should().Be(first.Value);
            changed.Value.Should().NotBe(first.Value);
        }

        [TestCase(128, 16)]
        [TestCase(192, 24)]
        public void DES3_CMAC_GenerateKey_UsesExistingDes3KeyLengths(int keyBits, int expectedBytes)
        {
            var result = Execute($"KEY k=GenerateKey(DES3-CMAC,{keyBits})");

            var key = result.Statements[0].Should().BeOfType<KeyVariableDeclaration>().Subject;
            key.Mechanism.Should().Be("DES3-CMAC");
            FormatConversions.HexStringToByteArray(key.Value).Should().HaveCount(expectedBytes);
        }

        [Test]
        public void DES3_CMAC_DefaultParameters_HaveNoIvAndNoExternalPadding()
        {
            var result = Execute("PARAM p=Parameters(DES3-CMAC)");

            var parameter = result.Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;
            parameter.Mechanism.Should().Be("DES3-CMAC");
            parameter.GetParameter("IV").Should().BeEmpty();
            parameter.GetParameter("PAD").Should().Be("NONE");
        }

        [Test]
        public void DES3_CMAC_RejectsInvalidImportedKeyLength()
        {
            Action act = () => Execute("KEY k=GenerateKey(DES3-CMAC,0x(0011223344556677))");

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("16 or 24 bytes"));
        }

        [Test]
        public void AlgorithmFactory_RecognizesDes3Cmac()
        {
            AlgorithmFactory.Create("DES3-CMAC").Should().BeOfType<DES3>();
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
