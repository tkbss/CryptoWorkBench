using CryptoScript.ErrorListner;
using CryptoScript.Model;

namespace CryptoScript.Variables
{
    public class VariableDictionary : Statement
    {
        private static readonly VariableDictionary _instance = new VariableDictionary();

        private Dictionary<string, VariableDeclaration > variables = new Dictionary<string, VariableDeclaration>();
        private VariableDictionary()
        {

        }
        public void Clear()
        {
            variables.Clear();
        }
        public VariableDeclaration Get(string Id)
        {
            VariableDeclaration? value=null;
            variables.TryGetValue(Id, out  value);
            return value;
        }
        public void Add(VariableDeclaration v)
        {
            if (variables.ContainsKey(v.Id))
            {
                variables.Remove(v.Id);
            }
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
        public IEnumerable<VariableDeclaration> GetVariables()
        {
            return variables.Values;
        }


    }
}
