namespace Kurio.Core.Models;

/// <summary>
///     Represents the state of an individual download segment.
/// </summary>
public sealed class SegmentState
{
    /// <summary>
    ///     Gets or sets the segment index.
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    ///     Gets or sets the starting byte position (inclusive).
    /// </summary>
    public long StartByte { get; set; }

    /// <summary>
    ///     Gets or sets the ending byte position (inclusive).
    /// </summary>
    public long EndByte { get; set; }

    /// <summary>
    ///     Gets or sets the number of bytes downloaded for this segment.
    /// </summary>
    public long BytesDownloaded { get; set; }

    /// <summary>
    ///     Gets or sets the current status of this segment.
    /// </summary>
    public SegmentStatus Status { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when this segment started downloading.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when this segment completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Gets or sets the number of retry attempts for this segment.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    ///     Gets or sets the checksum information for this segment.
    /// </summary>
    public SegmentChecksum? Checksum { get; set; }

    /// <summary>
    ///     Gets or sets the path to the segment file (for per-segment file mode).
    /// </summary>
    public string? SegmentFilePath { get; set; }

    /// <summary>
    ///     Gets the total size of this segment in bytes.
    /// </summary>
    public long TotalSize => EndByte - StartByte + 1;

    /// <summary>
    ///     Gets whether this segment is complete.
    /// </summary>
    public bool IsComplete => BytesDownloaded >= TotalSize;

    /// <summary>
    ///     Gets whether this segment has a verified checksum.
    /// </summary>
    public bool HasVerifiedChecksum => Checksum?.IsVerified == true;
}
