using FluentAssertions;

using Kurio.Core.Models;
using Kurio.Core.Statistics;

namespace Kurio.Core.Tests.Statistics;

public class ProgressTrackerTests
{
    [Fact]
    public void StartTracking_CreatesTrackingState()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();

        // Act
        tracker.StartTracking(taskId, 10000);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().NotBeNull();
        progress!.TaskId.Should().Be(taskId);
        progress.TotalBytes.Should().Be(10000);
        progress.BytesDownloaded.Should().Be(0);
    }

    [Fact]
    public void GetProgress_ForUnknownTask_ReturnsNull()
    {
        // Arrange
        using ProgressTracker tracker = new();

        // Act
        var progress = tracker.GetProgress(Guid.NewGuid());

        // Assert
        progress.Should().BeNull();
    }

    [Fact]
    public void RecordProgress_UpdatesBytesDownloaded()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 10000);

        // Act
        tracker.RecordProgress(taskId, 5000);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().NotBeNull();
        progress!.BytesDownloaded.Should().Be(5000);
        progress.Percentage.Should().BeApproximately(50, 0.1);
    }

    [Fact]
    public void RecordProgress_UpdatesSegmentProgress()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 10000);

        List<SegmentProgressInfo> segments = new()
        {
            new SegmentProgressInfo
            {
                SegmentIndex = 0,
                StartByte = 0,
                EndByte = 4999,
                BytesDownloaded = 2500,
                IsActive = true
            },
            new SegmentProgressInfo
            {
                SegmentIndex = 1,
                StartByte = 5000,
                EndByte = 9999,
                BytesDownloaded = 2500,
                IsActive = true
            }
        };

        // Act
        tracker.RecordProgress(taskId, 5000, segments);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().NotBeNull();
        progress!.SegmentProgress.Should().HaveCount(2);
        progress.ActiveConnections.Should().Be(2);
    }

    [Fact]
    public void StopTracking_RemovesTrackingState()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 10000);

        // Act
        tracker.StopTracking(taskId);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().BeNull();
    }

    [Fact]
    public void Pause_PausesSpeedCalculation()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 10000);
        tracker.RecordProgress(taskId, 1000);

        // Act
        tracker.Pause(taskId);
        Thread.Sleep(50);
        tracker.Resume(taskId);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().NotBeNull();
        progress!.TotalPausedTime.TotalMilliseconds.Should().BeGreaterOrEqualTo(40);
    }

    [Fact]
    public void RecordProgress_ForUnknownTask_DoesNotThrow()
    {
        // Arrange
        using ProgressTracker tracker = new();

        // Act & Assert
        var act = () => tracker.RecordProgress(Guid.NewGuid(), 1000);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task GetProgressUpdates_EmitsProgressForCorrectTask()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId1 = Guid.NewGuid();
        var taskId2 = Guid.NewGuid();
        tracker.StartTracking(taskId1, 10000);
        tracker.StartTracking(taskId2, 10000);

        List<EnhancedDownloadProgress> receivedProgress = new();
        using CancellationTokenSource cts = new();

        // Start streaming progress in background
        var streamTask = Task.Run(async () =>
        {
            await foreach (var progress in tracker.StreamProgressAsync(taskId1, cts.Token))
            {
                receivedProgress.Add(progress);
                if (receivedProgress.Count >= 2)
                {
                    cts.Cancel();
                }
            }
        });

        // Act
        await Task.Delay(50); // Let stream start
        tracker.RecordProgress(taskId1, 1000);
        tracker.RecordProgress(taskId2, 2000);
        tracker.RecordProgress(taskId1, 3000);

        // Wait for stream to collect progress
        await Task.WhenAny(streamTask, Task.Delay(500));
        cts.Cancel();

        // Assert
        receivedProgress.Should().HaveCount(2);
        receivedProgress.Should().OnlyContain(p => p.TaskId == taskId1);
    }

    [Fact]
    public async Task AllProgressUpdates_EmitsProgressForAllTasks()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId1 = Guid.NewGuid();
        var taskId2 = Guid.NewGuid();
        tracker.StartTracking(taskId1, 10000);
        tracker.StartTracking(taskId2, 10000);

        List<EnhancedDownloadProgress> receivedProgress = new();
        using CancellationTokenSource cts = new();

        // Start streaming all progress in background
        var streamTask = Task.Run(async () =>
        {
            await foreach (var progress in tracker.StreamProgressAsync(null, cts.Token))
            {
                receivedProgress.Add(progress);
                if (receivedProgress.Count >= 2)
                {
                    cts.Cancel();
                }
            }
        });

        // Act
        await Task.Delay(50); // Let stream start
        tracker.RecordProgress(taskId1, 1000);
        tracker.RecordProgress(taskId2, 2000);

        // Wait for stream to collect progress
        await Task.WhenAny(streamTask, Task.Delay(500));
        cts.Cancel();

        // Assert
        receivedProgress.Should().HaveCount(2);
        receivedProgress.Should().Contain(p => p.TaskId == taskId1);
        receivedProgress.Should().Contain(p => p.TaskId == taskId2);
    }

    [Fact]
    public void Progress_CalculatesPercentageCorrectly()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 1000);

        // Act
        tracker.RecordProgress(taskId, 250);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().NotBeNull();
        progress!.Percentage.Should().BeApproximately(25, 0.1);
    }

    [Fact]
    public void Progress_CalculatesElapsedActiveTime()
    {
        // Arrange
        using ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 10000);

        // Act
        Thread.Sleep(100); // Let some time pass
        tracker.RecordProgress(taskId, 1000);
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().NotBeNull();
        progress!.ElapsedActiveTime.TotalMilliseconds.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public void Dispose_ClearsAllTracking()
    {
        // Arrange
        ProgressTracker tracker = new();
        var taskId = Guid.NewGuid();
        tracker.StartTracking(taskId, 10000);

        // Act
        tracker.Dispose();
        var progress = tracker.GetProgress(taskId);

        // Assert
        progress.Should().BeNull();
    }
}
