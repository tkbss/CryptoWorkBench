using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    [NonParallelizable]
    public class Des3DocumentationTests
    {
        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [TestCase("DES3-CBC")]
        [TestCase("DES3-ECB")]
        [TestCase("DES3-CMAC")]
        [TestCase("DES3-RETAIL")]
        public void Info_ResolvesDeployedDocumentationAndExamplesExecute(string mechanism)
        {
            string? displayed = null;
            void Capture(string text) => displayed = text;
            OutputOperations.InfoEvent += Capture;
            try
            {
                Execute($"Info({mechanism})");
            }
            finally
            {
                OutputOperations.InfoEvent -= Capture;
            }

            string document = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "InfoDocs", $"Info.Mech.{mechanism}.md"));
            Assert.That(displayed, Is.EqualTo(document));
            Assert.That(document, Does.StartWith($"# MECHANISM {mechanism}"));

            string template = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "InfoDocs", "Info.Mech.AES-CBC.md"));
            Assert.That(Sections(document), Is.EqualTo(Sections(template)));

            string examples = string.Join(Environment.NewLine, document.Split('\n')
                .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ") || line.StartsWith("VAR ")));
            Assert.That(examples, Is.Not.Empty);
            Execute(examples);

            if (mechanism is "DES3-CBC" or "DES3-ECB")
            {
                var decrypted = (StringVariableDeclaration)VariableDictionary.Instance().Get("decrypted");
                var cleartext = (StringVariableDeclaration)VariableDictionary.Instance().Get("cleartext");
                Assert.That(FormatConversions.ToByteArray(decrypted.Value, decrypted.ValueFormat),
                    Is.EqualTo(FormatConversions.ToByteArray(cleartext.Value, cleartext.ValueFormat)));
            }
            else
            {
                var mac = (StringVariableDeclaration)VariableDictionary.Instance().Get("mac");
                Assert.That(FormatConversions.ToByteArray(mac.Value, mac.ValueFormat), Has.Length.EqualTo(8));
            }

            if (mechanism is "DES3-CBC" or "DES3-RETAIL")
            {
                Execute($"PARAM fullparams=Parameters(#MECH:{mechanism},#PAD:ISO-9797-M2) " +
                    "VAR fullmac=Mac(fullparams,k1,0x(0011223344556677))");
                Assert.That(Bytes("fullmac"), Has.Length.EqualTo(8));
                Assert.That(Bytes("macshort"), Is.EqualTo(Bytes("fullmac")[..4]));
            }
            if (mechanism == "DES3-CMAC")
            {
                Assert.That(Bytes("emptymac"), Has.Length.EqualTo(8));
                Assert.That(Bytes("emptymac"), Is.Not.EqualTo(Bytes("mac")));
            }
        }

        [TestCase("AES-CBC", "Info.Mech.AES-CBC.md")]
        [TestCase("functions", "Info.Functions.md")]
        [TestCase("mechanisms", "Info.Mechanisms.md")]
        [TestCase("types", "Info.Types.md")]
        [TestCase("parameters", "Info.Parameters.md")]
        [TestCase("keymap", "Info.Keymap.md")]
        [TestCase("paddings", "Info.Paddings.md")]
        public void ExistingInfo_ReturnsUnchangedDocumentation(string topic, string filename)
        {
            string? displayed = null;
            void Capture(string text) => displayed = text;
            OutputOperations.InfoEvent += Capture;
            try
            {
                Execute($"Info({topic})");
            }
            finally
            {
                OutputOperations.InfoEvent -= Capture;
            }
            Assert.That(displayed, Is.EqualTo(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "InfoDocs", filename))));
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
