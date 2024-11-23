using CryptoWorkBenchAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Models
{
    public class ParameterVariableModel: ViewModelBase
    {
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
        string _mechanism = string.Empty;
        public string Mechanism
        {
            get => _mechanism;
            set => SetProperty(ref _mechanism, value);
        }
        
    }
}
