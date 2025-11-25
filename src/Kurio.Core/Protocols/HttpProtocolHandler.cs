namespace Kurio.Core.Protocols;

using System.Net.Http;
using System.Net.Http.Headers;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;

/// <summary>
/// HTTP/HTTPS protocol handler implementation.
/// </summary>
public sealed class HttpProtocolHandler : IProtocolHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly HashSet<string> s_supportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http",
        "https"
    };

    /// <inheritdoc />
    public IReadOnlySet<string> SupportedSchemes => s_supportedSchemes;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpProtocolHandler"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public HttpProtocolHandler(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    /// <inheritdoc />
    public async Task<bool> SupportsRangeRequestsAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Check for Accept-Ranges header
            if (response.Headers.TryGetValues("Accept-Ranges", out var values))
            {
                var acceptRanges = string.Join(",", values);
                return !acceptRanges.Equals("none", StringComparison.OrdinalIgnoreCase);
            }

            // If no Accept-Ranges header, assume no support
            return false;
        }
        catch (HttpRequestException)
        {
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
        using var httpClient = CreateHttpClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return response.Content.Headers.ContentLength ?? -1;
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
        using var httpClient = CreateHttpClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ConfigureRequest(request, options);

        // Set range header
        request.Headers.Range = new RangeHeaderValue(range.Start, range.End);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        // Verify we got a partial content response if range was requested
        if (response.StatusCode != System.Net.HttpStatusCode.PartialContent &&
            range.Start > 0)
        {
            throw new InvalidOperationException(
                "Server did not honor range request. Expected 206 Partial Content.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[options.MinSegmentSize > 8192 ? 8192 : (int)options.MinSegmentSize];
        long totalBytesRead = 0;

        int bytesRead;
        while ((bytesRead = await responseStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }

    /// <inheritdoc />
    public async Task<ResourceMetadata> GetMetadataAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = CreateHttpClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        ConfigureRequest(request, options);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var metadata = new ResourceMetadata
        {
            ContentLength = response.Content.Headers.ContentLength ?? -1,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            ETag = response.Headers.ETag?.Tag,
            LastModified = response.Content.Headers.LastModified,
            SupportsRanges = await SupportsRangeRequestsAsync(url, options, cancellationToken)
        };

        // Try to get filename from Content-Disposition header
        if (response.Content.Headers.ContentDisposition is { } contentDisposition)
        {
            metadata.SuggestedFileName = contentDisposition.FileName?.Trim('"') ??
                                        contentDisposition.FileNameStar;
        }

        // Store additional headers
        foreach (var header in response.Headers)
        {
            if (!IsStandardHeader(header.Key))
            {
                metadata.AdditionalHeaders[header.Key] = string.Join(",", header.Value);
            }
        }

        return metadata;
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
                System.Text.Encoding.UTF8.GetBytes(options.Credentials));
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
            using var httpClient = CreateHttpClient(options);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            ConfigureRequest(request, options);

            // Request only the first byte
            request.Headers.Range = new RangeHeaderValue(0, 0);

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            // If we get 206 Partial Content, range requests are supported
            return response.StatusCode == System.Net.HttpStatusCode.PartialContent;
        }
        catch
        {
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
}
