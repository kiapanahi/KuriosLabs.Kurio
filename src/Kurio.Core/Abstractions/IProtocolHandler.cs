namespace Kurio.Core.Abstractions;

using Kurio.Core.Models;

/// <summary>
/// Abstraction for protocol-specific download operations.
/// </summary>
public interface IProtocolHandler
{
    /// <summary>
    /// Gets the set of supported protocol schemes (e.g., "http", "https", "ftp").
    /// </summary>
    IReadOnlySet<string> SupportedSchemes { get; }

    /// <summary>
    /// Checks if the server supports range requests (partial downloads).
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <param name="options">Download options including headers and authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if range requests are supported; otherwise, false.</returns>
    Task<bool> SupportsRangeRequestsAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total file size of the remote resource.
    /// </summary>
    /// <param name="url">The URL of the resource.</param>
    /// <param name="options">Download options including headers and authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file size in bytes, or -1 if unknown.</returns>
    Task<long> GetFileSizeAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific byte range from the remote resource.
    /// </summary>
    /// <param name="url">The URL of the resource.</param>
    /// <param name="range">The byte range to download.</param>
    /// <param name="destination">The stream to write the downloaded data to.</param>
    /// <param name="options">Download options including headers and authentication.</param>
    /// <param name="progress">Progress reporting callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadRangeAsync(
        Uri url,
        ByteRange range,
        Stream destination,
        DownloadOptions options,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata about the remote resource (ETag, Last-Modified, Content-Type, etc.).
    /// </summary>
    /// <param name="url">The URL of the resource.</param>
    /// <param name="options">Download options including headers and authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resource metadata.</returns>
    Task<ResourceMetadata> GetMetadataAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default);
}
