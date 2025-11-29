using Microsoft.Extensions.Logging;

namespace Kurio.Core.Engine;

internal static partial class SegmentManagerLogMessages
{
    [LoggerMessage(
        EventId = 4200,
        Level = LogLevel.Information,
        Message = "Using single segment download. SupportsRanges: {SupportsRanges}, FileSize: {FileSize}, MinSegmentSize: {MinSegmentSize}")]
    public static partial void LogUsingSingleSegment(
        this ILogger logger,
        bool supportsRanges,
        long fileSize,
        long minSegmentSize);

    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Information,
        Message = "Calculated {SegmentCount} segments for file size {FileSize} bytes. Average segment size: {SegmentSize} bytes")]
    public static partial void LogSegmentsCalculated(
        this ILogger logger,
        int segmentCount,
        long fileSize,
        long segmentSize);

    [LoggerMessage(
        EventId = 4202,
        Level = LogLevel.Information,
        Message = "Starting parallel download with {SegmentCount} segments")]
    public static partial void LogStartingParallelDownload(
        this ILogger logger,
        int segmentCount);

    [LoggerMessage(
        EventId = 4203,
        Level = LogLevel.Error,
        Message = "Segment {SegmentIndex} failed after all retries")]
    public static partial void LogSegmentFailedAfterRetries(
        this ILogger logger,
        Exception exception,
        int segmentIndex);

    [LoggerMessage(
        EventId = 4204,
        Level = LogLevel.Information,
        Message = "All {SegmentCount} segments downloaded successfully")]
    public static partial void LogAllSegmentsDownloaded(
        this ILogger logger,
        int segmentCount);

    [LoggerMessage(
        EventId = 4205,
        Level = LogLevel.Error,
        Message = "Download failed. {FailedCount} segments failed")]
    public static partial void LogDownloadFailed(
        this ILogger logger,
        Exception exception,
        int failedCount);

    [LoggerMessage(
        EventId = 4206,
        Level = LogLevel.Warning,
        Message = "Segment {SegmentIndex} failed (attempt {Attempt}/{MaxAttempts}). Retrying in {Delay}s...")]
    public static partial void LogSegmentRetrying(
        this ILogger logger,
        Exception exception,
        int segmentIndex,
        int attempt,
        int maxAttempts,
        double delay);

    [LoggerMessage(
        EventId = 4207,
        Level = LogLevel.Debug,
        Message = "Downloading segment {SegmentIndex}: {Start}-{End} ({Length} bytes)")]
    public static partial void LogDownloadingSegment(
        this ILogger logger,
        int segmentIndex,
        long start,
        long end,
        long length);

    [LoggerMessage(
        EventId = 4208,
        Level = LogLevel.Debug,
        Message = "Segment {SegmentIndex} checksum computed: {Checksum}")]
    public static partial void LogSegmentChecksumComputed(
        this ILogger logger,
        int segmentIndex,
        string checksum);

    [LoggerMessage(
        EventId = 4209,
        Level = LogLevel.Debug,
        Message = "Segment {SegmentIndex} completed. Duration: {Duration}ms")]
    public static partial void LogSegmentCompleted(
        this ILogger logger,
        int segmentIndex,
        double duration);

    [LoggerMessage(
        EventId = 4210,
        Level = LogLevel.Information,
        Message = "Verifying segment boundaries...")]
    public static partial void LogVerifyingSegmentBoundaries(
        this ILogger logger);

    [LoggerMessage(
        EventId = 4211,
        Level = LogLevel.Information,
        Message = "Segment boundary verification completed successfully. Total size: {TotalSize} bytes")]
    public static partial void LogSegmentBoundariesVerified(
        this ILogger logger,
        long totalSize);

    [LoggerMessage(
        EventId = 4212,
        Level = LogLevel.Information,
        Message = "Verifying checksums for {CompletedCount} completed segments")]
    public static partial void LogVerifyingCompletedSegments(
        this ILogger logger,
        int completedCount);

    [LoggerMessage(
        EventId = 4213,
        Level = LogLevel.Warning,
        Message = "Segment file not found for segment {SegmentIndex}, skipping verification")]
    public static partial void LogSegmentFileNotFound(
        this ILogger logger,
        int segmentIndex);

    [LoggerMessage(
        EventId = 4214,
        Level = LogLevel.Debug,
        Message = "Segment {SegmentIndex} checksum verified successfully")]
    public static partial void LogSegmentChecksumVerified(
        this ILogger logger,
        int segmentIndex);

    [LoggerMessage(
        EventId = 4215,
        Level = LogLevel.Warning,
        Message = "Segment {SegmentIndex} checksum verification failed. Expected: {ExpectedChecksum}, will re-download")]
    public static partial void LogSegmentChecksumFailed(
        this ILogger logger,
        int segmentIndex,
        string expectedChecksum);

    [LoggerMessage(
        EventId = 4216,
        Level = LogLevel.Warning,
        Message = "Detected {CorruptedCount} corrupted segments: {SegmentIndices}. These will be re-downloaded.")]
    public static partial void LogCorruptedSegmentsDetected(
        this ILogger logger,
        int corruptedCount,
        string segmentIndices);

    [LoggerMessage(
        EventId = 4217,
        Level = LogLevel.Information,
        Message = "All completed segments verified successfully")]
    public static partial void LogAllSegmentsVerified(
        this ILogger logger);
}
