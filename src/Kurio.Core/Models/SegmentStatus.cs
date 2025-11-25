namespace Kurio.Core.Models;

/// <summary>
/// Status of an individual download segment.
/// </summary>
public enum SegmentStatus
{
    /// <summary>
    /// Segment has not started downloading.
    /// </summary>
    Pending,

    /// <summary>
    /// Segment is currently being downloaded.
    /// </summary>
    Downloading,

    /// <summary>
    /// Segment download has been paused.
    /// </summary>
    Paused,

    /// <summary>
    /// Segment has completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Segment download failed.
    /// </summary>
    Failed
}
