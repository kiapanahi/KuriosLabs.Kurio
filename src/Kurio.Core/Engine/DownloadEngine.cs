using System.Collections.Concurrent;
using System.Reactive.Subjects;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using Kurio.Core.Queue;

namespace Kurio.Core.Engine;

/// <summary>
///     Main orchestrator for download operations.
/// </summary>
public sealed class DownloadEngine : IDownloadEngine, IDisposable
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly Subject<DownloadProgress> _progressSubject = new();
    private readonly IProtocolHandler _protocolHandler;
    private readonly IDownloadQueueManager _queueManager;
    private readonly Timer _schedulerTimer;
    private readonly ConcurrentDictionary<Guid, SegmentConfiguration> _segmentConfigs = new();
    private readonly ISegmentManager _segmentManager;
    private readonly IStatePersistence _statePersistence;
    private readonly ConcurrentDictionary<Guid, bool> _resumingTasks = new();
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
        IDownloadQueueManager? queueManager = null,
        int maxConcurrentDownloads = 3)
    {
        _protocolHandler = protocolHandler ?? throw new ArgumentNullException(nameof(protocolHandler));
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _segmentManager = segmentManager ?? throw new ArgumentNullException(nameof(segmentManager));
        _statePersistence = statePersistence ?? throw new ArgumentNullException(nameof(statePersistence));
        _queueManager = queueManager ?? new DownloadQueueManager { MaxConcurrentDownloads = maxConcurrentDownloads };

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

        _progressSubject?.Dispose();
    }

    /// <inheritdoc />
    public IObservable<DownloadProgress> ProgressUpdates => _progressSubject;

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
        DownloadTask task = new(url, options) { State = DownloadState.Queued };

        // Add to the dictionary
        if (!_tasks.TryAdd(task.Id, task))
        {
            throw new InvalidOperationException($"Task with ID {task.Id} already exists.");
        }

        // Add to queue for scheduling
        _queueManager.Enqueue(task);

        // Save initial state
        await SaveTaskStateAsync(task, cancellationToken);

        return task;
    }

    /// <inheritdoc />
    public async Task StartDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out DownloadTask? task))
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
        if (!_tasks.TryGetValue(taskId, out DownloadTask? task))
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
        if (_cancellationTokens.TryGetValue(taskId, out CancellationTokenSource? cts))
        {
            await cts.CancelAsync();
        }

        // Save state for resume
        await SaveTaskStateAsync(task, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResumeDownloadAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out DownloadTask? task))
        {
            throw new InvalidOperationException($"Task with ID {taskId} not found.");
        }

        if (task.State != DownloadState.Paused)
        {
            throw new InvalidOperationException(
                $"Only paused tasks can be resumed. Current state: {task.State}");
        }

        // Validate that the download can be resumed
        await ValidateResumeCapabilityAsync(task, cancellationToken);

        // Mark this task as resuming so scheduler knows to call ExecuteResumeAsync
        _resumingTasks.TryAdd(task.Id, true);

        task.State = DownloadState.Queued;

        // Re-queue the task
        _queueManager.Enqueue(task);

        await SaveTaskStateAsync(task, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelDownloadAsync(
        Guid taskId,
        bool removePartialFiles = false,
        CancellationToken cancellationToken = default)
    {
        if (!_tasks.TryGetValue(taskId, out DownloadTask? task))
        {
            throw new InvalidOperationException($"Task with ID {taskId} not found.");
        }

        // Cancel the download operation
        if (_cancellationTokens.TryGetValue(taskId, out CancellationTokenSource? cts))
        {
            await cts.CancelAsync();
        }

        task.State = DownloadState.Cancelled;

        // Remove from queue if present
        _queueManager.Dequeue(taskId);

        if (removePartialFiles)
        {
            await _storageManager.CleanupTemporaryFilesAsync(taskId, cancellationToken);
        }

        // Delete persisted state
        await _statePersistence.DeleteStateAsync(taskId, cancellationToken);

        // Cleanup tracking
        _cancellationTokens.TryRemove(taskId, out _);
        _segmentConfigs.TryRemove(taskId, out _);
        _tempFilePaths.TryRemove(taskId, out _);
        _resumingTasks.TryRemove(taskId, out _);
    }

    /// <inheritdoc />
    public IDownloadTask? GetDownload(Guid taskId)
    {
        return _tasks.TryGetValue(taskId, out DownloadTask? task) ? task : null;
    }

    /// <inheritdoc />
    public IEnumerable<IDownloadTask> GetDownloads(DownloadStateFilter filter)
    {
        return _tasks.Values.Where(task => filter.HasFlag(GetFilterForState(task.State)));
    }

    /// <inheritdoc />
    public bool ChangePriority(Guid taskId, DownloadPriority newPriority)
    {
        if (!_tasks.TryGetValue(taskId, out DownloadTask? task))
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
        IReadOnlyList<IDownloadTask> activeTasks = _queueManager.GetActiveTasks();
        int pausedCount = 0;

        foreach (IDownloadTask task in activeTasks)
        {
            try
            {
                await PauseDownloadAsync(task.Id, cancellationToken);
                pausedCount++;
            }
            catch
            {
                // Continue with other tasks
            }
        }

        return pausedCount;
    }

    /// <inheritdoc />
    public void ClearCompleted()
    {
        _queueManager.ClearCompleted();

        // Also remove from tasks dictionary
        List<Guid> completedTasks = _tasks.Values
            .Where(t => t.State == DownloadState.Completed)
            .Select(t => t.Id)
            .ToList();

        foreach (Guid taskId in completedTasks)
        {
            _tasks.TryRemove(taskId, out _);
        }
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
                IDownloadTask? nextTask = _queueManager.GetNextTask();
                if (nextTask == null)
                {
                    break; // No more tasks in queue
                }

                // Start the download
                _queueManager.MarkAsStarted(nextTask.Id);
                
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
            // Log scheduling errors but don't crash
            Console.WriteLine($"Error in download scheduler: {ex.Message}");
        }
    }

    /// <summary>
    ///     Executes the download for a task.
    /// </summary>
    private async Task ExecuteDownloadAsync(DownloadTask task, CancellationToken cancellationToken)
    {
        // Create a linked cancellation token source
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[task.Id] = cts;
        CancellationToken linkedToken = cts.Token;

        try
        {
            task.State = DownloadState.Analyzing;
            task.StartedAt = DateTime.UtcNow;

            // Get metadata
            task.Metadata = await _protocolHandler.GetMetadataAsync(
                task.Url,
                task.Options,
                linkedToken);

            task.FileSize = task.Metadata.ContentLength;

            // Update filename if suggested by server
            if (!string.IsNullOrEmpty(task.Metadata.SuggestedFileName))
            {
                task.FileName = task.Metadata.SuggestedFileName;
            }

            // Check available disk space
            long availableSpace = await _storageManager.GetAvailableDiskSpaceAsync(
                task.Options.DestinationDirectory,
                linkedToken);

            if (availableSpace < task.FileSize)
            {
                throw new InvalidOperationException(
                    $"Insufficient disk space. Required: {task.FileSize}, Available: {availableSpace}");
            }

            // Create temporary file
            string tempFilePath = await _storageManager.CreateTemporaryFileAsync(
                task.Id,
                task.FileName,
                task.FileSize,
                linkedToken);

            _tempFilePaths[task.Id] = tempFilePath;

            // Calculate segments
            SegmentOptions segmentOptions = new()
            {
                MaxConnections = task.Options.MaxConnections, MinSegmentSize = task.Options.MinSegmentSize
            };

            SegmentConfiguration segmentConfig = _segmentManager.CalculateSegments(
                task.FileSize,
                task.Metadata.SupportsRanges,
                segmentOptions);

            _segmentConfigs[task.Id] = segmentConfig;

            // Start downloading
            task.State = DownloadState.Downloading;

            // Save initial state
            await SaveTaskStateAsync(task, linkedToken);

            DateTime lastStateSave = DateTime.UtcNow;
            Progress<SegmentProgress> progress = new(segmentProgress =>
            {
                // Aggregate progress from all segments
                long totalDownloaded = segmentConfig.States.Sum(s => s.BytesDownloaded);
                int activeConnections = segmentConfig.States.Count(s => s.Status == SegmentStatus.Downloading);

                task.Progress.BytesDownloaded = totalDownloaded;
                task.Progress.TotalBytes = task.FileSize;
                task.Progress.ActiveConnections = activeConnections;

                _progressSubject.OnNext(task.Progress);

                // Periodically save state to disk (every 5 seconds)
                if ((DateTime.UtcNow - lastStateSave).TotalSeconds >= 5)
                {
                    lastStateSave = DateTime.UtcNow;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SaveTaskStateAsync(task, CancellationToken.None);
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
                linkedToken);

            // Commit the download
            string finalPath = await _storageManager.CommitDownloadAsync(
                tempFilePath,
                task.Options.DestinationDirectory,
                task.FileName,
                task.Options.FileNamingPolicy,
                linkedToken);

            // Mark as completed
            task.State = DownloadState.Completed;
            task.CompletedAt = DateTime.UtcNow;

            // Notify queue manager
            _queueManager.MarkAsCompleted(task.Id);

            // Delete persisted state
            await _statePersistence.DeleteStateAsync(task.Id, CancellationToken.None);

            // Cleanup tracking
            _segmentConfigs.TryRemove(task.Id, out _);
            _tempFilePaths.TryRemove(task.Id, out _);
        }
        catch (OperationCanceledException) when (task.State == DownloadState.Paused)
        {
            // Download was paused - state already saved
        }
        catch (AggregateException ex) when (task.State == DownloadState.Paused && 
            ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Download was paused - multiple segments canceled, state already saved
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

            // Notify queue manager
            _queueManager.MarkAsFailed(task.Id);

            // Save failed state
            await SaveTaskStateAsync(task, CancellationToken.None);
        }
        finally
        {
            _cancellationTokens.TryRemove(task.Id, out CancellationTokenSource? removedCts);
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
        CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cancellationTokens[task.Id] = cts;
        CancellationToken linkedToken = cts.Token;

        try
        {
            task.State = DownloadState.Downloading;

            // Get persisted segment configuration and temp file path
            if (!_segmentConfigs.TryGetValue(task.Id, out SegmentConfiguration? segmentConfig) ||
                !_tempFilePaths.TryGetValue(task.Id, out string? tempFilePath))
            {
                throw new InvalidOperationException(
                    "Cannot resume download: segment configuration or temp file path not found");
            }

            DateTime lastStateSave = DateTime.UtcNow;
            Progress<SegmentProgress> progress = new(segmentProgress =>
            {
                long totalDownloaded = segmentConfig.States.Sum(s => s.BytesDownloaded);
                int activeConnections = segmentConfig.States.Count(s => s.Status == SegmentStatus.Downloading);

                task.Progress.BytesDownloaded = totalDownloaded;
                task.Progress.TotalBytes = task.FileSize;
                task.Progress.ActiveConnections = activeConnections;

                _progressSubject.OnNext(task.Progress);

                // Periodically save state to disk (every 5 seconds)
                if ((DateTime.UtcNow - lastStateSave).TotalSeconds >= 5)
                {
                    lastStateSave = DateTime.UtcNow;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await SaveTaskStateAsync(task, CancellationToken.None);
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
                linkedToken);

            // Commit the download
            string finalPath = await _storageManager.CommitDownloadAsync(
                tempFilePath,
                task.Options.DestinationDirectory,
                task.FileName,
                task.Options.FileNamingPolicy,
                linkedToken);

            // Mark as completed
            task.State = DownloadState.Completed;
            task.CompletedAt = DateTime.UtcNow;

            // Notify queue manager
            _queueManager.MarkAsCompleted(task.Id);

            // Delete persisted state
            await _statePersistence.DeleteStateAsync(task.Id, CancellationToken.None);

            // Cleanup tracking
            _segmentConfigs.TryRemove(task.Id, out _);
            _tempFilePaths.TryRemove(task.Id, out _);
        }
        catch (OperationCanceledException) when (task.State == DownloadState.Paused)
        {
            // Download was paused again - state already saved
        }
        catch (AggregateException ex) when (task.State == DownloadState.Paused && 
            ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Download was paused again - multiple segments canceled, state already saved
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

            // Notify queue manager
            _queueManager.MarkAsFailed(task.Id);

            // Save failed state
            await SaveTaskStateAsync(task, CancellationToken.None);
        }
        finally
        {
            _cancellationTokens.TryRemove(task.Id, out CancellationTokenSource? removedCts);
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
            throw new InvalidOperationException("Cannot resume: metadata is missing");
        }

        if (!task.Metadata.SupportsRanges)
        {
            throw new InvalidOperationException("Cannot resume: server does not support range requests");
        }

        // Fetch current metadata to validate
        ResourceMetadata currentMetadata = await _protocolHandler.GetMetadataAsync(
            task.Url,
            task.Options,
            cancellationToken);

        // Validate ETag if available
        if (!string.IsNullOrEmpty(task.Metadata.ETag) &&
            !string.IsNullOrEmpty(currentMetadata.ETag) &&
            task.Metadata.ETag != currentMetadata.ETag)
        {
            throw new InvalidOperationException(
                "Cannot resume: file has changed on server (ETag mismatch)");
        }

        // Validate Last-Modified if available
        if (task.Metadata.LastModified.HasValue &&
            currentMetadata.LastModified.HasValue &&
            task.Metadata.LastModified != currentMetadata.LastModified)
        {
            throw new InvalidOperationException(
                "Cannot resume: file has changed on server (Last-Modified mismatch)");
        }

        // Validate file size
        if (task.FileSize != currentMetadata.ContentLength)
        {
            throw new InvalidOperationException(
                $"Cannot resume: file size has changed. Expected: {task.FileSize}, Current: {currentMetadata.ContentLength}");
        }
    }

    /// <summary>
    ///     Saves the current task state to persistence.
    /// </summary>
    private async Task SaveTaskStateAsync(DownloadTask task, CancellationToken cancellationToken)
    {
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

        await _statePersistence.SaveStateAsync(state, cancellationToken);
    }

    /// <summary>
    ///     Recovers persisted download states on engine initialization.
    /// </summary>
    private async Task RecoverPersistedStatesAsync()
    {
        try
        {
            IReadOnlyList<DownloadTaskState> persistedStates = await _statePersistence.LoadAllStatesAsync();

            foreach (DownloadTaskState state in persistedStates)
            {
                try
                {
                    // Recreate DownloadTask from persisted state
                    DownloadOptions options = state.Options ?? new DownloadOptions
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
                }
                catch (Exception ex)
                {
                    // Log error but continue with other states
                    Console.WriteLine($"Failed to recover state for task {state.TaskId}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log error during state recovery
            Console.WriteLine($"Failed to recover persisted states: {ex.Message}");
        }
    }
}
