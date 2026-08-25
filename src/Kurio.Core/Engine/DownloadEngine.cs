using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;
using KuriousLabs.Kurio.Core.Queue;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Engine;

/// <summary>
///     Main orchestrator for download operations.
/// </summary>
public sealed class DownloadEngine : IDownloadEngine, IDisposable
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly ILogger<DownloadEngine> _logger;
    private readonly Channel<DownloadProgress> _progressChannel;
    private readonly IProtocolHandler _protocolHandler;
    private readonly IDownloadQueueManager _queueManager;
    private readonly ConcurrentDictionary<Guid, bool> _resumingTasks = new();
    private readonly Timer _schedulerTimer;
    private readonly ConcurrentDictionary<Guid, SegmentConfiguration> _segmentConfigs = new();
    private readonly ISegmentManager _segmentManager;
    private readonly IStatePersistence _statePersistence;
    private readonly IStorageManager _storageManager;
    private readonly ConcurrentDictionary<Guid, DownloadTask> _tasks = new();
    private readonly ConcurrentDictionary<Guid, string> _tempFilePaths = new();
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DownloadEngine" /> class.
    /// </summary>
    /// <param name="protocolHandler">The protocol handler for downloads.</param>
    /// <param name="storageManager">The storage manager for file operations.</param>
    /// <param name="segmentManager">The segment manager for multi-threaded downloads.</param>
    /// <param name="statePersistence">The state persistence layer.</param>
    /// <param name="maxConcurrentDownloads">Maximum number of concurrent downloads.</param>
    public DownloadEngine(
        IProtocolHandler protocolHandler,
        IStorageManager storageManager,
        ISegmentManager segmentManager,
        IStatePersistence statePersistence,
        ILogger<DownloadEngine> logger,
        IDownloadQueueManager? queueManager = null,
        int maxConcurrentDownloads = 3)
    {
        _protocolHandler = protocolHandler ?? throw new ArgumentNullException(nameof(protocolHandler));
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _statePersistence = statePersistence ?? throw new ArgumentNullException(nameof(statePersistence));
        _queueManager = queueManager ?? new DownloadQueueManager { MaxConcurrentDownloads = maxConcurrentDownloads };
        _logger = logger;

        // Initialize unbounded channel for progress updates
        _progressChannel = Channel.CreateUnbounded<DownloadProgress>(new UnboundedChannelOptions
        {
            SingleWriter = false, // Multiple download tasks can publish
            SingleReader = false // Multiple clients can consume
        });

        // Start scheduler timer (check every 500ms)
        _schedulerTimer = new Timer(ScheduleNextDownloads, null, TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(500));

        // Load persisted states on initialization
        _ = Task.Run(RecoverPersistedStatesAsync);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop scheduler timer
        _schedulerTimer?.Dispose();

        // Cancel all ongoing downloads
        foreach (var cts in _cancellationTokens.Values)
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        _cancellationTokens.Clear();

        // Complete the channel (no more writes)
        _progressChannel.Writer.Complete();
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var progress in _progressChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            // Filter by task ID if specified
            if (taskId == null || progress.TaskId == taskId)
            {
                yield return progress;
            }
        }
    }

    /// <inheritdoc />
    public async Task<IDownloadTask> AddDownloadAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        ArgumentNullException.ThrowIfNull(options);

        // Create a new download task
        DownloadTask task = new(url, options) { State = DownloadState.Queued };

        // Add to the dictionary
        if (!_tasks.TryAdd(task.Id, task))
        {
            throw new InvalidOperationException($"Task with ID {task.Id} already exists.");
        }

        // Add to queue for scheduling
        _queueManager.Enqueue(task);
        _logger.LogTaskQueued(task.Id, task.Priority.ToString());

        // Save initial state
        await SaveTaskStateAsync(task, cancellationToken).ConfigureAwait(false);

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

        // Mark as started in queue
        _queueManager.MarkAsStarted(taskId);

        // Start the download in the background
        _ = Task.Run(() => ExecuteDownloadAsync(task, cancellationToken), cancellationToken);
    }

    /// <inheritdoc />
    public async Task PauseDownloadAsync(
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

        // Set state to Paused BEFORE canceling to ensure the catch block sees it
        task.State = DownloadState.Paused;

        // Mark as paused in queue
        _queueManager.MarkAsPaused(taskId);

        // Cancel the download operation
        if (_cancellationTokens.TryGetValue(taskId, out var cts))
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        // Save state for resume
        await SaveTaskStateAsync(task, cancellationToken).ConfigureAwait(false);
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

        // Validate that the download can be resumed
        await ValidateResumeCapabilityAsync(task, cancellationToken).ConfigureAwait(false);

        // Mark this task as resuming so scheduler knows to call ExecuteResumeAsync
        _resumingTasks.TryAdd(task.Id, true);

        task.State = DownloadState.Queued;

        // Re-queue the task
        _queueManager.Enqueue(task);

        await SaveTaskStateAsync(task, cancellationToken).ConfigureAwait(false);
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

        _logger.LogTaskCancelled(taskId, removePartialFiles);

        // Cancel the download operation
        if (_cancellationTokens.TryGetValue(taskId, out var cts))
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        task.State = DownloadState.Cancelled;

        // Remove from queue if present
        _queueManager.Dequeue(taskId);

        if (removePartialFiles)
        {
            await _storageManager.CleanupTemporaryFilesAsync(taskId, cancellationToken).ConfigureAwait(false);
        }

        // Delete persisted state
        await _statePersistence.DeleteStateAsync(taskId, cancellationToken).ConfigureAwait(false);

        // Cleanup tracking
        _cancellationTokens.TryRemove(taskId, out _);
        _segmentConfigs.TryRemove(taskId, out _);
        _tempFilePaths.TryRemove(taskId, out _);
        _resumingTasks.TryRemove(taskId, out _);
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

    /// <inheritdoc />
    public bool ChangePriority(Guid taskId, DownloadPriority newPriority)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return false;
        }

        if (task.State != DownloadState.Queued)
        {
            return false;
        }

        return _queueManager.ChangePriority(taskId, newPriority);
    }

    /// <inheritdoc />
    public bool MoveUp(Guid taskId)
    {
        return _queueManager.MoveUp(taskId);
    }

    /// <inheritdoc />
    public bool MoveDown(Guid taskId)
    {
        return _queueManager.MoveDown(taskId);
    }

    /// <inheritdoc />
    public async Task<int> PauseAllAsync(CancellationToken cancellationToken = default)
    {
        var activeTasks = _queueManager.GetActiveTasks();
        var activeCount = activeTasks.Count;
        _logger.LogPausingAll(activeCount);

        var pausedCount = 0;

        foreach (var task in activeTasks)
        {
            try
            {
                await PauseDownloadAsync(task.Id, cancellationToken).ConfigureAwait(false);
                pausedCount++;
            }
            catch
            {
                // Continue with other tasks
            }
        }

        _logger.LogPausedAll(pausedCount, activeCount);
        return pausedCount;
    }

    /// <inheritdoc />
    public void ClearCompleted()
    {
        // Get completed tasks before clearing
        var completedTasks = _tasks.Values
            .Where(t => t.State == DownloadState.Completed)
            .Select(t => t.Id)
            .ToList();

        _logger.LogClearingCompleted(completedTasks.Count);

        _queueManager.ClearCompleted();

        // Also remove from tasks dictionary and clean up tracking
        foreach (var taskId in completedTasks)
        {
            CleanupTaskTracking(taskId, includeCancellationToken: true);
            _tasks.TryRemove(taskId, out _);
        }
    }

    /// <summary>
    ///     Cleans up all tracking data for a task from internal dictionaries.
    ///     This should be called when a download reaches a final state (completed, failed, cancelled).
    ///     Note: Cancellation tokens are handled separately in the finally blocks of ExecuteDownloadAsync/ExecuteResumeAsync.
    /// </summary>
    /// <param name="taskId">The ID of the task to clean up.</param>
    /// <param name="includeCancellationToken">Whether to also clean up the cancellation token. Default is false since finally blocks handle this.</param>
    private void CleanupTaskTracking(Guid taskId, bool includeCancellationToken = false)
    {
        if (includeCancellationToken)
        {
            _cancellationTokens.TryRemove(taskId, out var cts);
            cts?.Dispose();
        }

        _segmentConfigs.TryRemove(taskId, out _);
        _tempFilePaths.TryRemove(taskId, out _);
        _resumingTasks.TryRemove(taskId, out _);
    }

    /// <inheritdoc />
    public (int Active, int Queued) GetQueueStatistics()
    {
        return (_queueManager.ActiveDownloadsCount, _queueManager.QueuedDownloadsCount);
    }

    /// <summary>
    ///     Scheduler callback that starts queued downloads when slots are available.
    /// </summary>
    private void ScheduleNextDownloads(object? state)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            // Check if we can start new downloads
            while (_queueManager.CanStartNewDownload())
            {
                // GetNextTask atomically claims an active slot for the task
                var nextTask = _queueManager.GetNextTask();
                if (nextTask == null)
                {
                    break; // No more tasks in queue
                }

                _logger.LogTaskStarted(nextTask.Id);

                // Check if this is a resume or new download
                if (_resumingTasks.TryRemove(nextTask.Id, out _))
                {
                    _ = Task.Run(() => ExecuteResumeAsync((DownloadTask)nextTask, CancellationToken.None));
                }
                else
                {
                    _ = Task.Run(() => ExecuteDownloadAsync((DownloadTask)nextTask, CancellationToken.None));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogSchedulerError(ex);
        }
    }

    /// <summary>
    ///     Executes the download for a task.
    /// </summary>
    private async Task ExecuteDownloadAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        // Create a linked cancellation token source
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[task.Id] = cts;
        var linkedToken = cts.Token;

        try
        {
            _logger.LogDownloadStarting(task.Id, task.Url.ToString());

            task.State = DownloadState.Analyzing;
            task.StartedAt = DateTime.UtcNow;

            _logger.LogDownloadAnalyzing(task.Id);

            // Get metadata
            task.Metadata = await _protocolHandler.GetMetadataAsync(
                task.Url,
                task.Options,
                linkedToken).ConfigureAwait(false);

            task.FileSize = task.Metadata.ContentLength;

            // Update filename if suggested by server, but never override a
            // filename the caller explicitly requested via options
            if (string.IsNullOrEmpty(task.Options.FileName) &&
                !string.IsNullOrEmpty(task.Metadata.SuggestedFileName))
            {
                task.FileName = task.Metadata.SuggestedFileName;
            }

            _logger.LogDownloadMetadata(task.Id, task.FileSize, task.FileName);

            // Check available disk space
            var availableSpace = await _storageManager.GetAvailableDiskSpaceAsync(
                task.Options.DestinationDirectory,
                linkedToken).ConfigureAwait(false);

            if (availableSpace < task.FileSize)
            {
                _logger.LogInsufficientDiskSpace(task.Id, task.FileSize, availableSpace);
                throw new InvalidOperationException(
                    $"Insufficient disk space. Required: {task.FileSize}, Available: {availableSpace}");
            }

            // Create temporary file
            var tempFilePath = await _storageManager.CreateTemporaryFileAsync(
                task.Id,
                task.FileName,
                task.FileSize,
                linkedToken).ConfigureAwait(false);

            _tempFilePaths[task.Id] = tempFilePath;
            _logger.LogTempFileCreated(task.Id, tempFilePath);

            // Calculate segments
            SegmentOptions segmentOptions = new()
            {
                MaxConnections = task.Options.MaxConnections,
                MinSegmentSize = task.Options.MinSegmentSize
            };

            var segmentConfig = _segmentManager.CalculateSegments(
                task.FileSize,
                task.Metadata.SupportsRanges,
                segmentOptions);

            _segmentConfigs[task.Id] = segmentConfig;

            // Start downloading
            task.State = DownloadState.Downloading;
            _logger.LogDownloadBeginning(task.Id, segmentConfig.SegmentCount);

            // Save initial state
            await SaveTaskStateAsync(task, linkedToken).ConfigureAwait(false);

            var lastStateSave = DateTime.UtcNow;
            Progress<SegmentProgress> progress = new(segmentProgress =>
            {
                // Aggregate progress from all segments
                var totalDownloaded = segmentConfig.States.Sum(s => s.BytesDownloaded);
                var activeConnections = segmentConfig.States.Count(s => s.Status == SegmentStatus.Downloading);

                task.Progress.TaskId = task.Id;
                task.Progress.BytesDownloaded = totalDownloaded;
                task.Progress.TotalBytes = task.FileSize;
                task.Progress.ActiveConnections = activeConnections;
                task.Progress.Timestamp = DateTime.UtcNow;

                _progressChannel.Writer.TryWrite(task.Progress);

                // Periodically save state to disk (every 5 seconds)
                if ((DateTime.UtcNow - lastStateSave).TotalSeconds >= 5)
                {
                    lastStateSave = DateTime.UtcNow;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SaveTaskStateAsync(task, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignore save errors during download - they shouldn't interrupt the download
                        }
                    });
                }
            });

            await _segmentManager.DownloadSegmentsAsync(
                _protocolHandler,
                task.Url,
                segmentConfig,
                tempFilePath,
                task.Options,
                progress,
                linkedToken).ConfigureAwait(false);

            // Merge segment files into the final temp file. This must also run for a
            // single segment: segments are always written to segment_NNNN.part files
            // and the commit path below does not exist until the merge produces it.
            _logger.LogMergingSegments(segmentConfig.SegmentCount, task.Id);

            await _storageManager.MergeSegmentFilesAsync(
                task.Id,
                tempFilePath,
                segmentConfig.SegmentCount,
                linkedToken).ConfigureAwait(false);

            // Commit the download
            var finalPath = await _storageManager.CommitDownloadAsync(
                tempFilePath,
                task.Options.DestinationDirectory,
                task.FileName,
                task.Options.FileNamingPolicy,
                linkedToken).ConfigureAwait(false);

            _logger.LogDownloadCompleted(task.Id, finalPath);

            // Mark as completed
            task.State = DownloadState.Completed;
            task.CompletedAt = DateTime.UtcNow;

            // Notify queue manager
            _queueManager.MarkAsCompleted(task.Id);

            // Delete persisted state
            await _statePersistence.DeleteStateAsync(task.Id, CancellationToken.None).ConfigureAwait(false);

            // Cleanup temporary directory and state file
            await _storageManager.CleanupTemporaryFilesAsync(task.Id, CancellationToken.None).ConfigureAwait(false);

            // Cleanup tracking
            _segmentConfigs.TryRemove(task.Id, out _);
            _tempFilePaths.TryRemove(task.Id, out _);
        }
        catch (OperationCanceledException) when (task.State == DownloadState.Paused)
        {
            _logger.LogDownloadPaused(task.Id);
            // Download was paused - state already saved
        }
        catch (AggregateException ex) when (task.State == DownloadState.Paused &&
                                            ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            _logger.LogDownloadPaused(task.Id);
            // Download was paused - multiple segments canceled, state already saved
        }
        catch (Exception ex)
        {
            _logger.LogDownloadFailed(ex, task.Id);

            task.State = DownloadState.Failed;
            task.LastError = new DownloadError
            {
                Message = ex.Message,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace,
                IsRecoverable = IsRecoverableError(ex)
            };
            task.RetryCount++;

            // Notify queue manager
            _queueManager.MarkAsFailed(task.Id);

            // Save failed state
            await SaveTaskStateAsync(task, CancellationToken.None).ConfigureAwait(false);

            // Cleanup tracking for failed downloads (cancellation token handled in finally)
            CleanupTaskTracking(task.Id);
        }
        finally
        {
            _cancellationTokens.TryRemove(task.Id, out var removedCts);
            removedCts?.Dispose();
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

    /// <summary>
    ///     Executes resume for a paused download.
    /// </summary>
    private async Task ExecuteResumeAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        // Create a linked cancellation token source
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[task.Id] = cts;
        var linkedToken = cts.Token;

        try
        {
            _logger.LogDownloadResuming(task.Id);

            task.State = DownloadState.Downloading;

            // Get persisted segment configuration and temp file path
            if (!_segmentConfigs.TryGetValue(task.Id, out var segmentConfig) ||
                !_tempFilePaths.TryGetValue(task.Id, out var tempFilePath))
            {
                _logger.LogResumeConfigurationMissing(task.Id);
                throw new InvalidOperationException(
                    "Cannot resume download: segment configuration or temp file path not found");
            }

            var lastStateSave = DateTime.UtcNow;
            Progress<SegmentProgress> progress = new(segmentProgress =>
            {
                var totalDownloaded = segmentConfig.States.Sum(s => s.BytesDownloaded);
                var activeConnections = segmentConfig.States.Count(s => s.Status == SegmentStatus.Downloading);

                task.Progress.TaskId = task.Id;
                task.Progress.BytesDownloaded = totalDownloaded;
                task.Progress.TotalBytes = task.FileSize;
                task.Progress.ActiveConnections = activeConnections;
                task.Progress.Timestamp = DateTime.UtcNow;

                _progressChannel.Writer.TryWrite(task.Progress);

                // Periodically save state to disk (every 5 seconds)
                if ((DateTime.UtcNow - lastStateSave).TotalSeconds >= 5)
                {
                    lastStateSave = DateTime.UtcNow;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SaveTaskStateAsync(task, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignore save errors during download - they shouldn't interrupt the download
                        }
                    });
                }
            });

            // Resume incomplete segments
            await _segmentManager.ResumeSegmentsAsync(
                _protocolHandler,
                task.Url,
                segmentConfig,
                segmentConfig.States,
                tempFilePath,
                task.Options,
                progress,
                linkedToken).ConfigureAwait(false);

            // Merge segment files into the final temp file (also required for a
            // single segment; see ExecuteDownloadAsync)
            _logger.LogMergingResumedSegments(segmentConfig.SegmentCount, task.Id);

            await _storageManager.MergeSegmentFilesAsync(
                task.Id,
                tempFilePath,
                segmentConfig.SegmentCount,
                linkedToken).ConfigureAwait(false);

            // Commit the download
            var finalPath = await _storageManager.CommitDownloadAsync(
                tempFilePath,
                task.Options.DestinationDirectory,
                task.FileName,
                task.Options.FileNamingPolicy,
                linkedToken).ConfigureAwait(false);

            _logger.LogResumedDownloadCompleted(task.Id, finalPath);

            // Mark as completed
            task.State = DownloadState.Completed;
            task.CompletedAt = DateTime.UtcNow;

            // Notify queue manager
            _queueManager.MarkAsCompleted(task.Id);

            // Delete persisted state
            await _statePersistence.DeleteStateAsync(task.Id, CancellationToken.None).ConfigureAwait(false);

            // Cleanup temporary directory and state file
            await _storageManager.CleanupTemporaryFilesAsync(task.Id, CancellationToken.None).ConfigureAwait(false);

            // Cleanup tracking
            _segmentConfigs.TryRemove(task.Id, out _);
            _tempFilePaths.TryRemove(task.Id, out _);
        }
        catch (OperationCanceledException) when (task.State == DownloadState.Paused)
        {
            _logger.LogDownloadPaused(task.Id);
            // Download was paused again - state already saved
        }
        catch (AggregateException ex) when (task.State == DownloadState.Paused &&
                                            ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            _logger.LogDownloadPaused(task.Id);
            // Download was paused again - multiple segments canceled, state already saved
        }
        catch (Exception ex)
        {
            _logger.LogResumeFailed(ex, task.Id);

            task.State = DownloadState.Failed;
            task.LastError = new DownloadError
            {
                Message = ex.Message,
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.StackTrace,
                IsRecoverable = IsRecoverableError(ex)
            };
            task.RetryCount++;

            // Notify queue manager
            _queueManager.MarkAsFailed(task.Id);

            // Save failed state
            await SaveTaskStateAsync(task, CancellationToken.None).ConfigureAwait(false);

            // Cleanup tracking for failed downloads (cancellation token handled in finally)
            CleanupTaskTracking(task.Id);
        }
        finally
        {
            _cancellationTokens.TryRemove(task.Id, out var removedCts);
            removedCts?.Dispose();
        }
    }

    /// <summary>
    ///     Validates that a download can be resumed.
    /// </summary>
    private async Task ValidateResumeCapabilityAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        if (task.Metadata == null)
        {
            _logger.LogResumeMetadataMissing(task.Id);
            throw new InvalidOperationException("Cannot resume: metadata is missing");
        }

        if (!task.Metadata.SupportsRanges)
        {
            _logger.LogResumeRangeNotSupported(task.Id);
            throw new InvalidOperationException("Cannot resume: server does not support range requests");
        }

        // Fetch current metadata to validate
        var currentMetadata = await _protocolHandler.GetMetadataAsync(
            task.Url,
            task.Options,
            cancellationToken).ConfigureAwait(false);

        // Validate ETag if available
        if (!string.IsNullOrEmpty(task.Metadata.ETag) &&
            !string.IsNullOrEmpty(currentMetadata.ETag) &&
            task.Metadata.ETag != currentMetadata.ETag)
        {
            _logger.LogResumeETagMismatch(task.Id, task.Metadata.ETag, currentMetadata.ETag);
            throw new InvalidOperationException(
                "Cannot resume: file has changed on server (ETag mismatch)");
        }

        // Validate Last-Modified if available
        if (task.Metadata.LastModified.HasValue &&
            currentMetadata.LastModified.HasValue &&
            task.Metadata.LastModified != currentMetadata.LastModified)
        {
            _logger.LogResumeLastModifiedMismatch(task.Id);
            throw new InvalidOperationException(
                "Cannot resume: file has changed on server (Last-Modified mismatch)");
        }

        // Validate file size
        if (task.FileSize != currentMetadata.ContentLength)
        {
            _logger.LogResumeFileSizeMismatch(task.Id, task.FileSize, currentMetadata.ContentLength);
            throw new InvalidOperationException(
                $"Cannot resume: file size has changed. Expected: {task.FileSize}, Current: {currentMetadata.ContentLength}");
        }
    }

    /// <summary>
    ///     Saves the current task state to persistence.
    /// </summary>
    private async Task SaveTaskStateAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogSavingTaskState(task.Id);

            SegmentConfiguration? segmentConfig = null;
            _segmentConfigs.TryGetValue(task.Id, out segmentConfig);

            string? tempFilePath = null;
            _tempFilePaths.TryGetValue(task.Id, out tempFilePath);

            DownloadTaskState state = new()
            {
                TaskId = task.Id,
                Url = task.Url.ToString(),
                FileName = task.FileName,
                FileSize = task.FileSize,
                DestinationDirectory = task.Options.DestinationDirectory,
                TempFilePath = tempFilePath,
                State = task.State,
                Priority = task.Priority,
                Metadata = task.Metadata,
                Segments = segmentConfig?.States.ToList() ?? [],
                CreatedAt = task.CreatedAt,
                StartedAt = task.StartedAt,
                CompletedAt = task.CompletedAt,
                RetryCount = task.RetryCount,
                LastError = task.LastError,
                Options = task.Options
            };

            await _statePersistence.SaveStateAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogSaveStateFailed(ex, task.Id);
            throw;
        }
    }

    /// <summary>
    ///     Recovers persisted download states on engine initialization.
    /// </summary>
    private async Task RecoverPersistedStatesAsync()
    {
        try
        {
            _logger.LogRecoveringStates();

            var persistedStates = await _statePersistence.LoadAllStatesAsync().ConfigureAwait(false);
            var recoveredCount = 0;

            foreach (var state in persistedStates)
            {
                try
                {
                    // Recreate DownloadTask from persisted state
                    var options = state.Options ?? new DownloadOptions
                    {
                        DestinationDirectory = state.DestinationDirectory
                    };
                    DownloadTask task = new(new Uri(state.Url), options)
                    {
                        Id = state.TaskId,
                        FileName = state.FileName,
                        FileSize = state.FileSize,
                        State = state.State,
                        Priority = state.Priority,
                        Metadata = state.Metadata,
                        CreatedAt = state.CreatedAt,
                        StartedAt = state.StartedAt,
                        CompletedAt = state.CompletedAt,
                        RetryCount = state.RetryCount,
                        LastError = state.LastError
                    };

                    // Add to tasks
                    _tasks.TryAdd(task.Id, task);

                    // Restore segment configuration if available
                    if (state.Segments.Count > 0)
                    {
                        SegmentConfiguration segmentConfig = new()
                        {
                            FileSize = state.FileSize,
                            SegmentCount = state.Segments.Count,
                            SupportsRanges = state.Metadata?.SupportsRanges ?? false,
                            Ranges = state.Segments.Select(s => new ByteRange(s.StartByte, s.EndByte)).ToArray(),
                            States = state.Segments.ToArray()
                        };

                        _segmentConfigs.TryAdd(task.Id, segmentConfig);
                    }

                    // Restore temp file path if available
                    if (!string.IsNullOrEmpty(state.TempFilePath))
                    {
                        _tempFilePaths.TryAdd(task.Id, state.TempFilePath);
                    }

                    var progress = state.Segments.Sum(s => s.BytesDownloaded);
                    _logger.LogTaskRestored(task.Id, state.State.ToString(), progress, state.FileSize);
                    recoveredCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogRecoverTaskStateFailed(ex, state.TaskId);
                }
            }

            _logger.LogStatesRecovered(recoveredCount);
        }
        catch (Exception ex)
        {
            _logger.LogRecoverStatesFailed(ex);
        }
    }
}
