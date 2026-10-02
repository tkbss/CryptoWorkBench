using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest
{
    [NonParallelizable]
    public class HashDocumentationTests
    {
        private static readonly object[] Mechanisms =
        {
            new object[] { "HASH-SHA1", 20 },
            new object[] { "HASH-SHA224", 28 },
            new object[] { "HASH-SHA256", 32 },
            new object[] { "HASH-SHA384", 48 },
            new object[] { "HASH-SHA512", 64 },
            new object[] { "HASH-SHA512-224", 28 },
            new object[] { "HASH-SHA512-256", 32 },
            new object[] { "HASH-SHA3-224", 28 },
            new object[] { "HASH-SHA3-256", 32 },
            new object[] { "HASH-SHA3-384", 48 },
            new object[] { "HASH-SHA3-512", 64 }
        };

        [SetUp]
        public void Setup()
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
        }

        [TestCaseSource(nameof(Mechanisms))]
        public void Info_ResolvesDeployedDocumentationAndExamplesExecute(string mechanism, int digestLength)
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
            MechanismDocumentationContract.AssertRequiredSections(document);
            Assert.That(document, Does.Contain("GenerateKey is not supported"));

            string examples = MechanismDocumentationContract
                .ExtractCombinedExecutableExample(document);
            Assert.That(examples, Is.Not.Empty);
            Assert.That(examples, Does.Not.Contain("GenerateKey("));
            Execute(examples);

            var parameter = (ParameterVariableDeclaration)VariableDictionary.Instance().Get("p0");
            Assert.That(parameter.GetParameters(), Has.Count.EqualTo(1));
            Assert.That(parameter.GetParameter("MECH"), Is.EqualTo(mechanism));
            var digest = (StringVariableDeclaration)VariableDictionary.Instance().Get("digest");
            Assert.Multiple(() =>
            {
                Assert.That(digest.Type, Is.TypeOf<CryptoTypeVar>());
                Assert.That(digest.ValueFormat, Is.EqualTo(FormatConversions.HEX));
                Assert.That(digest.Value, Does.StartWith("0x(").And.EndWith(")"));
                Assert.That(FormatConversions.HexStringToByteArray(digest.Value), Has.Length.EqualTo(digestLength));
            });
        }

        [Test]
        public void CentralDocumentation_ContainsEveryHashMechanismAndHashFunction()
        {
            string mechanisms = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "InfoDocs", "Info.Mechanisms.md"));
            foreach (object[] entry in Mechanisms)
                Assert.That(mechanisms, Does.Contain($"- {entry[0]} :"));

            string functions = File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "InfoDocs", "Info.Functions.md"));
            Assert.That(functions, Does.Contain("Hash(parameters, data)"));
        }

        private static void Execute(string script)
        {
            var context = ParserBuilder.StringBuild(script).program();
            Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
            Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            new CryptoScriptRunner().Execute(context);
        }
    }
}
