using Microsoft.Extensions.Logging;

namespace Kurio.Core.Protocols;

internal static partial class HttpProtocolHandlerLogMessages
{
    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Debug,
        Message = "Checking range request support for {Url}")]
    public static partial void LogCheckingRangeSupport(
        this ILogger logger,
        string url);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Debug,
        Message = "Server {SupportsText} range requests (Accept-Ranges: {AcceptRanges})")]
    public static partial void LogRangeSupportResult(
        this ILogger logger,
        string supportsText,
        string acceptRanges);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "No Accept-Ranges header found, assuming no range support")]
    public static partial void LogNoAcceptRangesHeader(
        this ILogger logger);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Warning,
        Message = "HEAD request failed, attempting range request test")]
    public static partial void LogHeadRequestFailed(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Debug,
        Message = "File size for {Url}: {Size} bytes")]
    public static partial void LogFileSize(
        this ILogger logger,
        string url,
        long size);

    [LoggerMessage(
        EventId = 5005,
        Level = LogLevel.Error,
        Message = "Failed to get file size for {Url}")]
    public static partial void LogFileSizeFailed(
        this ILogger logger,
        Exception exception,
        string url);

    [LoggerMessage(
        EventId = 5006,
        Level = LogLevel.Debug,
        Message = "Downloading range {Start}-{End} from {Url}")]
    public static partial void LogDownloadingRange(
        this ILogger logger,
        long start,
        long end,
        string url);

    [LoggerMessage(
        EventId = 5007,
        Level = LogLevel.Warning,
        Message = "Server returned {StatusCode} instead of 206 Partial Content for range request")]
    public static partial void LogUnexpectedStatusCode(
        this ILogger logger,
        int statusCode);

    [LoggerMessage(
        EventId = 5008,
        Level = LogLevel.Warning,
        Message = "Download stalled: no data received for {Seconds}s on range {Start}-{End}")]
    public static partial void LogDownloadStalled(
        this ILogger logger,
        double seconds,
        long start,
        long end);

    [LoggerMessage(
        EventId = 5009,
        Level = LogLevel.Debug,
        Message = "Successfully downloaded {Bytes} bytes from range {Start}-{End}")]
    public static partial void LogDownloadSuccess(
        this ILogger logger,
        long bytes,
        long start,
        long end);

    [LoggerMessage(
        EventId = 5010,
        Level = LogLevel.Error,
        Message = "Failed to download range {Start}-{End} from {Url}")]
    public static partial void LogDownloadFailed(
        this ILogger logger,
        Exception exception,
        long start,
        long end,
        string url);

    [LoggerMessage(
        EventId = 5011,
        Level = LogLevel.Information,
        Message = "Download range {Start}-{End} was cancelled")]
    public static partial void LogDownloadCancelled(
        this ILogger logger,
        long start,
        long end);

    [LoggerMessage(
        EventId = 5012,
        Level = LogLevel.Error,
        Message = "Timeout downloading range {Start}-{End} from {Url}")]
    public static partial void LogDownloadTimeout(
        this ILogger logger,
        Exception exception,
        long start,
        long end,
        string url);

    [LoggerMessage(
        EventId = 5013,
        Level = LogLevel.Debug,
        Message = "Fetching metadata for {Url}")]
    public static partial void LogFetchingMetadata(
        this ILogger logger,
        string url);

    [LoggerMessage(
        EventId = 5014,
        Level = LogLevel.Debug,
        Message = "Metadata fetched: Size={Size}, Type={Type}, Ranges={Ranges}")]
    public static partial void LogMetadataFetched(
        this ILogger logger,
        long size,
        string? type,
        bool ranges);

    [LoggerMessage(
        EventId = 5015,
        Level = LogLevel.Error,
        Message = "Failed to fetch metadata for {Url}")]
    public static partial void LogMetadataFetchFailed(
        this ILogger logger,
        Exception exception,
        string url);

    [LoggerMessage(
        EventId = 5016,
        Level = LogLevel.Debug,
        Message = "Testing range request support with byte range 0-0 for {Url}")]
    public static partial void LogTestingRangeRequest(
        this ILogger logger,
        string url);

    [LoggerMessage(
        EventId = 5017,
        Level = LogLevel.Debug,
        Message = "Range request test result: {Result}")]
    public static partial void LogRangeTestResult(
        this ILogger logger,
        bool result);

    [LoggerMessage(
        EventId = 5018,
        Level = LogLevel.Warning,
        Message = "Range request test failed for {Url}")]
    public static partial void LogRangeTestFailed(
        this ILogger logger,
        Exception exception,
        string url);
}
