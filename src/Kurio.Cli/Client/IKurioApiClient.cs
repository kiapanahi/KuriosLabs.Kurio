using Kurio.Core.Models;

namespace KuriousLabs.Kurio.Cli.Client;

/// <summary>
///     Interface for communicating with the Kurio server API.
/// </summary>
public interface IKurioApiClient : IAsyncDisposable
{
    /// <summary>
    ///     Gets the current connection state of the client.
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    ///     Event raised when the connection state changes.
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    ///     Connects to the Kurio server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Disconnects from the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Adds a new download to the queue.
    /// </summary>
    /// <param name="request">Download details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created download details.</returns>
    Task<DownloadResponse> AddDownloadAsync(
        AddDownloadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all downloads with optional filtering.
    /// </summary>
    /// <param name="filter">State filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of downloads.</returns>
    Task<List<DownloadResponse>> GetDownloadsAsync(
        DownloadStateFilter filter = DownloadStateFilter.All,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a specific download by ID.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download details or null if not found.</returns>
    Task<DownloadResponse?> GetDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Starts a queued download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartDownloadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pauses an active download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PauseDownloadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resumes a paused download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ResumeDownloadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cancels a download and optionally removes partial files.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="removeFiles">Whether to remove partial files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CancelDownloadAsync(
        Guid id,
        bool removeFiles = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Changes the priority of a queued download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="priority">New priority.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful.</returns>
    Task<bool> ChangePriorityAsync(
        Guid id,
        DownloadPriority priority,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Pauses all active downloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of downloads paused.</returns>
    Task<int> PauseAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Clears all completed downloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ClearCompletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets queue statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Queue statistics.</returns>
    Task<QueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Streams progress updates for downloads.
    /// </summary>
    /// <param name="taskId">Optional task ID to filter updates. If null, streams all progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of progress updates.</returns>
    IAsyncEnumerable<DownloadProgressDto> StreamProgressAsync(
        Guid? taskId = null,
        CancellationToken cancellationToken = default);
}
