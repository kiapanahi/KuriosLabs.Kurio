namespace Kurio.Core.Models;

/// <summary>
///     Filter for querying downloads by state.
/// </summary>
[Flags]
public enum DownloadStateFilter
{
    /// <summary>
    ///     No filter (matches none).
    /// </summary>
    None = 0,

    /// <summary>
    ///     Include created downloads.
    /// </summary>
    Created = 1 << 0,

    /// <summary>
    ///     Include queued downloads.
    /// </summary>
    Queued = 1 << 1,

    /// <summary>
    ///     Include analyzing downloads.
    /// </summary>
    Analyzing = 1 << 2,

    /// <summary>
    ///     Include downloading downloads.
    /// </summary>
    Downloading = 1 << 3,

    /// <summary>
    ///     Include paused downloads.
    /// </summary>
    Paused = 1 << 4,

    /// <summary>
    ///     Include completed downloads.
    /// </summary>
    Completed = 1 << 5,

    /// <summary>
    ///     Include failed downloads.
    /// </summary>
    Failed = 1 << 6,

    /// <summary>
    ///     Include cancelled downloads.
    /// </summary>
    Cancelled = 1 << 7,

    /// <summary>
    ///     Include all active downloads (Queued | Analyzing | Downloading).
    /// </summary>
    Active = Queued | Analyzing | Downloading,

    /// <summary>
    ///     Include all downloads.
    /// </summary>
    All = Created | Queued | Analyzing | Downloading | Paused | Completed | Failed | Cancelled
}
