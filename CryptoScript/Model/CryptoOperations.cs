using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLanguage_TestApp3.Model
{
    public interface IOperation
    {
        Func<string[], string> Create(string function);
    }
    public class CryptoOperations: IOperation
    {
        public Func<string[], string> Create(string function)
        {
            function=function.ToLower();
            switch(function) 
            {
                case "encrypt":
                    return Encrypt;
                case "sign":
                    return Sign;
                default: 
                    throw new Exception("unkown function");
            }
        }
        public string GenerateKey(params string[] args) 
        {
            if(args.Length != 2) 
            { 
                throw new ArgumentException("worng number of arguments"); 
            }
            return "0x(123456789)";
        }
        public string Encrypt(params string[]args)
        {
            return "0x(123456789)";
        }
        public string Sign(params string[]args)
        {
            return "0x(987654321)";
        }
    }
}
