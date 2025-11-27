using System;
using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class StatisticsViewModel : ViewModelBase
{
    private int _totalDownloads;
    private int _completedDownloads;
    private int _failedDownloads;
    private string _totalDataDownloaded = "0 GB";
    private string _averageSpeed = "0 MB/s";
    private int _activeDownloads;

    public int TotalDownloads
    {
        get => _totalDownloads;
        set => this.RaiseAndSetIfChanged(ref _totalDownloads, value);
    }

    public int CompletedDownloads
    {
        get => _completedDownloads;
        set => this.RaiseAndSetIfChanged(ref _completedDownloads, value);
    }

    public int FailedDownloads
    {
        get => _failedDownloads;
        set => this.RaiseAndSetIfChanged(ref _failedDownloads, value);
    }

    public string TotalDataDownloaded
    {
        get => _totalDataDownloaded;
        set => this.RaiseAndSetIfChanged(ref _totalDataDownloaded, value);
    }

    public string AverageSpeed
    {
        get => _averageSpeed;
        set => this.RaiseAndSetIfChanged(ref _averageSpeed, value);
    }

    public int ActiveDownloads
    {
        get => _activeDownloads;
        set => this.RaiseAndSetIfChanged(ref _activeDownloads, value);
    }
}
