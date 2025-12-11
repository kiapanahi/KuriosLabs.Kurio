using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Kurio.Core.Protocols;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Protocols;

/// <summary>
///     HTTP/HTTPS protocol handler implementation.
/// </summary>
public sealed class HttpProtocolHandler : IProtocolHandler
{
    private static readonly HashSet<string> s_supportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpProtocolHandler>? _logger;
    private readonly ISpeedLimiter? _speedLimiter;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpProtocolHandler" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">Optional logger instance.</param>
    /// <param name="speedLimiter">Optional speed limiter for bandwidth throttling.</param>
    public HttpProtocolHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpProtocolHandler>? logger = null,
        ISpeedLimiter? speedLimiter = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger;
        _speedLimiter = speedLimiter;
    }

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedSchemes => s_supportedSchemes;

    /// <inheritdoc />
    public async Task<bool> SupportsRangeRequestsAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);

        using var httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            _logger?.LogCheckingRangeSupport(url.ToString());

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            // Check for Accept-Ranges header
            if (response.Headers.TryGetValues("Accept-Ranges", out var values))
            {
                var acceptRanges = string.Join(",", values);
                var supportsRanges = !acceptRanges.Equals("none", StringComparison.OrdinalIgnoreCase);

                _logger?.LogRangeSupportResult(
                    supportsRanges ? "supports" : "does not support",
                    acceptRanges);

                return supportsRanges;
            }

            // If no Accept-Ranges header, assume no support
            _logger?.LogNoAcceptRangesHeader();
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogHeadRequestFailed(ex);
            // If HEAD fails, try a small range request
            return await TestRangeRequestAsync(url, options, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<long> GetFileSizeAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);

        using var httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var fileSize = response.Content.Headers.ContentLength ?? -1;
            _logger?.LogFileSize(url.ToString(), fileSize);

            return fileSize;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogFileSizeFailed(ex, url.ToString());
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DownloadRangeAsync(
        Uri url,
        ByteRange range,
        Stream destination,
        DownloadOptions options,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(options);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        using var httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        ConfigureRequest(request, options);

        // Set range header
        request.Headers.Range = new RangeHeaderValue(range.Start, range.End);

        _logger?.LogDownloadingRange(range.Start, range.End, url.ToString());

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            // Verify we got a partial content response if range was requested
            if (response.StatusCode != HttpStatusCode.PartialContent && range.Start > 0)
            {
                _logger?.LogUnexpectedStatusCode((int)response.StatusCode);

                throw new InvalidOperationException(
                    $"Server did not honor range request. Expected 206 Partial Content, got {response.StatusCode}.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            // Use optimal buffer size (8KB is generally optimal for most scenarios)
            const int bufferSize = 8192;
            var buffer = new byte[bufferSize];
            long totalBytesRead = 0;

            // Stall detection: timeout if no data received for 30 seconds
            const int stallTimeoutSeconds = 30;
            var lastDataReceivedAt = DateTime.UtcNow;

            int bytesRead;
            while (true)
            {
                // Create a timeout for this specific read operation
                // This ensures we don't hang indefinitely if the connection is lost
                using var readTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(stallTimeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, readTimeoutCts.Token);

                try
                {
                    bytesRead = await responseStream.ReadAsync(buffer, linkedCts.Token).ConfigureAwait(false);

                    // If we got 0 bytes, we've reached the end of the stream
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    lastDataReceivedAt = DateTime.UtcNow;

                    // Apply speed limiting if enabled
                    if (_speedLimiter?.IsEnabled == true)
                    {
                        await _speedLimiter.ThrottleAsync(bytesRead, cancellationToken).ConfigureAwait(false);
                    }

                    // Write the data using the main cancellation token (not the timeout token)
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;
                    progress?.Report(totalBytesRead);
                }
                catch (OperationCanceledException) when (readTimeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    // Read operation timed out - no data received for stallTimeoutSeconds
                    var timeSinceLastData = DateTime.UtcNow - lastDataReceivedAt;

                    _logger?.LogDownloadStalled(timeSinceLastData.TotalSeconds, range.Start, range.End);

                    throw new TimeoutException(
                        $"Download stalled: no data received for {stallTimeoutSeconds} seconds");
                }
            }

            _logger?.LogDownloadSuccess(totalBytesRead, range.Start, range.End);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogDownloadFailed(ex, range.Start, range.End, url.ToString());
            throw;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogDownloadCancelled(range.Start, range.End);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout occurred
            _logger?.LogDownloadTimeout(ex, range.Start, range.End, url.ToString());
            throw new TimeoutException(
                $"Request timed out after {options.TimeoutSeconds} seconds", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ResourceMetadata> GetMetadataAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);

        using var httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            _logger?.LogFetchingMetadata(url.ToString());

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var supportsRanges = false;
            if (response.Headers.TryGetValues("Accept-Ranges", out var acceptRangeValues))
            {
                var acceptRanges = string.Join(",", acceptRangeValues);
                supportsRanges = !acceptRanges.Equals("none", StringComparison.OrdinalIgnoreCase);
            }

            ResourceMetadata metadata = new()
            {
                ContentLength = response.Content.Headers.ContentLength ?? -1,
                ContentType = response.Content.Headers.ContentType?.MediaType,
                ETag = response.Headers.ETag?.Tag,
                LastModified = response.Content.Headers.LastModified,
                SupportsRanges = supportsRanges
            };

            // Try to get filename from Content-Disposition header
            if (response.Content.Headers.ContentDisposition is { } contentDisposition)
            {
                metadata.SuggestedFileName = contentDisposition.FileName?.Trim('"') ??
                                             contentDisposition.FileNameStar;
            }

            // If no filename in Content-Disposition, try to extract from URL
            if (string.IsNullOrEmpty(metadata.SuggestedFileName))
            {
                metadata.SuggestedFileName = GetFileNameFromUrl(url);
            }

            // Store additional headers
            foreach (var header in response.Headers)
            {
                if (!IsStandardHeader(header.Key))
                {
                    metadata.AdditionalHeaders[header.Key] = string.Join(",", header.Value);
                }
            }

            _logger?.LogMetadataFetched(metadata.ContentLength, metadata.ContentType, metadata.SupportsRanges);

            return metadata;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogMetadataFetchFailed(ex, url.ToString());
            throw;
        }
    }

    private HttpClient CreateHttpClient(DownloadOptions options)
    {
        var client = _httpClientFactory.CreateClient("KurioDownloader");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        return client;
    }

    private static void ConfigureRequest(HttpRequestMessage request, DownloadOptions options)
    {
        // Set user agent
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd(options.UserAgent);

        // Add custom headers
        foreach (var (key, value) in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        // Add authentication if provided
        if (!string.IsNullOrEmpty(options.Credentials))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(options.Credentials));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    private async Task<bool> TestRangeRequestAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogTestingRangeRequest(url.ToString());

            using var httpClient = CreateHttpClient(options);
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            ConfigureRequest(request, options);

            // Request only the first byte
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            // If we get 206 Partial Content, range requests are supported
            var supportsRanges = response.StatusCode == HttpStatusCode.PartialContent;

            _logger?.LogRangeTestResult(supportsRanges);

            return supportsRanges;
        }
        catch (Exception ex)
        {
            _logger?.LogRangeTestFailed(ex, url.ToString());
            return false;
        }
    }

    private static bool IsStandardHeader(string headerName)
    {
        return headerName switch
        {
            "Content-Type" or "Content-Length" or "ETag" or
                "Last-Modified" or "Content-Disposition" or
                "Accept-Ranges" => true,
            _ => false
        };
    }

    private static string? GetFileNameFromUrl(Uri url)
    {
        try
        {
            var segments = url.Segments;
            if (segments.Length > 0)
            {
                var lastSegment = segments[^1];
                // Remove trailing slash if present
                lastSegment = lastSegment.TrimEnd('/');

                // Decode URL encoding
                var decoded = Uri.UnescapeDataString(lastSegment);

                // Remove query string if present
                var queryIndex = decoded.IndexOf('?');
                if (queryIndex >= 0)
                {
                    decoded = decoded[..queryIndex];
                }

                return string.IsNullOrWhiteSpace(decoded) ? null : decoded;
            }
        }
        catch
        {
            // If extraction fails, return null
        }

        return null;
    }
}
