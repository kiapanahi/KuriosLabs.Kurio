using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
///     Provides download statistics calculation and aggregation.
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    ///     Gets the current download statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current download statistics.</returns>
    Task<DownloadStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a completed download for statistics.
    /// </summary>
    /// <param name="entry">The download history entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordCompletedDownloadAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records a failed download for statistics.
    /// </summary>
    /// <param name="entry">The download history entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RecordFailedDownloadAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resets session statistics while preserving all-time statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResetSessionStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Exports statistics to a dictionary for serialization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary containing the statistics.</returns>
    Task<IDictionary<string, object>> ExportStatisticsAsync(CancellationToken cancellationToken = default);
}
