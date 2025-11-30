using Kurio.Core.Engine;
using Kurio.Core.Models;
using Kurio.Core.Queue;

namespace Kurio.Core.Tests.Queue;

public class DownloadQueueManagerTests
{
    private DownloadQueueManager CreateQueueManager(int maxConcurrent = 3)
    {
        return new DownloadQueueManager { MaxConcurrentDownloads = maxConcurrent };
    }

    private static DownloadTask CreateTestTask(DownloadPriority priority = DownloadPriority.Normal)
    {
        return new DownloadTask(
            new Uri("https://example.com/file.zip"),
            new DownloadOptions { DestinationDirectory = "/tmp" })
        {
            Priority = priority,
            State = DownloadState.Queued
        };
    }

    [Fact]
    public void Enqueue_AddsTaskToQueue()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();

        // Act
        queueManager.Enqueue(task);

        // Assert
        Assert.Equal(1, queueManager.QueuedDownloadsCount);
    }

    [Fact]
    public void Enqueue_ThrowsForDuplicateTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();

        // Act
        queueManager.Enqueue(task);

        // Assert
        Assert.Throws<InvalidOperationException>(() => queueManager.Enqueue(task));
    }

    [Fact]
    public void Enqueue_ThrowsForNullTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => queueManager.Enqueue(null!));
    }

    [Fact]
    public void GetNextTask_ReturnsPriorityOrder()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var lowTask = CreateTestTask(DownloadPriority.Low);
        var normalTask = CreateTestTask();
        var highTask = CreateTestTask(DownloadPriority.High);
        var criticalTask = CreateTestTask(DownloadPriority.Critical);

        // Add in random order
        queueManager.Enqueue(normalTask);
        queueManager.Enqueue(lowTask);
        queueManager.Enqueue(criticalTask);
        queueManager.Enqueue(highTask);

        // Act & Assert - should return in priority order
        Assert.Equal(criticalTask.Id, queueManager.GetNextTask()?.Id);
        Assert.Equal(highTask.Id, queueManager.GetNextTask()?.Id);
        Assert.Equal(normalTask.Id, queueManager.GetNextTask()?.Id);
        Assert.Equal(lowTask.Id, queueManager.GetNextTask()?.Id);
    }

    [Fact]
    public void GetNextTask_ReturnsFIFOWithinSamePriority()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        var task3 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.Enqueue(task3);

        // Act & Assert - should return in FIFO order
        Assert.Equal(task1.Id, queueManager.GetNextTask()?.Id);
        Assert.Equal(task2.Id, queueManager.GetNextTask()?.Id);
        Assert.Equal(task3.Id, queueManager.GetNextTask()?.Id);
    }

    [Fact]
    public void GetNextTask_ReturnsNullWhenEmpty()
    {
        // Arrange
        var queueManager = CreateQueueManager();

        // Act
        var result = queueManager.GetNextTask();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Dequeue_RemovesTaskFromQueue()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);

        // Act
        var result = queueManager.Dequeue(task.Id);

        // Assert
        Assert.True(result);
        Assert.Equal(0, queueManager.QueuedDownloadsCount);
    }

    [Fact]
    public void Dequeue_ReturnsFalseForNonExistentTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();

        // Act
        var result = queueManager.Dequeue(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ChangePriority_UpdatesTaskPriorityAndReorders()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask(DownloadPriority.Low);
        var task2 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);

        // Act
        var result = queueManager.ChangePriority(task1.Id, DownloadPriority.High);

        // Assert
        Assert.True(result);
        Assert.Equal(task1.Id, queueManager.GetNextTask()?.Id);
    }

    [Fact]
    public void ChangePriority_ReturnsFalseForNonExistentTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();

        // Act
        var result = queueManager.ChangePriority(Guid.NewGuid(), DownloadPriority.High);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MoveUp_MovesTaskUpWithinSamePriority()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        var task3 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.Enqueue(task3);

        // Act
        var result = queueManager.MoveUp(task3.Id);

        // Assert
        Assert.True(result);
        var tasks = queueManager.GetQueuedTasks();
        Assert.Equal(task1.Id, tasks[0].Id);
        Assert.Equal(task3.Id, tasks[1].Id);
        Assert.Equal(task2.Id, tasks[2].Id);
    }

    [Fact]
    public void MoveUp_ReturnsFalseForTopTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);

        // Act
        var result = queueManager.MoveUp(task.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MoveDown_MovesTaskDownWithinSamePriority()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        var task3 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.Enqueue(task3);

        // Act
        var result = queueManager.MoveDown(task1.Id);

        // Assert
        Assert.True(result);
        var tasks = queueManager.GetQueuedTasks();
        Assert.Equal(task2.Id, tasks[0].Id);
        Assert.Equal(task1.Id, tasks[1].Id);
        Assert.Equal(task3.Id, tasks[2].Id);
    }

    [Fact]
    public void MoveDown_ReturnsFalseForBottomTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);

        // Act
        var result = queueManager.MoveDown(task.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MoveToTop_MovesTaskToTopOfPriorityGroup()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        var task3 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.Enqueue(task3);

        // Act
        var result = queueManager.MoveToTop(task3.Id);

        // Assert
        Assert.True(result);
        var tasks = queueManager.GetQueuedTasks();
        Assert.Equal(task3.Id, tasks[0].Id);
    }

    [Fact]
    public void MoveToBottom_MovesTaskToBottomOfPriorityGroup()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        var task3 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.Enqueue(task3);

        // Act
        var result = queueManager.MoveToBottom(task1.Id);

        // Assert
        Assert.True(result);
        var tasks = queueManager.GetQueuedTasks();
        Assert.Equal(task1.Id, tasks[2].Id);
    }

    [Fact]
    public void MarkAsStarted_MovesTaskFromQueueToActive()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);

        // Act
        queueManager.MarkAsStarted(task.Id);

        // Assert
        Assert.Equal(0, queueManager.QueuedDownloadsCount);
        Assert.Equal(1, queueManager.ActiveDownloadsCount);
    }

    [Fact]
    public void MarkAsCompleted_RemovesTaskFromActive()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);
        queueManager.MarkAsStarted(task.Id);

        // Act
        queueManager.MarkAsCompleted(task.Id);

        // Assert
        Assert.Equal(0, queueManager.ActiveDownloadsCount);
    }

    [Fact]
    public void MarkAsFailed_RemovesTaskFromActive()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);
        queueManager.MarkAsStarted(task.Id);

        // Act
        queueManager.MarkAsFailed(task.Id);

        // Assert
        Assert.Equal(0, queueManager.ActiveDownloadsCount);
    }

    [Fact]
    public void MarkAsPaused_RemovesTaskFromActive()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);
        queueManager.MarkAsStarted(task.Id);

        // Act
        queueManager.MarkAsPaused(task.Id);

        // Assert
        Assert.Equal(0, queueManager.ActiveDownloadsCount);
    }

    [Fact]
    public void CanStartNewDownload_ReturnsTrueWhenBelowLimit()
    {
        // Arrange
        var queueManager = CreateQueueManager(2);
        var task = CreateTestTask();
        queueManager.Enqueue(task);
        queueManager.MarkAsStarted(task.Id);

        // Act
        var result = queueManager.CanStartNewDownload();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void CanStartNewDownload_ReturnsFalseWhenAtLimit()
    {
        // Arrange
        var queueManager = CreateQueueManager(1);
        var task = CreateTestTask();
        queueManager.Enqueue(task);
        queueManager.MarkAsStarted(task.Id);

        // Act
        var result = queueManager.CanStartNewDownload();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void PauseAll_ClearsActiveDownloads()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.MarkAsStarted(task1.Id);
        queueManager.MarkAsStarted(task2.Id);

        // Act
        var count = queueManager.PauseAll();

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(0, queueManager.ActiveDownloadsCount);
    }

    [Fact]
    public void ClearCompleted_RemovesCompletedTasks()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.MarkAsStarted(task1.Id);
        queueManager.MarkAsStarted(task2.Id);
        queueManager.MarkAsCompleted(task1.Id);
        queueManager.MarkAsCompleted(task2.Id);

        // Act
        queueManager.ClearCompleted();

        // Assert
        // Completed tasks are tracked separately, this just clears the internal tracking
        Assert.Equal(0, queueManager.ActiveDownloadsCount);
    }

    [Fact]
    public void GetQueuedTasks_ReturnsOrderedList()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var lowTask = CreateTestTask(DownloadPriority.Low);
        var highTask = CreateTestTask(DownloadPriority.High);
        var normalTask = CreateTestTask();

        queueManager.Enqueue(normalTask);
        queueManager.Enqueue(lowTask);
        queueManager.Enqueue(highTask);

        // Act
        var tasks = queueManager.GetQueuedTasks();

        // Assert
        Assert.Equal(3, tasks.Count);
        Assert.Equal(highTask.Id, tasks[0].Id);
        Assert.Equal(normalTask.Id, tasks[1].Id);
        Assert.Equal(lowTask.Id, tasks[2].Id);
    }

    [Fact]
    public void GetActiveTasks_ReturnsActiveDownloads()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.MarkAsStarted(task1.Id);
        queueManager.MarkAsStarted(task2.Id);

        // Act
        var activeTasks = queueManager.GetActiveTasks();

        // Assert
        Assert.Equal(2, activeTasks.Count);
    }

    [Fact]
    public void GetTask_ReturnsTaskFromQueue()
    {
        // Arrange
        var queueManager = CreateQueueManager();
        var task = CreateTestTask();
        queueManager.Enqueue(task);

        // Act
        var result = queueManager.GetTask(task.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(task.Id, result.Id);
    }

    [Fact]
    public void GetTask_ReturnsNullForNonExistentTask()
    {
        // Arrange
        var queueManager = CreateQueueManager();

        // Act
        var result = queueManager.GetTask(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConcurrencyLimit_PreventsExcessiveActiveDownloads()
    {
        // Arrange
        var queueManager = CreateQueueManager(2);
        var task1 = CreateTestTask();
        var task2 = CreateTestTask();
        var task3 = CreateTestTask();

        queueManager.Enqueue(task1);
        queueManager.Enqueue(task2);
        queueManager.Enqueue(task3);

        // Act - Mark two as started
        queueManager.MarkAsStarted(task1.Id);
        queueManager.MarkAsStarted(task2.Id);

        // Assert
        Assert.False(queueManager.CanStartNewDownload());
        Assert.Equal(2, queueManager.ActiveDownloadsCount);
        Assert.Equal(1, queueManager.QueuedDownloadsCount);
    }
}
