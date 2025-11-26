using System.Collections.Concurrent;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace Kurio.Core.Engine;

/// <summary>
///     Manages download segmentation and parallel downloading.
/// </summary>
public sealed class SegmentManager : ISegmentManager
{
    private readonly ILogger<SegmentManager>? _logger;
    private readonly IStorageManager _storageManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SegmentManager" /> class.
    /// </summary>
    /// <param name="storageManager">The storage manager for file operations.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public SegmentManager(IStorageManager storageManager, ILogger<SegmentManager>? logger = null)
    {
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _logger = logger;
    }

    /// <inheritdoc />
    public SegmentConfiguration CalculateSegments(
        long fileSize,
        bool supportsRanges,
        SegmentOptions options)
    {
        if (fileSize <= 0)
        {
            throw new ArgumentException("File size must be greater than zero.", nameof(fileSize));
        }

        // If range requests are not supported, use a single segment
        if (!supportsRanges || fileSize < options.MinSegmentSize)
        {
            _logger?.LogInformation(
                "Using single segment download. SupportsRanges: {SupportsRanges}, FileSize: {FileSize}, MinSegmentSize: {MinSegmentSize}",
                supportsRanges,
                fileSize,
                options.MinSegmentSize);

            return new SegmentConfiguration
            {
                FileSize = fileSize,
                SegmentCount = 1,
                SupportsRanges = false,
                Ranges = new[] { new ByteRange(0, fileSize - 1) },
                States = new[]
                {
                    new SegmentState
                    {
                        SegmentIndex = 0,
                        StartByte = 0,
                        EndByte = fileSize - 1,
                        Status = SegmentStatus.Pending
                    }
                }
            };
        }

        // Calculate ideal number of segments
        int idealSegmentCount = Math.Min(
            options.MaxConnections,
            (int)(fileSize / options.MinSegmentSize));

        // Ensure at least one segment
        idealSegmentCount = Math.Max(1, idealSegmentCount);

        long segmentSize = fileSize / idealSegmentCount;
        ByteRange[] ranges = new ByteRange[idealSegmentCount];
        SegmentState[] states = new SegmentState[idealSegmentCount];

        for (int i = 0; i < idealSegmentCount; i++)
        {
            long start = i * segmentSize;
            long end = i == idealSegmentCount - 1 ? fileSize - 1 : start + segmentSize - 1;

            ranges[i] = new ByteRange(start, end);
            states[i] = new SegmentState
            {
                SegmentIndex = i, StartByte = start, EndByte = end, Status = SegmentStatus.Pending
            };
        }

        _logger?.LogInformation(
            "Calculated {SegmentCount} segments for file size {FileSize} bytes. Average segment size: {SegmentSize} bytes",
            idealSegmentCount,
            fileSize,
            segmentSize);

        return new SegmentConfiguration
        {
            FileSize = fileSize,
            SegmentCount = idealSegmentCount,
            SupportsRanges = true,
            Ranges = ranges,
            States = states
        };
    }

