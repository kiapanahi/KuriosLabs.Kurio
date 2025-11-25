namespace Kurio.Core.Models;

/// <summary>
/// Progress information for segment downloads.
/// </summary>
public sealed class SegmentProgress
{
    /// <summary>
    /// Gets or sets the segment index.
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes downloaded for this segment.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    /// Gets or sets the current segment status.
    /// </summary>
    public SegmentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of this progress update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
