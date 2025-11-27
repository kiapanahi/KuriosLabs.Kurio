namespace Kurio.Core.Tests.Engine;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Kurio.Core.Abstractions;
using Kurio.Core.Engine;
using Kurio.Core.Models;
using Kurio.Core.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

/// <summary>
/// Tests for pause and resume functionality in DownloadEngine.
/// </summary>
public sealed class PauseResumeTests : IDisposable
{
    private readonly string _testTempDirectory;
    private readonly string _testStateDirectory;
    private readonly Mock<IProtocolHandler> _mockProtocolHandler;
    private readonly Mock<IStorageManager> _mockStorageManager;
    private readonly Mock<IStatePersistence> _mockStatePersistence;
    private readonly Mock<ISegmentVerifier> _mockSegmentVerifier;
    private readonly ISegmentManager _segmentManager;

    public PauseResumeTests()
    {
        _testTempDirectory = Path.Combine(Path.GetTempPath(), "kurio-test-temp", Guid.NewGuid().ToString());
        _testStateDirectory = Path.Combine(Path.GetTempPath(), "kurio-test-state", Guid.NewGuid().ToString());

        _mockProtocolHandler = new Mock<IProtocolHandler>();
        _mockStorageManager = new Mock<IStorageManager>();
        _mockStatePersistence = new Mock<IStatePersistence>();
        _mockSegmentVerifier = new Mock<ISegmentVerifier>();
        _segmentManager = new SegmentManager(
            _mockStorageManager.Object,
            _mockSegmentVerifier.Object,
            NullLogger<SegmentManager>.Instance);

        Directory.CreateDirectory(_testTempDirectory);
        Directory.CreateDirectory(_testStateDirectory);
    }

    [Fact]
    public async Task PauseDownloadAsync_ChangesStateAndSavesProgress()
    {
        // Arrange
        SetupMockProtocolHandler();
        SetupMockStorageManager();

        var engine = CreateDownloadEngine();
        var downloadOptions = CreateDownloadOptions();
        var task = await engine.AddDownloadAsync(new Uri("https://example.com/file.zip"), downloadOptions);

        // Start download
        await engine.StartDownloadAsync(task.Id);

        // Wait for download to start
        await Task.Delay(500);

        // Act
        await engine.PauseDownloadAsync(task.Id);

        // Assert
        var downloadTask = engine.GetDownload(task.Id);
        Assert.NotNull(downloadTask);
        Assert.Equal(DownloadState.Paused, downloadTask.State);

        // Verify state was saved
        _mockStatePersistence.Verify(
            x => x.SaveStateAsync(It.IsAny<DownloadTaskState>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PauseDownloadAsync_ThrowsException_WhenTaskNotFound()
    {
        // Arrange
        var engine = CreateDownloadEngine();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.PauseDownloadAsync(nonExistentId));
    }

    [Fact]
    public async Task PauseDownloadAsync_ThrowsException_WhenNotDownloading()
    {
        // Arrange
        var engine = CreateDownloadEngine();
        var downloadOptions = CreateDownloadOptions();
        var task = await engine.AddDownloadAsync(new Uri("https://example.com/file.zip"), downloadOptions);

        // Act & Assert (task is in Queued state, not Downloading)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.PauseDownloadAsync(task.Id));
    }

    // Note: Resume validation tests require end-to-end integration testing
    // These tests would need to actually start downloads and pause them,
    // which is better suited for integration tests rather than unit tests

    private DownloadEngine CreateDownloadEngine()
    {
        return new DownloadEngine(
            _mockProtocolHandler.Object,
            _mockStorageManager.Object,
            _segmentManager,
            _mockStatePersistence.Object,
            Mock.Of<ILogger<DownloadEngine>>(),
            maxConcurrentDownloads: 1);
    }

    private DownloadOptions CreateDownloadOptions()
    {
        return new DownloadOptions
        {
            DestinationDirectory = _testTempDirectory,
            MaxConnections = 2,
            MinSegmentSize = 512 * 1024
        };
    }

    private void SetupMockProtocolHandler(
        bool supportsRanges = true,
        string? etag = null,
        DateTimeOffset? lastModified = null)
    {
        _mockProtocolHandler.Setup(x => x.GetMetadataAsync(
                It.IsAny<Uri>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceMetadata
            {
                ContentLength = 1024 * 1024,
                SupportsRanges = supportsRanges,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "application/zip"
            });

        _mockProtocolHandler.Setup(x => x.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.IsAny<ByteRange>(),
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(
                async (url, range, stream, options, progress, ct) =>
                {
                    // Simulate slow download
                    var data = new byte[range.Length];
                    await stream.WriteAsync(data, 0, data.Length, ct);
                    await Task.Delay(2000, ct); // Long delay to allow pause
                });
    }

    private void SetupMockStorageManager()
    {
        _mockStorageManager.Setup(x => x.CreateTemporaryFileAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string name, long size, CancellationToken ct) =>
                Path.Combine(_testTempDirectory, $"{id}.part"));

        _mockStorageManager.Setup(x => x.GetAvailableDiskSpaceAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10L * 1024 * 1024 * 1024); // 10 GB

        _mockStorageManager.Setup(x => x.WriteSegmentAsync(
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<byte[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockStorageManager.Setup(x => x.CommitDownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FileNamingPolicy>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string temp, string dest, string name, FileNamingPolicy policy, CancellationToken ct) =>
                Path.Combine(dest, name));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDirectory))
            {
                Directory.Delete(_testTempDirectory, recursive: true);
            }
            if (Directory.Exists(_testStateDirectory))
            {
                Directory.Delete(_testStateDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
