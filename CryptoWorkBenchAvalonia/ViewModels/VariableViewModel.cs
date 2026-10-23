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
    public VariableViewModel()
    {
        ModifyVariableCommand = new DelegateCommand<ParameterModel>(OnModifyVariable);
        ModifyValueCommand = new DelegateCommand<VariableModel>(OnModifyValue);
    }

    private void OnModifyValue(VariableModel? item)
    {
        if (item is null || SelectedVariable is null) return;
        Mutate(() =>
        {
            var variable = FindSelected();
            if (variable is null) return;
            variable.Value = SelectedVariable.Value;
            if (variable is ParameterVariableDeclaration parameters)
                parameters.ParseToDictionary(variable.Value);
        });
    }

    private void OnModifyVariable(ParameterModel? item)
    {
        if (item is null || SelectedVariable is null) return;
        Mutate(() =>
        {
            if (FindSelected() is not ParameterVariableDeclaration parameters) return;
            parameters.SetParameter(item.Parameter, item.Value);
            VariableDictionary.Instance().Update(parameters);
        });
    }

    private VariableDeclaration? FindSelected() => VariableDictionary.Instance().GetVariables()
        .FirstOrDefault(v => v.Id == SelectedVariable!.Identifier && v.Type?.Name == SelectedVariable.Type);

    private void Mutate(Action mutation)
    {
        if (IsExecutionActive?.Invoke() == true) return;
        try { WorkbenchAutomationService.Shared.Mutate(mutation, ApplyVariables); }
        catch (ExecutionBusyException) { /* Fail fast, including detached variable views. */ }
    }

    public DelegateCommand<ParameterModel> ModifyVariableCommand { get; }
    public DelegateCommand<VariableModel> ModifyValueCommand { get; }

    public void SetupVariables() => WorkbenchAutomationService.Shared.Mutate(() => { }, ApplyVariables);

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
            if (DataVariables.Count != 0) DataVariables.Clear();
            foreach (var variable in snapshot) DataVariables.Add(variable);
            IsSnapshotStale = false;
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
