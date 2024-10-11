using Prism.Commands;
using Prism.Regions;
using Avalonia.Controls;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia.Platform.Storage;
using System.IO;
using System;

namespace CryptoWorkBenchAvalonia.ViewModels;

public class SidebarViewModel : ViewModelBase
{
    private const int Collapsed = 50;
    private const int Expanded = 150;
    public TopLevel? _window;
    CryptoScriptEditViewModel _cseVm;

    ////private readonly IRegionNavigationJournal? _journal;
    private readonly IRegionManager _regionManager;
    public DelegateCommand OpenScriptBookCommand { get; }
    public DelegateCommand SaveScriptBookCommand { get; }

    private int _flyoutWidth;

  public SidebarViewModel(IRegionManager regionManager,CryptoScriptEditViewModel cseVm)
  {
        _regionManager = regionManager;
        _cseVm = cseVm;
        Title = "Navigation";
        FlyoutWidth = Collapsed;
        OpenScriptBookCommand = new DelegateCommand(async () => await OpenScriptBook());
        SaveScriptBookCommand = new DelegateCommand(async () => await SaveScriptBook());
    }

  public DelegateCommand CmdDashboard => new(() =>
  {
    // _journal.Clear();
    //_regionManager.RequestNavigate(RegionNames.ContentRegion, nameof(DashboardView));
  });

  public DelegateCommand CmdFlyoutMenu => new(() =>
  {
    var isExpanded = FlyoutWidth == Expanded;
    FlyoutWidth = isExpanded ? Collapsed : Expanded;
  });
    private async Task SaveScriptBook() 
    {
        //string documentsFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var docPath= await _window.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        // Create the CryptoScript folder path
        var CryptoScriptFolderPath =await docPath.CreateFolderAsync("CryptoScript");           
        var options = new FilePickerSaveOptions
        {
            Title = "Save a file",
            DefaultExtension = "sbk",
            SuggestedFileName = "ScriptBook",   
            SuggestedStartLocation = CryptoScriptFolderPath
        };

        var result = await _window.StorageProvider.SaveFilePickerAsync(options);
        if (result != null)
        {
            var filePath = result.Path.LocalPath;
            // Do something with the filePath
            _cseVm.SaveScriptBook(filePath);
            _cseVm.Status.StatusString = "Script book saved in: "+ filePath;
        }
        else
        {
            _cseVm.Status.StatusString = "Script book not saved";
        }
    }

    private async Task OpenScriptBook()
    {
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
            var file = result[0];
            var filePath = file.Path.LocalPath;
            // Do something with the filePath
        }
    } //_regionManager.RequestNavigate(RegionNames.ContentRegion, nameof(SettingsView)));

  public int FlyoutWidth { get => _flyoutWidth; set => SetProperty(ref _flyoutWidth, value); }
}
