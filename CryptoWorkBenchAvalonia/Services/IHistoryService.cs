using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services
{
    public interface IHistoryService
    {
        bool CanBack { get; }
        bool CanForward { get; }

        void Back();
        void Forward();
        void AddInfo(string entry);

        event EventHandler<string>? HistoryChanged;
    }
}
