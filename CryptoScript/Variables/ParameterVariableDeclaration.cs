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
        private BlockHeaderVariableDeclaration? headerReference;
        internal BlockHeaderVariableDeclaration? BoundHeader => headerReference;
        public BlockHeaderVariableDeclaration? HeaderReference { get => ReadOwned(headerReference); private set => headerReference = value; }
        private List<string> explicitParameterNames = new();

        internal IReadOnlyList<string> ExplicitParameterNames => explicitParameterNames;

        // Some legacy AES factories normalize explicit padding. File operations must
        // still reject an invalid declaration without changing existing VAR behavior.
        private string? originalExplicitPadding;
        internal string? OriginalExplicitPadding { get => ReadOwned(originalExplicitPadding); set => Change(ref originalExplicitPadding, value); }

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
            var stage = FromSnapshot(_mechanism, ParameterTypeValue, explicitParameterNames, Value, headerReference);
            stage.Id = Id;
            stage.OriginalExplicitPadding = OriginalExplicitPadding;
            return stage;
        }

        private bool Matches(ParameterVariableDeclaration stage) =>
            _mechanism == stage._mechanism && Value == stage.Value &&
            headerReference == stage.headerReference && OriginalExplicitPadding == stage.OriginalExplicitPadding &&
            ParameterTypeValue.SequenceEqual(stage.ParameterTypeValue) &&
            explicitParameterNames.SequenceEqual(stage.explicitParameterNames);

        internal static void CommitPinTranslation(ParameterVariableDeclaration source,
            ParameterVariableDeclaration sourceStage, ParameterVariableDeclaration target,
            ParameterVariableDeclaration targetStage)
        {
            lock (InterpreterExecution.Gate)
            {
                source.Owner?.ValidateArgument(source);
                target.Owner?.ValidateArgument(target);
                if (source.Owner != target.Owner) throw new InvalidOperationException("Different PARAM owners.");
                bool sourceChanged = !source.Matches(sourceStage), targetChanged = !target.Matches(targetStage);
                if (!sourceChanged && !targetChanged) return;
                source.Owner?.RecordMutation(source);
                if (sourceChanged) source.Publish(sourceStage);
                if (targetChanged) target.Publish(targetStage);
            }
        }

        private void Publish(ParameterVariableDeclaration stage)
        {
            ParameterTypeValue = stage.ParameterTypeValue;
            _mechanism = stage._mechanism;
            headerReference = stage.headerReference;
            originalExplicitPadding = stage.originalExplicitPadding;
            explicitParameterNames = stage.explicitParameterNames;
            SetPreparedValue(stage.Value);
        }

        private void Mutate(Action<ParameterVariableDeclaration> apply)
        {
            lock (InterpreterExecution.Gate)
            {
                if (Owner == null) { apply(this); return; }
                Owner.ValidateArgument(this);
                var stage = StagePinTranslation();
                apply(stage);
                if (Matches(stage)) return;
                Owner.RecordMutation(this);
                Publish(stage);
            }
        }

        internal void RecordExplicitParameter(string type)
        {
            string normalizedType = type.StartsWith('#') ? type : "#" + type;
            string canonicalType = ParameterTypeList.Instance.ParameterTypes.FirstOrDefault(
                item => item.Equals(normalizedType, StringComparison.OrdinalIgnoreCase))
                ?? normalizedType;
            Mutate(stage => stage.explicitParameterNames.Add(canonicalType));
        }

        public void SetInstance(string parameters) => Mutate(stage => stage.ApplyInstance(parameters));

        private void ApplyInstance(string parameters)
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
            get { return ReadOwned(_mechanism); }
            set => Mutate(stage =>
            {
                stage._mechanism = value;
                stage.ApplyParameter("MECH", value, null);
            });
        } 
        
        
        
        Dictionary<string, string> ParameterTypeValue = new Dictionary<string, string>();
        public Dictionary<string, string> GetParameters() 
        {
            var map = ReadOwned(ParameterTypeValue);
            return Owner != null ? new(map, StringComparer.Ordinal) : map;
        }
        internal void DetachParameters() => ParameterTypeValue = new(ParameterTypeValue, StringComparer.Ordinal);

        public ParameterVariableDeclaration() 
        {
            Type = new CryptoTypeParameters();
            ValueFormat = FormatConversions.PAR;
        }
        private void BuildValue() 
        {
            ParameterMechanismen(Mechanism);
            //_value = string.Empty;
            Value = string.Concat(ParameterTypeValue.Select(item => item.Key + ":" + item.Value));
            
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
        public void SetParameter(string type, string value) => SetParameter(type, value, null);

        private void SetParameter(string type, string value, BlockHeaderVariableDeclaration? reference)
        {
            lock (InterpreterExecution.Gate)
            {
                if (reference != null) Owner?.ValidateArgument(reference);
                Mutate(stage => stage.ApplyParameter(type, value, reference));
            }
        }

        private void ApplyParameter(string type, string value, BlockHeaderVariableDeclaration? reference)
        {
            string normalizedType = type.StartsWith('#') ? type : "#" + type;
            string? t = ParameterTypeList.Instance.ParameterTypes.FirstOrDefault(
                item => item.Equals(normalizedType, StringComparison.OrdinalIgnoreCase));
            if (t == null)
                throw new ArgumentException($"Unknown parameter name '{type}'.", nameof(type));
            if (t == "#BLKH" && HeaderReference != reference)
            {
                HeaderReference = reference;
            }

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
            ReadOwned(ParameterTypeValue);
            string normalizedType = type.StartsWith('#') ? type : "#" + type;
            string? t = ParameterTypeList.Instance.ParameterTypes.FirstOrDefault(
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
            lock (InterpreterExecution.Gate)
            {
                var map = ReadOwned(ParameterTypeValue);
                string text = string.Concat(map.Select(p => p.Key + ":" + p.Value));
                return map.ContainsKey("#MECH") ? text : text + "#MECH:" + Mechanism;
            }
        }
        public void SetParameter(ArgumentParameter p)
        {
            SetParameter(p.Type, p.Value, p.HeaderReference);

        }
        public void ParseToDictionary(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return;
            Owner?.ValidateArgument(this);
            var prepared = new Dictionary<string, string>();
            var segments = input.Split('#', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {                
                int colonIndex = segment.IndexOf(':');
                if (colonIndex <= 0 || colonIndex == segment.Length - 1)
                    continue;

                // Key wieder mit '#' herstellen
                string key = "#" + segment.Substring(0, colonIndex).Trim();
                string value = segment.Substring(colonIndex + 1).Trim();
                prepared[key] = value;
            }
            ReplaceParameters(prepared);
        }

        // Accept an independent metadata snapshot. Retained caller collections cannot mutate a live root.
        public void ReplaceParameters(IReadOnlyDictionary<string, string> parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            var prepared = new Dictionary<string, string>(parameters, StringComparer.Ordinal);
            lock (InterpreterExecution.Gate)
            {
                Owner?.ValidateArgument(this);
                if (!ParameterTypeValue.SequenceEqual(prepared))
                {
                    Owner?.RecordMutation(this);
                    if (ParameterTypeValue.GetValueOrDefault("#BLKH") != prepared.GetValueOrDefault("#BLKH"))
                        HeaderReference = null;
                    ParameterTypeValue = prepared;
                }
            }            
        }

    }
}
