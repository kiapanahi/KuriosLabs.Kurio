using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
/// Provides storage and retrieval of download history.
/// </summary>
public interface IDownloadHistoryRepository
{
    /// <summary>
    /// Adds a download history entry.
    /// </summary>
    /// <param name="entry">The history entry to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a download history entry by its identifier.
    /// </summary>
    /// <param name="id">The download identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The history entry, or null if not found.</returns>
    Task<DownloadHistoryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all download history entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All history entries.</returns>
    Task<IReadOnlyList<DownloadHistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets download history entries within a date range.
    /// </summary>
    /// <param name="from">The start date (inclusive).</param>
    /// <param name="to">The end date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>History entries within the date range.</returns>
    Task<IReadOnlyList<DownloadHistoryEntry>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets completed downloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All completed download entries.</returns>
    Task<IReadOnlyList<DownloadHistoryEntry>> GetCompletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed downloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All failed download entries.</returns>
    Task<IReadOnlyList<DownloadHistoryEntry>> GetFailedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches download history by file name or URL.
    /// </summary>
    /// <param name="searchTerm">The search term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching history entries.</returns>
    Task<IReadOnlyList<DownloadHistoryEntry>> SearchAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes history entries older than the specified retention period.
    /// </summary>
    /// <param name="retentionPeriod">The retention period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> CleanupOldEntriesAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific history entry.
    /// </summary>
    /// <param name="id">The download identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the entry was deleted; false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all download history.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of history entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total count of entries.</returns>
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
