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
    public class KeyVariablesViewModel:ViewModelBase
    {
        ObservableCollection<KeyVariableModel> _keyVariables = new ObservableCollection<KeyVariableModel>();
        public ObservableCollection<KeyVariableModel> KeyVariables
        {
            get => _keyVariables;
            set => SetProperty(ref _keyVariables, value);
        }
        KeyVariableModel? _tk;
        public KeyVariableModel TestKey
        {
            get => _tk;
            set => SetProperty(ref _tk, value);
        }
        string _identifier = "string.Empty";
        public string Identifier
        {
            get => _identifier;
            set => SetProperty(ref _identifier, value);
        }
        public void Clear()
        {
            KeyVariables = new ObservableCollection<KeyVariableModel>();
        }
        public void Add(VariableDeclaration keyVariable)
        {
            if (keyVariable.Type is CryptoTypeKey)
            {
                if (KeyVariables.Where(x => x.Identifier == keyVariable.Id).FirstOrDefault() != null)
                {
                    return;
                }
                var k = keyVariable as KeyVariableDeclaration;
                if ((k == null))
                    return;                   
                
                var dv = new KeyVariableModel() { Identifier = keyVariable.Id, Value = keyVariable.Value, Mechanism=k.Mechanism, KeySize=k.KeySize };
                KeyVariables.Add(dv);
                TestKey = dv;   
            }

        }
        public void Remove(VariableDeclaration keyVariable)
        {
            if (keyVariable.Type is CryptoTypeKey)
            {
                var dv = KeyVariables.Where(x => x.Identifier == keyVariable.Id).FirstOrDefault();
                if (dv != null)
                {
                    KeyVariables.Remove(dv);
                }
            }
        }
    }
}
