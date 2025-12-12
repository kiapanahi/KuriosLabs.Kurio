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
}

internal static partial class KurioApiClientLogging
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Health check failed")]
    public static partial void LogHealthCheckFailed(this ILogger logger, Exception exception);
}
