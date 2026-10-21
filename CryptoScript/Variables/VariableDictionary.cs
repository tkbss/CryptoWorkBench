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
            InterpreterExecution.VerifyAccess();
            variables.Clear();
        }
        public VariableDeclaration Get(string Id)
        {
            InterpreterExecution.VerifyAccess();
            VariableDeclaration? value=null;
            variables.TryGetValue(Id, out  value);
            return value;
        }
        public void Add(VariableDeclaration v)
        {
            InterpreterExecution.VerifyAccess();
            if (variables.ContainsKey(v.Id))
            {
                variables.Remove(v.Id);
            }
            variables[v.Id] = v;
        }
        public void Update(VariableDeclaration v)
        {
            InterpreterExecution.VerifyAccess();
            if(variables.ContainsKey(v.Id))
                variables[v.Id]=v;
        }
        // Prepare the complete state privately. Enumeration, validation and allocation
        // may fail, but the live dictionary is untouched until the final reference swap.
        // No callbacks or fallible work follow publication in the synchronous interpreter.
        internal void PublishBatch(IEnumerable<VariableDeclaration> state)
        {
            InterpreterExecution.VerifyAccess();
            var prepared = state.ToDictionary(variable => variable.Id, StringComparer.Ordinal);
            variables = prepared;
        }
        public bool Contains(string Id)
        {
            InterpreterExecution.VerifyAccess();
            return variables.ContainsKey(Id);
        }
       
        public static VariableDictionary Instance()
        {
            return _instance;
        }
        public IEnumerable<VariableDeclaration> GetVariables()
        {
            InterpreterExecution.VerifyAccess();
            return variables.Values;
        }


    }
}
