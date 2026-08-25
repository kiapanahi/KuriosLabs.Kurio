using System.Net.Sockets;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.ErrorHandling;

/// <summary>
///     Classifies exceptions into error categories and determines recovery actions.
/// </summary>
public sealed class ErrorClassifier(ILogger<ErrorClassifier> logger) : IErrorClassifier
{
    /// <inheritdoc />
    public DownloadError Classify(Exception exception)
    {
        DownloadError error = new()
        {
            Message = exception.Message,
            ExceptionType = exception.GetType().FullName,
            StackTrace = exception.StackTrace,
            Timestamp = DateTime.UtcNow,
            // Classify based on exception type
            Category = ClassifyException(exception)
        };

        // Extract HTTP status code if available
        if (exception is HttpRequestException httpEx)
        {
            error.HttpStatusCode = (int?)httpEx.StatusCode;
        }

        error.IsRecoverable = IsRecoverableCategory(error.Category);
        error.RecoveryAction = GetRecoveryAction(error);
        error.UserFriendlyMessage = GetUserFriendlyMessage(error);

        // Check for rate limiting
        if (error.HttpStatusCode is 429 or 503)
        {
            error.Category = DownloadErrorCategory.RateLimiting;
            if (exception is HttpRequestException httpRequestEx)
            {
                error.RetryAfter = ExtractRetryAfter(httpRequestEx);
            }
        }

        logger.LogExceptionClassified(exception.GetType().Name, error.Category.ToString());

        return error;
    }

    /// <inheritdoc />
    public bool IsTransient(DownloadError error)
    {
        return error.Category switch
        {
            DownloadErrorCategory.Network => true,
            DownloadErrorCategory.RateLimiting => true,
            DownloadErrorCategory.Http when error.HttpStatusCode >= 500 => true,
            _ => false
        };
    }

    /// <inheritdoc />
    public ErrorRecoveryAction GetRecoveryAction(DownloadError error)
    {
        return error.Category switch
        {
            DownloadErrorCategory.Network => ErrorRecoveryAction.Retry,
            DownloadErrorCategory.RateLimiting => ErrorRecoveryAction.Retry,
            DownloadErrorCategory.Http when error.HttpStatusCode == 404 => ErrorRecoveryAction.Fail,
            DownloadErrorCategory.Http when error.HttpStatusCode is 401 or 403 =>
                ErrorRecoveryAction.Pause,
            DownloadErrorCategory.Http when error.HttpStatusCode == 416 => ErrorRecoveryAction.FallbackToSingleThread,
            DownloadErrorCategory.Http when error.HttpStatusCode >= 500 => ErrorRecoveryAction.Retry,
            DownloadErrorCategory.DiskIo => ErrorRecoveryAction.Pause,
            DownloadErrorCategory.Protocol => ErrorRecoveryAction.Retry,
            DownloadErrorCategory.ResourceNotFound => ErrorRecoveryAction.Fail,
            DownloadErrorCategory.Authentication => ErrorRecoveryAction.Pause,
            _ => ErrorRecoveryAction.Fail
        };
    }

    private static DownloadErrorCategory ClassifyException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => ClassifyHttpException(httpEx),
            SocketException or TimeoutException or TaskCanceledException => DownloadErrorCategory.Network,
            IOException ioEx => ClassifyIoException(ioEx),
            UnauthorizedAccessException => DownloadErrorCategory.Authentication,
            _ => DownloadErrorCategory.Unknown
        };
    }

    private static DownloadErrorCategory ClassifyHttpException(HttpRequestException httpEx)
    {
        if (httpEx.StatusCode == null)
        {
            return DownloadErrorCategory.Network;
        }

        return (int)httpEx.StatusCode switch
        {
            404 => DownloadErrorCategory.ResourceNotFound,
            401 or 403 => DownloadErrorCategory.Authentication,
            429 or 503 => DownloadErrorCategory.RateLimiting,
            >= 400 and < 500 or >= 500 => DownloadErrorCategory.Http,
            _ => DownloadErrorCategory.Unknown
        };
    }

    private static DownloadErrorCategory ClassifyIoException(IOException ioEx)
    {
        var message = ioEx.Message.ToLowerInvariant();

        // Check for network-related IO exceptions first
        if (message.Contains("eof") ||
            message.Contains("transport stream") ||
            message.Contains("connection") ||
            message.Contains("reset") ||
            message.Contains("broken pipe") ||
            message.Contains("unable to read") ||
            message.Contains("unable to write") ||
            ioEx.InnerException is SocketException)
        {
            return DownloadErrorCategory.Network;
        }

        // Check for disk-related IO exceptions
        if (message.Contains("space") ||
            message.Contains("disk full") ||
            message.Contains("permission") ||
            message.Contains("access denied"))
        {
            return DownloadErrorCategory.DiskIo;
        }

        // Default to disk IO for other IO exceptions
        return DownloadErrorCategory.DiskIo;
    }

    private static bool IsRecoverableCategory(DownloadErrorCategory category)
    {
        return category switch
        {
            DownloadErrorCategory.Network => true,
            DownloadErrorCategory.RateLimiting => true,
            DownloadErrorCategory.Http => true,
            DownloadErrorCategory.Protocol => true,
            _ => false
        };
    }

    private static string GetUserFriendlyMessage(DownloadError error)
    {
        return error.Category switch
        {
            DownloadErrorCategory.Network =>
                "Network connection failed. The download will retry automatically.",
            DownloadErrorCategory.RateLimiting =>
                "Server is rate limiting requests. The download will retry after a delay.",
            DownloadErrorCategory.Http when error.HttpStatusCode == 404 =>
                "The file could not be found on the server.",
            DownloadErrorCategory.Http when error.HttpStatusCode == 401 || error.HttpStatusCode == 403 =>
                "Authentication required. Please check your credentials.",
            DownloadErrorCategory.Http when error.HttpStatusCode >= 500 =>
                "Server error occurred. The download will retry automatically.",
            DownloadErrorCategory.DiskIo =>
                "Disk I/O error. Please check available disk space and permissions.",
            DownloadErrorCategory.ResourceNotFound =>
                "The requested resource could not be found.",
            DownloadErrorCategory.Authentication =>
                "Authentication failed. Please check your credentials.",
            _ => "An unexpected error occurred during the download."
        };
    }

    private static TimeSpan? ExtractRetryAfter(HttpRequestException httpEx)
    {
        // This would need access to response headers in a real implementation
        // For now, return a default value
        return TimeSpan.FromSeconds(60);
    }
}
