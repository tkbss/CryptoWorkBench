using Antlr4.Runtime;
using CryptoScript.Model;
using System.Reflection;

namespace CryptoScriptUnitTest;

// Frontend compatibility contract; the runtime list itself must not depend on the lexer.
[NonParallelizable]
public class MechanismListLexerCompatibilityTests
{
    [Test]
    public void RuntimeNamesMatchExistingGeneratedLexerMetadataInOrder()
    {
        var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
        var names = typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.Name.StartsWith("M_"))
            .Select(field => lexer.Vocabulary.GetDisplayName((int)field.GetValue(null)!).Trim('\''))
            .ToArray();

        Assert.That(MechanismList.Instance.Mechanisms, Is.EqualTo(names));
    }
}
