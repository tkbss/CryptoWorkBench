using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using FluentAssertions;

namespace CryptoScriptUnitTest
{
    public class SymmetricAlgoDes3CbcTests
    {
        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [Test]
        public void DES3_CBC_KnownAnswer_EncryptsExpectedCiphertext()
        {
            const string input =
                "KEY k=GenerateKey(DES3-CBC,0x(0123456789ABCDEFFEDCBA98765432100011223344556677)) " +
                "PARAM p=Parameters(#MECH:DES3-CBC,#IV:0x(1234567890ABCDEF),#PAD:NONE) " +
                "VAR c=Encrypt(p,k,0x(4E6F77206973207468652074696D6520666F7220616C6C20))";

            var result = Execute(input);

            var ciphertext = result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
            ciphertext.Value.Should().BeEquivalentTo(
                "0x(0B1A2D632A9CB6288AF4F7A1E3CF596E34C0D58362DEAEFE)",
                options => options.IgnoringCase());
        }

        [TestCase(128, 16)]
        [TestCase(192, 24)]
        public void DES3_CBC_GenerateKey_UsesRequestedRepresentation(int keyBits, int expectedBytes)
        {
            var result = Execute($"KEY k=GenerateKey(DES3-CBC,{keyBits})");

            var key = result.Statements[0].Should().BeOfType<KeyVariableDeclaration>().Subject;
            key.Mechanism.Should().Be("DES3-CBC");
            key.KeySize.Should().Be(keyBits.ToString());
            FormatConversions.HexStringToByteArray(key.Value).Should().HaveCount(expectedBytes);
        }

        [TestCase(64)]
        [TestCase(112)]
        [TestCase(168)]
        [TestCase(256)]
        public void DES3_CBC_GenerateKey_RejectsUnsupportedSizes(int keyBits)
        {
            Action act = () => Execute($"KEY k=GenerateKey(DES3-CBC,{keyBits})");

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("128 or 192"));
        }

        [TestCase("0011223344556677")]
        [TestCase("00112233445566778899AABBCCDDEE")]
        [TestCase("00112233445566778899AABBCCDDEEFF00")]
        public void DES3_CBC_ImportKey_RejectsLengthsOtherThan16Or24Bytes(string keyHex)
        {
            Action act = () => Execute($"KEY k=GenerateKey(DES3-CBC,0x({keyHex}))");

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("16 or 24 bytes"));
        }

        [Test]
        public void DES3_CBC_DefaultParameters_UseEightByteIvAndPkcs7()
        {
            var result = Execute("PARAM p=Parameters(DES3-CBC)");

            var parameter = result.Statements[0].Should().BeOfType<ParameterVariableDeclaration>().Subject;
            parameter.Mechanism.Should().Be("DES3-CBC");
            parameter.GetParameter("PAD").Should().Be("PKCS-7");
            FormatConversions.HexStringToByteArray(parameter.GetParameter("IV")).Should().HaveCount(8);
        }

        [Test]
        public void DES3_CBC_TwoKey_Pkcs7RoundtripReturnsPlaintext()
        {
            const string plaintext = "0x(00112233445566778899AABBCC)";
            const string input =
                "KEY k=GenerateKey(DES3-CBC,0x(0123456789ABCDEFFEDCBA9876543210)) " +
                "PARAM p=Parameters(#MECH:DES3-CBC,#IV:0x(1234567890ABCDEF),#PAD:PKCS-7) " +
                $"VAR c=Encrypt(p,k,{plaintext}) VAR clear=Decrypt(p,k,c)";

            var result = Execute(input);

            var ciphertext = result.Statements[2].Should().BeOfType<StringVariableDeclaration>().Subject;
            FormatConversions.HexStringToByteArray(ciphertext.Value).Should().HaveCount(16);
            result.Statements[3].Should().BeOfType<StringVariableDeclaration>().Subject.Value
                .Should().BeEquivalentTo(plaintext, options => options.IgnoringCase());
        }

        [Test]
        public void DES3_CBC_RejectsIvThatIsNotEightBytes()
        {
            const string input =
                "KEY k=GenerateKey(DES3-CBC,0x(0123456789ABCDEFFEDCBA9876543210)) " +
                "PARAM p=Parameters(#MECH:DES3-CBC,#IV:0x(1234567890ABCD),#PAD:PKCS-7) " +
                "VAR c=Encrypt(p,k,0x(001122))";

            Action act = () => Execute(input);

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("exactly 8 bytes"));
        }

        [Test]
        public void DES3_CBC_NoPadding_RejectsNonAlignedPlaintext()
        {
            const string input =
                "KEY k=GenerateKey(DES3-CBC,0x(0123456789ABCDEFFEDCBA9876543210)) " +
                "PARAM p=Parameters(#MECH:DES3-CBC,#IV:0x(1234567890ABCDEF),#PAD:NONE) " +
                "VAR c=Encrypt(p,k,0x(00112233445566))";

            Action act = () => Execute(input);

            act.Should().Throw<SemanticErrorException>()
                .Where(exception => exception.SemanticError!.Message.Contains("multiple of 8 bytes"));
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
