using CryptoScript.Model;
using CryptoScript.Variables;

namespace CryptoScript.CryptoAlgorithm.HASH
{
    public class HASH : CryptoAlgorithm
    {
        private static readonly HashSet<string> SupportedMechanisms = new(StringComparer.OrdinalIgnoreCase)
        {
            "HASH-SHA1",
            "HASH-SHA224",
            "HASH-SHA256",
            "HASH-SHA384",
            "HASH-SHA512",
            "HASH-SHA512-224",
            "HASH-SHA512-256",
            "HASH-SHA3-224",
            "HASH-SHA3-256",
            "HASH-SHA3-384",
            "HASH-SHA3-512"
        };

        internal static bool IsSupportedMechanism(string mechanism)
        {
            return SupportedMechanisms.Contains(NormalizeMechanism(mechanism));
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism)
        {
            mechanism = NormalizeAndValidateMechanism(mechanism);
            var parameter = new ParameterVariableDeclaration
            {
                Mechanism = mechanism
            };
            parameter.ValueFormat = FormatConversions.ParseString(parameter.Value);
            return parameter;
        }

        public override ParameterVariableDeclaration GenerateParameters(string mechanism, string[] parameters)
        {
            if (parameters.Length != 0)
                throw new ArgumentException("HASH does not support additional parameters.");

            return GenerateParameters(mechanism);
        }

        public override KeyVariableDeclaration GenerateKey(string mechanism, string keySize)
        {
            throw new ArgumentException("HASH does not support key generation.");
        }

        public override StringVariableDeclaration Hash(string[] parameters)
        {
            ParameterVariableDeclaration parameter = ResolveParameter(parameters[0]);
            NormalizeAndValidateMechanism(parameter.Mechanism);
            StringVariableDeclaration data = ResolveData(parameters[1]);

            return new HASHMode().ModeHash(parameter, data);
        }

        private static string NormalizeAndValidateMechanism(string mechanism)
        {
            mechanism = NormalizeMechanism(mechanism);
            if (!SupportedMechanisms.Contains(mechanism))
                throw new ArgumentException($"Unsupported HASH mechanism: {mechanism}.");

            return mechanism;
        }

        private static string NormalizeMechanism(string mechanism)
        {
            return mechanism.StartsWith("#MECH:", StringComparison.OrdinalIgnoreCase)
                ? mechanism["#MECH:".Length..]
                : mechanism;
        }

        private static ParameterVariableDeclaration ResolveParameter(string value)
        {
            if (VariableDictionary.Instance().Get(value) is ParameterVariableDeclaration declared)
                return declared;
            if (FormatConversions.ParseString(value) == FormatConversions.PAR)
            {
                var parameter = new ParameterVariableDeclaration();
                parameter.SetInstance(value);
                return parameter;
            }

            throw new ArgumentException("wrong parameter argument");
        }

        private static StringVariableDeclaration ResolveData(string value)
        {
            if (VariableDictionary.Instance().Get(value) is StringVariableDeclaration declared)
                return declared;

            string format = FormatConversions.ParseString(value);
            if (format == FormatConversions.HEX || format == FormatConversions.B64 || format == FormatConversions.STR)
                return new StringVariableDeclaration { Value = value, ValueFormat = format };

            throw new ArgumentException("wrong data argument");
        }
    }
}
