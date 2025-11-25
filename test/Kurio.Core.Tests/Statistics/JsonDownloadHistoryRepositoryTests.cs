namespace Kurio.Core.Tests.Statistics;

using FluentAssertions;
using Kurio.Core.Models;
using Kurio.Core.Statistics;
using Microsoft.Extensions.Logging;
using Moq;

public class JsonDownloadHistoryRepositoryTests
{
    private readonly Mock<ILogger<JsonDownloadHistoryRepository>> _mockLogger;
    private readonly string _testDirectory;

    public JsonDownloadHistoryRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<JsonDownloadHistoryRepository>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"kurio_history_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    private JsonDownloadHistoryRepository CreateRepository()
    {
        return new JsonDownloadHistoryRepository(_testDirectory, _mockLogger.Object);
    }

    private static DownloadHistoryEntry CreateEntry(bool isSuccessful = true, string fileName = "test.txt")
    {
        return new DownloadHistoryEntry
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/file.txt",
            FileName = fileName,
            FileSize = 1000,
            DestinationDirectory = "/downloads",
            IsSuccessful = isSuccessful,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task AddAsync_AddsEntry()
    {
        // Arrange
        var repo = CreateRepository();
        var entry = CreateEntry();

        // Act
        await repo.AddAsync(entry);
        var result = await repo.GetByIdAsync(entry.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(entry.Id);
        result.FileName.Should().Be(entry.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNull()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var result = await repo.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntriesOrderedByDate()
    {
        // Arrange
        var repo = CreateRepository();
        var entry1 = CreateEntry();
        entry1.CompletedAt = DateTime.UtcNow.AddHours(-2);
        var entry2 = CreateEntry();
        entry2.CompletedAt = DateTime.UtcNow.AddHours(-1);
        var entry3 = CreateEntry();
        entry3.CompletedAt = DateTime.UtcNow;

        await repo.AddAsync(entry1);
        await repo.AddAsync(entry2);
        await repo.AddAsync(entry3);

        // Act
        var results = await repo.GetAllAsync();

        // Assert
        results.Should().HaveCount(3);
        results[0].Id.Should().Be(entry3.Id); // Most recent first
        results[2].Id.Should().Be(entry1.Id); // Oldest last
    }

    [Fact]
    public async Task GetCompletedAsync_ReturnsOnlySuccessful()
    {
        // Arrange
        var repo = CreateRepository();
        var successful = CreateEntry(isSuccessful: true);
        var failed = CreateEntry(isSuccessful: false);

        await repo.AddAsync(successful);
        await repo.AddAsync(failed);

        // Act
        var results = await repo.GetCompletedAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(successful.Id);
    }

    [Fact]
    public async Task GetFailedAsync_ReturnsOnlyFailed()
    {
        // Arrange
        var repo = CreateRepository();
        var successful = CreateEntry(isSuccessful: true);
        var failed = CreateEntry(isSuccessful: false);

        await repo.AddAsync(successful);
        await repo.AddAsync(failed);

        // Act
        var results = await repo.GetFailedAsync();

        // Assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(failed.Id);
    }

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsEntriesInRange()
    {
        // Arrange
        var repo = CreateRepository();
        var entry1 = CreateEntry();
        entry1.CompletedAt = DateTime.UtcNow.AddDays(-5);
        var entry2 = CreateEntry();
        entry2.CompletedAt = DateTime.UtcNow.AddDays(-2);
        var entry3 = CreateEntry();
        entry3.CompletedAt = DateTime.UtcNow;

        await repo.AddAsync(entry1);
        await repo.AddAsync(entry2);
        await repo.AddAsync(entry3);

        // Act
        var results = await repo.GetByDateRangeAsync(
            DateTime.UtcNow.AddDays(-3),
            DateTime.UtcNow);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.Id == entry2.Id);
        results.Should().Contain(e => e.Id == entry3.Id);
    }

    [Fact]
    public async Task SearchAsync_FindsByFileName()
    {
        // Arrange
        var repo = CreateRepository();
        var entry1 = CreateEntry(fileName: "document.pdf");
        var entry2 = CreateEntry(fileName: "image.jpg");
        var entry3 = CreateEntry(fileName: "my_document.pdf");

        await repo.AddAsync(entry1);
        await repo.AddAsync(entry2);
        await repo.AddAsync(entry3);

        // Act
        var results = await repo.SearchAsync("document");

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.FileName == "document.pdf");
        results.Should().Contain(e => e.FileName == "my_document.pdf");
    }

    [Fact]
    public async Task SearchAsync_FindsByUrl()
    {
        // Arrange
        var repo = CreateRepository();
        var entry = new DownloadHistoryEntry
        {
            Id = Guid.NewGuid(),
            Url = "https://github.com/file.zip",
            FileName = "file.zip",
            FileSize = 1000,
            DestinationDirectory = "/downloads",
            IsSuccessful = true,
            CreatedAt = DateTime.UtcNow
        };

        await repo.AddAsync(entry);

        // Act
        var results = await repo.SearchAsync("github");

        // Assert
        results.Should().HaveCount(1);
        results[0].Url.Should().Contain("github");
    }

    [Fact]
    public async Task DeleteAsync_RemovesEntry()
    {
        // Arrange
        var repo = CreateRepository();
        var entry = CreateEntry();
        await repo.AddAsync(entry);

        // Act
        var deleted = await repo.DeleteAsync(entry.Id);
        var result = await repo.GetByIdAsync(entry.Id);

        // Assert
        deleted.Should().BeTrue();
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_ReturnsFalse()
    {
        // Arrange
        var repo = CreateRepository();

        // Act
        var deleted = await repo.DeleteAsync(Guid.NewGuid());

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task ClearAllAsync_RemovesAllEntries()
    {
        // Arrange
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntry());
        await repo.AddAsync(CreateEntry());
        await repo.AddAsync(CreateEntry());

        // Act
        await repo.ClearAllAsync();
        var count = await repo.GetCountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var repo = CreateRepository();
        await repo.AddAsync(CreateEntry());
        await repo.AddAsync(CreateEntry());

        // Act
        var count = await repo.GetCountAsync();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task CleanupOldEntriesAsync_RemovesOldEntries()
    {
        // Arrange
        var repo = CreateRepository();
        var oldEntry = CreateEntry();
        oldEntry.CompletedAt = DateTime.UtcNow.AddDays(-10);
        var recentEntry = CreateEntry();
        recentEntry.CompletedAt = DateTime.UtcNow;

        await repo.AddAsync(oldEntry);
        await repo.AddAsync(recentEntry);

        // Act
        var deletedCount = await repo.CleanupOldEntriesAsync(TimeSpan.FromDays(5));
        var remaining = await repo.GetAllAsync();

        // Assert
        deletedCount.Should().Be(1);
        remaining.Should().HaveCount(1);
        remaining[0].Id.Should().Be(recentEntry.Id);
    }

    [Fact]
    public async Task Persistence_DataSurviresReload()
    {
        // Arrange
        var entry = CreateEntry();

        // Act - Save with first repository
        var repo1 = CreateRepository();
        await repo1.AddAsync(entry);

        // Create new repository instance to simulate reload
        var repo2 = CreateRepository();
        var result = await repo2.GetByIdAsync(entry.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(entry.Id);
        result.FileName.Should().Be(entry.FileName);
    }
}
