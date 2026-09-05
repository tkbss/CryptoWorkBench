using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    [NonParallelizable]
    public class AesDocumentationTests
    {
        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [TestCase("AES-ECB")]
        [TestCase("AES-CTR")]
        [TestCase("AES-CMAC")]
        [TestCase("AES-GMAC")]
        [TestCase("AES-GCM")]
        [TestCase("AES-CCM")]
        [TestCase("WRAP-AES-TR31")]
        public void InfoAndExamples_MatchDocumentedBehavior(string mechanism)
        {
            string? displayed = null;
            void Capture(string text) => displayed = text;
            OutputOperations.InfoEvent += Capture;
            try { Execute($"Info({mechanism})"); }
            finally { OutputOperations.InfoEvent -= Capture; }
            string document = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "InfoDocs", $"Info.Mech.{mechanism}.md"));
            Assert.That(displayed, Is.EqualTo(document));
            string template = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "InfoDocs", "Info.Mech.AES-CBC.md"));
            Assert.That(Sections(document), Is.EqualTo(Sections(template)));
            string examples = string.Join(Environment.NewLine, document.Split('\n')
                .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ") || line.StartsWith("VAR ")));
            Assert.That(examples, Is.Not.Empty);
            Execute(examples);

            if (mechanism == "WRAP-AES-TR31")
            {
                var original = (KeyVariableDeclaration)VariableDictionary.Instance().Get("keyToWrap");
                var recovered = (KeyVariableDeclaration)VariableDictionary.Instance().Get("recovered");
                Assert.That(recovered.KeyValue, Is.EqualTo(original.KeyValue).IgnoreCase);
                Assert.That(recovered.KeySize, Is.EqualTo(original.KeySize));
                return;
            }
            if (mechanism is "AES-CMAC" or "AES-GMAC")
            {
                Assert.That(Bytes("mac"), Has.Length.EqualTo(16));
                Assert.That(Bytes("emptymac"), Has.Length.EqualTo(16));
                Assert.That(Bytes("emptymac"), Is.Not.EqualTo(Bytes("mac")));
            }
            else
            {
                int overhead = mechanism is "AES-GCM" or "AES-CCM" ? 16 : 0;
                Assert.That(Bytes("decrypted"), Is.EqualTo(Bytes("cleartext")));
                Assert.That(Bytes("ciphertext"), Has.Length.EqualTo(Bytes("cleartext").Length + overhead));
                Assert.That(Bytes("emptycipher"), Has.Length.EqualTo(overhead));
                Assert.That(Bytes("emptyplain"), Is.Empty);
            }
            if (mechanism == "AES-ECB")
                Assert.That(Param("p0").GetParameter("PAD"), Is.EqualTo("NONE"));
            if (mechanism == "AES-CTR")
                Assert.That(Param("p0").GetParameter("COUNTER"), Is.EqualTo("0x(00000000)"));
        }

        [Test]
        public void Ecb_OverridesPaddingAndRejectsUnalignedInput()
        {
            Execute("KEY k=GenerateKey(AES-ECB,128) " +
                "PARAM p=Parameters(#MECH:AES-ECB,#PAD:PKCS-7,#IV:0x(0011))");
            Assert.That(Param("p").GetParameter("PAD"), Is.EqualTo("NONE"));
            Assert.That(Param("p").GetParameter("IV"), Is.Empty);
            Assert.Throws<SemanticErrorException>(() => Execute("VAR c=Encrypt(p,k,0x(001122))"));
        }

        [TestCase("AES-GCM")]
        [TestCase("AES-CCM")]
        public void Aead_DefaultsAndAuthenticationMatchDocumentation(string mechanism)
        {
            string mode = mechanism[4..];
            Execute($"KEY k=GenerateKey({mechanism},128) PARAM defaults=Parameters({mechanism}) " +
                $"PARAM p=Parameters(#MECH:{mechanism},#NONCE:0x(00112233445566778899AABB),#ADATA:\"original\") " +
                "VAR c=Encrypt(p,k,0x(001122))");
            Assert.That(ParameterBytes("defaults", "NONCE"), Has.Length.EqualTo(12));
            Assert.That(Param("defaults").GetParameter("ADATA"), Is.EqualTo($"\"DEFAULT_{mode}_AUTHENTICATION_DATA\""));
            var result = (StringVariableDeclaration)VariableDictionary.Instance().Get("c");
            Assert.That(FormatConversions.HexStringToByteArray(result.GMAC), Is.EqualTo(Bytes("c")[^16..]));
            Execute($"PARAM changed=Parameters(#MECH:{mechanism},#NONCE:0x(00112233445566778899AABB),#ADATA:\"changed\")");
            Assert.Throws<SemanticErrorException>(() => Execute("VAR bad=Decrypt(changed,k,c)"));
            byte[] tampered = Bytes("c");
            tampered[^1] ^= 1;
            Assert.Throws<SemanticErrorException>(() => Execute($"VAR badtag=Decrypt(p,k,0x({Convert.ToHexString(tampered)}))"));
        }

        [TestCase("AES-GCM", true)]
        [TestCase("AES-CCM", false)]
        [TestCase("AES-GMAC", false)]
        public void ExplicitParameters_HaveDocumentedNonceDefaults(string mechanism, bool generated)
        {
            Execute($"PARAM p=Parameters(#MECH:{mechanism},#PAD:NONE)");
            Assert.That(ParameterBytes("p", "NONCE"), Has.Length.EqualTo(generated ? 12 : 0));
            Assert.That(Param("p").GetParameter("ADATA"), Is.Empty);
        }

        [TestCase("AES-CMAC")]
        [TestCase("AES-GMAC")]
        public void MacLengthAndExternalPadding_DoNotChangeMac(string mechanism)
        {
            Execute($"KEY k=GenerateKey({mechanism},128) " +
                $"PARAM p=Parameters(#MECH:{mechanism},#NONCE:0x(00112233445566778899AABB)) " +
                $"PARAM alternate=Parameters(#MECH:{mechanism},#NONCE:0x(00112233445566778899AABB),#PAD:PKCS-7,#MACLEN:\"4\",#ADATA:\"ignored\") " +
                "VAR first=Mac(p,k,0x(001122)) VAR second=Mac(alternate,k,0x(001122))");
            Assert.That(Bytes("first"), Has.Length.EqualTo(16));
            Assert.That(Bytes("second"), Is.EqualTo(Bytes("first")));
            Assert.Throws<SemanticErrorException>(() => Execute("VAR bad=Encrypt(p,k,0x(001122))"));
        }

        [Test]
        public void Ctr_CounterWrapsAndShortNonceFailsForNonemptyInput()
        {
            Execute("KEY k=GenerateKey(AES-CTR,128) " +
                "PARAM p=Parameters(#MECH:AES-CTR,#NONCE:0x(00112233445566778899AABB),#COUNTER:0x(FFFFFFFF)) " +
                "PARAM zero=Parameters(#MECH:AES-CTR,#NONCE:0x(00112233445566778899AABB),#COUNTER:0x(00000000)) " +
                "VAR c=Encrypt(p,k,0x(0000000000000000000000000000000000000000000000000000000000000000)) " +
                "VAR z=Encrypt(zero,k,0x(00000000000000000000000000000000)) " +
                "PARAM shortnonce=Parameters(#MECH:AES-CTR,#NONCE:0x(0011))");
            Assert.That(Bytes("c")[16..], Is.EqualTo(Bytes("z")));
            Assert.That(Param("p").GetParameter("COUNTER"), Is.EqualTo("0x(FFFFFFFF)"));
            Assert.Throws<SemanticErrorException>(() => Execute("VAR bad=Encrypt(shortnonce,k,0x(00))"));
        }

        [TestCase(6, false)]
        [TestCase(7, true)]
        [TestCase(13, true)]
        [TestCase(14, false)]
        public void Ccm_ValidatesNonceLength(int length, bool valid)
        {
            Execute("KEY k=GenerateKey(AES-CCM,128) " +
                $"PARAM p=Parameters(#MECH:AES-CCM,#NONCE:0x({new string('1', length * 2)}))");
            if (valid)
            {
                Execute("VAR c=Encrypt(p,k,\"\")");
                Assert.That(Bytes("c"), Has.Length.EqualTo(16));
            }
            else
                Assert.Throws<SemanticErrorException>(() => Execute("VAR c=Encrypt(p,k,\"\")"));
        }

        [Test]
        public void Ccm_ThirteenByteNonceLimitsPlaintextLength()
        {
            Execute("KEY k=GenerateKey(AES-CCM,128) " +
                "PARAM p=Parameters(#MECH:AES-CCM,#NONCE:0x(00112233445566778899AABBCC))");
            Execute($"VAR c=Encrypt(p,k,\"{new string('a', 65535)}\")");
            Assert.That(Bytes("c"), Has.Length.EqualTo(65535 + 16));
            Assert.Throws<SemanticErrorException>(() =>
                Execute($"VAR bad=Encrypt(p,k,\"{new string('a', 65536)}\")"));
        }

        private static ParameterVariableDeclaration Param(string name) =>
            (ParameterVariableDeclaration)VariableDictionary.Instance().Get(name);
        private static byte[] ParameterBytes(string name, string field)
        {
            string value = Param(name).GetParameter(field);
            return FormatConversions.ToByteArray(value, FormatConversions.ParseString(value));
        }
        private static byte[] Bytes(string name)
        {
            var value = (StringVariableDeclaration)VariableDictionary.Instance().Get(name);
            return FormatConversions.ToByteArray(value.Value, value.ValueFormat);
        }
        private static string[] Sections(string document) => document.Split('\n')
            .Where(line => line.StartsWith("## ")).Select(line => line.Trim()).ToArray();
        private static void Execute(string script)
        {
            var context = ParserBuilder.StringBuild(script).program();
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            new AntlrToProgram().Visit(context);
        }
    }
}
