using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Core.Verification;

/// <summary>
///     Provides checksum calculation and verification functionality for downloads.
/// </summary>
public sealed class ChecksumVerifier : IChecksumVerifier
{
    private const int DefaultBufferSize = 81920; // 80 KB buffer

    private static readonly char[] SpaceSeparator = [' '];

    /// <inheritdoc />
    public async Task<string> CalculateChecksumAsync(
        string filePath,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}", filePath);
        }

        FileStream fileStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            DefaultBufferSize,
            true);

        await using (fileStream.ConfigureAwait(false))
        {
            return await CalculateChecksumAsync(fileStream, algorithm, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> CalculateChecksumAsync(
        Stream stream,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        using var hashAlgorithm = CreateHashAlgorithm(algorithm);
        var hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);

        return BytesToHexString(hashBytes);
    }

    /// <inheritdoc />
    public async Task<ChecksumResult> VerifyFileAsync(
        string filePath,
        string expectedChecksum,
        ChecksumAlgorithm algorithm,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedChecksum);

        var calculatedChecksum = await CalculateChecksumAsync(filePath, algorithm, cancellationToken).ConfigureAwait(false);

        return new ChecksumResult
        {
            Algorithm = algorithm,
            CalculatedChecksum = calculatedChecksum,
            ExpectedChecksum = expectedChecksum,
            VerifiedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> ParseChecksumFileAsync(
        string checksumFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checksumFilePath);

        if (!File.Exists(checksumFilePath))
        {
            throw new FileNotFoundException($"Checksum file not found: {checksumFilePath}", checksumFilePath);
        }

        Dictionary<string, string> checksums = new(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(checksumFilePath, cancellationToken).ConfigureAwait(false);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue; // Skip empty lines and comments
            }

            var parsed = ParseChecksumLine(line);
            if (parsed.HasValue)
            {
                checksums[parsed.Value.FileName] = parsed.Value.Checksum;
            }
        }

        return checksums;
    }

    /// <inheritdoc />
    public (ChecksumAlgorithm Algorithm, string Checksum)? ExtractChecksumFromHeaders(
        IDictionary<string, IEnumerable<string>> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        // Check for common checksum headers
        var headerMappings = new[]
        {
            ("Content-MD5", ChecksumAlgorithm.MD5), ("X-Checksum-MD5", ChecksumAlgorithm.MD5),
            ("X-Checksum-SHA1", ChecksumAlgorithm.SHA1), ("X-Checksum-SHA-1", ChecksumAlgorithm.SHA1),
            ("X-Checksum-SHA256", ChecksumAlgorithm.SHA256), ("X-Checksum-SHA-256", ChecksumAlgorithm.SHA256),
            ("X-Checksum-SHA512", ChecksumAlgorithm.SHA512), ("X-Checksum-SHA-512", ChecksumAlgorithm.SHA512),
            ("Digest", ChecksumAlgorithm.SHA256) // RFC 3230
        };

        foreach (var (headerName, algorithm) in headerMappings)
        {
            if (headers.TryGetValue(headerName, out var values))
            {
                var value = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Handle RFC 3230 Digest header format: "SHA-256=base64hash"
                    if (headerName == "Digest" && value.Contains('='))
                    {
                        var parts = value.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var digestAlgorithm = ParseDigestAlgorithm(parts[0].Trim());
                            var digestValue = parts[1].Trim();

                            if (digestAlgorithm.HasValue)
                            {
                                // Convert base64 to hex if needed
                                var hexValue = ConvertBase64ToHex(digestValue);
                                return (digestAlgorithm.Value, hexValue);
                            }
                        }
                    }
                    else
                    {
                        return (algorithm, value.Trim());
                    }
                }
            }
        }

        return null;
    }

    private static HashAlgorithm CreateHashAlgorithm(ChecksumAlgorithm algorithm)
    {
        return algorithm switch
        {
#pragma warning disable CA5351 // MD5 is required for checksum verification compatibility
            ChecksumAlgorithm.MD5 => MD5.Create(),
#pragma warning restore CA5351
#pragma warning disable CA5350 // SHA1 is required for checksum verification compatibility
            ChecksumAlgorithm.SHA1 => SHA1.Create(),
#pragma warning restore CA5350
            ChecksumAlgorithm.SHA256 => SHA256.Create(),
            ChecksumAlgorithm.SHA512 => SHA512.Create(),
            ChecksumAlgorithm.None => throw new ArgumentException("Algorithm cannot be None.", nameof(algorithm)),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported algorithm.")
        };
    }

    private static string BytesToHexString(byte[] bytes)
    {
        StringBuilder builder = new(bytes.Length * 2);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static (string FileName, string Checksum)? ParseChecksumLine(string line)
    {
        // Support formats:
        // 1. "checksum *filename" or "checksum  filename" (GNU format)
        // 2. "checksum filename" (BSD format)

        var parts = line.Split(SpaceSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        var checksum = parts[0];
        var fileName = string.Join(" ", parts.Skip(1)).TrimStart('*'); // Remove leading asterisk if present

        return (fileName, checksum);
    }

    private static ChecksumAlgorithm? ParseDigestAlgorithm(string algorithmName)
    {
        return algorithmName.ToUpperInvariant() switch
        {
            "MD5" => ChecksumAlgorithm.MD5,
            "SHA" or "SHA-1" or "SHA1" => ChecksumAlgorithm.SHA1,
            "SHA-256" or "SHA256" => ChecksumAlgorithm.SHA256,
            "SHA-512" or "SHA512" => ChecksumAlgorithm.SHA512,
            _ => null
        };
    }

    private static string ConvertBase64ToHex(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return BytesToHexString(bytes);
        }
        catch
        {
            // If it's not valid base64, assume it's already hex
            return base64;
        }
    }
}
