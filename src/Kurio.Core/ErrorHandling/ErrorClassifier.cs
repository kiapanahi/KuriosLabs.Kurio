using System.Net;
using System.Net.Sockets;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kurio.Core.ErrorHandling;

/// <summary>
/// Classifies exceptions into error categories and determines recovery actions.
/// </summary>
public sealed class ErrorClassifier : IErrorClassifier
{
    private readonly ILogger<ErrorClassifier> _logger;

    public ErrorClassifier(ILogger<ErrorClassifier> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public DownloadError Classify(Exception exception)
    {
        var error = new DownloadError
        {
            Message = exception.Message,
            ExceptionType = exception.GetType().FullName,
            StackTrace = exception.StackTrace,
            Timestamp = DateTime.UtcNow
        };

        // Classify based on exception type
        error.Category = ClassifyException(exception);
        
        // Extract HTTP status code if available
        if (exception is HttpRequestException httpEx)
        {
            error.HttpStatusCode = (int?)httpEx.StatusCode;
        }
        
        error.IsRecoverable = IsRecoverableCategory(error.Category);
        error.RecoveryAction = GetRecoveryAction(error);
        error.UserFriendlyMessage = GetUserFriendlyMessage(error);

        // Check for rate limiting
        if (error.HttpStatusCode == 429 || error.HttpStatusCode == 503)
        {
            error.Category = DownloadErrorCategory.RateLimiting;
            if (exception is HttpRequestException httpRequestEx)
            {
                error.RetryAfter = ExtractRetryAfter(httpRequestEx);
            }
        }

        _logger.LogDebug("Classified exception {ExceptionType} as {Category}",
            exception.GetType().Name, error.Category);

        return error;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public ErrorRecoveryAction GetRecoveryAction(DownloadError error)
    {
        return error.Category switch
        {
            DownloadErrorCategory.Network => ErrorRecoveryAction.Retry,
            DownloadErrorCategory.RateLimiting => ErrorRecoveryAction.Retry,
            DownloadErrorCategory.Http when error.HttpStatusCode == 404 => ErrorRecoveryAction.Fail,
            DownloadErrorCategory.Http when error.HttpStatusCode == 401 || error.HttpStatusCode == 403 =>
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

    private DownloadErrorCategory ClassifyException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => ClassifyHttpException(httpEx),
            SocketException => DownloadErrorCategory.Network,
            TimeoutException => DownloadErrorCategory.Network,
            TaskCanceledException => DownloadErrorCategory.Network,
            IOException ioEx => ClassifyIoException(ioEx),
            UnauthorizedAccessException => DownloadErrorCategory.Authentication,
            _ => DownloadErrorCategory.Unknown
        };
    }

    private DownloadErrorCategory ClassifyHttpException(HttpRequestException httpEx)
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
            >= 400 and < 500 => DownloadErrorCategory.Http,
            >= 500 => DownloadErrorCategory.Http,
            _ => DownloadErrorCategory.Unknown
        };
    }

    private DownloadErrorCategory ClassifyIoException(IOException ioEx)
    {
        var message = ioEx.Message.ToLowerInvariant();

        if (message.Contains("space") || message.Contains("disk full"))
        {
            return DownloadErrorCategory.DiskIo;
        }

        if (message.Contains("permission") || message.Contains("access denied"))
        {
            return DownloadErrorCategory.DiskIo;
        }

        return DownloadErrorCategory.DiskIo;
    }

    private bool IsRecoverableCategory(DownloadErrorCategory category)
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

    private string GetUserFriendlyMessage(DownloadError error)
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

    private TimeSpan? ExtractRetryAfter(HttpRequestException httpEx)
    {
        // This would need access to response headers in a real implementation
        // For now, return a default value
        return TimeSpan.FromSeconds(60);
    }
}
