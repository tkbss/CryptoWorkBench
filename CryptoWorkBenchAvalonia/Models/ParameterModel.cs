using CryptoWorkBenchAvalonia.ViewModels;


namespace CryptoWorkBenchAvalonia.Models
{
    public class ParameterModel: ViewModelBase
    {
        string _parameter = string.Empty;
        public string Parameter
        {
            get => _parameter;
            set => SetProperty(ref _parameter, value);
        }
        string _value = string.Empty;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        
        
    }
}
