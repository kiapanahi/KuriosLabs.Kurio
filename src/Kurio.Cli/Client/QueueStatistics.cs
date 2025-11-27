namespace KuriousLabs.Kurio.Cli.Client;

/// <summary>
/// Statistics about the download queue.
/// </summary>
public record QueueStatistics
{
    /// <summary>
    /// Number of currently active downloads.
    /// </summary>
    public required int ActiveDownloads { get; init; }

    /// <summary>
    /// Number of queued downloads waiting to start.
    /// </summary>
    public required int QueuedDownloads { get; init; }

    /// <summary>
    /// Total number of downloads (all states).
    /// </summary>
    public required int TotalDownloads { get; init; }

    /// <summary>
    /// Number of completed downloads.
    /// </summary>
    public int CompletedDownloads { get; init; }

    /// <summary>
    /// Number of failed downloads.
    /// </summary>
    public int FailedDownloads { get; init; }
}
