namespace Kurio.Core.Engine;

using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;

/// <summary>
/// Main orchestrator for download operations.
/// </summary>
public sealed class DownloadEngine : IDownloadEngine, IDisposable
{
    private readonly ConcurrentDictionary<Guid, DownloadTask> _tasks = new();
    private readonly IProtocolHandler _protocolHandler;
    private readonly IStorageManager _storageManager;
    private readonly ISegmentManager _segmentManager;
    private readonly Subject<DownloadProgress> _progressSubject = new();
    private readonly SemaphoreSlim _concurrencyLock;
    private readonly int _maxConcurrentDownloads;

    /// <inheritdoc />
    public IObservable<DownloadProgress> ProgressUpdates => _progressSubject;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadEngine"/> class.
    /// </summary>
    /// <param name="protocolHandler">The protocol handler for downloads.</param>
    /// <param name="storageManager">The storage manager for file operations.</param>
    /// <param name="segmentManager">The segment manager for multi-threaded downloads.</param>
    /// <param name="maxConcurrentDownloads">Maximum number of concurrent downloads.</param>
    public DownloadEngine(
        IProtocolHandler protocolHandler,
        IStorageManager storageManager,
        ISegmentManager segmentManager,
        int maxConcurrentDownloads = 3)
    {
        _protocolHandler = protocolHandler ?? throw new ArgumentNullException(nameof(protocolHandler));
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _maxConcurrentDownloads = maxConcurrentDownloads;
        _concurrencyLock = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
    }

    /// <inheritdoc />
    public async Task<IDownloadTask> AddDownloadAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        if (url == null)
        {
            throw new ArgumentNullException(nameof(url));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Create a new download task
        var task = new DownloadTask(url, options)
        {
            State = DownloadState.Queued
        };

        // Add to the dictionary
        if (!_tasks.TryAdd(task.Id, task))
        {
            throw new InvalidOperationException($"Task with ID {task.Id} already exists.");
        }

        return task;
    }

    /// <inheritdoc />
    public async Task StartDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task with ID {taskId} not found.");
        }

        if (task.State != DownloadState.Queued)
        {
            throw new InvalidOperationException(
                $"Task must be in Queued state to start. Current state: {task.State}");
        }

        // Start the download in the background
        _ = Task.Run(() => ExecuteDownloadAsync(task, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public Task PauseDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task with ID {taskId} not found.");
        }

        if (task.State != DownloadState.Downloading)
        {
            throw new InvalidOperationException(
                $"Only downloading tasks can be paused. Current state: {task.State}");
        }

        task.State = DownloadState.Paused;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ResumeDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task with ID {taskId} not found.");
        }

        if (task.State != DownloadState.Paused)
        {
            throw new InvalidOperationException(
                $"Only paused tasks can be resumed. Current state: {task.State}");
        }

        task.State = DownloadState.Queued;
        await StartDownloadAsync(taskId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelDownloadAsync(
        Guid taskId,
        bool removePartialFiles = false,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task with ID {taskId} not found.");
        }

        task.State = DownloadState.Cancelled;

        if (removePartialFiles)
        {
            await _storageManager.CleanupTemporaryFilesAsync(taskId, cancellationToken);
        }
    }

    /// <inheritdoc />
    public IDownloadTask? GetDownload(Guid taskId)
    {
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    /// <inheritdoc />
    public IEnumerable<IDownloadTask> GetDownloads(DownloadStateFilter filter)
    {
        return _tasks.Values.Where(task => filter.HasFlag(GetFilterForState(task.State)));
    }

    /// <summary>
    /// Executes the download for a task.
    /// </summary>
    private async Task ExecuteDownloadAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        // Wait for concurrency slot
        await _concurrencyLock.WaitAsync(cancellationToken);

        try
        {
            task.State = DownloadState.Analyzing;
            task.StartedAt = DateTime.UtcNow;

            // Get metadata
            task.Metadata = await _protocolHandler.GetMetadataAsync(
                task.Url,
                task.Options,
                cancellationToken);

            task.FileSize = task.Metadata.ContentLength;

            // Update filename if suggested by server
            if (!string.IsNullOrEmpty(task.Metadata.SuggestedFileName))
            {
                task.FileName = task.Metadata.SuggestedFileName;
            }

            // Check available disk space
            var availableSpace = await _storageManager.GetAvailableDiskSpaceAsync(
                task.Options.DestinationDirectory,
                cancellationToken);

            if (availableSpace < task.FileSize)
            {
                throw new InvalidOperationException(
                    $"Insufficient disk space. Required: {task.FileSize}, Available: {availableSpace}");
            }

            // Create temporary file
            var tempFilePath = await _storageManager.CreateTemporaryFileAsync(
                task.Id,
                task.FileName,
                task.FileSize,
                cancellationToken);

            // Calculate segments
            var segmentOptions = new SegmentOptions
            {
                MaxConnections = task.Options.MaxConnections,
                MinSegmentSize = task.Options.MinSegmentSize
            };

            var segmentConfig = _segmentManager.CalculateSegments(
                task.FileSize,
                task.Metadata.SupportsRanges,
                segmentOptions);

            // Start downloading
            task.State = DownloadState.Downloading;

            var progress = new Progress<SegmentProgress>(segmentProgress =>
            {
                // Aggregate progress from all segments
                var totalDownloaded = segmentConfig.States.Sum(s => s.BytesDownloaded);
                var activeConnections = segmentConfig.States.Count(s => s.Status == SegmentStatus.Downloading);

                task.Progress.BytesDownloaded = totalDownloaded;
                task.Progress.TotalBytes = task.FileSize;
                task.Progress.ActiveConnections = activeConnections;

                _progressSubject.OnNext(task.Progress);
            });

            await _segmentManager.DownloadSegmentsAsync(
                _protocolHandler,
                task.Url,
                segmentConfig,
                tempFilePath,
                task.Options,
                progress,
                cancellationToken);

            // Commit the download
            var finalPath = await _storageManager.CommitDownloadAsync(
                tempFilePath,
                task.Options.DestinationDirectory,
                task.FileName,
                task.Options.FileNamingPolicy,
                cancellationToken);

            // Mark as completed
            task.State = DownloadState.Completed;
            task.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            task.State = DownloadState.Failed;
            task.LastError = new DownloadError
            {
                Message = ex.Message,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace,
                IsRecoverable = IsRecoverableError(ex)
            };
            task.RetryCount++;
        }
        finally
        {
            _concurrencyLock.Release();
        }
    }

    private static DownloadStateFilter GetFilterForState(DownloadState state)
    {
        return state switch
        {
            DownloadState.Created => DownloadStateFilter.Created,
            DownloadState.Queued => DownloadStateFilter.Queued,
            DownloadState.Analyzing => DownloadStateFilter.Analyzing,
            DownloadState.Downloading => DownloadStateFilter.Downloading,
            DownloadState.Paused => DownloadStateFilter.Paused,
            DownloadState.Completed => DownloadStateFilter.Completed,
            DownloadState.Failed => DownloadStateFilter.Failed,
            DownloadState.Cancelled => DownloadStateFilter.Cancelled,
            _ => DownloadStateFilter.None
        };
    }

    private static bool IsRecoverableError(Exception ex)
    {
        return ex is HttpRequestException or TimeoutException or IOException;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _progressSubject?.Dispose();
        _concurrencyLock?.Dispose();
    }
}
