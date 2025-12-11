using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Avalonia.Services;

public interface IKurioApiClient : IAsyncDisposable
{
    ConnectionState State { get; }
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<DownloadResponse> AddDownloadAsync(AddDownloadRequest request, CancellationToken cancellationToken = default);

    Task<List<DownloadResponse>> GetDownloadsAsync(DownloadStateFilter filter = DownloadStateFilter.All,
        CancellationToken cancellationToken = default);

    Task<DownloadResponse?> GetDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task StartDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task PauseDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task ResumeDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelDownloadAsync(Guid id, bool removeFiles = false, CancellationToken cancellationToken = default);
    Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    Task<SpeedLimitResponse> GetSpeedLimitAsync(CancellationToken cancellationToken = default);
    Task<SpeedLimitResponse> UpdateSpeedLimitAsync(UpdateSpeedLimitRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<DownloadProgressDto> StreamProgressAsync(Guid? taskId = null,
        CancellationToken cancellationToken = default);
}

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

public class KurioApiClient : IKurioApiClient
{
    private readonly HttpClient _httpClient;
    private readonly HubConnection _hubConnection;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<KurioApiClient> _logger;
    private readonly Channel<DownloadProgressDto> _progressChannel;
    private bool _disposed;
    private ConnectionState _state = ConnectionState.Disconnected;

    public KurioApiClient(
        HttpClient httpClient,
        ILogger<KurioApiClient> logger,
        string serverUrl)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(serverUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _progressChannel = Channel.CreateUnbounded<DownloadProgressDto>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });

        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/downloads")
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)
            })
            .ConfigureLogging(logging =>
            {
                logging.AddDebug();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        _hubConnection.Closed += OnConnectionClosed;
        _hubConnection.Reconnecting += OnReconnecting;
        _hubConnection.Reconnected += OnReconnected;

        _hubConnection.On<DownloadProgressDto>("ProgressUpdate", progress =>
        {
            _progressChannel.Writer.TryWrite(progress);
        });
    }

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                ConnectionStateChanged?.Invoke(this, value);
                _logger.LogInformation("Connection state changed to {State}", value);
            }
        }
    }

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectionState.Connecting;

        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            response.EnsureSuccessStatusCode();

            await _hubConnection.StartAsync(cancellationToken);
            await _hubConnection.InvokeAsync("SubscribeToProgress", null, cancellationToken);

            State = ConnectionState.Connected;
            _logger.LogInformation("Connected to Kurio server at {Url}", _httpClient.BaseAddress);
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            _logger.LogError(ex, "Failed to connect to server");
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        State = ConnectionState.Disconnected;

        if (_hubConnection.State == HubConnectionState.Connected)
        {
            await _hubConnection.StopAsync(cancellationToken);
        }

        _logger.LogInformation("Disconnected from server");
    }

    public async Task<DownloadResponse> AddDownloadAsync(
        AddDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/downloads",
            request,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);

        return await response.Content.ReadFromJsonAsync<DownloadResponse>(_jsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public async Task<List<DownloadResponse>> GetDownloadsAsync(
        DownloadStateFilter filter = DownloadStateFilter.All,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/downloads?filter={filter}",
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);

        return await response.Content.ReadFromJsonAsync<List<DownloadResponse>>(_jsonOptions, cancellationToken)
               ?? new List<DownloadResponse>();
    }

    public async Task<DownloadResponse?> GetDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/downloads/{id}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessStatusCodeAsync(response);

        return await response.Content.ReadFromJsonAsync<DownloadResponse>(_jsonOptions, cancellationToken);
    }

    public async Task StartDownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/downloads/{id}/start",
            null,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);
    }

    public async Task PauseDownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/downloads/{id}/pause",
            null,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);
    }

    public async Task ResumeDownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/downloads/{id}/resume",
            null,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);
    }

    public async Task CancelDownloadAsync(
        Guid id,
        bool removeFiles = false,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"api/downloads/{id}?removeFiles={removeFiles}",
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);
    }

    public async Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            "api/downloads/statistics",
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);

        return await response.Content.ReadFromJsonAsync<QueueStatistics>(_jsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Failed to deserialize statistics");
    }

    public async Task<SpeedLimitResponse> GetSpeedLimitAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            "api/config/speed-limit",
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);

        return await response.Content.ReadFromJsonAsync<SpeedLimitResponse>(_jsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Failed to deserialize speed limit response");
    }

    public async Task<SpeedLimitResponse> UpdateSpeedLimitAsync(
        UpdateSpeedLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            "api/config/speed-limit",
            request,
            _jsonOptions,
            cancellationToken);

        await EnsureSuccessStatusCodeAsync(response);

        return await response.Content.ReadFromJsonAsync<SpeedLimitResponse>(_jsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Failed to deserialize speed limit response");
    }

    public async IAsyncEnumerable<DownloadProgressDto> StreamProgressAsync(
        Guid? taskId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var progress in _progressChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return progress;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _progressChannel.Writer.Complete();
        await DisconnectAsync();
        await _hubConnection.DisposeAsync();
        _httpClient.Dispose();

        GC.SuppressFinalize(this);
    }

    private async Task EnsureSuccessStatusCodeAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            $"Request failed with status {response.StatusCode}: {errorContent}");
    }

    private Task OnConnectionClosed(Exception? exception)
    {
        State = ConnectionState.Disconnected;
        if (exception != null)
        {
            _logger.LogError(exception, "SignalR connection closed with error");
        }

        return Task.CompletedTask;
    }

    private Task OnReconnecting(Exception? exception)
    {
        State = ConnectionState.Reconnecting;
        _logger.LogWarning(exception, "SignalR connection lost, reconnecting...");
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        State = ConnectionState.Connected;
        _logger.LogInformation("SignalR reconnected with connection ID: {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }
}
