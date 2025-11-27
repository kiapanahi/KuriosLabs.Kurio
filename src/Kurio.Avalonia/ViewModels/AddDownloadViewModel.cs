using System;
using System.Reactive;
using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class AddDownloadViewModel : ViewModelBase
{
    private string _url = string.Empty;
    private string _savePath = string.Empty;
    private int _segments = 8;
    private bool _startImmediately = true;

    public AddDownloadViewModel()
    {
        AddCommand = ReactiveCommand.Create(AddDownload);
        BrowseCommand = ReactiveCommand.Create(BrowseSavePath);
    }

    public string Url
    {
        get => _url;
        set => this.RaiseAndSetIfChanged(ref _url, value);
    }

    public string SavePath
    {
        get => _savePath;
        set => this.RaiseAndSetIfChanged(ref _savePath, value);
    }

    public int Segments
    {
        get => _segments;
        set => this.RaiseAndSetIfChanged(ref _segments, value);
    }

    public bool StartImmediately
    {
        get => _startImmediately;
        set => this.RaiseAndSetIfChanged(ref _startImmediately, value);
    }

    public ReactiveCommand<Unit, Unit> AddCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }

    private void AddDownload()
    {
        // TODO: Implement add download via API client
        // Validate URL and save path
        // Call API to add download
    }

    private void BrowseSavePath()
    {
        // TODO: Implement file dialog to select save path
    }
}
