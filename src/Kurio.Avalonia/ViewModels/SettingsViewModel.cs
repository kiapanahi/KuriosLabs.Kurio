using System.Reactive;

using KuriousLabs.Kurio.Avalonia.Services;

using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IKurioApiClient _apiClient;
    private string _defaultSavePath = string.Empty;
    private int _defaultSegments = 8;
    private int _maxConcurrentDownloads = 3;
    private int _maxSpeedKBps; // 0 = unlimited
    private bool _startDownloadsAutomatically = true;
    private bool _speedLimitEnabled;

    public SettingsViewModel(IKurioApiClient apiClient)
    {
        _apiClient = apiClient;
        
        LoadSettingsCommand = ReactiveCommand.CreateFromTask(LoadSettingsAsync);
        SaveSettingsCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
        
        // Load settings on initialization
        _ = LoadSettingsAsync();
    }

    public ReactiveCommand<Unit, Unit> LoadSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }

    public int MaxConcurrentDownloads
    {
        get => _maxConcurrentDownloads;
        set => this.RaiseAndSetIfChanged(ref _maxConcurrentDownloads, value);
    }

    public int DefaultSegments
    {
        get => _defaultSegments;
        set => this.RaiseAndSetIfChanged(ref _defaultSegments, value);
    }

    public string DefaultSavePath
    {
        get => _defaultSavePath;
        set => this.RaiseAndSetIfChanged(ref _defaultSavePath, value);
    }

    public bool StartDownloadsAutomatically
    {
        get => _startDownloadsAutomatically;
        set => this.RaiseAndSetIfChanged(ref _startDownloadsAutomatically, value);
    }

    public bool SpeedLimitEnabled
    {
        get => _speedLimitEnabled;
        set => this.RaiseAndSetIfChanged(ref _speedLimitEnabled, value);
    }

    public int MaxSpeedKBps
    {
        get => _maxSpeedKBps;
        set => this.RaiseAndSetIfChanged(ref _maxSpeedKBps, value);
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var speedLimit = await _apiClient.GetSpeedLimitAsync();
            SpeedLimitEnabled = speedLimit.Enabled;
            MaxSpeedKBps = (int)(speedLimit.MaxDownloadSpeedBytesPerSecond / 1024); // Convert bytes to KB
        }
        catch (Exception)
        {
            // Log error or show notification
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var request = new UpdateSpeedLimitRequest(
                SpeedLimitEnabled,
                MaxSpeedKBps * 1024L, // Convert KB to bytes
                0); // Upload speed not implemented yet

            await _apiClient.UpdateSpeedLimitAsync(request);
        }
        catch (Exception)
        {
            // Log error or show notification
        }
    }
}
