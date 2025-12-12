using System.Net.Http.Json;

using KuriousLabs.Kurio.Contracts.Downloads;
using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Contracts.Stats;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Web.Services;

public sealed class KurioApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KurioApiClient> _logger;

    public KurioApiClient(HttpClient httpClient, ILogger<KurioApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> AddDownloadAsync(CreateDownloadRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/downloads", request, cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    public Task<bool> StartDownloadAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, $"api/downloads/{id}/start", null, cancellationToken);

    public Task<bool> PauseDownloadAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, $"api/downloads/{id}/pause", null, cancellationToken);

    public Task<bool> ResumeDownloadAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, $"api/downloads/{id}/resume", null, cancellationToken);

    public Task<bool> CancelDownloadAsync(Guid id, bool removeFiles, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Delete, $"api/downloads/{id}?removeFiles={removeFiles}", null, cancellationToken);

    public Task<bool> ChangeDownloadPriorityAsync(Guid id, DownloadPriority priority, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, $"api/downloads/{id}/priority", new ChangePriorityRequest { Priority = priority }, cancellationToken);

    public async Task<int?> PauseAllAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync("api/downloads/pause-all", null, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> ClearCompletedAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, "api/downloads/clear-completed", null, cancellationToken);

    public async Task<List<QueueItem>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var items = await _httpClient.GetFromJsonAsync<List<QueueItem>>("api/queue", cancellationToken)
            .ConfigureAwait(false);
        return items ?? [];
    }

    public Task<bool> MoveQueueAsync(Guid id, string action, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, $"api/queue/{id}/{action}", null, cancellationToken);

    public Task<bool> ChangeQueuePriorityAsync(Guid id, DownloadPriority priority, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Post, $"api/queue/{id}/priority", new ChangePriorityRequest { Priority = priority }, cancellationToken);

    public async Task<StatsSnapshot?> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<StatsSnapshot>("api/stats", cancellationToken).ConfigureAwait(false);
    }

    public async Task<SpeedLimitResponseModel?> GetSpeedLimitAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<SpeedLimitResponseModel>("api/config/speed-limit", cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> UpdateSpeedLimitAsync(UpdateSpeedLimitRequestModel request, CancellationToken cancellationToken = default) =>
        SendCommandAsync(HttpMethod.Put, "api/config/speed-limit", request, cancellationToken);

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogHealthCheckFailed(ex);
            return false;
        }
    }

    private async Task<bool> SendCommandAsync(HttpMethod method, string path, object? payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = payload is null ? null : JsonContent.Create(payload)
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }
}

internal static partial class KurioApiClientLogging
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Health check failed")]
    public static partial void LogHealthCheckFailed(this ILogger logger, Exception exception);
}

public sealed record SpeedLimitResponseModel
{
    public bool Enabled { get; set; }

    public long MaxDownloadSpeedBytesPerSecond { get; set; }

    public long MaxUploadSpeedBytesPerSecond { get; set; }

    public long CurrentLimitBytesPerSecond { get; set; }
}

public sealed record UpdateSpeedLimitRequestModel
{
    public bool Enabled { get; set; }

    public long MaxDownloadSpeedBytesPerSecond { get; set; }

    public long MaxUploadSpeedBytesPerSecond { get; set; }
}

public sealed record CreateDownloadRequest
{
    public string Url { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string? DestinationDirectory { get; set; }

    public int? MaxConnections { get; set; }

    public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
}