    /// <inheritdoc />
    public async Task DownloadSegmentsAsync(
        IProtocolHandler handler,
        Uri url,
        SegmentConfiguration config,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting parallel download with {SegmentCount} segments", config.SegmentCount);

        // Use a semaphore to limit concurrent segment downloads
        using SemaphoreSlim semaphore = new(config.SegmentCount, config.SegmentCount);

        // Track failed segments for retry logic
        ConcurrentBag<int> failedSegments = new();

        // Create tasks for all segments
        List<Task> tasks = new(config.SegmentCount);

        for (int i = 0; i < config.SegmentCount; i++)
        {
            int segmentIndex = i;
            ByteRange range = config.Ranges[i];
            SegmentState state = config.States[i];

            await semaphore.WaitAsync(cancellationToken);

            Task task = Task.Run(async () =>
            {
                try
                {
                    await DownloadSegmentWithRetryAsync(
                        handler,
                        url,
                        range,
                        state,
                        tempFilePath,
                        options,
                        progress,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Segment {SegmentIndex} failed after all retries", segmentIndex);
                    failedSegments.Add(segmentIndex);
                    throw;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);

            tasks.Add(task);
        }

        // Wait for all segments to complete
        try
        {
            await Task.WhenAll(tasks);
            _logger?.LogInformation("All {SegmentCount} segments downloaded successfully", config.SegmentCount);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Download failed. {FailedCount} segments failed", failedSegments.Count);
            throw new AggregateException(
                $"Failed to download {failedSegments.Count} segment(s): {string.Join(", ", failedSegments)}",
                ex);
        }

        // Verify segment boundaries
        await VerifySegmentBoundariesAsync(config, tempFilePath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResumeSegmentsAsync(
        IProtocolHandler handler,
        Uri url,
        SegmentConfiguration config,
        SegmentState[] segmentStates,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Find incomplete segments
        List<Task> incompleteTasks = new();

        for (int i = 0; i < segmentStates.Length; i++)
        {
            SegmentState state = segmentStates[i];

            if (state.Status == SegmentStatus.Completed)
            {
                continue;
            }

            // Calculate remaining range for this segment
            long remainingStart = state.StartByte + state.BytesDownloaded;
            ByteRange remainingRange = new(remainingStart, state.EndByte);

            // Reset state for retry
            state.Status = SegmentStatus.Downloading;
            state.StartedAt = DateTime.UtcNow;

            incompleteTasks.Add(DownloadSegmentAsync(
                handler,
                url,
                remainingRange,
                state,
                tempFilePath,
                options,
                progress,
                cancellationToken,
                true));
        }

        // Wait for all incomplete segments to complete
        await Task.WhenAll(incompleteTasks);
    }

    /// <summary>
    ///     Downloads a segment with automatic retry logic.
    /// </summary>
    private async Task DownloadSegmentWithRetryAsync(
        IProtocolHandler handler,
        Uri url,
        ByteRange range,
        SegmentState state,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress>? progress,
        CancellationToken cancellationToken,
        int maxRetries = 3)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount <= maxRetries)
        {
            try
            {
                await DownloadSegmentAsync(
                    handler,
                    url,
                    range,
                    state,
                    tempFilePath,
                    options,
                    progress,
                    cancellationToken);

                // Success - exit retry loop
                return;
            }
            catch (OperationCanceledException)
            {
                // Don't retry on cancellation
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                state.RetryCount = retryCount;

                if (retryCount <= maxRetries)
                {
                    TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
                    _logger?.LogWarning(
                        ex,
                        "Segment {SegmentIndex} failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}s...",
                        state.SegmentIndex,
                        retryCount,
                        maxRetries + 1,
                        delay.TotalSeconds);

                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // All retries exhausted
        throw new InvalidOperationException(
            $"Segment {state.SegmentIndex} failed after {maxRetries + 1} attempts",
            lastException);
    }

    private async Task DownloadSegmentAsync(
        IProtocolHandler handler,
        Uri url,
        ByteRange range,
        SegmentState state,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress>? progress,
        CancellationToken cancellationToken,
        bool isResume = false)
    {
        state.Status = SegmentStatus.Downloading;
        state.StartedAt ??= DateTime.UtcNow;

        _logger?.LogDebug(
            "Downloading segment {SegmentIndex}: {Start}-{End} ({Length} bytes)",
            state.SegmentIndex,
            range.Start,
            range.End,
            range.Length);

        // Create a memory stream to buffer the segment data
        await using MemoryStream memoryStream = new((int)range.Length);

        // Progress tracking for this segment
        Progress<long> segmentProgress = new(bytesRead =>
        {
            long totalForSegment = isResume ? state.BytesDownloaded + bytesRead : bytesRead;
            state.BytesDownloaded = totalForSegment;

            progress?.Report(new SegmentProgress
            {
                SegmentIndex = state.SegmentIndex,
                BytesDownloaded = totalForSegment,
                Status = SegmentStatus.Downloading,
                Timestamp = DateTime.UtcNow
            });
        });

        // Download the range
        await handler.DownloadRangeAsync(
            url,
            range,
            memoryStream,
            options,
            segmentProgress,
            cancellationToken);

        // Verify downloaded size matches expected
        long downloadedBytes = memoryStream.Length;
        if (downloadedBytes != range.Length)
        {
            throw new InvalidOperationException(
                $"Segment {state.SegmentIndex} size mismatch. Expected: {range.Length}, Got: {downloadedBytes}");
        }

        // Write the buffered data to the file at the correct offset using StorageManager
        byte[] buffer = memoryStream.ToArray();
        await _storageManager.WriteSegmentAsync(
            tempFilePath,
            range.Start,
            buffer,
            buffer.Length,
            cancellationToken);

        // Update state
        state.BytesDownloaded = range.Length;
        state.Status = SegmentStatus.Completed;
        state.CompletedAt = DateTime.UtcNow;

        _logger?.LogDebug(
            "Segment {SegmentIndex} completed. Duration: {Duration}ms",
            state.SegmentIndex,
            (state.CompletedAt.Value - state.StartedAt.Value).TotalMilliseconds);

        progress?.Report(new SegmentProgress
        {
            SegmentIndex = state.SegmentIndex,
            BytesDownloaded = state.BytesDownloaded,
            Status = SegmentStatus.Completed,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Verifies that all segment boundaries are correct and no gaps exist.
    /// </summary>
    private async Task VerifySegmentBoundariesAsync(
        SegmentConfiguration config,
        string tempFilePath,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Verifying segment boundaries...");

        // Check file size
        FileInfo fileInfo = new(tempFilePath);
        if (fileInfo.Length != config.FileSize)
        {
            throw new InvalidOperationException(
                $"Downloaded file size mismatch. Expected: {config.FileSize}, Got: {fileInfo.Length}");
        }

        // Verify all segments are completed
        List<int> incompleteSegments = config.States
            .Where(s => s.Status != SegmentStatus.Completed)
            .Select(s => s.SegmentIndex)
            .ToList();

        if (incompleteSegments.Count > 0)
        {
            throw new InvalidOperationException(
                $"Incomplete segments detected: {string.Join(", ", incompleteSegments)}");
        }

        // Verify segment boundaries don't overlap or have gaps
        SegmentState[] sortedStates = config.States.OrderBy(s => s.StartByte).ToArray();
        for (int i = 0; i < sortedStates.Length - 1; i++)
        {
            SegmentState current = sortedStates[i];
            SegmentState next = sortedStates[i + 1];

            if (current.EndByte + 1 != next.StartByte)
            {
                throw new InvalidOperationException(
                    $"Gap or overlap detected between segments {current.SegmentIndex} and {next.SegmentIndex}. " +
                    $"Segment {current.SegmentIndex} ends at {current.EndByte}, " +
                    $"Segment {next.SegmentIndex} starts at {next.StartByte}");
            }
        }

        _logger?.LogInformation("Segment boundary verification completed successfully");
    }
}
