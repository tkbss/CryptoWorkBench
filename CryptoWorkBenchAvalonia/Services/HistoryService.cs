using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.Services
{
    public class HistoryService : IHistoryService
    {
        private readonly List<string> _infoHistory = new();
        private readonly List<string> _lineHistory = new();
        private int _currentIndexInfo = -1;
        private int _currentIndexLine = -1;

        public event EventHandler<string>? InfoHistoryChanged;
        public event EventHandler<string>? LineHistoryChanged;

        public bool InfoCanBack => _currentIndexInfo > 0;
        public bool InfoCanForward => _currentIndexInfo < _infoHistory.Count - 1;

        public bool LineCanBack => _currentIndexLine > 0;

        public bool LineCanForward => _currentIndexLine < _lineHistory.Count - 1;

        public void AddInfo(string entry)
        {
            _infoHistory.Add(entry);
            _currentIndexInfo = _infoHistory.Count - 1;
        }
        public void AddLine(string entry)
        {
            _lineHistory.Add(entry);
            _currentIndexLine = _lineHistory.Count - 1;
        }

        public void InfoBack()
        {
            if (!InfoCanBack) return;
            var value = _infoHistory[--_currentIndexInfo];
            InfoHistoryChanged?.Invoke(this, value);
        }
        public void LineBack()
        {
            if (!LineCanBack) return;
            var value = _lineHistory[--_currentIndexLine];
            LineHistoryChanged?.Invoke(this, value);
        }

        public void InfoForward()
        {
            if (!InfoCanForward) return;
            var value = _infoHistory[++_currentIndexInfo];
            InfoHistoryChanged?.Invoke(this, value);
        }
        public void LineForward()
        {
            if (!LineCanForward) return;
            var value = _lineHistory[++_currentIndexLine];
            LineHistoryChanged?.Invoke(this, value);
        }

        

        

        
    }
}
