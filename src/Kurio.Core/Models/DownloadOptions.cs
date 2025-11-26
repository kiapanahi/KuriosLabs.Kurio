namespace Kurio.Core.Models;

/// <summary>
///     Configuration options for a download task.
/// </summary>
public sealed class DownloadOptions
{
    /// <summary>
    ///     Gets or sets the destination directory for the downloaded file.
    /// </summary>
    public required string DestinationDirectory { get; set; }

    /// <summary>
    ///     Gets or sets the file name (optional, will be inferred from URL if not provided).
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of parallel connections for this download.
    /// </summary>
    public int MaxConnections { get; set; } = 8;

    /// <summary>
    ///     Gets or sets the minimum segment size in bytes.
    /// </summary>
    public long MinSegmentSize { get; set; } = 1024 * 1024; // 1 MB

    /// <summary>
    ///     Gets or sets the file naming policy for conflicts.
    /// </summary>
    public FileNamingPolicy FileNamingPolicy { get; set; } = FileNamingPolicy.AutoRename;

    /// <summary>
    ///     Gets or sets custom HTTP headers to include with requests.
    /// </summary>
    public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    /// <summary>
    ///     Gets or sets the authentication credentials (username:password).
    /// </summary>
    public string? Credentials { get; set; }

    /// <summary>
    ///     Gets or sets the user agent string.
    /// </summary>
    public string UserAgent { get; set; } = "Kurio/1.0";

    /// <summary>
    ///     Gets or sets the request timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Gets or sets whether to follow HTTP redirects.
    /// </summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum number of redirects to follow.
    /// </summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>
    ///     Gets or sets whether to validate SSL certificates.
    /// </summary>
    public bool ValidateCertificate { get; set; } = true;

    /// <summary>
    ///     Gets or sets the expected checksum for verification (optional).
    /// </summary>
    public string? ExpectedChecksum { get; set; }

    /// <summary>
    ///     Gets or sets the checksum algorithm (e.g., "SHA256", "MD5").
    /// </summary>
    public string? ChecksumAlgorithm { get; set; }

    /// <summary>
    ///     Gets or sets the verification options for checksum validation.
    /// </summary>
    public VerificationOptions? Verification { get; set; }
}
