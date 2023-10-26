using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    public class Mechanism
    {
        public List<string>? MechList { get; set; }
        public void MechanismList() 
        {
            MechList = new List<string>();
            //var lexer = new CryptoScriptLexer(new AntlrInputStream(""));
            var remove = "M_";
            foreach (var field in typeof(CryptoScriptLexer).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                // Check if the field corresponds to one of our mechanisms
                if (field.Name.StartsWith("M_"))
                {
                    string m = field.Name.TrimStart(remove.ToCharArray());                    
                    MechList.Add(m);
                }
            }
         }
    }
}



