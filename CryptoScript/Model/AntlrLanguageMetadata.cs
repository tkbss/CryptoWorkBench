using Antlr4.Runtime;
using System.Reflection;

namespace CryptoScript.Model
{
    public static class AntlrLanguageMetadata
    {
        public static List<string> GetMechanisms() => ReadNames("M_");
        public static List<string> GetParameters() => ReadNames("P_");
        public static List<string> GetPaddings() => ReadNames("PAD_");

        // CryptoType IDs retain vocabulary quotes and honor its public lexer override.
        public static string GetTypeDisplayName(CryptoScriptLexer lexer, int tokenType) =>
            lexer.Vocabulary.GetDisplayName(tokenType);

        // Return a fresh list on every call; runtime consumers own their mutable copies.
        private static List<string> ReadNames(string prefix)
        {
            var names = new List<string>();
            var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
            foreach (var field in typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.Name.StartsWith(prefix))
                {
                    int index = (int)field.GetValue(null)!;
                    names.Add(lexer.Vocabulary.GetDisplayName(index).Trim('\''));
                }
            }
            return names;
        }
    }
}
