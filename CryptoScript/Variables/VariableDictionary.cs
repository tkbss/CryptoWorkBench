using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoScript.Model;
using SimpleLanguage_TestApp3.Model;

namespace CryptoScript.Variables
{
    public class VariableDictionary : Statement
    {
        private static readonly VariableDictionary _instance = new VariableDictionary();

        private Dictionary<string, VariableDeclaration > variables = new Dictionary<string, VariableDeclaration>();
        private VariableDictionary()
        {

        }
        public VariableDeclaration Get(string Id)
        {
            return variables[Id];
        }
        public void Add(VariableDeclaration v)
        {
            variables[v.Id] = v;
        }
        public void Remove(StringVariableDeclaration v)
        {
            variables.Remove(v.Id);
        }
        public bool Contains(string Id)
        {
            return variables.ContainsKey(Id);
        }
        public static VariableDictionary Instance()
        {
            return _instance;
        }


    }
}
