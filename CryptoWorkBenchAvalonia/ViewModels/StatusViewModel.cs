using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CryptoScript.ErrorListner;
using CryptoWorkBenchAvalonia.Models;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class StatusViewModel : ViewModelBase
    {
        string _statusString=string.Empty;
        private readonly ObservableCollection<DiagnosticDisplayItem> diagnostics = new();
        public ReadOnlyObservableCollection<DiagnosticDisplayItem> Diagnostics { get; }
        public bool HasDiagnostics => diagnostics.Count > 0;

        public void ClearDiagnostics() => Publish(diagnostics.Clear);

        /// <summary>Append in source order; only an already transported exception instance is omitted.</summary>
        public void AppendDiagnostics(IEnumerable<SemanticError> source,
            SemanticError? exceptionDiagnostic = null, int? scriptBookLine = null) =>
            AppendDiagnostics(source, exceptionDiagnostic, scriptBookLine, null);

        internal void AppendDiagnostics(IEnumerable<SemanticError> source, SemanticError? exceptionDiagnostic,
            int? scriptBookLine, Func<bool>? current)
        {
            ArgumentNullException.ThrowIfNull(source);
            bool exceptionSeen = false;
            foreach (var diagnostic in source)
            {
                if (current?.Invoke() == false) return;
                exceptionSeen |= ReferenceEquals(diagnostic, exceptionDiagnostic);
                Publish(() => diagnostics.Add(new DiagnosticDisplayItem(diagnostic, scriptBookLine)));
            }
            if (exceptionDiagnostic is not null && !exceptionSeen && current?.Invoke() != false)
                Publish(() => diagnostics.Add(new DiagnosticDisplayItem(exceptionDiagnostic, scriptBookLine)));
        }

        public string StatusString { get => _statusString; set => SetProperty(ref _statusString, value); }
        public StatusViewModel()
        {
            Diagnostics = new ReadOnlyObservableCollection<DiagnosticDisplayItem>(diagnostics);
            diagnostics.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasDiagnostics));
            StatusString = "bla";
        }
    }
}
