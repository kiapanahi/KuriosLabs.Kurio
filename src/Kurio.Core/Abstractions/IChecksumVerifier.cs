using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Core.Abstractions;

/// <summary>
///     Provides checksum calculation and verification functionality for downloads.
/// </summary>
public interface IChecksumVerifier
{
    /// <summary>
    ///     Calculates the checksum of a file.
    /// </summary>
    /// <param name="filePath">The path to the file.</param>
    /// <param name="algorithm">The checksum algorithm to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated checksum as a hexadecimal string.</returns>
    Task<string> CalculateChecksumAsync(
        string filePath,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Calculates the checksum of a stream.
    /// </summary>
    /// <param name="stream">The stream to calculate checksum from.</param>
    /// <param name="algorithm">The checksum algorithm to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated checksum as a hexadecimal string.</returns>
    Task<string> CalculateChecksumAsync(
        Stream stream,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies a file against an expected checksum.
    /// </summary>
    /// <param name="filePath">The path to the file to verify.</param>
    /// <param name="expectedChecksum">The expected checksum value.</param>
    /// <param name="algorithm">The checksum algorithm to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The verification result.</returns>
    Task<ChecksumResult> VerifyFileAsync(
        string filePath,
        string expectedChecksum,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Parses a checksum file and extracts checksums for files.
    /// </summary>
    /// <param name="checksumFilePath">The path to the checksum file (.md5, .sha1, .sha256).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping file names to their expected checksums.</returns>
    Task<Dictionary<string, string>> ParseChecksumFileAsync(
        string checksumFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Extracts checksum from HTTP response headers.
    /// </summary>
    /// <param name="headers">The HTTP response headers.</param>
    /// <returns>A tuple containing the algorithm and checksum value, or null if not found.</returns>
    (ChecksumAlgorithm Algorithm, string Checksum)? ExtractChecksumFromHeaders(
        IDictionary<string, IEnumerable<string>> headers);
}
