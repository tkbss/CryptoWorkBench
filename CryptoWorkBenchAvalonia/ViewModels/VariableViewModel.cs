using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryptoScript.Model;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class VariableViewModel:ViewModelBase
    {
        internal Func<bool>? IsExecutionActive { get; set; }
        public VariableViewModel() 
        {
            ModifyVariableCommand = new DelegateCommand<ParameterModel>(OnModifyVariable);
            ModifyValueCommand = new DelegateCommand<VariableModel>(OnModifyValue);
        }
        private void OnModifyValue(VariableModel? obj)
        {
            if (IsExecutionActive?.Invoke() == true) return;
            IDisposable lease;
            try { lease = InterpreterExecution.Enter(); }
            catch (ExecutionBusyException) { return; }
            using var ownedLease = lease;
            if (obj == null)
                return;
            if(SelectedVariable == null)
                return;
            var v = VariableDictionary.Instance().GetVariables();
            var p = v.FirstOrDefault(x => x.Id == SelectedVariable!.Identifier && x.Type!.Name == SelectedVariable.Type);
            if (p != null)
            {
                p.Value = SelectedVariable.Value;
                if(p is ParameterVariableDeclaration pv)
                {
                    pv.ParseToDictionary(p.Value);
                }
                DataVariables = new ObservableCollection<VariableModel>();
                SetupVariables();                    
            }
        }
        private void OnModifyVariable(ParameterModel? item)
        {
            if (IsExecutionActive?.Invoke() == true) return;
            IDisposable lease;
            try { lease = InterpreterExecution.Enter(); }
            catch (ExecutionBusyException) { return; }
            using var ownedLease = lease;
            if (item == null)
                return;
            var v = VariableDictionary.Instance().GetVariables();
            var p = v.FirstOrDefault(x => x.Id == SelectedVariable!.Identifier && x.Type!.Name == SelectedVariable.Type);
            if(p != null && p is ParameterVariableDeclaration pv)
            {
                pv.SetParameter(item.Parameter, item.Value);
                VariableDictionary.Instance().Update(pv);
                DataVariables = new ObservableCollection<VariableModel>();
                SetupVariables();
                
            }
        }
        public DelegateCommand<ParameterModel> ModifyVariableCommand { get; }
        public DelegateCommand<VariableModel> ModifyValueCommand { get; }

        public void SetupVariables()
        {
            using var lease = InterpreterExecution.Enter();
            ApplyVariables(CaptureVariables());
        }

        internal void ApplyVariables(IReadOnlyList<VariableModel> snapshot)
        {
            if (DataVariables.Count != 0) DataVariables.Clear();
            foreach (var variable in snapshot) DataVariables.Add(variable);
        }

        internal static IReadOnlyList<VariableModel> CaptureVariables()
        {
            InterpreterExecution.VerifyAccess();
            var snapshot = new List<VariableModel>();
            foreach (var v in VariableDictionary.Instance().GetVariables())
            {
                var dv = new VariableModel()
                {
                    Type = v.Type?.Name ?? string.Empty,
                    Identifier = v.Id,
                    Value = v.Value,
                    ValueFormat = v.ValueFormat
                };
                switch (v)
                {
                    case KeyVariableDeclaration a:
                        dv.IsKey = true;
                        dv.Algorithm = FormatAlgorithm(a.KeyType.Algorithm);
                        dv.Material = a.KeyType.MaterialKind.ToString();
                        dv.HasKeySize = a.KeySizeInBits.IsKnown;
                        dv.KeySize = a.KeySizeInBits.IsKnown
                            ? a.KeySizeInBits.Bits.ToString(CultureInfo.InvariantCulture)
                            : string.Empty;
                        dv.Derivation = a.DerivationMechanism;
                        dv.HasDerivation = !string.IsNullOrWhiteSpace(a.DerivationMechanism);
                        break;
                    case StringVariableDeclaration b:                       
                        dv.GMAC = b.GMAC;
                        break;
                     case ParameterVariableDeclaration c:
                        dv.IsParameter = true;
                        dv.Mechanism = c.Mechanism;
                        var parameters = c.GetParameters();
                        foreach (var param in parameters)
                        { 
                            
                            dv.Parameters.Add(new ParameterModel
                            {
                                Parameter = param.Key,
                                Value = param.Value
                            });
                        }
                        break;
                }
                snapshot.Add(dv);
            }
            return snapshot;
        }
        private static string FormatAlgorithm(KeyAlgorithm algorithm) => algorithm switch
        {
            KeyAlgorithm.Aes => "AES",
            KeyAlgorithm.Tdea => "TDEA",
            KeyAlgorithm.Hmac => "HMAC",
            KeyAlgorithm.Rsa => "RSA",
            KeyAlgorithm.Ec => "EC",
            _ => "Unknown"
        };
        ObservableCollection<VariableModel> _dataVariables = new ObservableCollection<VariableModel>();
        public ObservableCollection<VariableModel> DataVariables
        {
            get => _dataVariables;
            set => SetProperty(ref _dataVariables, value);
        }
        private VariableModel? _selectedVariable;
        public VariableModel? SelectedVariable
        {
            get => _selectedVariable;
            set => SetProperty(ref _selectedVariable, value);
        }
    }
}
