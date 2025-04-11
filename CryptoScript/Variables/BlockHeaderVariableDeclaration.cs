using CryptoScript.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{
    public class BlockHeaderVariableDeclaration : VariableDeclaration
    {
        public override string Value { get => base.Value; set => base.Value = value; }

        public override CryptoType? Type { get => base.Type; set => base.Type = value; }
        public override string PrintOutput()
        {
            return base.PrintOutput();
        }
        public BlockHeaderVariableDeclaration()
        {
            Id = string.Empty;
            Type = new CryptoTypeVar();
            Value = string.Empty;
        }
        public override string ToString()
        {
            return base.ToString();
        }


    }
}
