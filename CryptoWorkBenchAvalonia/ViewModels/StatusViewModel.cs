using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CryptoScript.ErrorListner;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services.Automation;
using CryptoWorkBenchAvalonia.Shell;
using CryptoWorkBench.Automation.Abstractions;
using System.Globalization;

namespace CryptoWorkBenchAvalonia.ViewModels
{
    public class StatusViewModel : ViewModelBase
    {
        string _statusString=string.Empty;
        private readonly ObservableCollection<DiagnosticDisplayItem> diagnostics = new();
        private readonly ObservableCollection<DiagnosticDisplayItem> visibleDiagnostics = new();
        private readonly HashSet<Guid> published = new();
        private int epoch;
        private DiagnosticFilter filter;
        public ReadOnlyObservableCollection<DiagnosticDisplayItem> Diagnostics { get; }
        public ReadOnlyObservableCollection<DiagnosticDisplayItem> VisibleDiagnostics { get; }
        public bool HasDiagnostics => diagnostics.Count > 0;
        public DiagnosticFilter Filter
        {
            get => filter;
            set { if (SetProperty(ref filter, value)) Publish(RefreshVisible); }
        }
        private void RefreshVisible()
        {
            visibleDiagnostics.Clear();
            foreach (var item in diagnostics)
                if (Filter == DiagnosticFilter.All || (Filter == DiagnosticFilter.Warnings ? item.IsWarning : item.IsError))
                    visibleDiagnostics.Add(item);
        }

        public void ClearDiagnostics() => Publish(() => { epoch++; published.Clear(); diagnostics.Clear(); });

        internal void AppendExecution(ExecutionPresentation result, OutputSource? source, int? bookLine, Func<bool> current)
        {
            if (!current() || !published.Add(result.PresentationId)) return;
            int generation = epoch;
            bool Current() => generation == epoch && current();
            void Add(DiagnosticCategory category, SemanticDiagnosticSeverity severity, string text, int? line = null, int? column = null)
            {
                if (!Current()) return;
                Publish(() =>
                {
                    if (Current()) diagnostics.Add(new(category, severity, text, source?.DocumentId,
                        result.ExecutionId == Guid.Empty ? null : result.ExecutionId, source?.Version,
                        line, column, diagnostics.Count + 1, bookLine));
                });
            }
            foreach (var syntax in result.SyntaxDiagnostics)
            {
                int? line = null, column = null;
                if (source?.FirstLine is > 0 && int.TryParse(syntax.Line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int localLine) && localLine > 0)
                {
                    long mapped = (long)source.FirstLine.Value + localLine - 1;
                    if (mapped <= int.MaxValue) line = (int)mapped;
                    if (line.HasValue && int.TryParse(syntax.Column, NumberStyles.Integer, CultureInfo.InvariantCulture, out int localColumn) && localColumn >= 0 && localColumn < int.MaxValue)
                        column = localColumn + 1;
                }
                Add(DiagnosticCategory.SyntaxError, SemanticDiagnosticSeverity.Error,
                    $"SYNTAX ERROR: Token {syntax.Symbol} (line {syntax.Line}, column {syntax.Column}) : {syntax.Message}", line, column);
            }
            bool exceptionSeen = false;
            void Semantic(SemanticError item) => Add(item.Severity == SemanticDiagnosticSeverity.Warning
                    ? DiagnosticCategory.SemanticWarning : DiagnosticCategory.SemanticError,
                item.Severity, SemanticDiagnosticFormatter.Format(item),
                bookLine.HasValue && source?.FirstLine is > 0 ? source.FirstLine : null);
            foreach (var item in result.Diagnostics)
            {
                exceptionSeen |= ReferenceEquals(item, result.Error);
                Semantic(item);
            }
            if (result.Error != null && !exceptionSeen) Semantic(result.Error);
            if (result.Outcome == ExecutionState.Failed)
                Add(DiagnosticCategory.ExecutionError, SemanticDiagnosticSeverity.Error, result.Status);
            if (result.CaptureFailed)
                Add(DiagnosticCategory.ExecutionError, SemanticDiagnosticSeverity.Error,
                    "Variable state capture failed; displayed variables are stale.");
        }

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
            VisibleDiagnostics = new(visibleDiagnostics);
            diagnostics.CollectionChanged += (_, _) => { RefreshVisible(); RaisePropertyChanged(nameof(HasDiagnostics)); };
            StatusString = "Ready";
        }
    }
}
