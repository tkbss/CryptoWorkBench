using CryptoScript.ErrorListner;
using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class MechanismDocumentationContractTests
{
    [SetUp]
    public void SetUp()
    {
        VariableDictionary.Instance().Clear();
        SyntaxErrorListner.SyntaxErrorOccured = false;
        LexerErrorListener.LexerErrorOccured = false;
    }

    [TearDown]
    public void TearDown() => VariableDictionary.Instance().Clear();

    [Test]
    public void ExtractExecutableExamples_PreservesMixedExamplesInDocumentOrder()
    {
        const string document = """
            # MECHANISM TEST

            KEY legacyBefore = GenerateKey(AES-CBC, 128)

            ```text
            KEY ignored = not CryptoScript
            ```

            ```cryptoscript
            KEY fenced = GenerateKey(
                AES-CBC,
                128)
            ```

            KEY legacyAfter = GenerateKey(AES-CBC, 256)
            """;

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);

        Assert.Multiple(() =>
        {
            Assert.That(examples, Is.EqualTo(new[]
            {
                "KEY legacyBefore = GenerateKey(AES-CBC, 128)",
                string.Join(Environment.NewLine, new[]
                {
                    "KEY fenced = GenerateKey(",
                    "    AES-CBC,",
                    "    128)"
                }),
                "KEY legacyAfter = GenerateKey(AES-CBC, 256)"
            }));
            string combined = string.Join(Environment.NewLine, examples);
            Assert.That(combined, Does.Not.Contain("ignored"));
            Assert.That(CountOccurrences(combined, "legacyBefore"), Is.EqualTo(1));
            Assert.That(CountOccurrences(combined, "fenced"), Is.EqualTo(1));
            Assert.That(CountOccurrences(combined, "legacyAfter"), Is.EqualTo(1));
        });
    }

    [Test]
    public void ExtractExecutableExamples_ReturnsIndependentBlocksForBothLanguageNames()
    {
        const string document = """
            ```cryptoscript
            VAR first = 0x(0011)
            ```

            ```crypto-script
            VAR second = 0x(2233)
            ```
            """;

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);

        Assert.That(examples, Is.EqualTo(new[]
        {
            "VAR first = 0x(0011)",
            "VAR second = 0x(2233)"
        }));

        foreach (string example in examples)
        {
            VariableDictionary.Instance().Clear();
            SyntaxErrorListner.SyntaxErrorOccured = false;
            LexerErrorListener.LexerErrorOccured = false;
            var context = ParserBuilder.StringBuild(example).program();
            Assert.Multiple(() =>
            {
                Assert.That(SyntaxErrorListner.SyntaxErrorOccured, Is.False);
                Assert.That(LexerErrorListener.LexerErrorOccured, Is.False);
            });
            Assert.DoesNotThrow(() => new CryptoScriptRunner().Execute(context));
        }
    }

    [Test]
    public void ExtractExecutableExamples_PreservesLegacyDocumentationUntilItIsMigrated()
    {
        const string document = """
            ## Example Usage
            ### Existing Example
            KEY key = GenerateKey(AES-CBC, 128)
            explanatory prose
            PARAM parameters = Parameters(AES-CBC)
            VAR result = Encrypt(parameters, key, 0x(00112233445566778899AABBCCDDEEFF))
            """;

        IReadOnlyList<string> examples =
            MechanismDocumentationContract.ExtractExecutableExamples(document);

        Assert.That(examples, Is.EqualTo(new[]
        {
            string.Join(Environment.NewLine, new[]
            {
                "KEY key = GenerateKey(AES-CBC, 128)",
                "PARAM parameters = Parameters(AES-CBC)",
                "VAR result = Encrypt(parameters, key, 0x(00112233445566778899AABBCCDDEEFF))"
            })
        }));
    }

    [Test]
    public void ExtractExecutableExamples_RejectsUnclosedCryptoScriptFence()
    {
        const string document = """
            ```cryptoscript
            KEY key = GenerateKey(AES-CBC, 128)
            """;

        Assert.Throws<InvalidOperationException>(() =>
            MechanismDocumentationContract.ExtractExecutableExamples(document));
    }

    [Test]
    public void ExtractExecutableExamples_ProducesTheSameResultForLfAndCrLf()
    {
        const string lfDocument =
            "KEY legacy = GenerateKey(AES-CBC, 128)\n" +
            "```cryptoscript\n" +
            "VAR value = 0x(0011)\n" +
            "```\n" +
            "PARAM parameters = Parameters(AES-CBC)\n";
        string crlfDocument = lfDocument.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.That(
            MechanismDocumentationContract.ExtractExecutableExamples(crlfDocument),
            Is.EqualTo(MechanismDocumentationContract.ExtractExecutableExamples(lfDocument)));
    }

    private static int CountOccurrences(string value, string search) =>
        (value.Length - value.Replace(search, string.Empty, StringComparison.Ordinal).Length) /
        search.Length;
}
