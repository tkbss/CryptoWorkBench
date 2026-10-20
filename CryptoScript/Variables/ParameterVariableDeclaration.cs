using CryptoScript.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CryptoScript.Variables
{

    
    public class ParameterVariableDeclaration : VariableDeclaration
    {
        public BlockHeaderVariableDeclaration? HeaderReference { get; private set; }
        private readonly List<string> explicitParameterNames = new();

        internal IReadOnlyList<string> ExplicitParameterNames => explicitParameterNames;

        // Snapshot restoration deliberately bypasses setters and BuildValue: those paths
        // rebuild text, change provenance and may add parameters.
        internal static ParameterVariableDeclaration FromSnapshot(string mechanism,
            IEnumerable<KeyValuePair<string, string>> parameters, IEnumerable<string> explicitNames,
            string value, BlockHeaderVariableDeclaration? header)
        {
            var result = new ParameterVariableDeclaration { _mechanism = mechanism };
            result.ParameterTypeValue = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            result.explicitParameterNames.AddRange(explicitNames);
            result.Value = value;
            result.HeaderReference = header;
            return result;
        }

        // Private staging preserves declaration provenance without parsing serialized values.
        internal ParameterVariableDeclaration StagePinTranslation()
        {
            var stage = new ParameterVariableDeclaration { Id = Id, _mechanism = _mechanism };
            stage.ParameterTypeValue = new Dictionary<string, string>(ParameterTypeValue);
            stage.explicitParameterNames.AddRange(explicitParameterNames);
            stage.Value = Value;
            return stage;
        }

        internal static void CommitPinTranslation(ParameterVariableDeclaration source,
            ParameterVariableDeclaration sourceStage, ParameterVariableDeclaration target,
            ParameterVariableDeclaration targetStage)
        {
            // All allocation, validation and virtual setters run on private stages before here.
            // The interpreter is synchronous. These non-throwing reference assignments publish
            // the pair without callbacks or fallible SetParameter/rollback operations.
            source.PublishPinTranslation(sourceStage);
            target.PublishPinTranslation(targetStage);
        }

        private void PublishPinTranslation(ParameterVariableDeclaration stage)
        {
            ParameterTypeValue = stage.ParameterTypeValue;
            base.Value = stage.Value;
        }

        internal void RecordExplicitParameter(string type)
        {
            string normalizedType = type.StartsWith('#') ? type : "#" + type;
            string canonicalType = ParameterTypeList.Instance.ParameterTypes.Find(
                item => item.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
                ?? normalizedType;
            explicitParameterNames.Add(canonicalType);
        }

        public void SetInstance(string parameters)
        {            
            MechanismParameterContractValidator.ValidateSerializedEmvCryptogramParameters(parameters);
            MechanismParameterContractValidator.ValidateSerializedEmvMasterParameters(parameters);
            
            // Split by '#' and remove empty entries
            var parts = parameters.Split('#', StringSplitOptions.RemoveEmptyEntries);  
            foreach (var part in parts)
            {
                // Split each part by ':' to separate key and value
                var kv = part.Split(':', 2);
                if (kv.Length == 2)
                {
                    if (kv[0] == "MECH")
                        Mechanism = kv[1];
                    var key = "#" + kv[0];    // Add '#' back to the key                    
                    var value = kv[1];
                    SetParameter(key, value);
                    RecordExplicitParameter(key);

                }
            }

            MechanismParameterContractValidator.ValidatePsnSupport(Mechanism, this);
            MechanismParameterContractValidator.ValidateEmvCryptogramParameterSupport(Mechanism, this);
         }
        string _mechanism = string.Empty;
        public string Mechanism 
        {
            get { return _mechanism; }
            set { _mechanism = value;SetParameter("MECH",_mechanism); }
        } 
        
        
        
        Dictionary<string, string> ParameterTypeValue = new Dictionary<string, string>();
        public Dictionary<string, string> GetParameters() 
        {
            return ParameterTypeValue;
        }
        public ParameterVariableDeclaration() 
        {
            Type = new CryptoTypeParameters();
            ValueFormat = FormatConversions.PAR;
        }
        private void BuildValue() 
        {
            ParameterMechanismen(Mechanism);
            //_value = string.Empty;
            Value= string.Empty;
            foreach (var item in ParameterTypeValue)
            {
                Value += item.Key + ":" + item.Value;
            }
            
        }
        private void ParameterMechanismen(string mech)
        {
            if (mech.Contains("#"))
            {
                var kv = mech.Split(':', 2);
                Mechanism = kv[1];
            }
            if (!ParameterTypeValue.ContainsKey("#MECH"))
                ParameterTypeValue.Add("#MECH", mech);
            
        }
        public void SetParameter(string type, string value)
        {
            string normalizedType = type.StartsWith('#') ? type : "#" + type;
            string? t = ParameterTypeList.Instance.ParameterTypes.Find(
                item => item.Equals(normalizedType, StringComparison.OrdinalIgnoreCase));
            if (t == null)
                throw new ArgumentException($"Unknown parameter name '{type}'.", nameof(type));
            if (t == "#BLKH") HeaderReference = null;

            if (ParameterTypeValue.ContainsKey(t))
            {
                ParameterTypeValue[t] = value;
            }
            else
            {
                ParameterTypeValue.Add(t, value);
            }
            BuildValue();
        }
        public void SetParameter(string parameter)
        {
            string[] parts = parameter.Split(':');
            if (parts.Length != 2)
                throw new ArgumentException("Error: invalid parameter format");
            SetParameter(parts[0], parts[1]);
        }
        public string GetParameter(string type)
        {
            string normalizedType = type.StartsWith('#') ? type : "#" + type;
            string? t = ParameterTypeList.Instance.ParameterTypes.Find(
                item => item.Equals(normalizedType, StringComparison.OrdinalIgnoreCase));
            if (t != null && ParameterTypeValue.ContainsKey(t))
            {
                return ParameterTypeValue[t];
            }
            else
            {
                return string.Empty;
            }
        }

        public override string PrintOutput()
        {
            BuildValue();
            return Value;
        }
        public void SetParameter(ArgumentParameter p)
        {
            SetParameter(p.Type, p.Value);
            if (p.Type.Equals("#BLKH", StringComparison.OrdinalIgnoreCase))
                HeaderReference = p.HeaderReference;

        }
        public void ParseToDictionary(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;
            ParameterTypeValue = new Dictionary<string, string>();      
            var segments = input.Split('#', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {                
                int colonIndex = segment.IndexOf(':');
                if (colonIndex <= 0 || colonIndex == segment.Length - 1)
                    continue;

                // Key wieder mit '#' herstellen
                string key = "#" + segment.Substring(0, colonIndex).Trim();
                string value = segment.Substring(colonIndex + 1).Trim();
                ParameterTypeValue[key] = value;
            }            
        }

    }
}
