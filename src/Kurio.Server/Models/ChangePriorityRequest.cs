using Kurio.Core.Models;

namespace KuriousLabs.Kurio.Server.Models;

/// <summary>
///     Request model for changing download priority.
/// </summary>
public record ChangePriorityRequest
{
    /// <summary>
    ///     New priority for the download.
    /// </summary>
    public required DownloadPriority Priority { get; init; }
}
