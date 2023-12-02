using Antlr4.Runtime;
using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class ParameterTypeList
    {
        private static ParameterTypeList instance = null;
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
                    int index = (int)field.GetValue(null);//field.Name.TrimStart(remove.ToCharArray());                    
                    string m = lexer.Vocabulary.GetDisplayName(index);
                    m = m.Trim('\'');
                    Paddings.Add(m);
                }
                if (field.Name.StartsWith("P_"))
                {
                    int index = (int)field.GetValue(null);//field.Name.TrimStart(remove.ToCharArray());                    
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
    public class Parameter : VariableDeclaration
    {
        public string Mechanism { get; set; } = string.Empty;
        public Parameter()
        {
            Type = new CryptoTypeParameters();
        }

    }
    public class ParameterVariableDeclaration : Parameter
    {
        
        public string IV { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public string Counter { get; set; } = string.Empty;
        public string Padding { get; set; } = string.Empty;
        
        public override string PrintOutput()
        {
            return base.PrintOutput();
        }
        public void SetParameter(ArgumentParameter p)
        {
            if (p.Type.ToLower().Contains("iv"))
            {
                IV = p.Value;
            }
            if (p.Type.ToLower().Contains("pad"))
            {
                Padding = p.Value;
            }
            if (p.Type.ToLower().Contains("nonce"))
            {
                Nonce = p.Value;
            }
            if (p.Type.ToLower().Contains("counter"))
            {
                Counter = p.Value;
            }

        }
    }
}
