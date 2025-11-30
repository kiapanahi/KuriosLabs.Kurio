using KuriousLabs.Kurio.Core.Configuration;
using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Server.Models;

/// <summary>
///     Request model for adding a new download to the queue.
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
    ///     Optional destination directory. If not specified, uses the default from configuration.
    /// </summary>
    public string? DestinationDirectory { get; init; }

    /// <summary>
    ///     Maximum number of concurrent connections for this download. If not specified, uses the default from configuration.
    /// </summary>
    public int? MaxConnections { get; init; }

    /// <summary>
    ///     Priority of the download in the queue.
    /// </summary>
    public DownloadPriority Priority { get; init; } = DownloadPriority.Normal;

    /// <summary>
    ///     Converts this request to DownloadOptions for the engine.
    /// </summary>
    public DownloadOptions ToDownloadOptions()
    {
        DownloadOptionsBuilder builder = new();

        // Set destination directory and/or filename
        if (!string.IsNullOrWhiteSpace(DestinationDirectory) || !string.IsNullOrWhiteSpace(FileName))
        {
            builder.WithDestination(
                DestinationDirectory ?? string.Empty,
                FileName);
        }

        if (MaxConnections.HasValue)
        {
            builder.WithMaxConnections(MaxConnections.Value);
        }

        return builder.Build();
    }
}
