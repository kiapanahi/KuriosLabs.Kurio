using KuriousLabs.Kurio.Avalonia.Services;

using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class StatisticsViewModel : ViewModelBase
{
    private readonly IKurioApiClient _apiClient;
    private int _activeDownloads;
    private string _averageSpeed = "0 MB/s";
    private int _completedDownloads;
    private int _failedDownloads;
    private string _totalDataDownloaded = "0 GB";
    private int _totalDownloads;

    public StatisticsViewModel(IKurioApiClient apiClient)
    {
        _apiClient = apiClient;
        _ = LoadStatisticsAsync();
    }

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

    private async Task LoadStatisticsAsync()
    {
        try
        {
            var stats = await _apiClient.GetStatisticsAsync();

            TotalDownloads = stats.TotalDownloads;
            CompletedDownloads = stats.CompletedDownloads;
            FailedDownloads = stats.FailedDownloads;
            ActiveDownloads = stats.ActiveDownloads;
            TotalDataDownloaded = FormatBytes(stats.TotalBytesDownloaded);
            AverageSpeed = $"{FormatBytes((long)stats.AverageSpeed)}/s";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load statistics: {ex.Message}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
