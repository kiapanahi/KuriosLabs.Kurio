namespace Kurio.Core.Models;

/// <summary>
/// Represents an error that occurred during download.
/// </summary>
public sealed class DownloadError
{
    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Gets or sets the exception details if available.
    /// </summary>
    public string? ExceptionType { get; set; }

    /// <summary>
    /// Gets or sets the stack trace if available.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether the error is recoverable (can retry).
    /// </summary>
    public bool IsRecoverable { get; set; }
}
