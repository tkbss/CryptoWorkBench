using CryptoScript.Model;

namespace CryptoScript.Variables
{
    public class StringVariableDeclaration : VariableDeclaration
    {
        
        
        public string GMAC { get; set; } = string.Empty;
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
