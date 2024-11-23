using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class ParameterVariablesViewModel :ViewModelBase
    {
        ObservableCollection<ParameterVariableModel> _parameterVariables = new ObservableCollection<ParameterVariableModel>();
        public ObservableCollection<ParameterVariableModel> ParameterVariables
        {
            get => _parameterVariables;
            set => SetProperty(ref _parameterVariables, value);
        }
        public void Clear()
        {
            ParameterVariables = new ObservableCollection<ParameterVariableModel>();
        }
        public void Add(VariableDeclaration parameterVariable)
        {
            if (parameterVariable.Type is CryptoTypeParameters)
            {
                if (ParameterVariables.Where(x => x.Identifier == parameterVariable.Id).FirstOrDefault() != null)
                {
                    return;
                }
                var k = parameterVariable as ParameterVariableDeclaration;
                if ((k == null))
                    return;

                var dv = new ParameterVariableModel() { Identifier = parameterVariable.Id, Value = parameterVariable.Value, Mechanism = k.Mechanism};
                ParameterVariables.Add(dv);
            }

        }
        public void Remove(VariableDeclaration parameterVariable)
        {
            if (parameterVariable.Type is CryptoTypeKey)
            {
                var dv = ParameterVariables.Where(x => x.Identifier == parameterVariable.Id).FirstOrDefault();
                if (dv != null)
                {
                    ParameterVariables.Remove(dv);
                }
            }
        }
    }
}
