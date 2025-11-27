using System.Collections.ObjectModel;
using System.Reactive;

using KuriousLabs.Kurio.Avalonia.Services;

using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class DownloadListViewModel : ViewModelBase
{
    private readonly IKurioApiClient _apiClient;
    private ObservableCollection<DownloadItemViewModel> _downloads = new();
    private DownloadItemViewModel? _selectedDownload;

    public DownloadListViewModel(IKurioApiClient apiClient)
    {
        _apiClient = apiClient;

        PauseCommand = ReactiveCommand.CreateFromTask<DownloadItemViewModel>(PauseAsync);
        ResumeCommand = ReactiveCommand.CreateFromTask<DownloadItemViewModel>(ResumeAsync);
        CancelCommand = ReactiveCommand.CreateFromTask<DownloadItemViewModel>(CancelAsync);
        RemoveCommand = ReactiveCommand.Create<DownloadItemViewModel>(Remove);

        // Load downloads from server
        _ = LoadDownloadsAsync();
    }

    public ObservableCollection<DownloadItemViewModel> Downloads
    {
        get => _downloads;
        set => this.RaiseAndSetIfChanged(ref _downloads, value);
    }

    public DownloadItemViewModel? SelectedDownload
    {
        get => _selectedDownload;
        set => this.RaiseAndSetIfChanged(ref _selectedDownload, value);
    }

    public ReactiveCommand<DownloadItemViewModel, Unit> PauseCommand { get; }
    public ReactiveCommand<DownloadItemViewModel, Unit> ResumeCommand { get; }
    public ReactiveCommand<DownloadItemViewModel, Unit> CancelCommand { get; }
    public ReactiveCommand<DownloadItemViewModel, Unit> RemoveCommand { get; }

    private async Task PauseAsync(DownloadItemViewModel download)
    {
        try
        {
            await _apiClient.PauseDownloadAsync(download.Id);
            download.Status = "Paused";
        }
        catch (Exception ex)
        {
            // TODO: Show error to user
            Console.WriteLine($"Failed to pause download: {ex.Message}");
        }
    }

    private async Task ResumeAsync(DownloadItemViewModel download)
    {
        try
        {
            await _apiClient.ResumeDownloadAsync(download.Id);
            download.Status = "Downloading";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to resume download: {ex.Message}");
        }
    }

    private async Task CancelAsync(DownloadItemViewModel download)
    {
        try
        {
            await _apiClient.CancelDownloadAsync(download.Id, true);
            Downloads.Remove(download);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to cancel download: {ex.Message}");
        }
    }

    private void Remove(DownloadItemViewModel download)
    {
        Downloads.Remove(download);
    }

    private async Task LoadDownloadsAsync()
    {
        try
        {
            var downloads = await _apiClient.GetDownloadsAsync();

            Downloads.Clear();
            foreach (var download in downloads)
            {
                Downloads.Add(new DownloadItemViewModel
                {
                    Id = download.Id,
                    FileName = download.FileName ?? "Unknown",
                    Url = download.Url,
                    Status = download.State.ToString(),
                    Progress = download.TotalBytes.HasValue && download.TotalBytes > 0
                        ? (double)download.DownloadedBytes / download.TotalBytes.Value * 100
                        : 0,
                    DownloadedSize = FormatBytes(download.DownloadedBytes),
                    TotalSize = download.TotalBytes.HasValue ? FormatBytes(download.TotalBytes.Value) : "Unknown",
                    Speed = download.Speed.HasValue ? $"{FormatBytes((long)download.Speed.Value)}/s" : "0 B/s"
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load downloads: {ex.Message}");
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

public class DownloadItemViewModel : ViewModelBase
{
    private string _downloadedSize = string.Empty;
    private string _fileName = string.Empty;
    private Guid _id;
    private double _progress;
    private string _speed = string.Empty;
    private string _status = string.Empty;
    private string _totalSize = string.Empty;
    private string _url = string.Empty;

    public Guid Id
    {
        get => _id;
        set => this.RaiseAndSetIfChanged(ref _id, value);
    }

    public string FileName
    {
        get => _fileName;
        set => this.RaiseAndSetIfChanged(ref _fileName, value);
    }

    public string Url
    {
        get => _url;
        set => this.RaiseAndSetIfChanged(ref _url, value);
    }

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public string DownloadedSize
    {
        get => _downloadedSize;
        set => this.RaiseAndSetIfChanged(ref _downloadedSize, value);
    }

    public string TotalSize
    {
        get => _totalSize;
        set => this.RaiseAndSetIfChanged(ref _totalSize, value);
    }

    public string Speed
    {
        get => _speed;
        set => this.RaiseAndSetIfChanged(ref _speed, value);
    }
}
