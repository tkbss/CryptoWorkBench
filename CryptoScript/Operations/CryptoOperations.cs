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
        private readonly Func<string, CryptoAlgorithm.CryptoAlgorithm> algorithmFactory;

        public CryptoOperations() : this(AlgorithmFactory.Create) { }

        // Instance-local dispatch seam for transport tests; no registry or global mutation.
        internal CryptoOperations(Func<string, CryptoAlgorithm.CryptoAlgorithm> algorithmFactory)
        {
            this.algorithmFactory = algorithmFactory ?? throw new ArgumentNullException(nameof(algorithmFactory));
        }

        public VariableDeclaration GenerateParameters(OperationInvocation invocation)
        {
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is ParameterVariableDeclaration sourceParameter)
                MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(
                    sourceParameter.Mechanism, sourceParameter, CryptoScriptFunction.Parameters);
            string[] values = invocation.Values;
            if (values.Length == 0)
                return GenerateParameters(values);

            string mechanism = NormalizeMechanismSelector(values[0]);
            var suppliedParameters = new ParameterVariableDeclaration { Mechanism = mechanism };
            suppliedParameters.RecordExplicitParameter("#MECH");
            foreach (string parameter in values.Skip(1))
            {
                suppliedParameters.SetParameter(parameter);
                suppliedParameters.RecordExplicitParameter(parameter.Split(':', 2)[0]);
            }

            MechanismParameterContractValidator.Validate(
                mechanism, CryptoScriptFunction.Parameters, suppliedParameters);
            return GenerateParameters(values);
        }
        public VariableDeclaration GenerateKey(OperationInvocation invocation)
        {
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is ParameterVariableDeclaration parameters)
                MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(
                    parameters.Mechanism, parameters, CryptoScriptFunction.GenerateKey);
            return GenerateKey(invocation.Values);
        }
        public VariableDeclaration Mac(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments, parameters) = DetermineAlgorithm(arguments, CryptoScriptFunction.Mac);
            MechanismParameterContractValidator.Validate(
                parameters.Mechanism, CryptoScriptFunction.Mac, parameters);
            return algorithm.Mac(resolvedArguments);
        }
        public VariableDeclaration Hash(OperationInvocation invocation)
        {
            if (invocation.Arguments.FirstOrDefault()?.SourceVariable is ParameterVariableDeclaration parameters)
                MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(
                    parameters.Mechanism, parameters, CryptoScriptFunction.Hash);
            return Hash(invocation.Values);
        }
        public VariableDeclaration Derive(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments, _) = DetermineAlgorithm(arguments, CryptoScriptFunction.Derive);
            return algorithm.Derive(resolvedArguments);
        }
        public VariableDeclaration Encrypt(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments, parameters) = DetermineAlgorithm(arguments, CryptoScriptFunction.Encrypt);
            MechanismParameterContractValidator.Validate(
                parameters.Mechanism, CryptoScriptFunction.Encrypt, parameters);
            return algorithm.Encrypt(resolvedArguments);
        }
        public VariableDeclaration Decrypt(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments, parameters) = DetermineAlgorithm(arguments, CryptoScriptFunction.Decrypt);
            MechanismParameterContractValidator.Validate(
                parameters.Mechanism, CryptoScriptFunction.Decrypt, parameters);
            return algorithm.Decrypt(resolvedArguments);
        }
        public VariableDeclaration BlockHeader(OperationInvocation invocation) => BlockHeader(invocation.Values);
        public VariableDeclaration Wrap(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments, parameters) = DetermineAlgorithm(arguments, CryptoScriptFunction.Wrap);
            MechanismParameterContractValidator.Validate(
                parameters.Mechanism, CryptoScriptFunction.Wrap, parameters);
            return algorithm.Wrap(resolvedArguments);
        }
        public VariableDeclaration Unwrap(OperationInvocation invocation)
        {
            AlgorithmCallArguments arguments = ToAlgorithmArguments(invocation);
            if (arguments.Arguments.Count != 3)
                throw new ArgumentException("wrong number of arguments");
            var (algorithm, resolvedArguments, parameters) = DetermineAlgorithm(arguments, CryptoScriptFunction.Unwrap);
            MechanismParameterContractValidator.Validate(
                parameters.Mechanism, CryptoScriptFunction.Unwrap, parameters);
            return algorithm.Unwrap(resolvedArguments);
        }

        //the requirement for generate parameters is that there will be an undefined number of parameters
        //the first parameter is the mechanism
        public VariableDeclaration GenerateParameters(params string[] args)
        {
            if(args.Length == 0)
            {
                throw new ArgumentException("wrong number of arguments"); 
            }
            string mech = args[0];
            MechanismParameterContractValidator.ValidateEmvCryptogramParameterNames(
                NormalizeMechanismSelector(mech), args.Skip(1).Select(parameter => parameter.Split(':', 2)[0]));
            var algo=AlgorithmFactory.Create(mech);
            VariableDeclaration? returnValue = null;
            if(args.Length > 1)
            {
                var parameters = args.Skip(1).ToArray();
                returnValue = algo.GenerateParameters(mech,parameters);
                
            }
            else
                returnValue = algo.GenerateParameters(mech);

            if (returnValue is ParameterVariableDeclaration generatedParameters)
                MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(
                    NormalizeMechanismSelector(mech), generatedParameters);

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
            var entry = GenerateKeyContract.RequireMechanism(args[0]);
            string size = args[1];
            var algo = AlgorithmFactory.Create(entry.CanonicalName);
            var returnValue = algo.GenerateKey(entry.CanonicalName, size);
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
            MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(parameter.Mechanism, parameter);
            var algo=AlgorithmFactory.Create(parameter.Mechanism);
            if (algo is CryptoScript.CryptoAlgorithm.EP2.Ep2Tr31KeyBlock)
                throw new ArgumentException("EP2 TR-31 requires structured calls with genuine PARAM and KEY references.");
            if (algo is CryptoScript.CryptoAlgorithm.EP2.Ep2PanReceiptEncryption)
                throw new ArgumentException("EP2 PAN receipt encryption requires structured calls with genuine PARAM and KEY references.");
            if (algo is CryptoScript.CryptoAlgorithm.EP2.Ep2DataEncryption)
                throw new ArgumentException("EP2 encryption requires structured calls with genuine PARAM and KEY references.");
            if (algo is CryptoScript.CryptoAlgorithm.EP2.Ep2Mac)
                throw new ArgumentException("EP2 MAC mechanisms require structured Mac calls with genuine PARAM and KEY references.");
            return algo;
        }

        private static AlgorithmCallArguments ToAlgorithmArguments(OperationInvocation invocation) =>
            new(invocation.Arguments.Select(argument =>
                new AlgorithmCallArgument(argument.Value, argument.SourceVariable, argument.Kind)),
                invocation.DiagnosticSink);

        private (CryptoAlgorithm.CryptoAlgorithm Algorithm, AlgorithmCallArguments Arguments,
                ParameterVariableDeclaration Parameters)
            DetermineAlgorithm(AlgorithmCallArguments arguments, CryptoScriptFunction function)
        {
            for (int index = 0; index < arguments.Arguments.Count; index++)
            {
                AlgorithmCallArgument argument = arguments.Arguments[index];
                if (argument.SourceVariable is ParameterVariableDeclaration sourceParameter)
                {
                    MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(
                        sourceParameter.Mechanism, sourceParameter, function);
                    return (algorithmFactory(sourceParameter.Mechanism), arguments, sourceParameter);
                }

                if (argument.Value is not null &&
                    FormatConversions.ParseString(argument.Value) == FormatConversions.PAR)
                {
                    ParameterVariableDeclaration temporary =
                        AlgorithmArgumentResolver.ResolveParameter(argument);
                    AlgorithmCallArguments resolved =
                        arguments.WithSourceVariable(index, temporary);
                    return (algorithmFactory(temporary.Mechanism), resolved, temporary);
                }
            }

            throw new ArgumentException("Missing argument of type PARAM");
        }

        private static string NormalizeMechanismSelector(string mechanism) =>
            mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
                ? mechanism["#MECH:".Length..]
                : mechanism;
    }
}
