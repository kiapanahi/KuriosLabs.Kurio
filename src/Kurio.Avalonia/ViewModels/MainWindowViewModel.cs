using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _statusText = "Ready";
    private ViewModelBase? _currentView;

    public MainWindowViewModel()
    {
        // Initialize with downloads view
        CurrentView = new DownloadListViewModel();
        
        ShowDownloadsCommand = ReactiveCommand.Create(ShowDownloads);
        ShowAddDownloadCommand = ReactiveCommand.Create(ShowAddDownload);
        ShowSettingsCommand = ReactiveCommand.Create(ShowSettings);
        ShowStatisticsCommand = ReactiveCommand.Create(ShowStatistics);
    }

    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public ReactiveCommand<Unit, Unit> ShowDownloadsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowAddDownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowStatisticsCommand { get; }

    private void ShowDownloads()
    {
        CurrentView = new DownloadListViewModel();
        StatusText = "Downloads";
    }

    private void ShowAddDownload()
    {
        CurrentView = new AddDownloadViewModel();
        StatusText = "Add Download";
    }

    private void ShowSettings()
    {
        CurrentView = new SettingsViewModel();
        StatusText = "Settings";
    }

    private void ShowStatistics()
    {
        CurrentView = new StatisticsViewModel();
        StatusText = "Statistics";
    }
}
