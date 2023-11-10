using CryptoScript.CryptoAlgorithm;
using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoScript.Model
{
    
    public class CryptoOperations
    {
        
        public Func<string[], VariableDeclaration> Create(string function)
        {
            function=function.ToLower();
            switch(function) 
            {
                case "encrypt":
                    return Encrypt;
                case "sign":
                    return Sign;
                case "generatekey":
                    return GenerateKey;
                default: 
                    throw new Exception("unkown function");
            }
        }
        public VariableDeclaration GenerateKey(params string[] args) 
        {
            if(args.Length != 2) 
            { 
                throw new ArgumentException("worng number of arguments"); 
            }
            string mech = args[0];
            string size = args[1];
            var algo=SymmetricAlgorithmFactory.Create(mech);
            var variable = algo.GenerateKey(mech, size);            
            return variable;
        }
        public VariableDeclaration Encrypt(params string[]args)
        {
            string res = "0x(123456789)";
            var variable = new StringVariableDeclaration() {Type = new CryptoTypeVar(),Value = res,ValueFormat = FormatConversions.ParseString(res)};
            return variable;
        }
        public VariableDeclaration Sign(params string[]args)
        {
            string res = "0x(123456789)";
            var variable = new StringVariableDeclaration() {Type = new CryptoTypeVar(),Value = res, ValueFormat = FormatConversions.ParseString(res)};
            return variable;
        }
    }
}
