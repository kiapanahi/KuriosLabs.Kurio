namespace Kurio.Core.Models;

/// <summary>
///     Options for checksum verification during download.
/// </summary>
public sealed class VerificationOptions
{
    /// <summary>
    ///     Gets or sets the verification mode.
    /// </summary>
    public VerificationMode Mode { get; set; } = VerificationMode.PostDownload;

    /// <summary>
    ///     Gets or sets the checksum algorithm to use.
    /// </summary>
    public ChecksumAlgorithm Algorithm { get; set; } = ChecksumAlgorithm.SHA256;

    /// <summary>
    ///     Gets or sets the expected checksum value.
    /// </summary>
    public string? ExpectedChecksum { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether to fail the download if verification fails.
    /// </summary>
    public bool FailOnMismatch { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether to automatically delete the file on verification failure.
    /// </summary>
    public bool DeleteOnFailure { get; set; } = false;

    /// <summary>
    ///     Gets or sets a value indicating whether to automatically verify checksums from common header fields.
    /// </summary>
    public bool AutoDetectFromHeaders { get; set; } = true;

    /// <summary>
    ///     Gets or sets the path to a checksum file (.md5, .sha1, .sha256).
    /// </summary>
    public string? ChecksumFilePath { get; set; }
}
