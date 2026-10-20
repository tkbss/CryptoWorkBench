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
        public CryptoAlgorithm.WRAPPERS.Tr31HeaderDefinition? Definition { get; init; }
        public override string Value
        {
            get => Definition?.ToString() ?? base.Value;
            set
            {
                if (Definition is not null) throw new InvalidOperationException("A validated TR31H definition is immutable.");
                base.Value = value;
            }
        }
        //public override string Value { get => base.Value; set => base.Value = value; }

        //public override CryptoType? Type { get => base.Type; set => base.Type = value; }
        public override string PrintOutput()
        {
            return Value;
        }
        public BlockHeaderVariableDeclaration()
        {
            Id = string.Empty;
            Type = new CryptoTypeTR31Header();
            Value = string.Empty;
        }
        public override string ToString()
        {
            return base.ToString();
        }


    }
}
