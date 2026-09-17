using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    [NonParallelizable]
    public class HmacDocumentationTests
    {
        private static readonly object[] Mechanisms =
        {
            new object[] { "HMAC-SHA1", 20 },
            new object[] { "HMAC-SHA224", 28 },
            new object[] { "HMAC-SHA256", 32 },
            new object[] { "HMAC-SHA384", 48 },
            new object[] { "HMAC-SHA512", 64 },
            new object[] { "HMAC-SHA512-224", 28 },
            new object[] { "HMAC-SHA512-256", 32 },
            new object[] { "HMAC-SHA3-224", 28 },
            new object[] { "HMAC-SHA3-256", 32 },
            new object[] { "HMAC-SHA3-384", 48 },
            new object[] { "HMAC-SHA3-512", 64 }
        };

        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [TestCaseSource(nameof(Mechanisms))]
        public void Info_ResolvesDeployedDocumentationAndExamplesExecute(string mechanism, int tagLength)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "InfoDocs", $"Info.Mech.{mechanism}.md");
            Assert.That(File.Exists(path), Is.True, $"Missing deployed documentation for {mechanism}");
            string document = File.ReadAllText(path);

            string? displayed = null;
            void Capture(string text) => displayed = text;
            OutputOperations.InfoEvent += Capture;
            try { Execute($"Info({mechanism})"); }
            finally { OutputOperations.InfoEvent -= Capture; }

            Assert.That(displayed, Is.EqualTo(document));
            Assert.That(document, Does.StartWith($"# MECHANISM {mechanism}"));
            string template = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "InfoDocs", "Info.Mech.AES-CBC.md"));
            Assert.That(Sections(document), Is.EqualTo(Sections(template)));

            string examples = string.Join(Environment.NewLine, document.Split('\n')
                .Where(line => line.StartsWith("KEY ") || line.StartsWith("PARAM ") || line.StartsWith("VAR ")));
            Assert.That(examples, Is.Not.Empty);
            Execute(examples);

            var parameter = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p0");
            Assert.That(parameter.GetParameters(), Has.Count.EqualTo(1));
            Assert.That(parameter.GetParameter("MECH"), Is.EqualTo(mechanism));
            var mac = (StringVariableDeclaration)VariableDictionary.Instance().Get("mac");
            Assert.Multiple(() =>
            {
                Assert.That(mac.Type, Is.TypeOf<CryptoTypeVar>());
                Assert.That(mac.ValueFormat, Is.EqualTo(FormatConversions.HEX));
                Assert.That(mac.Value, Does.StartWith("0x(").And.EndWith(")"));
                Assert.That(FormatConversions.HexStringToByteArray(mac.Value), Has.Length.EqualTo(tagLength));
            });
        }

        [Test]
        public void MechanismList_ContainsEveryDocumentedHmacMechanism()
        {
            string document = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "InfoDocs", "Info.Mechanisms.md"));

            foreach (object[] entry in Mechanisms)
                Assert.That(document, Does.Contain($"- {entry[0]} :"));
        }

        private static string[] Sections(string document) => document.Split('\n')
            .Where(line => line.StartsWith("## ")).Select(line => line.Trim()).ToArray();

        private static void Execute(string script)
        {
            var context = ParserBuilder.StringBuild(script).program();
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            new CryptoScriptRunner().Execute(context);
        }
    }
}
