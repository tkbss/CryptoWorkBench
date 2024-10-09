using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class StatusViewModel : ViewModelBase
    {
        string _statusString=string.Empty;
        public string StatusString { get => _statusString; set => SetProperty(ref _statusString, value); }
        public StatusViewModel()
        {
            StatusString = "bla";
        }
    }
}
