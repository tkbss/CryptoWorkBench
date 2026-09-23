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
        string _algorithm = string.Empty;
        public string Algorithm
        {
            get => _algorithm;
            set => SetProperty(ref _algorithm, value);
        }
        string _material = string.Empty;
        public string Material
        {
            get => _material;
            set => SetProperty(ref _material, value);
        }
        string _derivation = string.Empty;
        public string Derivation
        {
            get => _derivation;
            set => SetProperty(ref _derivation, value);
        }
        bool _isKey;
        public bool IsKey
        {
            get => _isKey;
            set => SetProperty(ref _isKey, value);
        }
        bool _isParameter;
        public bool IsParameter
        {
            get => _isParameter;
            set => SetProperty(ref _isParameter, value);
        }
        bool _hasKeySize;
        public bool HasKeySize
        {
            get => _hasKeySize;
            set => SetProperty(ref _hasKeySize, value);
        }
        bool _hasDerivation;
        public bool HasDerivation
        {
            get => _hasDerivation;
            set => SetProperty(ref _hasDerivation, value);
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
