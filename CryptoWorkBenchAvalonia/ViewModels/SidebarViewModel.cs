using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CryptoScript.ErrorListner;
using CryptoScript.Variables;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Prism.Commands;
using Prism.Ioc;
using Prism.Navigation.Regions;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CryptoWorkBenchAvalonia.ViewModels;

public class SidebarViewModel : ViewModelBase
{
    private const int Collapsed = 50;
    private const int Expanded = 170;
    public TopLevel? _window;
    CryptoScriptEditViewModel _cseVm;

    ////private readonly IRegionNavigationJournal? _journal;
    private readonly IRegionManager _regionManager;
    public AsyncDelegateCommand OpenScriptBookCommand { get; }
    public AsyncDelegateCommand SaveScriptBookCommand { get; }
    public DelegateCommand ShowVariableViewCommand { get; }
    public DelegateCommand ShowInfoViewCommand { get; }
    public DelegateCommand CmdFlyoutMenu => new(() =>
    {
        var isExpanded = FlyoutWidth == Expanded;
        FlyoutWidth = isExpanded ? Collapsed : Expanded;
    });
    private WindowIcon _windowIcon;
    public WindowIcon WindowIcon
    {
        get => _windowIcon;
        set => SetProperty(ref _windowIcon, value);
    }
    private int _flyoutWidth;
    private readonly IContainerProvider _container;
    public SidebarViewModel(IRegionManager regionManager,CryptoScriptEditViewModel cseVm, IContainerProvider container)
  {
        _regionManager = regionManager;
        _cseVm = cseVm;
        _container = container;
        Title = "Navigation";
        FlyoutWidth = Collapsed;
        _windowIcon = new WindowIcon("./Assets/avalonia-logo.ico");
        OpenScriptBookCommand = new AsyncDelegateCommand(() => GuardAsync(OpenScriptBook));
        SaveScriptBookCommand = new AsyncDelegateCommand(() => GuardAsync(SaveScriptBook));
        ShowVariableViewCommand = new DelegateCommand(ShowVariableView);
        ShowInfoViewCommand = new DelegateCommand(ShowInfoView);
    }

    private void ShowVariableView()
    {
        _container.Resolve<MainViewModel>().InfoViewTitle = "Variable View";
        _regionManager.RequestNavigate("InfoRegion", "VariableView");
    }
    private void ShowInfoView()
    {
        _container.Resolve<MainViewModel>().InfoViewTitle = "Info View";
        _regionManager.RequestNavigate("InfoRegion", "InfoView");
    }


    private async Task SaveScriptBook() 
    {
        if (_cseVm.IsExecuting) return;
        
        var docPath= await _window!.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        // Create the CryptoScript folder path
        var CryptoScriptFolderPath =await docPath!.CreateFolderAsync("CryptoScript");           
        var options = new FilePickerSaveOptions
        {
            Title = "Save a file",
            DefaultExtension = "sbk",
            SuggestedFileName = "ScriptBook",   
            SuggestedStartLocation = CryptoScriptFolderPath,
            ShowOverwritePrompt = false
        };

        var result = await _window.StorageProvider.SaveFilePickerAsync(options);
        if (result != null)
        {
            var filePath = result.Path.LocalPath;
            if (File.Exists(filePath))
            {
                var p=new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = "Yes", },
                        new ButtonDefinition { Name = "No", },                        
                    },
                    WindowIcon = _windowIcon,
                    ContentTitle = "OVERWRITE PROMPT",
                    ContentMessage = "The file already exists. Do you want to overwrite it?",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                // Prompt the user for confirmation
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxCustom(p);   
                    
                var r = await messageBoxStandardWindow.ShowAsync();

                if (r == "No")
                {
                    _cseVm.Status.StatusString = "Save operation canceled by the user.";
                    return;
                }
                else
                {                    
                    await result.DeleteAsync();
                }
            }
            _cseVm.SaveScriptBook(filePath);
            _cseVm.Status.StatusString = "Script book saved in: "+ filePath;
        }
        else
        {
            _cseVm.Status.StatusString = "Script book not saving canceld";
        }
    }

    private async Task OpenScriptBook()
    {
        if (_cseVm.IsExecuting) return;
        var options = new FilePickerOpenOptions
        {
            Title = "Select a file",
            AllowMultiple = false, // Set to true if you want to allow multiple file selection
            FileTypeFilter = new FilePickerFileType[]
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.sbk" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        };
        
        var result = await _window.StorageProvider.OpenFilePickerAsync(options);
        if (result.Count > 0)
        {
            bool clear = false;
            if (_cseVm.IsTextEditorEmpty() == false)
            {
                var p = new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = "Yes", },
                        new ButtonDefinition { Name = "No", },
                    },
                    WindowIcon = _windowIcon,
                    ContentTitle = "EDITOR WINDOW CLEARENCE",
                    ContentMessage = "Do you want to clear editor window before loading book?",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxCustom(p);
                var r = await messageBoxStandardWindow.ShowAsync();
                if (r == "Yes")
                {
                    clear = true;
                }
            }
            try
            {
                await _cseVm.OpenScriptBookAsync(result[0].Path.LocalPath, clear);
            }
            catch (SemanticErrorException ex)
            {
                _cseVm.Status.AppendDiagnostics(System.Array.Empty<SemanticError>(), ex.SemanticError);
                _cseVm.Status.StatusString = "Script book loading aborted (semantic error).";
                return;
            }
            SetScriptBookCompletionStatus();
        }
        
    } 

    private void SetScriptBookCompletionStatus()
    {
        if (_cseVm.LastScriptBookSucceeded)
            _cseVm.Status.StatusString = "Script book loaded and all lines successfull parsed";
    }

    private async Task GuardAsync(System.Func<Task> operation)
    {
        try { await operation(); }
        catch (System.Exception) { _cseVm.Status.StatusString = "Script book operation failed."; }
    }

  public int FlyoutWidth { get => _flyoutWidth; set => SetProperty(ref _flyoutWidth, value); }
}
