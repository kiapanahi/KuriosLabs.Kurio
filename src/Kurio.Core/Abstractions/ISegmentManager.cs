namespace Kurio.Core.Abstractions;

using Kurio.Core.Models;

/// <summary>
/// Manages download segmentation and parallel downloading.
/// </summary>
public interface ISegmentManager
{
    /// <summary>
    /// Calculates the optimal segment configuration for a download.
    /// </summary>
    /// <param name="fileSize">The total file size in bytes.</param>
    /// <param name="supportsRanges">Whether the server supports range requests.</param>
    /// <param name="options">Segmentation options.</param>
    /// <returns>The segment configuration.</returns>
    SegmentConfiguration CalculateSegments(
        long fileSize,
        bool supportsRanges,
        SegmentOptions options);

    /// <summary>
    /// Downloads all segments in parallel.
    /// </summary>
    /// <param name="handler">The protocol handler to use for downloading.</param>
    /// <param name="url">The URL of the resource.</param>
    /// <param name="config">The segment configuration.</param>
    /// <param name="tempFilePath">The temporary file path to write segments to.</param>
    /// <param name="options">Download options.</param>
    /// <param name="progress">Progress reporting callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DownloadSegmentsAsync(
        IProtocolHandler handler,
        Uri url,
        SegmentConfiguration config,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes downloading incomplete segments.
    /// </summary>
    /// <param name="handler">The protocol handler to use for downloading.</param>
    /// <param name="url">The URL of the resource.</param>
    /// <param name="config">The segment configuration.</param>
    /// <param name="segmentStates">The current state of each segment.</param>
    /// <param name="tempFilePath">The temporary file path to write segments to.</param>
    /// <param name="options">Download options.</param>
    /// <param name="progress">Progress reporting callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResumeSegmentsAsync(
        IProtocolHandler handler,
        Uri url,
        SegmentConfiguration config,
        SegmentState[] segmentStates,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
