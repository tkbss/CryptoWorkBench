using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services
{
    public interface IHistoryService
    {
        bool InfoCanBack { get; }
        bool InfoCanForward { get; }
        bool LineCanBack { get; }
        bool LineCanForward { get; }

        void InfoBack();
        void InfoForward();
        void LineBack();
        void LineForward();
        void AddInfo(string entry);
        void AddLine(string entry);

        event EventHandler<string>? InfoHistoryChanged;
        event EventHandler<string>? LineHistoryChanged;
    }
}
