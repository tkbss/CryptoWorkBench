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
    public class DataVariablesViewModel :ViewModelBase
    {
        ObservableCollection<VariableModel> _dataVariables=new ObservableCollection<VariableModel>();
        public ObservableCollection<VariableModel> DataVariables
        {
            get => _dataVariables;
            set => SetProperty(ref _dataVariables, value);
        }
        public void Clear()
        {
            DataVariables= new ObservableCollection<VariableModel>(); 
        }
        public void Add(VariableDeclaration dataVariable)
        {
            if(dataVariable.Type == null)
            {
                return;
            }
            if (DataVariables.Where(x => x.Identifier == dataVariable.Id && x.Type== dataVariable.Type.Name).FirstOrDefault() != null)
            {
                return;
            }
            var dv = new VariableModel()
            {
                Type = dataVariable.Type.Name,
                Identifier = dataVariable.Id,
                Value = dataVariable.Value,
                ValueFormat = dataVariable.ValueFormat
            };
            DataVariables.Add(dv);
            
            
        }
        public void Remove(VariableDeclaration dataVariable)
        {
            if (dataVariable.Type is CryptoTypeVar d)
            {
                var dv = DataVariables.Where(x => x.Identifier == dataVariable.Id).FirstOrDefault();
                if (dv != null)
                {
                    DataVariables.Remove(dv);
                }
            }
        }
    }
}
