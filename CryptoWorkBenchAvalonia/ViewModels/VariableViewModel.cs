using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class VariableViewModel:ViewModelBase
    {
        public VariableViewModel() 
        {
            ModifyVariableCommand = new DelegateCommand<ParameterModel>(OnModifyVariable);
        }
        private void OnModifyVariable(ParameterModel? item)
        {
            if (item == null)
                return;
            var v = VariableDictionary.Instance().GetVariables();
            var p = v.FirstOrDefault(x => x.Id == SelectedVariable!.Identifier && x.Type!.Name == SelectedVariable.Type);
            if(p != null && p is ParameterVariableDeclaration pv)
            {
                pv.SetParameter(item.Parameter, item.Value);
                _dataVariables = new ObservableCollection<VariableModel>();
                SetupVariables();   
            }
        }
        public DelegateCommand<ParameterModel> ModifyVariableCommand { get; }
        public void SetupVariables()
        {

            foreach (var v in VariableDictionary.Instance().GetVariables())
            {
                if (DataVariables.Any(x => x.Identifier == v.Id && x.Type == v.Type!.Name))
                    continue;
                var dv = new VariableModel()
                {
                    Type = v.Type!.Name,
                    Identifier = v.Id,
                    Value = v.Value,
                    ValueFormat = v.ValueFormat
                };
                switch (v)
                {
                    case KeyVariableDeclaration a:
                        dv.KeySize = a.KeySize;
                        dv.Mechanism = a.Mechanism;
                        break;
                    case StringVariableDeclaration b:                       
                        dv.GMAC = b.GMAC;
                        break;
                     case ParameterVariableDeclaration c:
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
                DataVariables.Add(dv);
            }
        }
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
