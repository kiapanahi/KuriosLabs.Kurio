using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Cli.Client;

/// <summary>
///     Request model for adding a new download.
/// </summary>
public record AddDownloadRequest
{
    /// <summary>
    ///     The URL to download from.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    ///     Optional custom filename for the download.
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    ///     Optional destination directory.
    /// </summary>
    public string? DestinationDirectory { get; init; }

    /// <summary>
    ///     Maximum number of concurrent connections for this download.
    /// </summary>
    public int? MaxConnections { get; init; }

    /// <summary>
    ///     Priority of the download in the queue.
    /// </summary>
    public DownloadPriority Priority { get; init; } = DownloadPriority.Normal;
}
