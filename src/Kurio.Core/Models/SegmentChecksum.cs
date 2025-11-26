namespace Kurio.Core.Models;

/// <summary>
///     Represents checksum information for a downloaded segment.
/// </summary>
public sealed class SegmentChecksum
{
    /// <summary>
    ///     Gets or sets the hash algorithm used (e.g., "SHA256").
    /// </summary>
    public required string Algorithm { get; set; }

    /// <summary>
    ///     Gets or sets the computed hash value (hex string).
    /// </summary>
    public required string Hash { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when the checksum was computed.
    /// </summary>
    public DateTime ComputedAt { get; set; }

    /// <summary>
    ///     Gets or sets whether the checksum has been verified.
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    ///     Gets or sets the timestamp when verification was performed.
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    ///     Gets or sets whether verification failed.
    /// </summary>
    public bool VerificationFailed { get; set; }

    /// <summary>
    ///     Creates a new unverified checksum.
    /// </summary>
    public static SegmentChecksum Create(string algorithm, string hash)
    {
        return new SegmentChecksum
        {
            Algorithm = algorithm,
            Hash = hash,
            ComputedAt = DateTime.UtcNow,
            IsVerified = false
        };
    }

    /// <summary>
    ///     Marks this checksum as verified successfully.
    /// </summary>
    public void MarkAsVerified()
    {
        IsVerified = true;
        VerifiedAt = DateTime.UtcNow;
        VerificationFailed = false;
    }

    /// <summary>
    ///     Marks this checksum verification as failed.
    /// </summary>
    public void MarkAsFailed()
    {
        IsVerified = false;
        VerifiedAt = DateTime.UtcNow;
        VerificationFailed = true;
    }
}
