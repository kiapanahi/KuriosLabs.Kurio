namespace Kurio.Core.Abstractions;

/// <summary>
///     Provides segment-level checksum computation and verification.
/// </summary>
public interface ISegmentVerifier
{
    /// <summary>
    ///     Computes the checksum for a segment's data.
    /// </summary>
    /// <param name="data">The segment data to hash.</param>
    /// <param name="algorithm">The hash algorithm to use (default: SHA256).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The computed hash as a hex string.</returns>
    Task<string> ComputeChecksumAsync(
        byte[] data,
        string algorithm = "SHA256",
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Computes the checksum for a segment's data from a stream.
    /// </summary>
    /// <param name="stream">The stream containing segment data.</param>
    /// <param name="algorithm">The hash algorithm to use (default: SHA256).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The computed hash as a hex string.</returns>
    Task<string> ComputeChecksumAsync(
        Stream stream,
        string algorithm = "SHA256",
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Verifies a segment file against its expected checksum.
    /// </summary>
    /// <param name="filePath">Path to the segment file.</param>
    /// <param name="offset">Byte offset within the file (for single-file mode).</param>
    /// <param name="length">Length of the segment data.</param>
    /// <param name="expectedChecksum">The expected checksum to verify against.</param>
    /// <param name="algorithm">The hash algorithm used (default: SHA256).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if checksum matches, false otherwise.</returns>
    Task<bool> VerifySegmentAsync(
        string filePath,
        long offset,
        long length,
        string expectedChecksum,
        string algorithm = "SHA256",
        CancellationToken cancellationToken = default);
}
