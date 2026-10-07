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
        public VariableDeclaration GenerateParameters(OperationInvocation invocation) => GenerateParameters(invocation.Values);
        public VariableDeclaration GenerateKey(OperationInvocation invocation) => GenerateKey(invocation.Values);
        public VariableDeclaration Mac(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments) = DetermineAlgorithm(arguments);
            return algorithm.Mac(resolvedArguments);
        }
        public VariableDeclaration Hash(OperationInvocation invocation) => Hash(invocation.Values);
        public VariableDeclaration Derive(OperationInvocation invocation) => Derive(invocation.Values);
        public VariableDeclaration Encrypt(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments) = DetermineAlgorithm(arguments);
            return algorithm.Encrypt(resolvedArguments);
        }
        public VariableDeclaration Decrypt(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments) = DetermineAlgorithm(arguments);
            return algorithm.Decrypt(resolvedArguments);
        }
        public VariableDeclaration BlockHeader(OperationInvocation invocation) => BlockHeader(invocation.Values);
        public VariableDeclaration Wrap(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments) = DetermineAlgorithm(arguments);
            return algorithm.Wrap(resolvedArguments);
        }
        public VariableDeclaration Unwrap(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments) = DetermineAlgorithm(arguments);
            return algorithm.Unwrap(resolvedArguments);
        }
        public VariableDeclaration Sign(OperationInvocation invocation) => Sign(invocation.Values);

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
        //second argument is KeySize or existing key value
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
        public VariableDeclaration Hash(params string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            var algo = DetermineAlgorithm(args);
            return algo.Hash(args);
        }
        public VariableDeclaration Derive(params string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            var algo = DetermineAlgorithm(args);
            return algo.Derive(args);
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
        public VariableDeclaration BlockHeader(params string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            string mech = "BLOCKHEADER-"+args[0];
            var algo = AlgorithmFactory.Create(mech);
            if(args.Length == 1)
            {
                
                return algo.GenerateBlockHeader(args[0]);
                
            }
            else
            {
                return algo.GenerateBlockHeader(args);
            }
            
        }
        public VariableDeclaration Wrap(params string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            var algo = DetermineAlgorithm(args);
            var returnValue = algo.Wrap(args);
            return returnValue;
        }
        public VariableDeclaration Unwrap(params string[] args)
        {
            if (args.Length != 3)
            {
                throw new ArgumentException("wrong number of arguments");
            }
            var algo = DetermineAlgorithm(args);
            var returnValue = algo.Unwrap(args);
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

        private static AlgorithmCallArguments ToAlgorithmArguments(OperationInvocation invocation) =>
            new(invocation.Arguments.Select(argument =>
                new AlgorithmCallArgument(argument.Value, argument.SourceVariable)));

        private static (CryptoAlgorithm.CryptoAlgorithm Algorithm, AlgorithmCallArguments Arguments)
            DetermineAlgorithm(AlgorithmCallArguments arguments)
        {
            for (int index = 0; index < arguments.Arguments.Count; index++)
            {
                AlgorithmCallArgument argument = arguments.Arguments[index];
                if (argument.SourceVariable is ParameterVariableDeclaration sourceParameter)
                {
                    return (AlgorithmFactory.Create(sourceParameter.Mechanism), arguments);
                }

                if (argument.Value is not null &&
                    FormatConversions.ParseString(argument.Value) == FormatConversions.PAR)
                {
                    ParameterVariableDeclaration temporary =
                        AlgorithmArgumentResolver.ResolveParameter(argument);
                    AlgorithmCallArguments resolved =
                        arguments.WithSourceVariable(index, temporary);
                    return (AlgorithmFactory.Create(temporary.Mechanism), resolved);
                }
            }

            throw new ArgumentException("Missing argument of type PARAM");
        }
    }
}
