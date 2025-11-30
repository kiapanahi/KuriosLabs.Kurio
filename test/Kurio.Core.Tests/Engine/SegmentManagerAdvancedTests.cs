using System.Collections.Concurrent;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Engine;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;

using Moq;

namespace KuriousLabs.Kurio.Engine;

/// <summary>
///     Advanced tests for segment manager including parallel downloads and error handling.
/// </summary>
public class SegmentManagerAdvancedTests : IDisposable
{
    private readonly Mock<ILogger<SegmentManager>> _mockLogger;
    private readonly Mock<IProtocolHandler> _mockProtocolHandler;
    private readonly Mock<ISegmentVerifier> _mockSegmentVerifier;
    private readonly Mock<IStorageManager> _mockStorageManager;
    private readonly SegmentManager _segmentManager;
    private readonly string _tempDirectory;

    public SegmentManagerAdvancedTests()
    {
        _mockStorageManager = new Mock<IStorageManager>();
        _mockSegmentVerifier = new Mock<ISegmentVerifier>();
        _mockLogger = new Mock<ILogger<SegmentManager>>();
        _mockProtocolHandler = new Mock<IProtocolHandler>();
        _segmentManager =
            new SegmentManager(_mockStorageManager.Object, _mockSegmentVerifier.Object, _mockLogger.Object);

        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DownloadSegmentsAsync_WithMultipleSegments_ShouldDownloadInParallel()
    {
        // Arrange
        var fileSize = 10 * 1024 * 1024L; // 10 MB
        SegmentOptions options = new()
        {
            MaxConnections = 4,
            MinSegmentSize = 1024 * 1024 // 1 MB
        };

        var config = _segmentManager.CalculateSegments(fileSize, true, options);
        var tempFilePath = Path.Combine(_tempDirectory, "test.part");
        File.WriteAllBytes(tempFilePath, new byte[fileSize]);

        ConcurrentBag<int> downloadedSegments = new();
        ConcurrentBag<SegmentProgress> progressReports = new();

        // Mock protocol handler to simulate segment downloads
        _mockProtocolHandler
            .Setup(h => h.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.IsAny<ByteRange>(),
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range,
                stream, opts, progress, ct) =>
            {
                // Simulate download of random data
                var data = new byte[range.Length];
                new Random().NextBytes(data);
                await stream.WriteAsync(data, ct);

                // Report progress
                progress?.Report(range.Length);

                // Track downloaded segment
                var segmentIndex = Array.FindIndex(config.Ranges, r => r.Start == range.Start);
                downloadedSegments.Add(segmentIndex);
            });

        // Mock storage manager
        _mockStorageManager
            .Setup(s => s.WriteSegmentAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Progress<SegmentProgress> progress = new(p => progressReports.Add(p));

        // Act
        await _segmentManager.DownloadSegmentsAsync(
            _mockProtocolHandler.Object,
            new Uri("https://example.com/file.bin"),
            config,
            tempFilePath,
            new DownloadOptions { DestinationDirectory = _tempDirectory },
            progress,
            CancellationToken.None);

        // Assert
        Assert.Equal(4, downloadedSegments.Count);
        Assert.All(config.States, state => Assert.Equal(SegmentStatus.Completed, state.Status));
        Assert.NotEmpty(progressReports);
    }

    [Fact]
    public async Task DownloadSegmentsAsync_WithSegmentFailure_ShouldRetry()
    {
        // Arrange
        var fileSize = 2 * 1024 * 1024L; // 2 MB
        SegmentOptions options = new()
        {
            MaxConnections = 2,
            MinSegmentSize = 1024 * 1024 // 1 MB
        };

        var config = _segmentManager.CalculateSegments(fileSize, true, options);
        var tempFilePath = Path.Combine(_tempDirectory, "test.part");
        File.WriteAllBytes(tempFilePath, new byte[fileSize]);

        var attemptCount = 0;

        // Mock protocol handler to fail first attempt, succeed on retry
        _mockProtocolHandler
            .Setup(h => h.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.Is<ByteRange>(r => r.Start == 0), // First segment
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range,
                stream, opts, progress, ct) =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    throw new IOException("Simulated network error");
                }

                // Succeed on retry
                var data = new byte[range.Length];
                await stream.WriteAsync(data, ct);
                progress?.Report(range.Length);
            });

        // Second segment succeeds immediately
        _mockProtocolHandler
            .Setup(h => h.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.Is<ByteRange>(r => r.Start > 0), // Other segments
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range,
                stream, opts, progress, ct) =>
            {
                var data = new byte[range.Length];
                await stream.WriteAsync(data, ct);
                progress?.Report(range.Length);
            });

        _mockStorageManager
            .Setup(s => s.WriteSegmentAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _segmentManager.DownloadSegmentsAsync(
            _mockProtocolHandler.Object,
            new Uri("https://example.com/file.bin"),
            config,
            tempFilePath,
            new DownloadOptions { DestinationDirectory = _tempDirectory },
            null,
            CancellationToken.None);

        // Assert
        Assert.True(attemptCount > 1, "Should have retried after failure");
        Assert.All(config.States, state => Assert.Equal(SegmentStatus.Completed, state.Status));
    }

    [Fact]
    public async Task DownloadSegmentsAsync_WithSizeMismatch_ShouldThrowException()
    {
        // Arrange
        var fileSize = 1024 * 1024L; // 1 MB
        SegmentOptions options = new() { MaxConnections = 1, MinSegmentSize = 1024 * 1024 };

        var config = _segmentManager.CalculateSegments(fileSize, true, options);
        var tempFilePath = Path.Combine(_tempDirectory, "test.part");
        File.WriteAllBytes(tempFilePath, new byte[fileSize]);

        // Mock protocol handler to return wrong size
        _mockProtocolHandler
            .Setup(h => h.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.IsAny<ByteRange>(),
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range,
                stream, opts, progress, ct) =>
            {
                // Write less data than expected
                var data = new byte[range.Length / 2];
                await stream.WriteAsync(data, ct);
            });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AggregateException>(async () =>
            await _segmentManager.DownloadSegmentsAsync(
                _mockProtocolHandler.Object,
                new Uri("https://example.com/file.bin"),
                config,
                tempFilePath,
                new DownloadOptions { DestinationDirectory = _tempDirectory },
                null,
                CancellationToken.None));

        // Verify the inner exception is InvalidOperationException
        Assert.Contains(exception.InnerExceptions, e => e is InvalidOperationException);
    }

    [Fact]
    public async Task ResumeSegmentsAsync_ShouldOnlyDownloadIncompleteSegments()
    {
        // Arrange
        var fileSize = 4 * 1024 * 1024L; // 4 MB
        SegmentOptions options = new() { MaxConnections = 4, MinSegmentSize = 1024 * 1024 };

        var config = _segmentManager.CalculateSegments(fileSize, true, options);
        var tempFilePath = Path.Combine(_tempDirectory, "test.part");
        File.WriteAllBytes(tempFilePath, new byte[fileSize]);

        // Mark some segments as completed
        config.States[0].Status = SegmentStatus.Completed;
        config.States[0].BytesDownloaded = config.States[0].TotalSize;
        config.States[2].Status = SegmentStatus.Completed;
        config.States[2].BytesDownloaded = config.States[2].TotalSize;

        ConcurrentBag<int> downloadedSegmentIndices = new();

        _mockProtocolHandler
            .Setup(h => h.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.IsAny<ByteRange>(),
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range,
                stream, opts, progress, ct) =>
            {
                var data = new byte[range.Length];
                await stream.WriteAsync(data, ct);
                progress?.Report(range.Length);

                // Track which segment was downloaded
                var segmentIndex = Array.FindIndex(config.Ranges, r =>
                    range.Start >= r.Start && range.End <= r.End);
                downloadedSegmentIndices.Add(segmentIndex);
            });

        _mockStorageManager
            .Setup(s => s.WriteSegmentAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _segmentManager.ResumeSegmentsAsync(
            _mockProtocolHandler.Object,
            new Uri("https://example.com/file.bin"),
            config,
            config.States,
            tempFilePath,
            new DownloadOptions { DestinationDirectory = _tempDirectory },
            null,
            CancellationToken.None);

        // Assert - should only download segments 1 and 3
        Assert.Equal(2, downloadedSegmentIndices.Count);
        Assert.Contains(1, downloadedSegmentIndices);
        Assert.Contains(3, downloadedSegmentIndices);
        Assert.DoesNotContain(0, downloadedSegmentIndices);
        Assert.DoesNotContain(2, downloadedSegmentIndices);
    }

    [Theory]
    [InlineData(1024)] // 1 KB
    [InlineData(10 * 1024)] // 10 KB
    [InlineData(1024 * 1024)] // 1 MB
    [InlineData(10 * 1024 * 1024)] // 10 MB
    [InlineData(100 * 1024 * 1024)] // 100 MB
    public void CalculateSegments_WithVariousFileSizes_ShouldCalculateCorrectly(long fileSize)
    {
        // Arrange
        SegmentOptions options = new()
        {
            MaxConnections = 8,
            MinSegmentSize = 1024 * 1024 // 1 MB
        };

        // Act
        var config = _segmentManager.CalculateSegments(fileSize, true, options);

        // Assert
        Assert.True(config.SegmentCount > 0);
        Assert.Equal(config.SegmentCount, config.Ranges.Length);
        Assert.Equal(config.SegmentCount, config.States.Length);

        // Verify total size
        var totalSize = config.Ranges.Sum(r => r.Length);
        Assert.Equal(fileSize, totalSize);

        // Verify no gaps or overlaps
        var sortedRanges = config.Ranges.OrderBy(r => r.Start).ToArray();
        Assert.Equal(0, sortedRanges[0].Start);
        Assert.Equal(fileSize - 1, sortedRanges[^1].End);

        for (var i = 0; i < sortedRanges.Length - 1; i++)
        {
            Assert.Equal(sortedRanges[i].End + 1, sortedRanges[i + 1].Start);
        }
    }

    [Fact]
    public void CalculateSegments_WithEdgeCases_ShouldHandleCorrectly()
    {
        // Test edge case: 1 byte file
        SegmentOptions options = new() { MaxConnections = 8, MinSegmentSize = 1 };
        var config = _segmentManager.CalculateSegments(1, true, options);
        Assert.Equal(1, config.SegmentCount);
        Assert.Equal(0, config.Ranges[0].Start);
        Assert.Equal(0, config.Ranges[0].End);
        Assert.Equal(1, config.Ranges[0].Length);

        // Test edge case: Exact multiple of min segment size
        var fileSize = 8 * 1024 * 1024L; // 8 MB
        options = new SegmentOptions { MaxConnections = 8, MinSegmentSize = 1024 * 1024 };
        config = _segmentManager.CalculateSegments(fileSize, true, options);
        Assert.Equal(8, config.SegmentCount);
    }
}
