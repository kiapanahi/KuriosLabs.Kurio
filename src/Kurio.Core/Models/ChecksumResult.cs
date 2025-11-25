namespace Kurio.Core.Models;

/// <summary>
/// Represents the result of a checksum verification operation.
/// </summary>
public sealed class ChecksumResult
{
    /// <summary>
    /// Gets the checksum algorithm used.
    /// </summary>
    public required ChecksumAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the calculated checksum value.
    /// </summary>
    public required string CalculatedChecksum { get; init; }

    /// <summary>
    /// Gets the expected checksum value (if provided).
    /// </summary>
    public string? ExpectedChecksum { get; init; }

    /// <summary>
    /// Gets a value indicating whether the verification passed.
    /// True if ExpectedChecksum matches CalculatedChecksum, or if ExpectedChecksum is null.
    /// </summary>
    public bool IsValid => ExpectedChecksum == null || 
                          string.Equals(CalculatedChecksum, ExpectedChecksum, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the verification timestamp.
    /// </summary>
    public required DateTime VerifiedAt { get; init; }

    /// <summary>
    /// Gets additional verification metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}
