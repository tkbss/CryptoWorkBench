using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CryptoWorkBenchAvalonia.Services.Inspector;

namespace CryptoWorkBenchAvalonia.ViewModels;

internal sealed class VariableInspectorViewModel : IDisposable
{
    private readonly InspectorSession session;
    private InspectorSnapshot? displayed;
    private readonly Dictionary<string,int> slots = new(StringComparer.Ordinal);
    private ImmutableArray<InspectorStep> path = [];
    private int pageNumber;
    private bool working, disposed;
    private CancellationTokenSource? action;
    internal VariableInspectorViewModel(VariableViewModel variables)
    {
        session = new(variables, propertyInspector: true);
        session.StateChanged += Update;
        session.WorkspaceReset += CancelAction;
    }
    internal event Action? Changed;
    internal string Title => displayed?.Stamp.Identifier ?? "Select a variable";
    internal string Location => DisplayTextPreview.Join(" › ", path.Select(s => s.DisplayName));
    internal int Depth => path.Length;
    internal string AncestorName(int depth) => depth == 0 ? Title :
        path[depth-1].DisplayName;
    internal bool CanBack => path.Length > 0;
    internal int PageNumber => pageNumber;
    internal CancellationToken ActionCancellation => action?.Token ?? default;
    internal bool Busy => working || session.Loading;
    internal string Status => disposed ? "" : DisplayTextPreview.Join(" · ", new[] { session.Historical ? "Historical" : "Snapshot",
        displayed == null ? null : $"R{displayed.Stamp.Revision}", PagePart?.CapturedAt.ToString("T"),
        session.Progress is { } p && Busy ? $"{p.Completed:N0} loaded" : null,
        Busy ? "Loading" : null, session.Error, session.CleanupError, Message }.Where(s => !string.IsNullOrEmpty(s)));
    internal string? Message { get; private set; }
    internal string? Detail { get; private set; }
    internal InspectorPage? Page => PagePart?.Page;
    private SnapshotPart? PagePart => Find(InspectorMember.PropertyPage, Slot(path,pageNumber));
    private int Slot(ImmutableArray<InspectorStep> steps, int page)
    {
        if (steps.IsEmpty && page == 0) return 0;
        string key = page + ":" + InspectorProjector.Key(steps);
        if (!slots.TryGetValue(key,out int id)) slots.Add(key,id = slots.Count + 1);
        return id;
    }
    private SnapshotPart? Find(InspectorMember member,int id) =>
        displayed?.Parts.FirstOrDefault(p => p.Member == member && p.ShareIndex == id);
    private void Update()
    {
        if (!ReferenceEquals(displayed,session.Snapshot))
        {
            displayed = session.Snapshot; path = []; pageNumber = 0; slots.Clear();
            Detail = null; Message = null;
        }
        Changed?.Invoke();
    }
    private void CancelAction() => action?.Cancel();
    internal void Cancel()
    {
        action?.Cancel();
        session.CancelRead();
    }
    internal bool CanReload => !disposed && !Busy && !session.Historical && displayed != null && Page == null;
    internal Task ReloadAsync() => OpenAsync(null);
    internal bool NeedsCalculation(InspectorRow row) => row.State == "NotCalculated" &&
        (displayed == null || InspectorValueSource.Find(displayed,path.Add(row.Step)) == null);
    internal Task CalculateAsync(InspectorRow row) => NavigateAsync(row,0,false,null,true);
    internal Task OpenAsync(InspectorRow? row, int page = 0, bool back = false, int? ancestor = null) =>
        NavigateAsync(row,page,back,ancestor,false);

    private Task NavigateAsync(InspectorRow? row, int page, bool back, int? ancestor, bool calculate) => Run(async token =>
    {
        CheckRow(row);
        var snapshot = displayed ?? throw new InvalidOperationException("Select a variable.");
        var next = ancestor.HasValue ? path[..ancestor.Value] : back ? path.RemoveAt(path.Length-1) :
            row?.Reference ?? (row == null ? path : path.Add(row.Step));
        var request = new PropertyRequest(next,page,calculate);
        int slot = Slot(next,page);
        string? shown = null;
        if (Find(InspectorMember.PropertyPage,slot)==null)
        {
            InspectorPage? detached = null;
            if (row?.ExactScalar is { } exact)
            {
                detached = ScalarPage(exact,row.ScalarType,request);
                if (session.Historical) shown=exact;
            }
            else if (InspectorValueSource.Find(snapshot,next) is { } source)
            {
                var type = source.Slice?.Type;
                if (source.Slice == null || type == typeof(string) || type == typeof(char) ||
                    type == null || type.IsPrimitive || type.IsEnum)
                {
                    await source.CopyAsync(snapshot,text =>
                    {
                        detached=ScalarPage(text,source.Slice == null ? typeof(string) : type,request);
                        if (session.Historical) shown=text;
                        return Task.CompletedTask;
                    },token);
                }
                else if (session.Historical) detached=InspectorProjector.IndexedPage(source,request);
            }
            if (detached != null)
            {
                VerifySnapshot(snapshot,token);
                await StorePage(snapshot,slot,request,detached,token);
            }
            else
            {
                if (row?.State=="NotCalculated" && !calculate)
                    throw new InvalidOperationException("Calculate this property first.");
                await EnsureAsync(InspectorMember.PropertyPage,slot,request,token);
            }
        }
        VerifySnapshot(snapshot,token);
        path = next; pageNumber = page;
        Detail = shown ?? (session.Historical ? Page?.ExactScalar : null);
    });

