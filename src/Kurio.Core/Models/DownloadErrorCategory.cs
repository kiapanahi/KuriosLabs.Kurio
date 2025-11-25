namespace Kurio.Core.Models;

/// <summary>
/// Categorizes different types of download errors for appropriate handling.
/// </summary>
public enum DownloadErrorCategory
{
    /// <summary>
    /// Network-related errors (connection failed, timeout, DNS failure).
    /// </summary>
    Network,

    /// <summary>
    /// HTTP protocol errors (4xx, 5xx status codes).
    /// </summary>
    Http,

    /// <summary>
    /// Disk I/O errors (no space, permission denied, disk failure).
    /// </summary>
    DiskIo,

    /// <summary>
    /// Protocol-level errors (invalid response, connection reset).
    /// </summary>
    Protocol,

    /// <summary>
    /// Resource not found or unavailable.
    /// </summary>
    ResourceNotFound,

    /// <summary>
    /// Authentication or authorization errors.
    /// </summary>
    Authentication,

    /// <summary>
    /// Rate limiting or server throttling.
    /// </summary>
    RateLimiting,

    /// <summary>
    /// Unknown or uncategorized errors.
    /// </summary>
    Unknown
}
