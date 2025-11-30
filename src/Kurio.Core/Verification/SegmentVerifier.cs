using System.Security.Cryptography;

using Kurio.Core.Abstractions;

namespace Kurio.Core.Verification;

/// <summary>
///     Provides segment-level checksum computation and verification.
/// </summary>
public sealed class SegmentVerifier : ISegmentVerifier
{
    /// <inheritdoc />
    public Task<string> ComputeChecksumAsync(
        byte[] data,
        string algorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var hashAlgorithm = CreateHashAlgorithm(algorithm);
        var hash = hashAlgorithm.ComputeHash(data);
        return Task.FromResult(Convert.ToHexString(hash));
    }

    /// <inheritdoc />
    public async Task<string> ComputeChecksumAsync(
        Stream stream,
        string algorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        using var hashAlgorithm = CreateHashAlgorithm(algorithm);
        var hash = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc />
    public async Task<bool> VerifySegmentAsync(
        string filePath,
        long offset,
        long length,
        string expectedChecksum,
        string algorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        await using FileStream fileStream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        // Seek to the segment offset
        fileStream.Seek(offset, SeekOrigin.Begin);

        // Create a bounded stream for just this segment
        using BoundedStream boundedStream = new(fileStream, length);

        // Compute checksum of the segment
        var actualChecksum = await ComputeChecksumAsync(boundedStream, algorithm, cancellationToken);

        // Compare checksums (case-insensitive)
        return string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }

    private static HashAlgorithm CreateHashAlgorithm(string algorithm)
    {
        return algorithm.ToUpperInvariant() switch
        {
            "SHA256" => SHA256.Create(),
            "SHA512" => SHA512.Create(),
#pragma warning disable CA5350, CA5351 // Legacy algorithm support for compatibility
            "SHA1" => SHA1.Create(),
            "MD5" => MD5.Create(),
#pragma warning restore CA5350, CA5351
            _ => throw new ArgumentException($"Unsupported hash algorithm: {algorithm}", nameof(algorithm))
        };
    }

    /// <summary>
    ///     A stream wrapper that limits reading to a specific length.
    /// </summary>
    private sealed class BoundedStream : Stream
    {
        private readonly Stream _innerStream;
        private long _position;

        public BoundedStream(Stream innerStream, long maxLength)
        {
            _innerStream = innerStream;
            Length = maxLength;
            _position = 0;
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remainingBytes = Length - _position;
            if (remainingBytes <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, remainingBytes);
            var bytesRead = _innerStream.Read(buffer, offset, bytesToRead);
            _position += bytesRead;
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count,
            CancellationToken cancellationToken)
        {
            var remainingBytes = Length - _position;
            if (remainingBytes <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(count, remainingBytes);
            var bytesRead = await _innerStream.ReadAsync(buffer.AsMemory(offset, bytesToRead), cancellationToken);
            _position += bytesRead;
            return bytesRead;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var remainingBytes = Length - _position;
            if (remainingBytes <= 0)
            {
                return 0;
            }

            var bytesToRead = (int)Math.Min(buffer.Length, remainingBytes);
            var bytesRead = await _innerStream.ReadAsync(buffer[..bytesToRead], cancellationToken);
            _position += bytesRead;
            return bytesRead;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