    private static InspectorPage ScalarPage(string text,Type? type,PropertyRequest request)
    {
        object? value = type == null ? null : type.IsEnum ? Enum.Parse(type,text) :
            Convert.ChangeType(text,type,CultureInfo.InvariantCulture);
        return InspectorProjector.DetachedPage(value,request);
    }
    private void VerifySnapshot(InspectorSnapshot snapshot,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(displayed,snapshot)) throw new OperationCanceledException("Selection changed.");
    }
    private async Task StorePage(InspectorSnapshot snapshot,int id,PropertyRequest request,InspectorPage page,CancellationToken token)
    {
        using var transfer=session.BeginTransfer();
        var payload = new InspectorPayload();
        try
        {
            var text = page.ExactScalar ?? (page.StringLength.HasValue ?
                string.Concat(page.Rows.Where(r=>r.Step.Kind==StepKind.Entry).Select(r=>r.ExactScalar)) : "");
            await payload.AppendAsync(MemoryMarshal.AsBytes(text.AsSpan()).ToArray(),token);
            await payload.SealAsync(token);
            VerifySnapshot(snapshot,token);
            snapshot.Add(new(snapshot.Stamp,InspectorMember.PropertyPage,id,0,text.Length,text.Length,payload,Page:page,Request:request));
        }
        catch { payload.Dispose(); throw; }
    }
    internal Task ValueAsync(InspectorRow? row, bool calculate, Func<string,Task>? clipboard) => Run(async token =>
    {
        CheckRow(row);
        var next = row?.Reference ?? (row == null ? path : path.Add(row.Step));
        var snapshot = displayed ?? throw new InvalidOperationException("Select a variable.");
        var request = new PropertyRequest(next,Calculate:calculate);
        int id = Slot(next,0);
        var source = InspectorValueSource.Find(snapshot,next);
        if (source == null && row?.ExactScalar is { } exact)
        {
            await StorePage(snapshot,id,request,ScalarPage(exact,row.ScalarType,request),token);
            source = InspectorValueSource.Find(snapshot,next);
        }
        if (source == null)
        {
            await EnsureAsync(InspectorMember.PropertyValue,id,request,token);
            source = InspectorValueSource.Find(snapshot,next) ?? throw new InvalidOperationException("Incomplete property.");
        }
        await source.CopyAsync(snapshot,async text =>
        {
            if (clipboard != null)
            {
                await clipboard(text);
                if (ReferenceEquals(displayed,snapshot)) Message = $"Copied {text.Length:N0} characters · R{snapshot.Stamp.Revision}";
            }
            else if (ReferenceEquals(displayed,snapshot)) Detail = text;
        },token);
    });
    private void CheckRow(InspectorRow? row)
    {
        if (row != null && Page?.Rows.Any(current => ReferenceEquals(current,row)) != true)
            throw new InvalidOperationException("Stale property selection.");
    }
    private async Task<SnapshotPart> EnsureAsync(InspectorMember member,int id,PropertyRequest request,CancellationToken token)
    {
        var old = displayed;
        if (Find(member,id) is { } existing) return existing;
        if (session.Historical) throw new InvalidOperationException("Historical value was not fully captured.");
        await session.ReadAsync(new(member,id,Property:request));
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(displayed,old)) throw new OperationCanceledException();
        return Find(member,id) ?? throw new InvalidOperationException(session.Error ?? "Capture unavailable or busy.");
    }
    private async Task Run(Func<CancellationToken,Task> operation)
    {
        if (disposed || working) return;
        working = true; Message = null;
        using var source = action = new();
        try { Changed?.Invoke(); await operation(source.Token); }
        catch (Exception error) { if (!disposed) Message = error.GetBaseException().Message; }
        finally { action = null; working = false; if (!disposed) Changed?.Invoke(); }
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Cancel(); session.StateChanged -= Update; session.WorkspaceReset -= CancelAction; session.Dispose();
        displayed = null; Detail = null; Message = null; path = []; pageNumber = 0; slots.Clear();
        try { Changed?.Invoke(); } finally { Changed = null; }
    }
}
