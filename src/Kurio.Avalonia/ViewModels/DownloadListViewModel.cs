using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class DownloadListViewModel : ViewModelBase
{
    private ObservableCollection<DownloadItemViewModel> _downloads = new();
    private DownloadItemViewModel? _selectedDownload;

    public DownloadListViewModel()
    {
        PauseCommand = ReactiveCommand.Create<DownloadItemViewModel>(Pause);
        ResumeCommand = ReactiveCommand.Create<DownloadItemViewModel>(Resume);
        CancelCommand = ReactiveCommand.Create<DownloadItemViewModel>(Cancel);
        RemoveCommand = ReactiveCommand.Create<DownloadItemViewModel>(Remove);
        
        // Add some sample data for demonstration
        LoadSampleData();
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

    private void Pause(DownloadItemViewModel download)
    {
        // TODO: Implement pause via API client
        download.Status = "Paused";
    }

    private void Resume(DownloadItemViewModel download)
    {
        // TODO: Implement resume via API client
        download.Status = "Downloading";
    }

    private void Cancel(DownloadItemViewModel download)
    {
        // TODO: Implement cancel via API client
        download.Status = "Cancelled";
    }

    private void Remove(DownloadItemViewModel download)
    {
        Downloads.Remove(download);
    }

    private void LoadSampleData()
    {
        // Sample downloads for UI demonstration
        Downloads.Add(new DownloadItemViewModel
        {
            FileName = "ubuntu-24.04-desktop-amd64.iso",
            Url = "https://releases.ubuntu.com/24.04/ubuntu-24.04-desktop-amd64.iso",
            Status = "Downloading",
            Progress = 45.5,
            DownloadedSize = "2.3 GB",
            TotalSize = "5.0 GB",
            Speed = "12.5 MB/s"
        });
        
        Downloads.Add(new DownloadItemViewModel
        {
            FileName = "sample-video.mp4",
            Url = "https://example.com/video.mp4",
            Status = "Paused",
            Progress = 25.0,
            DownloadedSize = "250 MB",
            TotalSize = "1.0 GB",
            Speed = "0 MB/s"
        });
    }
}

public class DownloadItemViewModel : ViewModelBase
{
    private string _fileName = string.Empty;
    private string _url = string.Empty;
    private string _status = string.Empty;
    private double _progress;
    private string _downloadedSize = string.Empty;
    private string _totalSize = string.Empty;
    private string _speed = string.Empty;

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
