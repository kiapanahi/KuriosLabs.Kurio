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
    private readonly ISegmentVerifier _segmentVerifier;
    private readonly IStorageManager _storageManager;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SegmentManager" /> class.
    /// </summary>
    /// <param name="storageManager">The storage manager for file operations.</param>
    /// <param name="segmentVerifier">The segment verifier for checksum operations.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public SegmentManager(
        IStorageManager storageManager,
        ISegmentVerifier segmentVerifier,
        ILogger<SegmentManager>? logger = null)
    {
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        _segmentVerifier = segmentVerifier ?? throw new ArgumentNullException(nameof(segmentVerifier));
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
                Ranges = [new ByteRange(0, fileSize - 1)],
                States =
                [
                    new SegmentState
                    {
                        SegmentIndex = 0, StartByte = 0, EndByte = fileSize - 1, Status = SegmentStatus.Pending
                    }
                ]
            };
        }

        // Calculate ideal number of segments
        var idealSegmentCount = Math.Min(
            options.MaxConnections,
            (int)(fileSize / options.MinSegmentSize));

        // Ensure at least one segment
        idealSegmentCount = Math.Max(1, idealSegmentCount);

        var segmentSize = fileSize / idealSegmentCount;
        var ranges = new ByteRange[idealSegmentCount];
        var states = new SegmentState[idealSegmentCount];

        for (var i = 0; i < idealSegmentCount; i++)
        {
            var start = i * segmentSize;
            var end = i == idealSegmentCount - 1 ? fileSize - 1 : start + segmentSize - 1;

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

        for (var i = 0; i < config.SegmentCount; i++)
        {
            var segmentIndex = i;
            var range = config.Ranges[i];
            var state = config.States[i];

            await semaphore.WaitAsync(cancellationToken);

            var task = Task.Run(async () =>
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
        // Verify completed segments' checksums before resuming
        await VerifyCompletedSegmentsAsync(segmentStates, tempFilePath, cancellationToken);

        // Find incomplete segments
        List<Task> incompleteTasks = new();

        for (var i = 0; i < segmentStates.Length; i++)
        {
            var state = segmentStates[i];

            if (state.Status == SegmentStatus.Completed)
            {
                continue;
            }

            // Calculate remaining range for this segment
            var remainingStart = state.StartByte + state.BytesDownloaded;
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
        var retryCount = 0;
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
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount)); // Exponential backoff
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

        // Get the directory containing the temp file
        var tempDir = Path.GetDirectoryName(tempFilePath);
        if (string.IsNullOrEmpty(tempDir))
        {
            throw new InvalidOperationException("Invalid temporary file path");
        }

        // Create segment-specific file path
        var segmentFilePath = Path.Combine(tempDir, $"segment_{state.SegmentIndex:D4}.part");

        // Store the initial bytes downloaded (for resume scenarios)
        // This represents bytes that were previously written to disk
        var initialBytesDownloaded = isResume ? state.BytesDownloaded : 0;

        // Open segment file for writing - stream directly to disk, no in-memory buffering
        // Use OpenOrCreate for resume support
        await using FileStream segmentStream = new(
            segmentFilePath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.None,
            81920,
            true);

        // For resumed downloads, seek to the position where we left off
        segmentStream.Seek(initialBytesDownloaded, SeekOrigin.Begin);

        // Track bytes written in this session for progress reporting
        long bytesWrittenThisSession = 0;

        // Progress tracking for this segment
        // Note: We update state.BytesDownloaded for live progress aggregation,
        // but only the final value (after flush) is persisted to disk
        Progress<long> segmentProgress = new(bytesRead =>
        {
            // bytesRead is cumulative bytes written to disk in this session
            bytesWrittenThisSession = bytesRead;
            var totalForSegment = initialBytesDownloaded + bytesRead;

            // Update state for live progress aggregation (not persisted until completion)
            state.BytesDownloaded = totalForSegment;

            progress?.Report(new SegmentProgress
            {
                SegmentIndex = state.SegmentIndex,
                BytesDownloaded = totalForSegment,
                Status = SegmentStatus.Downloading,
                Timestamp = DateTime.UtcNow
            });
        });

        // Download directly to the segment file stream (no in-memory buffering!)
        await handler.DownloadRangeAsync(
            url,
            range,
            segmentStream,
            options,
            segmentProgress,
            cancellationToken);

        // Flush to ensure all data is written to disk
        await segmentStream.FlushAsync(cancellationToken);

        // Verify downloaded size matches expected
        var finalPosition = segmentStream.Position;
        var expectedPosition = initialBytesDownloaded + range.Length;

        if (finalPosition != expectedPosition)
        {
            throw new InvalidOperationException(
                $"Segment {state.SegmentIndex} size mismatch. Expected position: {expectedPosition}, Got: {finalPosition}");
        }

        // Close the stream before reading for checksum
        await segmentStream.DisposeAsync();

        // CRITICAL: Set the final persisted value after successful flush
        // This ensures resume starts from the correct position and state file has accurate data
        // Note: state.BytesDownloaded was updated during progress for live aggregation,
        // but we set the final confirmed value here
        state.BytesDownloaded = initialBytesDownloaded + range.Length;
        state.SegmentFilePath = segmentFilePath; // Store the segment file path for merging later

        // Compute checksum for the ENTIRE segment from the segment file (not just the newly written part)
        // This is important for resume scenarios where we need to verify the complete segment
        // Read the entire segment from its file to compute checksum
        using (FileStream fileStream = new(
                   segmentFilePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read,
                   81920,
                   true))
        {
            var segmentData = new byte[state.TotalSize];
            var totalRead = 0;

            while (totalRead < state.TotalSize)
            {
                var bytesRead = await fileStream.ReadAsync(
                    segmentData.AsMemory(totalRead, (int)(state.TotalSize - totalRead)),
                    cancellationToken);

                if (bytesRead == 0)
                {
                    throw new InvalidOperationException(
                        $"Unexpected end of file while reading segment {state.SegmentIndex}");
                }

                totalRead += bytesRead;
            }

            var checksum = await _segmentVerifier.ComputeChecksumAsync(segmentData, "SHA256", cancellationToken);
            state.Checksum = SegmentChecksum.Create("SHA256", checksum);

            _logger?.LogDebug(
                "Segment {SegmentIndex} checksum computed: {Checksum}",
                state.SegmentIndex,
                checksum[..16]); // Log first 16 chars
        }

        // Mark segment as completed
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

        // Verify all segments are completed
        var incompleteSegments = config.States
            .Where(s => s.Status != SegmentStatus.Completed)
            .Select(s => s.SegmentIndex)
            .ToList();

        if (incompleteSegments.Count > 0)
        {
            throw new InvalidOperationException(
                $"Incomplete segments detected: {string.Join(", ", incompleteSegments)}");
        }

        // Verify segment boundaries don't overlap or have gaps
        var sortedStates = config.States.OrderBy(s => s.StartByte).ToArray();
        for (var i = 0; i < sortedStates.Length - 1; i++)
        {
            var current = sortedStates[i];
            var next = sortedStates[i + 1];

            if (current.EndByte + 1 != next.StartByte)
            {
                throw new InvalidOperationException(
                    $"Gap or overlap detected between segments {current.SegmentIndex} and {next.SegmentIndex}. " +
                    $"Segment {current.SegmentIndex} ends at {current.EndByte}, " +
                    $"Segment {next.SegmentIndex} starts at {next.StartByte}");
            }
        }

        // Verify segment files exist and have correct sizes
        var tempDir = Path.GetDirectoryName(tempFilePath);
        if (string.IsNullOrEmpty(tempDir))
        {
            throw new InvalidOperationException("Invalid temporary file path");
        }

        long totalBytesVerified = 0;
        foreach (var state in config.States)
        {
            var segmentFilePath = state.SegmentFilePath ??
                                  Path.Combine(tempDir, $"segment_{state.SegmentIndex:D4}.part");

            if (!File.Exists(segmentFilePath))
            {
                throw new FileNotFoundException($"Segment file not found: {segmentFilePath}");
            }

            FileInfo segmentInfo = new(segmentFilePath);
            if (segmentInfo.Length != state.TotalSize)
            {
                throw new InvalidOperationException(
                    $"Segment {state.SegmentIndex} size mismatch. Expected: {state.TotalSize}, Got: {segmentInfo.Length}");
            }

            totalBytesVerified += segmentInfo.Length;
        }

        // Verify total size matches expected file size
        if (totalBytesVerified != config.FileSize)
        {
            throw new InvalidOperationException(
                $"Total downloaded size mismatch. Expected: {config.FileSize}, Got: {totalBytesVerified}");
        }

        _logger?.LogInformation("Segment boundary verification completed successfully. Total size: {TotalSize} bytes",
            totalBytesVerified);
    }

    /// <summary>
    ///     Verifies checksums of completed segments to detect corruption.
    /// </summary>
    private async Task VerifyCompletedSegmentsAsync(
        SegmentState[] segmentStates,
        string tempFilePath,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Verifying checksums for {CompletedCount} completed segments",
            segmentStates.Count(s => s.Status == SegmentStatus.Completed));

        List<int> corruptedSegments = new();

        foreach (var state in segmentStates)
        {
            // Only verify completed segments with checksums
            if (state.Status != SegmentStatus.Completed || state.Checksum == null)
            {
                continue;
            }

            // Get segment file path
            var segmentFile = state.SegmentFilePath;
            if (string.IsNullOrEmpty(segmentFile))
            {
                // Fallback: construct segment file path from temp file directory
                var tempDir = Path.GetDirectoryName(tempFilePath);
                if (!string.IsNullOrEmpty(tempDir))
                {
                    segmentFile = Path.Combine(tempDir, $"segment_{state.SegmentIndex:D4}.part");
                }
            }

            // Skip if segment file doesn't exist (shouldn't happen but be defensive)
            if (string.IsNullOrEmpty(segmentFile) || !File.Exists(segmentFile))
            {
                _logger?.LogWarning("Segment file not found for segment {SegmentIndex}, skipping verification",
                    state.SegmentIndex);
                continue;
            }

            // Verify the segment from its file (offset is 0 since each segment has its own file)
            var isValid = await _segmentVerifier.VerifySegmentAsync(
                segmentFile,
                0, // Offset is 0 for per-segment files
                state.TotalSize,
                state.Checksum.Hash,
                state.Checksum.Algorithm,
                cancellationToken);

            if (isValid)
            {
                state.Checksum.MarkAsVerified();
                _logger?.LogDebug("Segment {SegmentIndex} checksum verified successfully", state.SegmentIndex);
            }
            else
            {
                state.Checksum.MarkAsFailed();
                state.Status = SegmentStatus.Failed;
                state.BytesDownloaded = 0; // Reset to re-download
                corruptedSegments.Add(state.SegmentIndex);

                _logger?.LogWarning(
                    "Segment {SegmentIndex} checksum verification failed. Expected: {ExpectedChecksum}, will re-download",
                    state.SegmentIndex,
                    state.Checksum.Hash[..16]);
            }
        }

        if (corruptedSegments.Count > 0)
        {
            _logger?.LogWarning(
                "Detected {CorruptedCount} corrupted segments: {SegmentIndices}. These will be re-downloaded.",
                corruptedSegments.Count,
                string.Join(", ", corruptedSegments));
        }
        else
        {
            _logger?.LogInformation("All completed segments verified successfully");
        }
    }
}
