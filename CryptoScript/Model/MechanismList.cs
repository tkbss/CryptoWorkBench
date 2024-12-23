using Antlr4.Runtime;
using System.Reflection;

namespace CryptoScript.Model
{
    public class MechanismList
    {
        private static MechanismList? instance = null;
        private static readonly object padlock = new object();
        public List<string> Mechanisms { get; set; }
        MechanismList()
        {
            Mechanisms = new List<string>();
            var lexer = new CryptoScriptLexer(new AntlrInputStream(""));            
            foreach (var field in typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // Check if the field corresponds to one of our mechanisms
                if (field.Name.StartsWith("M_"))
                {
                    int index = (int)field.GetValue(null);
                    string m = lexer.Vocabulary.GetDisplayName(index);
                    m = m.Trim('\'');
                    Mechanisms.Add(m);
                }
            }
        }
        public static MechanismList Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new MechanismList();
                    }
                    return instance;
                }
            }
        }
    }
}
