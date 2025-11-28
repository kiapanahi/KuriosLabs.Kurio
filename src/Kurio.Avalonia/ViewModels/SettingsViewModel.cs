using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string _defaultSavePath = string.Empty;
    private int _defaultSegments = 8;
    private int _maxConcurrentDownloads = 3;
    private int _maxSpeedKBps; // 0 = unlimited
    private bool _startDownloadsAutomatically = true;

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

    public int MaxSpeedKBps
    {
        get => _maxSpeedKBps;
        set => this.RaiseAndSetIfChanged(ref _maxSpeedKBps, value);
    }
}
