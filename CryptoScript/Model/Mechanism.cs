using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class Mechanism : Statement
    {
        public List<string> MechList { get; set; }= new List<string>();
        public string Value { get; private set; } = string.Empty;
        public void MechanismList() 
        {           
            var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
            var remove = "M_";
            foreach (var field in typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // Check if the field corresponds to one of our mechanisms
                if (field.Name.StartsWith("M_"))
                {
                    int index = (int)field.GetValue(null);//field.Name.TrimStart(remove.ToCharArray());                    
                    string m=lexer.Vocabulary.GetDisplayName(index);
                    m=m.Trim('\'');
                    MechList.Add(m);
                }
            }
         }
        public void SetMechanismValue(string value) 
        {
            MechanismList();
            if (MechList.Contains(value))
                Value = value;
            else
                throw new ArgumentException();

        }

    }
}



