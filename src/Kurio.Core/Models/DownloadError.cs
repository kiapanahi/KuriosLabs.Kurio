namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Represents an error that occurred during download.
/// </summary>
public sealed class DownloadError
{
    /// <summary>
    ///     Gets or sets the error message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    ///     Gets or sets the user-friendly error description.
    /// </summary>
    public string? UserFriendlyMessage { get; set; }

    /// <summary>
    ///     Gets or sets the error category.
    /// </summary>
    public DownloadErrorCategory Category { get; set; } = DownloadErrorCategory.Unknown;

    /// <summary>
    ///     Gets or sets the exception details if available.
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    ///     Gets or sets the stack trace if available.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    ///     Gets or sets the HTTP status code if applicable.
    /// </summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets whether the error is recoverable (can retry).
    /// </summary>
    public bool IsRecoverable { get; set; }

    /// <summary>
    ///     Gets or sets the recommended recovery action.
    /// </summary>
    public ErrorRecoveryAction RecoveryAction { get; set; } = ErrorRecoveryAction.Fail;

    /// <summary>
    ///     Gets or sets the retry-after duration for rate limiting errors.
    /// </summary>
    public TimeSpan? RetryAfter { get; set; }
}
