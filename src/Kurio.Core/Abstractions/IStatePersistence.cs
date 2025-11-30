using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Core.Abstractions;

/// <summary>
///     Provides functionality for persisting and loading download task state.
/// </summary>
public interface IStatePersistence
{
    /// <summary>
    ///     Gets the directory where state files are stored.
    /// </summary>
    string StateDirectory { get; }

    /// <summary>
    ///     Saves the state of a download task.
    /// </summary>
    /// <param name="state">The download task state to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveStateAsync(DownloadTaskState state, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads the state of a download task.
    /// </summary>
    /// <param name="taskId">The task ID to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The download task state, or null if not found.</returns>
    Task<DownloadTaskState?> LoadStateAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes the persisted state of a download task.
    /// </summary>
    /// <param name="taskId">The task ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteStateAsync(Guid taskId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Loads all persisted download task states.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of all persisted states.</returns>
    Task<IReadOnlyList<DownloadTaskState>> LoadAllStatesAsync(CancellationToken cancellationToken = default);
}
