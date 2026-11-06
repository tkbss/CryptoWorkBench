using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoScript.Variables;

namespace CryptoScript.Model
{
    public  class Argument: Statement { }
    // A snapshot root name, deliberately not looked up in the live dictionary.
    public sealed class ArgumentSnapshotSelector : Argument
    {
        public required string Identifier { get; init; }
    }
    public sealed class ArgumentHeaderAttribute : Argument
    {
        public string Name { get; init; } = "";
        public string RawValue { get; init; } = "";
    }
    public class ArgumentVariable: Argument
    {
        public VariableDeclaration? Id { get; set; }        
        public ArgumentVariable () 
        {
            Id = null;            
        }
    }
    public class ArgumentInfo: Argument 
    {
        public string InfoType { get; set; } = string.Empty;
    }
    public class ArgumentExpression : Argument
    { 
        public Expression? Expr { get; set; }
        public ResolvedCallArgumentKind Kind { get; set; } = ResolvedCallArgumentKind.Expression;
        public ArgumentExpression() 
        {
            Expr = null;
        }
    }
    public class ArgumentMechanism: Argument 
    { 
        public Mechanism? Mechanism { get; set; }
        public ArgumentMechanism() 
        {
            Mechanism = null;
        }
    }
    public class ArgumentParameter: Argument
    {
        public BlockHeaderVariableDeclaration? HeaderReference { get; private set; }
        
        public string Type { get; set; }    = string.Empty;
        public string Value { get; set; } = string.Empty;
        
        public ArgumentParameter()
        {
            
        }
        public void SetParameter(string type, string value)
        {
            Type = type;
            SetValue(value);
        }
        private void SetValue(string value)
        {
            HeaderReference = null;
            if (Type.Equals("#BLKH", StringComparison.OrdinalIgnoreCase) &&
                VariableDictionary.Instance().Get(value) is BlockHeaderVariableDeclaration header)
            {
                if (header.Definition is null) throw new ArgumentException("TR31H has no validated header definition.");
                HeaderReference = header;
                Value = value;
                return;
            }
            if (Type.Equals("#PSN", StringComparison.OrdinalIgnoreCase))
            {
                if (!value.StartsWith('"') || !value.EndsWith('"') || value.Length != 4 ||
                    value[1] is < '0' or > '9' || value[2] is < '0' or > '9')
                    throw new ArgumentException("PSN must be a normal string literal containing exactly two ASCII decimal digits.");
                Value = value;
                return;
            }
            if (Type.Equals("#VARIANT", StringComparison.OrdinalIgnoreCase) ||
                Type.Equals("#USAGE", StringComparison.OrdinalIgnoreCase) ||
                Type.Equals("#KEYTYPE", StringComparison.OrdinalIgnoreCase))
            {
                Value = value;
                return;
            }
            string formatType=FormatConversions.ParseString(value);
            if (formatType==FormatConversions.HEX || formatType == FormatConversions.B64 || formatType == FormatConversions.STR)
            {
                Value = value;
                return;
            }
            if (ParameterTypeList.Instance.Paddings.Contains(value))
            {
                Value = value;
                return;
            }
            if (ParameterTypeList.Instance.Mechanism.Contains(value))
            {
                Value = value;
                return;
            }
            if (value.Length > 0 && value.All(char.IsDigit))
            {
                Value = value;
                return;
            }
            if (VariableDictionary.Instance().Contains(value))
                Value = VariableDictionary.Instance().Get(value).Value;
            else
                throw new ArgumentException("Unknown parameter value : " + value);
        }
    }

}
