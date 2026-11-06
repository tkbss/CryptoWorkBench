using CryptoScript.Model;
using Newtonsoft.Json;

namespace CryptoScript.Variables
{
    public class VariableDeclaration : Statement
    {
        public string Id { get; set; } = string.Empty;
        private CryptoType? type;
        public virtual CryptoType? Type
        {
            get => type;
            set
            {
                if (value is CryptoTypeVal)
                    throw new ArgumentException("VAL requires a Boolean result object.");
                type = value;
            }
        }
        public virtual string Value { get; set; }=string.Empty;
        public string ValueFormat { get; set; } = string.Empty;        
        public virtual string PrintOutput() {  return string.Empty; }
        

    }
}

