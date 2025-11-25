namespace Kurio.Core.Models;

/// <summary>
/// Supported checksum algorithms for download verification.
/// </summary>
public enum ChecksumAlgorithm
{
    /// <summary>
    /// No checksum algorithm specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// MD5 hash algorithm (128-bit).
    /// </summary>
    MD5 = 1,

    /// <summary>
    /// SHA-1 hash algorithm (160-bit).
    /// </summary>
    SHA1 = 2,

    /// <summary>
    /// SHA-256 hash algorithm (256-bit).
    /// </summary>
    SHA256 = 3,

    /// <summary>
    /// SHA-512 hash algorithm (512-bit).
    /// </summary>
    SHA512 = 4
}
