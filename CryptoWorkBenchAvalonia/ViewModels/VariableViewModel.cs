using CryptoScript.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class VariableViewModel:ViewModelBase
    {
        ParameterVariablesViewModel _parameterVariables = new ParameterVariablesViewModel();
        public ParameterVariablesViewModel ParameterVariables
        {
            get => _parameterVariables;
            set => SetProperty(ref _parameterVariables, value);
        }
        DataVariablesViewModel _dataVariables = new DataVariablesViewModel();
        public DataVariablesViewModel DataVariables
        {
            get => _dataVariables;
            set => SetProperty(ref _dataVariables, value);
        }
        KeyVariablesViewModel _keyVariables = new KeyVariablesViewModel();
        public KeyVariablesViewModel KeyVariables
        {
            get => _keyVariables;
            set => SetProperty(ref _keyVariables, value);
        }
        public void SetupVariables()
        {

            foreach (var v in VariableDictionary.Instance().GetVariables())
            {
                DataVariables.Add(v);
                KeyVariables.Add(v);
                ParameterVariables.Add(v);
            }
        }
    }
}
