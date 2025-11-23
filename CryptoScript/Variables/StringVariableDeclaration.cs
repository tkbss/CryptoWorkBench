using CryptoScript.Model;

namespace CryptoScript.Variables
{
    public class StringVariableDeclaration : VariableDeclaration
    {
        
        //public override string Value { get => base.Value; set => base.Value = value; }
        
        //public override CryptoType? Type { get => base.Type; set => base.Type = value; }
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
