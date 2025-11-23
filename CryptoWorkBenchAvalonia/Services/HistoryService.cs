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
        private int _currentIndex = -1;

        public event EventHandler<string>? HistoryChanged;

        public bool CanBack => _currentIndex > 0;
        public bool CanForward => _currentIndex < _infoHistory.Count - 1;

        public void AddInfo(string entry)
        {
            _infoHistory.Add(entry);
            _currentIndex = _infoHistory.Count - 1;
        }

        public void Back()
        {
            if (!CanBack) return;
            var value = _infoHistory[--_currentIndex];
            HistoryChanged?.Invoke(this, value);
        }

        public void Forward()
        {
            if (!CanForward) return;
            var value = _infoHistory[++_currentIndex];
            HistoryChanged?.Invoke(this, value);
        }
    }
}
