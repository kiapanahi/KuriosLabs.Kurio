using System.Reactive;

using KuriousLabs.Kurio.Avalonia.Services;

using ReactiveUI;

namespace KuriousLabs.Kurio.Avalonia.ViewModels;

public class AddDownloadViewModel : ViewModelBase
{
    private readonly IKurioApiClient _apiClient;
    private string _savePath = string.Empty;
    private int _segments = 8;
    private bool _startImmediately = true;
    private string _url = string.Empty;

    public AddDownloadViewModel(IKurioApiClient apiClient)
    {
        _apiClient = apiClient;

        AddCommand = ReactiveCommand.CreateFromTask(AddDownloadAsync);
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

    private async Task AddDownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            // TODO: Show validation error to user
            Console.WriteLine("URL is required");
            return;
        }

        try
        {
            AddDownloadRequest request = new(
                Url,
                string.IsNullOrWhiteSpace(SavePath) ? null : SavePath,
                Segments,
                StartImmediately
            );

            var response = await _apiClient.AddDownloadAsync(request);

            // Clear form on success
            Url = string.Empty;
            SavePath = string.Empty;
            Segments = 8;
            StartImmediately = true;

            Console.WriteLine($"Download added: {response.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to add download: {ex.Message}");
        }
    }

    private void BrowseSavePath()
    {
        // TODO: Implement file dialog to select save path
    }
}
