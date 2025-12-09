using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Models
{
    public class VariableModel: ViewModelBase
    {
        ObservableCollection<ParameterModel> parameters = new ObservableCollection<ParameterModel>();
        public ObservableCollection<ParameterModel> Parameters
        {
            get => parameters;
            set => SetProperty(ref parameters, value);
        }
        string _mechanism = string.Empty;
        public string Mechanism
        {
            get => _mechanism;
            set => SetProperty(ref _mechanism, value);
        }
        string _gmac = string.Empty;
        public string GMAC
        {
            get => _gmac;
            set => SetProperty(ref _gmac, value);
        }
        string _type = string.Empty;
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }
        string _identifier = string.Empty;
        public string Identifier
        {
            get => _identifier;
            set => SetProperty(ref _identifier, value);
        }
        string _value = string.Empty;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
                
        }
        string _valueFormat = string.Empty;
        public string ValueFormat
        {
            get => _valueFormat;
            set => SetProperty(ref _valueFormat, value);
        }
        string _keySize = string.Empty;
        public string KeySize
        {
            get => _keySize;
            set => SetProperty(ref _keySize, value);
        }
       
    }
}
