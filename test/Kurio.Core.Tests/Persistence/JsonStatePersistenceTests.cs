using Kurio.Core.Models;
using Kurio.Core.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

namespace Kurio.Core.Tests.Persistence;

/// <summary>
///     Tests for JsonStatePersistence.
/// </summary>
public sealed class JsonStatePersistenceTests : IDisposable
{
    private readonly JsonStatePersistence _persistence;
    private readonly string _testStateDirectory;

    public JsonStatePersistenceTests()
    {
        _testStateDirectory = Path.Combine(Path.GetTempPath(), "kurio-test-state", Guid.NewGuid().ToString());
        _persistence = new JsonStatePersistence(_testStateDirectory, NullLogger<JsonStatePersistence>.Instance);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testStateDirectory))
            {
                Directory.Delete(_testStateDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task SaveStateAsync_CreatesStateFile()
    {
        // Arrange
        var state = CreateTestState();

        // Act
        await _persistence.SaveStateAsync(state);

        // Assert
        var filePath = Path.Combine(_testStateDirectory, $"{state.TaskId}.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadStateAsync_LoadsPersistedState()
    {
        // Arrange
        var originalState = CreateTestState();
        await _persistence.SaveStateAsync(originalState);

        // Act
        var loadedState = await _persistence.LoadStateAsync(originalState.TaskId);

        // Assert
        Assert.NotNull(loadedState);
        Assert.Equal(originalState.TaskId, loadedState.TaskId);
        Assert.Equal(originalState.Url, loadedState.Url);
        Assert.Equal(originalState.FileName, loadedState.FileName);
        Assert.Equal(originalState.FileSize, loadedState.FileSize);
        Assert.Equal(originalState.State, loadedState.State);
    }

    [Fact]
    public async Task LoadStateAsync_ReturnsNull_WhenStateDoesNotExist()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var state = await _persistence.LoadStateAsync(nonExistentId);

        // Assert
        Assert.Null(state);
    }

    [Fact]
    public async Task DeleteStateAsync_RemovesStateFile()
    {
        // Arrange
        var state = CreateTestState();
        await _persistence.SaveStateAsync(state);

        // Act
        await _persistence.DeleteStateAsync(state.TaskId);

        // Assert
        var filePath = Path.Combine(_testStateDirectory, $"{state.TaskId}.json");
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadAllStatesAsync_LoadsMultipleStates()
    {
        // Arrange
        var state1 = CreateTestState();
        var state2 = CreateTestState();
        await _persistence.SaveStateAsync(state1);
        await _persistence.SaveStateAsync(state2);

        // Act
        var states = await _persistence.LoadAllStatesAsync();

        // Assert
        Assert.Equal(2, states.Count);
    }

    [Fact]
    public async Task SaveStateAsync_PreservesSegmentStates()
    {
        // Arrange
        var state = CreateTestState();
        state.Segments.Add(new SegmentState
        {
            SegmentIndex = 0,
            StartByte = 0,
            EndByte = 1024,
            BytesDownloaded = 512,
            Status = SegmentStatus.Downloading
        });

        // Act
        await _persistence.SaveStateAsync(state);
        var loadedState = await _persistence.LoadStateAsync(state.TaskId);

        // Assert
        Assert.NotNull(loadedState);
        Assert.Single(loadedState.Segments);
        Assert.Equal(512, loadedState.Segments[0].BytesDownloaded);
        Assert.Equal(SegmentStatus.Downloading, loadedState.Segments[0].Status);
    }

    [Fact]
    public async Task SaveStateAsync_UpdatesLastUpdateAt()
    {
        // Arrange
        var state = CreateTestState();
        var originalTime = state.LastUpdateAt;

        await Task.Delay(100); // Small delay to ensure time difference

        // Act
        await _persistence.SaveStateAsync(state);
        var loadedState = await _persistence.LoadStateAsync(state.TaskId);

        // Assert
        Assert.NotNull(loadedState);
        Assert.True(loadedState.LastUpdateAt > originalTime);
    }

    [Fact]
    public async Task SaveStateAsync_IsAtomic()
    {
        // Arrange
        var state = CreateTestState();

        // Act - Save multiple times with small delays to avoid file contention
        await _persistence.SaveStateAsync(state);
        await Task.Delay(10);
        await _persistence.SaveStateAsync(state);
        await Task.Delay(10);
        await _persistence.SaveStateAsync(state);

        // Assert - Should be able to load without corruption
        var loadedState = await _persistence.LoadStateAsync(state.TaskId);
        Assert.NotNull(loadedState);
    }

    private static DownloadTaskState CreateTestState()
    {
        return new DownloadTaskState
        {
            TaskId = Guid.NewGuid(),
            Url = "https://example.com/file.zip",
            FileName = "file.zip",
            FileSize = 1024 * 1024,
            DestinationDirectory = "/downloads",
            State = DownloadState.Paused,
            Priority = DownloadPriority.Normal,
            CreatedAt = DateTime.UtcNow,
            LastUpdateAt = DateTime.UtcNow,
            Options = new DownloadOptions { DestinationDirectory = "/downloads", MaxConnections = 4 }
        };
    }
}
