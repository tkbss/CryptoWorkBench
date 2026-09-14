using Antlr4.Runtime;
using CryptoScript.Model;
using CryptoScript.Variables;
using Newtonsoft.Json.Linq;

namespace CryptoScriptUnitTest;

[NonParallelizable]
public class CryptoTypeMetadataTests
{
    private CryptoScriptLexer previous = null!;

    [SetUp]
    public void SaveLexer()
    {
        previous = CryptoType.Lexer;
        CryptoType.Lexer = new CryptoScriptLexer(new AntlrInputStream(""));
    }

    [TearDown]
    public void RestoreLexer() => CryptoType.Lexer = previous;

    private static CryptoType Create(string name) => name switch
    {
        "KEY" => new CryptoTypeKey(),
        "VAR" => new CryptoTypeVar(),
        "PARAM" => new CryptoTypeParameters(),
        "PATH" => new CryptoTypePath(),
        "TR31H" => new CryptoTypeTR31Header(),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    [TestCase("KEY", CryptoScriptLexer.T_KEY)]
    [TestCase("VAR", CryptoScriptLexer.T_VAR)]
    [TestCase("PARAM", CryptoScriptLexer.T_PARAMETER)]
    [TestCase("PATH", CryptoScriptLexer.T_PATH)]
    [TestCase("TR31H", CryptoScriptLexer.T_TR31H)]
    public void PreservesExactQuotedIdsNamesAndSerializedRepresentation(string name, int token)
    {
        var type = Create(name);
        var formerId = CryptoType.Lexer.Vocabulary.GetDisplayName(token);
        Assert.That(type.Name, Is.EqualTo(name));
        Assert.That(type.Id, Is.EqualTo("'" + name + "'"));
        Assert.That(type.Id, Is.EqualTo(formerId));

        var key = new KeyVariableDeclaration { Type = type };
        var json = key.Serialize();
        Assert.That((string?)JObject.Parse(json)["Type"]!["Id"], Is.EqualTo(formerId));
        type.Id = formerId;
        Assert.That(key.Serialize(), Is.EqualTo(json));
        Assert.That(KeyVariableDeclaration.Deserialize(json).Type!.Id, Is.EqualTo(formerId));
    }

    [TestCase("KEY")]
    [TestCase("VAR")]
    [TestCase("PARAM")]
    [TestCase("PATH")]
    public void ParseKeepsClrMappingAndCaseHandling(string name)
    {
        Assert.That(CryptoType.Parse(name).GetType(), Is.EqualTo(Create(name).GetType()));
        Assert.That(CryptoType.Parse(name.ToLowerInvariant()).Id, Is.EqualTo(Create(name).Id));
    }

    [TestCase("TR31H")]
    [TestCase("unknown")]
    public void ParseStillRejectsUnsupportedTypes(string name)
    {
        Assert.Throws<Exception>(() => CryptoType.Parse(name));
    }

    [Test]
    public void PublicLexerOverrideStillControlsIds()
    {
        CryptoType.Lexer = new CustomLexer();
        Assert.That(new CryptoTypeKey().Id, Is.EqualTo("'custom-key'"));
        Assert.That(new CryptoTypeKey().Name, Is.EqualTo("KEY"));
    }

    [Test]
    public void NullLexerStillRestoresDefaultOnConstruction()
    {
        CryptoType.Lexer = null!;
        Assert.That(new CryptoTypeKey().Id, Is.EqualTo("'KEY'"));
        Assert.That(CryptoType.Lexer, Is.Not.Null);
    }

    private sealed class CustomLexer : CryptoScriptLexer
    {
        private static readonly IVocabulary CustomVocabulary = new Vocabulary(
            Enumerable.Range(0, CryptoScriptLexer.T_KEY + 1)
                .Select(i => i == CryptoScriptLexer.T_KEY ? "'custom-key'" : null).ToArray(),
            Array.Empty<string>());

        public CustomLexer() : base(new AntlrInputStream("")) { }
        public override IVocabulary Vocabulary => CustomVocabulary;
    }
}
