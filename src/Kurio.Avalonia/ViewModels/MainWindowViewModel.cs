using System.Reactive;

using KuriousLabs.Kurio.Avalonia.Services;

using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IKurioApiClient _apiClient;
    private ViewModelBase? _currentView;
    private string _statusText = "Ready";

    public MainWindowViewModel(IKurioApiClient apiClient)
    {
        _apiClient = apiClient;

        // Initialize with downloads view
        CurrentView = new DownloadListViewModel(_apiClient);

        ShowDownloadsCommand = ReactiveCommand.Create(ShowDownloads);
        ShowAddDownloadCommand = ReactiveCommand.Create(ShowAddDownload);
        ShowSettingsCommand = ReactiveCommand.Create(ShowSettings);
        ShowStatisticsCommand = ReactiveCommand.Create(ShowStatistics);

        // Update status based on connection state
        _apiClient.ConnectionStateChanged += (_, state) =>
        {
            StatusText = state switch
            {
                ConnectionState.Connected => "Connected to server",
                ConnectionState.Connecting => "Connecting to server...",
                ConnectionState.Reconnecting => "Reconnecting...",
                ConnectionState.Disconnected => "Disconnected",
                ConnectionState.Error => "Connection error",
                _ => "Ready"
            };
        };
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
        CurrentView = new DownloadListViewModel(_apiClient);
        StatusText = "Downloads";
    }

    private void ShowAddDownload()
    {
        CurrentView = new AddDownloadViewModel(_apiClient);
        StatusText = "Add Download";
    }

    private void ShowSettings()
    {
        CurrentView = new SettingsViewModel();
        StatusText = "Settings";
    }

    private void ShowStatistics()
    {
        CurrentView = new StatisticsViewModel(_apiClient);
        StatusText = "Statistics";
    }
}
