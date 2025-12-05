using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Engine;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace KuriousLabs.Kurio.Engine;

/// <summary>
///     Tests for memory cleanup functionality in DownloadEngine.
///     Validates that tracking dictionaries are properly cleaned when downloads reach final states.
/// </summary>
public sealed class MemoryCleanupTests : IDisposable
{
    private readonly DownloadEngine _engine;
    private readonly Mock<IProtocolHandler> _mockProtocolHandler;
    private readonly Mock<ISegmentVerifier> _mockSegmentVerifier;
    private readonly Mock<IStatePersistence> _mockStatePersistence;
    private readonly Mock<IStorageManager> _mockStorageManager;
    private readonly ISegmentManager _segmentManager;
    private readonly string _testTempDirectory;

    public MemoryCleanupTests()
    {
        _testTempDirectory = Path.Combine(Path.GetTempPath(), "kurio-test-cleanup", Guid.NewGuid().ToString());

        _mockProtocolHandler = new Mock<IProtocolHandler>();
        _mockStorageManager = new Mock<IStorageManager>();
        _mockStatePersistence = new Mock<IStatePersistence>();
        _mockSegmentVerifier = new Mock<ISegmentVerifier>();
        _segmentManager = new SegmentManager(
            _mockStorageManager.Object,
            _mockSegmentVerifier.Object,
            NullLogger<SegmentManager>.Instance);

        Directory.CreateDirectory(_testTempDirectory);

        SetupMockStatePersistence();
        SetupMockStorageManager();

        _engine = new DownloadEngine(
            _mockProtocolHandler.Object,
            _mockStorageManager.Object,
            _segmentManager,
            _mockStatePersistence.Object,
            Mock.Of<ILogger<DownloadEngine>>(),
            maxConcurrentDownloads: 1);
    }

