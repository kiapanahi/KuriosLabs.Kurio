namespace Kurio.Core.Engine;

using System.Collections.Concurrent;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;

/// <summary>
/// Manages download segmentation and parallel downloading.
/// </summary>
public sealed class SegmentManager : ISegmentManager
{
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
            var end = (i == idealSegmentCount - 1) ? fileSize - 1 : start + segmentSize - 1;

            ranges[i] = new ByteRange(start, end);
            states[i] = new SegmentState
            {
                SegmentIndex = i,
                StartByte = start,
                EndByte = end,
                Status = SegmentStatus.Pending
            };
        }

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
        // Create tasks for all segments
        var tasks = new Task[config.SegmentCount];

        for (var i = 0; i < config.SegmentCount; i++)
        {
            var segmentIndex = i;
            var range = config.Ranges[i];
            var state = config.States[i];

            tasks[i] = DownloadSegmentAsync(
                handler,
                url,
                range,
                state,
                tempFilePath,
                options,
                progress,
                cancellationToken);
        }

        // Wait for all segments to complete
        await Task.WhenAll(tasks);
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
        var incompleteTasks = new List<Task>();

        for (var i = 0; i < segmentStates.Length; i++)
        {
            var state = segmentStates[i];

            if (state.Status == SegmentStatus.Completed)
            {
                continue;
            }

            // Calculate remaining range for this segment
            var remainingStart = state.StartByte + state.BytesDownloaded;
            var remainingRange = new ByteRange(remainingStart, state.EndByte);

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
                isResume: true));
        }

        // Wait for all incomplete segments to complete
        await Task.WhenAll(incompleteTasks);
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
        try
        {
            state.Status = SegmentStatus.Downloading;
            state.StartedAt ??= DateTime.UtcNow;

            // Create a memory stream to buffer the segment data
            await using var memoryStream = new MemoryStream();

            // Progress tracking for this segment
            var segmentProgress = new Progress<long>(bytesRead =>
            {
                var totalForSegment = isResume ? state.BytesDownloaded + bytesRead : bytesRead;
                progress?.Report(new SegmentProgress
                {
                    SegmentIndex = state.SegmentIndex,
                    BytesDownloaded = totalForSegment,
                    Status = SegmentStatus.Downloading
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

            // Write the buffered data to the file at the correct offset
            var buffer = memoryStream.ToArray();
            var offset = range.Start;

            // Get storage manager to write segment
            // Note: In a real implementation, we'd inject IStorageManager
            // For now, we'll write directly (this will be refactored)
            await using var fileStream = new FileStream(
                tempFilePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.Write,
                bufferSize: 4096,
                useAsync: true);

            fileStream.Seek(offset, SeekOrigin.Begin);
            await fileStream.WriteAsync(buffer, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);

            // Update state
            state.BytesDownloaded = range.Length;
            state.Status = SegmentStatus.Completed;
            state.CompletedAt = DateTime.UtcNow;

            progress?.Report(new SegmentProgress
            {
                SegmentIndex = state.SegmentIndex,
                BytesDownloaded = state.BytesDownloaded,
                Status = SegmentStatus.Completed
            });
        }
        catch (Exception)
        {
            state.Status = SegmentStatus.Failed;
            state.RetryCount++;
            throw;
        }
    }
}
