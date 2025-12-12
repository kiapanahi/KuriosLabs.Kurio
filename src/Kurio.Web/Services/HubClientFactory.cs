using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuriousLabs.Kurio.Web.Services;

public sealed class HubClientFactory
{
    private readonly IOptionsMonitor<KurioServerOptions> _options;
    private readonly ILogger<HubClientFactory> _logger;

    public HubClientFactory(IOptionsMonitor<KurioServerOptions> options, ILogger<HubClientFactory> logger)
    {
        _options = options;
        _logger = logger;
    }

    public HubConnection CreateDownloadsHub() => CreateHub(_options.CurrentValue.Hubs.Downloads);

    public HubConnection CreateQueueHub() => CreateHub(_options.CurrentValue.Hubs.Queue);

    public HubConnection CreateStatsHub() => CreateHub(_options.CurrentValue.Hubs.Stats);

    private HubConnection CreateHub(string relativePath)
    {
        var baseUri = _options.CurrentValue.BaseUrl;
        var hubUri = new Uri(baseUri, relativePath);

        _logger.LogCreatingHub(hubUri);

        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                // Add API key header if authentication is enabled
                var auth = _options.CurrentValue.Authentication;
                if (auth?.Enabled == true && !string.IsNullOrEmpty(auth.ApiKey))
                {
                    options.Headers.Add("X-Api-Key", auth.ApiKey);
                }
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        connection.Reconnecting += error =>
        {
            _logger.LogHubReconnecting(hubUri, error?.Message ?? "reconnecting");
            return Task.CompletedTask;
        };

        connection.Reconnected += connectionId =>
        {
            _logger.LogHubReconnected(hubUri, connectionId ?? "(none)");
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            _logger.LogHubClosed(hubUri, error?.Message ?? "closed");
            return Task.CompletedTask;
        };

        return connection;
    }
}

internal static partial class HubClientFactoryLogging
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Creating hub connection to {HubUri}")]
    public static partial void LogCreatingHub(this ILogger logger, Uri hubUri);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Hub reconnecting {HubUri}: {Reason}")]
    public static partial void LogHubReconnecting(this ILogger logger, Uri hubUri, string reason);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Hub reconnected {HubUri} with connection id {ConnectionId}")]
    public static partial void LogHubReconnected(this ILogger logger, Uri hubUri, string connectionId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Hub closed {HubUri}: {Reason}")]
    public static partial void LogHubClosed(this ILogger logger, Uri hubUri, string reason);
}
