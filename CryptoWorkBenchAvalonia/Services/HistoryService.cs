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
        private Func<string, Action, bool>? lineNavigation;

        internal IDisposable BindLineNavigation(Func<string, Action, bool> navigate)
        {
            if (lineNavigation is not null) throw new InvalidOperationException("Line history already has an editor.");
            lineNavigation = navigate;
            return new NavigationBinding(this, navigate);
        }

        private sealed class NavigationBinding(HistoryService owner, Func<string, Action, bool> navigate) : IDisposable
        {
            public void Dispose()
            {
                if (owner.lineNavigation == navigate) owner.lineNavigation = null;
            }
        }

        private void NavigateLine(int next)
        {
            if (next < 0 || next >= _lineHistory.Count || next == _currentIndexLine) return;
            // No bound editor means no index movement. The callback must admit
            // the entire operation before invoking this one-shot continuation.
            bool consumed = false;
            lineNavigation?.Invoke(_lineHistory[next], () =>
            {
                if (consumed) throw new InvalidOperationException("History continuation consumed.");
                consumed = true;
                _currentIndexLine = next;
            });
        }

        internal void PublishLine(string entry) => LineHistoryChanged?.Invoke(this, entry);

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
            NavigateLine(_currentIndexLine - 1);
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
            NavigateLine(_currentIndexLine + 1);
        }

        

        

        
    }
}
