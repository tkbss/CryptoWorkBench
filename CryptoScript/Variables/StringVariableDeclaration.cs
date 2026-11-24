using CryptoScript.Model;

namespace CryptoScript.Variables
{
    public class StringVariableDeclaration : VariableDeclaration
    {
        
        
        public string GMAC { get => ReadOwned(field); set => Change(ref field, value); } = string.Empty;
        public StringVariableDeclaration()
        {
            Id = string.Empty;
            Type = new CryptoTypeVar();
            Value = string.Empty;
        }

        public override string PrintOutput()
        {
            return base.PrintOutput();
        }
    }
}
