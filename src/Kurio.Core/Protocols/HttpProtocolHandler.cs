using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace Kurio.Core.Protocols;

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

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpProtocolHandler" /> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">Optional logger instance.</param>
    public HttpProtocolHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpProtocolHandler>? logger = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger;
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

        using HttpClient httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            _logger?.LogDebug("Checking range request support for {Url}", url);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Check for Accept-Ranges header
            if (response.Headers.TryGetValues("Accept-Ranges", out IEnumerable<string>? values))
            {
                string acceptRanges = string.Join(",", values);
                bool supportsRanges = !acceptRanges.Equals("none", StringComparison.OrdinalIgnoreCase);

                _logger?.LogDebug("Server {Supports} range requests (Accept-Ranges: {AcceptRanges})",
                    supportsRanges ? "supports" : "does not support", acceptRanges);

                return supportsRanges;
            }

            // If no Accept-Ranges header, assume no support
            _logger?.LogDebug("No Accept-Ranges header found, assuming no range support");
            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "HEAD request failed, attempting range request test");
            // If HEAD fails, try a small range request
            return await TestRangeRequestAsync(url, options, cancellationToken);
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

        using HttpClient httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            long fileSize = response.Content.Headers.ContentLength ?? -1;
            _logger?.LogDebug("File size for {Url}: {Size} bytes", url, fileSize);

            return fileSize;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to get file size for {Url}", url);
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

        using HttpClient httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        ConfigureRequest(request, options);

        // Set range header
        request.Headers.Range = new RangeHeaderValue(range.Start, range.End);

        _logger?.LogDebug("Downloading range {Start}-{End} from {Url}", range.Start, range.End, url);

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Verify we got a partial content response if range was requested
            if (response.StatusCode != HttpStatusCode.PartialContent && range.Start > 0)
            {
                _logger?.LogWarning(
                    "Server returned {StatusCode} instead of 206 Partial Content for range request",
                    response.StatusCode);

                throw new InvalidOperationException(
                    $"Server did not honor range request. Expected 206 Partial Content, got {response.StatusCode}.");
            }

            await using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Use optimal buffer size (8KB is generally optimal for most scenarios)
            const int bufferSize = 8192;
            byte[] buffer = new byte[bufferSize];
            long totalBytesRead = 0;

            int bytesRead;
            while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;
                progress?.Report(totalBytesRead);
            }

            _logger?.LogDebug("Successfully downloaded {Bytes} bytes from range {Start}-{End}",
                totalBytesRead, range.Start, range.End);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to download range {Start}-{End} from {Url}",
                range.Start, range.End, url);
            throw;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger?.LogInformation("Download range {Start}-{End} was cancelled", range.Start, range.End);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout occurred
            _logger?.LogError(ex, "Timeout downloading range {Start}-{End} from {Url}",
                range.Start, range.End, url);
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

        using HttpClient httpClient = CreateHttpClient(options);
        using HttpRequestMessage request = new(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            _logger?.LogDebug("Fetching metadata for {Url}", url);

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            bool supportsRanges = false;
            if (response.Headers.TryGetValues("Accept-Ranges", out IEnumerable<string>? acceptRangeValues))
            {
                string acceptRanges = string.Join(",", acceptRangeValues);
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
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
            {
                if (!IsStandardHeader(header.Key))
                {
                    metadata.AdditionalHeaders[header.Key] = string.Join(",", header.Value);
                }
            }

            _logger?.LogDebug("Metadata fetched: Size={Size}, Type={Type}, Ranges={Ranges}",
                metadata.ContentLength, metadata.ContentType, metadata.SupportsRanges);

            return metadata;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to fetch metadata for {Url}", url);
            throw;
        }
    }

    private HttpClient CreateHttpClient(DownloadOptions options)
    {
        HttpClient client = _httpClientFactory.CreateClient("KurioDownloader");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        return client;
    }

    private static void ConfigureRequest(HttpRequestMessage request, DownloadOptions options)
    {
        // Set user agent
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd(options.UserAgent);

        // Add custom headers
        foreach ((string key, string value) in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        // Add authentication if provided
        if (!string.IsNullOrEmpty(options.Credentials))
        {
            string credentials = Convert.ToBase64String(
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
            _logger?.LogDebug("Testing range request support with byte range 0-0 for {Url}", url);

            using HttpClient httpClient = CreateHttpClient(options);
            using HttpRequestMessage request = new(HttpMethod.Get, url);
            ConfigureRequest(request, options);

            // Request only the first byte
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using HttpResponseMessage response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // If we get 206 Partial Content, range requests are supported
            bool supportsRanges = response.StatusCode == HttpStatusCode.PartialContent;

            _logger?.LogDebug("Range request test result: {Result}", supportsRanges);

            return supportsRanges;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Range request test failed for {Url}", url);
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
            string[] segments = url.Segments;
            if (segments.Length > 0)
            {
                string lastSegment = segments[^1];
                // Remove trailing slash if present
                lastSegment = lastSegment.TrimEnd('/');

                // Decode URL encoding
                string decoded = Uri.UnescapeDataString(lastSegment);

                // Remove query string if present
                int queryIndex = decoded.IndexOf('?');
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
