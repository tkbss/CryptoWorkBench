using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Operations
{
    public class GeneralOperations
    {
        public static VariableDeclaration Compare(params string[] args) 
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            string a1  = args[0];
            string a2= args[1];
            string f1 = FormatConversions.ParseString(a1);
            string f2= FormatConversions.ParseString(a2);   
            if (f1!=f2)
            {
                return new StringVariableDeclaration() { Value = $"Formats are not equal {f1}, {f2} ", ValueFormat = FormatConversions.STR };
            }
            if(a1.Length!=a2.Length)    
            {
                return new StringVariableDeclaration() { Value = "Values length are not equal", ValueFormat = f1 };
            }
            if(f1==FormatConversions.HEX )
            {
                var b1=FormatConversions.HexStringToByteArray(a1);
                var b2=FormatConversions.HexStringToByteArray(a2);
                
                if (b1.SequenceEqual(b2)
)
                {
                    return new StringVariableDeclaration() {Value="Values are equal",ValueFormat= f1 };
                }
                else
                {
                    for(int i = 0; i < b1.Length; i++)
                    {
                        if (b1[i] != b2[i])
                        {
                            return new StringVariableDeclaration() { Value = $"Values are not equal at byte index {i}", ValueFormat = f1 };
                        }
                    }
                    return new StringVariableDeclaration() { Value = "Values are not equal", ValueFormat = f1 }; 
                }
            }
            else
            {
                if (a1.Equals(a2))
                {
                    return new StringVariableDeclaration() { Value = "Values are equal", ValueFormat = f1 };
                }
                else
                {
                    for(int i = 0; i < a1.Length; i++)
                    {
                        if (a1[i] != a2[i])
                        {
                            return new StringVariableDeclaration() { Value = $"Values are not equal at character index {i}", ValueFormat = f1 };
                        }
                    }
                    return new StringVariableDeclaration() { Value = "Values are not equal", ValueFormat = f1 };
                }
            }
                
            
        }
    }
}
