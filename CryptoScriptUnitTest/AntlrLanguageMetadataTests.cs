using Antlr4.Runtime;
using CryptoScript.Model;
using CryptoScript.Variables;
using System.Reflection;

namespace CryptoScriptUnitTest;

// Frontend compatibility contract; the runtime list itself must not depend on the lexer.
[NonParallelizable]
public class AntlrLanguageMetadataTests
{
    private static List<string> Read(string prefix) => prefix switch
    {
        "M_" => AntlrLanguageMetadata.GetMechanisms(),
        "P_" => AntlrLanguageMetadata.GetParameters(),
        "PAD_" => AntlrLanguageMetadata.GetPaddings(),
        _ => throw new ArgumentOutOfRangeException(nameof(prefix))
    };

    [TestCase("M_")]
    [TestCase("P_")]
    [TestCase("PAD_")]
    public void NamesMatchExistingGeneratedLexerMetadataInOrder(string prefix)
    {
        var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
        var names = typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.StartsWith(prefix))
            .Select(field => lexer.Vocabulary.GetDisplayName((int)field.GetValue(null)!).Trim('\''))
            .ToArray();

        Assert.That(Read(prefix), Is.EqualTo(names));
    }

    [TestCase("M_")]
    [TestCase("P_")]
    [TestCase("PAD_")]
    public void EachReadReturnsAnIndependentMutableList(string prefix)
    {
        var first = Read(prefix);
        var second = Read(prefix);
        var expected = second.ToArray();
        Assert.That(first, Is.Not.SameAs(second));
        first.Clear();
        Assert.That(second, Is.EqualTo(expected));
        Assert.That(Read(prefix), Is.EqualTo(expected));
    }

    [Test]
    public void PreservesParameterAndPaddingSnapshots()
    {
        var parameters = new[] { "#MECH", "#IV", "#PAD", "#MACLEN", "#NONCE", "#COUNTER", "#ADATA", "#BLKH", "#RND" };
        var paddings = new[] { "ISO-7816", "PKCS-7", "ISO-9797-M1", "ISO-9797-M2", "ISO-9797-M3", "ANSI-X923", "TLS-CBC", "NONE" };
        Assert.That(AntlrLanguageMetadata.GetParameters(), Is.EqualTo(parameters));
        Assert.That(AntlrLanguageMetadata.GetPaddings(), Is.EqualTo(paddings));
        Assert.That(ParameterTypeList.Instance.Paddings, Is.EqualTo(paddings));
        Assert.That(ParameterTypeList.Instance.ParameterTypes, Is.EqualTo(parameters));
        Assert.That(ParameterTypeList.Instance.ParameterTypes.Count(p => p == "#MACLEN"), Is.EqualTo(1));
    }
}
