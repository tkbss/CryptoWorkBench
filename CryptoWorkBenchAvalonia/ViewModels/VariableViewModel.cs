using CryptoScript.Model;
using CryptoScript.Variables;
using CryptoWorkBenchAvalonia.Models;
using CryptoWorkBenchAvalonia.Services.Automation;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CryptoWorkBenchAvalonia.ViewModels;

public class VariableViewModel : ViewModelBase
{
    internal Func<bool>? IsExecutionActive { get; set; }
    private long draftRevision;
    private long draftGeneration;
    private object? boundView;
    private bool detached;
    private long publication;
    private sealed record Draft(string Identifier, Dictionary<ParameterModel, string> Parameters);
    private Dictionary<VariableModel, Draft> published = new();

    public VariableViewModel() => BindCommands();

    private void BindCommands()
    {
        var lifetime = commandLifetime = new object();
        bool Current() => ReferenceEquals(commandLifetime, lifetime) && !detached;
        RefreshCommand = new DelegateCommand(() => { if (Current()) SetupVariables(); }, Current);
        ModifyVariableCommand = new DelegateCommand<ParameterModel>(item => { if (Current()) OnModifyVariable(item); });
        ModifyValueCommand = new DelegateCommand<VariableModel>(item => { if (Current()) OnModifyValue(item); });
        RaisePropertyChanged(nameof(RefreshCommand));
        RaisePropertyChanged(nameof(ModifyVariableCommand));
        RaisePropertyChanged(nameof(ModifyValueCommand));
    }

    private object? commandLifetime;

    private void OnModifyValue(VariableModel? item)
    {
        if (item is null || SelectedVariable is not { } selected) return;
        if (!ReferenceEquals(item, selected)) return;
        var value = selected.Value;
        if (value is not ("true" or "false") && selected.Origin?.TryGetTarget(out var origin) == true &&
            origin is ValVariableDeclaration)
        {
            Mutate(selected, null); // Restore this row through the same admission checks as an edit.
            return;
        }
        Mutate(selected, variable =>
        {
            variable.Value = value;
            if (variable is ParameterVariableDeclaration parameters)
                parameters.ParseToDictionary(variable.Value);
        });
    }

    private void OnModifyVariable(ParameterModel? item)
    {
        if (item is null || SelectedVariable is not { } selected) return;
        if (!published.TryGetValue(selected, out var draft) || !draft.Parameters.TryGetValue(item, out var key) || item.Parameter != key || !selected.Parameters.Contains(item)) return;
        Mutate(selected, variable =>
        {
            if (variable is not ParameterVariableDeclaration parameters) return;
            parameters.SetParameter(key, item.Value);
            VariableDictionary.Instance().Update(parameters);
        });
    }

    private void Mutate(VariableModel selected, Action<VariableDeclaration>? mutation)
    {
        if (IsExecutionActive?.Invoke() == true || IsSnapshotStale || !published.TryGetValue(selected, out var draft)) return;
        if (selected.Origin == null || !selected.Origin.TryGetTarget(out var original))
        {
            IsSnapshotStale = true;
            return;
        }
        var originalPublication = publication;
        var coordinator = Automation.EditorCoordinator;
        if (draftGeneration != coordinator.WorkspaceGeneration)
        {
            IsSnapshotStale = true;
            return;
        }
        using var ticket = coordinator.TryBegin();
        if (ticket == null) return;
        Exception? failure = null;
        coordinator.Commit(ticket, () =>
        {
            bool CurrentDraft() => originalPublication == publication && ReferenceEquals(SelectedVariable, selected) &&
                coordinator.IsActiveGuiOperation(ticket) && draftRevision == Services.InterpreterHostQueue.StateRevision &&
                ReferenceEquals(VariableDictionary.Instance().Get(draft.Identifier), original);
            try
            {
                if (mutation == null)
                    Automation.ReadVariableValue(original, CurrentDraft, value => selected.Value = value);
                else
                    Automation.Mutate(() => mutation(original), ApplyVariables, validate: CurrentDraft);
            }
            catch (ExecutionBusyException)
            {
                IsSnapshotStale = true;
                ticket.Error = EditorOperationError.Stale;
            }
            catch (Exception error)
            {
                IsSnapshotStale = true;
                ticket.Error = EditorOperationError.CallbackFailed;
                failure = error;
            }
        });
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public DelegateCommand<ParameterModel> ModifyVariableCommand { get; private set; } = null!;
    public DelegateCommand<VariableModel> ModifyValueCommand { get; private set; } = null!;

    public DelegateCommand RefreshCommand { get; private set; } = null!;
    public void SetupVariables()
    {
        if (detached || !Automation.EditorCoordinator.CanStartGui) { IsSnapshotStale = true; return; }
        try { Automation.Refresh(ApplyVariables); }
        catch (ExecutionBusyException) { IsSnapshotStale = true; }
    }

    internal void AttachView(object view)
    {
        boundView = view;
        detached = false;
        BindCommands();
        InvalidateDraft();
        SetupVariables();
    }

    internal void DetachView(object view)
    {
        if (!ReferenceEquals(boundView, view)) return;
        boundView = null;
        detached = true;
        commandLifetime = null;
        InvalidateDraft();
    }

    internal void InvalidateDraft()
    {
        publication++;
        published.Clear();
        IsSnapshotStale = true;
    }

    private bool isSnapshotStale;
    public bool IsSnapshotStale
    {
        get => isSnapshotStale;
        internal set => SetProperty(ref isSnapshotStale, value);
    }

    internal void ApplyVariables(IReadOnlyList<VariableModel> snapshot)
    {
        try
        {
            if (detached) return;
            var stamp = ++publication;
            var coordinator = Automation.EditorCoordinator;
            var generation = coordinator.WorkspaceGeneration;
            var version = Automation.DraftRevision;
            var drafts = snapshot.ToDictionary(variable => variable,
                variable => new Draft(variable.Identifier, variable.Parameters.ToDictionary(p => p, p => p.Parameter)));
            published.Clear();
            Publish(() =>
            {
                SelectedVariable = null;
                if (stamp != publication) return;
                if (DataVariables.Count != 0) DataVariables.Clear();
                foreach (var variable in snapshot)
                {
                    if (stamp != publication) return;
                    variable.Automation = Automation;
                    foreach (var parameter in variable.Parameters) parameter.Automation = Automation;
                    DataVariables.Add(variable);
                }
                if (stamp != publication || generation != coordinator.WorkspaceGeneration) return;
                draftRevision = version;
                draftGeneration = generation;
                published = drafts;
                IsSnapshotStale = false;
            });
        }
        catch
        {
            IsSnapshotStale = true;
            throw;
        }
    }

    private ObservableCollection<VariableModel> dataVariables = new();
    public ObservableCollection<VariableModel> DataVariables
    {
        get => dataVariables;
        set => SetProperty(ref dataVariables, value);
    }

    private VariableModel? selectedVariable;
    public VariableModel? SelectedVariable
    {
        get => selectedVariable;
        set => SetProperty(ref selectedVariable, value);
    }
}
