using CryptoWorkBenchAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Models
{
    public class DataVariableModel: ViewModelBase
    {
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
    }
}
