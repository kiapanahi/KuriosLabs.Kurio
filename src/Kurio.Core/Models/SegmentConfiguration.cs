namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Configuration for download segmentation.
/// </summary>
public sealed class SegmentConfiguration
{
    /// <summary>
    ///     Gets or sets the total file size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Gets or sets the number of segments.
    /// </summary>
    public int SegmentCount { get; set; }

    /// <summary>
    ///     Gets or sets whether range requests are supported.
    /// </summary>
    public bool SupportsRanges { get; set; }

    /// <summary>
    ///     Gets or sets the byte ranges for each segment.
    /// </summary>
    public ByteRange[] Ranges { get; set; } = Array.Empty<ByteRange>();

    /// <summary>
    ///     Gets or sets the states for each segment.
    /// </summary>
    public SegmentState[] States { get; set; } = Array.Empty<SegmentState>();
}
