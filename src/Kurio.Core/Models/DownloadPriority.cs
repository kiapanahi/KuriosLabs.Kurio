namespace Kurio.Core.Models;

/// <summary>
/// Priority level for download tasks in the queue.
/// </summary>
public enum DownloadPriority
{
    /// <summary>
    /// Low priority download.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority download (default).
    /// </summary>
    Normal = 1,

    /// <summary>
    /// High priority download.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority download (starts immediately if possible).
    /// </summary>
    Critical = 3
}