    public void Dispose()
    {
        _engine.Dispose();

        try
        {
            if (Directory.Exists(_testTempDirectory))
            {
                Directory.Delete(_testTempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task CancelDownloadAsync_ShouldCleanupTrackingData()
    {
        // Arrange
        var downloadOptions = CreateDownloadOptions();
        var task = await _engine.AddDownloadAsync(new Uri("https://example.com/file.zip"), downloadOptions);

        // Act
        await _engine.CancelDownloadAsync(task.Id, removePartialFiles: true);

        // Assert - task should still be accessible but tracking should be cleaned
        var cancelledTask = _engine.GetDownload(task.Id);
        Assert.NotNull(cancelledTask);
        Assert.Equal(DownloadState.Cancelled, cancelledTask.State);

        // Verify cleanup was called
        _mockStorageManager.Verify(
            x => x.CleanupTemporaryFilesAsync(task.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockStatePersistence.Verify(
            x => x.DeleteStateAsync(task.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClearCompleted_ShouldCleanupTrackingDataForCompletedTasks()
    {
        // Arrange
        SetupSuccessfulDownload();

        var downloadOptions = CreateDownloadOptions();
        var task = await _engine.AddDownloadAsync(new Uri("https://example.com/file.zip"), downloadOptions);

        // Start download and wait for completion
        await _engine.StartDownloadAsync(task.Id);
        await WaitForTaskState(task.Id, DownloadState.Completed, TimeSpan.FromSeconds(10));

        // Act
        _engine.ClearCompleted();

        // Assert - task should be removed from tracking
        var clearedTask = _engine.GetDownload(task.Id);
        Assert.Null(clearedTask);
    }

    [Fact]
    public async Task FailedDownload_ShouldCleanupTrackingData()
    {
        // Arrange
        SetupFailingDownload();

        var downloadOptions = CreateDownloadOptions();
        var task = await _engine.AddDownloadAsync(new Uri("https://example.com/file.zip"), downloadOptions);

        // Act - Start download which will fail
        await _engine.StartDownloadAsync(task.Id);
        await WaitForTaskState(task.Id, DownloadState.Failed, TimeSpan.FromSeconds(15));

        // Assert - task should be accessible with failed state
        var failedTask = _engine.GetDownload(task.Id);
        Assert.NotNull(failedTask);
        Assert.Equal(DownloadState.Failed, failedTask.State);
        Assert.NotNull(failedTask.LastError);

        // Verify state was saved
        _mockStatePersistence.Verify(
            x => x.SaveStateAsync(It.Is<DownloadTaskState>(s => s.State == DownloadState.Failed), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PausedDownload_ShouldNotCleanupTrackingData()
    {
        // Arrange
        SetupSlowDownload();

        var downloadOptions = CreateDownloadOptions();
        var task = await _engine.AddDownloadAsync(new Uri("https://example.com/file.zip"), downloadOptions);

        // Start download
        await _engine.StartDownloadAsync(task.Id);

        // Wait for download to start
        await WaitForTaskState(task.Id, DownloadState.Downloading, TimeSpan.FromSeconds(5));

        // Act - Pause the download
        await _engine.PauseDownloadAsync(task.Id);

        // Assert - task should be paused but still tracked
        var pausedTask = _engine.GetDownload(task.Id);
        Assert.NotNull(pausedTask);
        Assert.Equal(DownloadState.Paused, pausedTask.State);

        // Verify state was saved for resume
        _mockStatePersistence.Verify(
            x => x.SaveStateAsync(It.IsAny<DownloadTaskState>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    private DownloadOptions CreateDownloadOptions()
    {
        return new DownloadOptions
        {
            DestinationDirectory = _testTempDirectory,
            MaxConnections = 1,
            MinSegmentSize = 512 * 1024
        };
    }

    private void SetupMockStatePersistence()
    {
        _mockStatePersistence.Setup(x => x.LoadAllStatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadTaskState>());

        _mockStatePersistence.Setup(x => x.SaveStateAsync(It.IsAny<DownloadTaskState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockStatePersistence.Setup(x => x.DeleteStateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupMockStorageManager()
    {
        _mockStorageManager.Setup(x => x.CreateTemporaryFileAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string name, long size, CancellationToken ct) =>
            {
                var tempPath = Path.Combine(_testTempDirectory, $"{id}.part");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
                return tempPath;
            });

        _mockStorageManager.Setup(x => x.GetAvailableDiskSpaceAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(10L * 1024 * 1024 * 1024); // 10 GB

        _mockStorageManager.Setup(x => x.CommitDownloadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FileNamingPolicy>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string temp, string dest, string name, FileNamingPolicy policy, CancellationToken ct) =>
                Path.Combine(dest, name));

        _mockStorageManager.Setup(x => x.CleanupTemporaryFilesAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockStorageManager.Setup(x => x.MergeSegmentFilesAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupSuccessfulDownload()
    {
        _mockProtocolHandler.Setup(x => x.GetMetadataAsync(
                It.IsAny<Uri>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceMetadata
            {
                ContentLength = 1024,
                SupportsRanges = false,
                ContentType = "application/zip"
            });

        _mockProtocolHandler.Setup(x => x.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.IsAny<ByteRange>(),
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range, stream, options, progress, ct) =>
            {
                var data = new byte[range.Length];
                await stream.WriteAsync(data, 0, data.Length, ct);
                progress?.Report(range.Length);
            });

        _mockSegmentVerifier.Setup(x => x.ComputeChecksumAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("abcd1234abcd1234abcd1234abcd1234");
    }

    private void SetupFailingDownload()
    {
        _mockProtocolHandler.Setup(x => x.GetMetadataAsync(
                It.IsAny<Uri>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Simulated network failure"));
    }

    private void SetupSlowDownload()
    {
        _mockProtocolHandler.Setup(x => x.GetMetadataAsync(
                It.IsAny<Uri>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceMetadata
            {
                ContentLength = 1024 * 1024,
                SupportsRanges = true,
                ContentType = "application/zip"
            });

        _mockProtocolHandler.Setup(x => x.DownloadRangeAsync(
                It.IsAny<Uri>(),
                It.IsAny<ByteRange>(),
                It.IsAny<Stream>(),
                It.IsAny<DownloadOptions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Uri, ByteRange, Stream, DownloadOptions, IProgress<long>, CancellationToken>(async (url, range, stream, options, progress, ct) =>
            {
                // Simulate slow download
                await Task.Delay(5000, ct);
            });
    }

    private async Task WaitForTaskState(Guid taskId, DownloadState expectedState, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        while (DateTime.UtcNow - startTime < timeout)
        {
            var task = _engine.GetDownload(taskId);
            if (task?.State == expectedState)
            {
                return;
            }

            await Task.Delay(100);
        }

        var currentTask = _engine.GetDownload(taskId);
        throw new TimeoutException($"Task did not reach state {expectedState} within {timeout}. Current state: {currentTask?.State}");
    }
}
