using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class MechanismList
    {
        private static MechanismList instance = null;
        private static readonly object padlock = new object();
        public List<string> Mechanisms { get; set; }
        MechanismList()
        {
            Mechanisms = new List<string>();
            var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
            var remove = "M_";
            foreach (var field in typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // Check if the field corresponds to one of our mechanisms
                if (field.Name.StartsWith("M_"))
                {
                    int index = (int)field.GetValue(null);//field.Name.TrimStart(remove.ToCharArray());                    
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

        
    
    public class Mechanism : Statement
    {
       
        public string Value { get; private set; } = string.Empty;
        public Mechanism() 
        {           
            
        }
        public void SetMechanismValue(string value) 
        {
            
            if (MechanismList.Instance.Mechanisms.Contains(value))
                Value = value;
            else
                throw new ArgumentException();

        }

    }
}



