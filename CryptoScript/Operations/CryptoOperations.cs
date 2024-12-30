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
        //the requirement for generate parameters is that there will be an undefined number of parameters
        //the first parameter is the mechanism
        public VariableDeclaration GenerateParameters(params string[] args)
        {
            if(args.Length == 0)
            {
                throw new ArgumentException("wrong number of arguments"); 
            }
            string mech = args[0];
            var algo=AlgorithmFactory.Create(mech);
            VariableDeclaration? returnValue = null;
            if(args.Length > 1)
            {
                var parameters = args.Skip(1).ToArray();
                returnValue = algo.GenerateParameters(mech,parameters);
                
            }
            else
                returnValue = algo.GenerateParameters(mech);
            
            return returnValue;
        }
        //the requirement for generate key is that there will be 2 arguments
        //first argument is Mechanism
        //second argument is KeySize
        public VariableDeclaration GenerateKey(params string[] args) 
        {
            if(args.Length != 2) 
            { 
                throw new ArgumentException("wrong number of arguments"); 
            }
            string mech = args[0];
            string size = args[1];
            var algo=AlgorithmFactory.Create(mech);
            var returnValue = algo.GenerateKey(mech, size);            
            return returnValue;
        }
        //the requirement for mac is that there will be 3 arguments
        //first argument is Parameter
        //second argument is  Key
        //third argument is data as hex or base64 string
        public VariableDeclaration Mac(params string[] args) 
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            var algo = DetermineAlgorithm(args);
            var returnValue = algo.Mac(args);   
            return returnValue; 
        }
        //the requirement for encrypt/decrypt is that there will be 3 arguments
        //first argument is Parameter
        //second argument is  Key
        //third argument is data as hex or base64 string
        public VariableDeclaration Encrypt(params string[]args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            var algo = DetermineAlgorithm(args);
            var returnValue= algo.Encrypt(args);
            return returnValue;
        }
        public VariableDeclaration Decrypt(params string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            
            var algo = DetermineAlgorithm(args);
            var returnValue = algo.Decrypt(args);
            return returnValue;
        }
        public VariableDeclaration Sign(params string[]args)
        {
            string res = "0x(123456789)";
            var variable = new StringVariableDeclaration() {Type = new CryptoTypeVar(),Value = res, ValueFormat = FormatConversions.ParseString(res)};
            return variable;
        }
        private CryptoAlgorithm.CryptoAlgorithm DetermineAlgorithm(params string[] args) 
        {
            ParameterVariableDeclaration? parameter = null;
            foreach (var p in args)
            {
                if (VariableDictionary.Instance().Get(p) as ParameterVariableDeclaration != null)
                {
                    parameter = VariableDictionary.Instance().Get(p) as ParameterVariableDeclaration;
                    break;
                }
                if(FormatConversions.ParseString(p) == FormatConversions.PAR)
                {
                    parameter = new ParameterVariableDeclaration();
                    parameter.SetInstance(p);
                    break;
                }
            }
            if (parameter == null)
            {
                throw new ArgumentException("Missing argument of type PARAM");
            }            
            var algo=AlgorithmFactory.Create(parameter.Mechanism);
            return algo;
        }
    }
}
