using Antlr4.Runtime;
using System.Reflection;

namespace CryptoScript.Variables
{
    public class ParameterTypeList
    {
        private static ParameterTypeList? instance = null;
        private static readonly object padlock = new object();
        public List<string> Paddings { get; set; }
        public List<string> ParameterTypes { get; set; }
        ParameterTypeList()
        {
            Paddings = new List<string>();
            ParameterTypes = new List<string>();
            var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
            foreach (var field in typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // Check if the field corresponds to one of our mechanisms
                if (field.Name.StartsWith("PAD_"))
                {
                    int index = (int)field?.GetValue(null);
                    string m = lexer.Vocabulary.GetDisplayName(index);
                    m = m.Trim('\'');
                    Paddings.Add(m);
                }
                if (field.Name.StartsWith("P_"))
                {
                    int index = (int)field?.GetValue(null);
                    string m = lexer.Vocabulary.GetDisplayName(index);
                    m = m.Trim('\'');
                    ParameterTypes.Add(m);
                }
            }
        }
        public static ParameterTypeList Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new ParameterTypeList();
                    }
                    return instance;
                }
            }
        }
    }
}
