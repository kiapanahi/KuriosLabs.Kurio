namespace Kurio.Core.Tests.Statistics;

using FluentAssertions;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using Kurio.Core.Statistics;
using Microsoft.Extensions.Logging;
using Moq;

public class StatisticsServiceTests
{
    private readonly Mock<IDownloadHistoryRepository> _mockHistoryRepository;
    private readonly Mock<ILogger<StatisticsService>> _mockLogger;
    private readonly string _testDirectory;

    public StatisticsServiceTests()
    {
        _mockHistoryRepository = new Mock<IDownloadHistoryRepository>();
        _mockLogger = new Mock<ILogger<StatisticsService>>();
        _testDirectory = Path.Combine(Path.GetTempPath(), $"kurio_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    private StatisticsService CreateService()
    {
        return new StatisticsService(_testDirectory, _mockHistoryRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsInitialStatistics()
    {
        // Arrange
        var service = CreateService();

        // Act
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        stats.SessionBytesDownloaded.Should().Be(0);
        stats.AllTimeBytesDownloaded.Should().Be(0);
        stats.SessionCompletedDownloads.Should().Be(0);
        stats.AllTimeCompletedDownloads.Should().Be(0);
    }

    [Fact]
    public async Task RecordCompletedDownloadAsync_UpdatesSessionStatistics()
    {
        // Arrange
        var service = CreateService();
        _mockHistoryRepository.Setup(r => r.GetCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadHistoryEntry>());

        var entry = CreateHistoryEntry(true, 1000, 500);

        // Act
        await service.RecordCompletedDownloadAsync(entry);
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.SessionBytesDownloaded.Should().Be(1000);
        stats.SessionCompletedDownloads.Should().Be(1);
        stats.AllTimeBytesDownloaded.Should().Be(1000);
        stats.AllTimeCompletedDownloads.Should().Be(1);
    }

    [Fact]
    public async Task RecordCompletedDownloadAsync_UpdatesPeakSpeed()
    {
        // Arrange
        var service = CreateService();
        _mockHistoryRepository.Setup(r => r.GetCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadHistoryEntry>());

        var entry = CreateHistoryEntry(true, 1000, 500, peakSpeed: 10000);

        // Act
        await service.RecordCompletedDownloadAsync(entry);
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.PeakDownloadSpeed.Should().Be(10000);
    }

    [Fact]
    public async Task RecordFailedDownloadAsync_UpdatesFailedCount()
    {
        // Arrange
        var service = CreateService();
        var entry = CreateHistoryEntry(false, 0, 0);

        // Act
        await service.RecordFailedDownloadAsync(entry);
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.SessionFailedDownloads.Should().Be(1);
        stats.AllTimeFailedDownloads.Should().Be(1);
    }

    [Fact]
    public async Task ResetSessionStatisticsAsync_ClearsSessionCounters()
    {
        // Arrange
        var service = CreateService();
        _mockHistoryRepository.Setup(r => r.GetCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadHistoryEntry>());

        var entry = CreateHistoryEntry(true, 1000, 500);
        await service.RecordCompletedDownloadAsync(entry);

        // Act
        await service.ResetSessionStatisticsAsync();
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.SessionBytesDownloaded.Should().Be(0);
        stats.SessionCompletedDownloads.Should().Be(0);
        stats.AllTimeBytesDownloaded.Should().Be(1000); // All-time should remain
        stats.AllTimeCompletedDownloads.Should().Be(1);
    }

    [Fact]
    public async Task ExportStatisticsAsync_ReturnsDictionary()
    {
        // Arrange
        var service = CreateService();

        // Act
        var exported = await service.ExportStatisticsAsync();

        // Assert
        exported.Should().NotBeNull();
        exported.Should().ContainKey("sessionBytesDownloaded");
        exported.Should().ContainKey("allTimeBytesDownloaded");
        exported.Should().ContainKey("sessionCompletedDownloads");
        exported.Should().ContainKey("allTimeCompletedDownloads");
    }

    [Fact]
    public async Task RecordCompletedDownloadAsync_UpdatesFileTypeCounts()
    {
        // Arrange
        var service = CreateService();
        _mockHistoryRepository.Setup(r => r.GetCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadHistoryEntry>());

        var entry1 = CreateHistoryEntry(true, 1000, 500, fileName: "test.pdf");
        var entry2 = CreateHistoryEntry(true, 2000, 600, fileName: "doc.pdf");
        var entry3 = CreateHistoryEntry(true, 1500, 550, fileName: "image.jpg");

        // Act
        await service.RecordCompletedDownloadAsync(entry1);
        await service.RecordCompletedDownloadAsync(entry2);
        await service.RecordCompletedDownloadAsync(entry3);
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.FileTypeCounts.Should().ContainKey(".pdf");
        stats.FileTypeCounts[".pdf"].Should().Be(2);
        stats.FileTypeCounts.Should().ContainKey(".jpg");
        stats.FileTypeCounts[".jpg"].Should().Be(1);
    }

    [Fact]
    public async Task RecordCompletedDownloadAsync_UpdatesDownloadsByHour()
    {
        // Arrange
        var service = CreateService();
        _mockHistoryRepository.Setup(r => r.GetCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadHistoryEntry>());

        var entry = CreateHistoryEntry(true, 1000, 500);
        entry.CompletedAt = DateTime.UtcNow;

        // Act
        await service.RecordCompletedDownloadAsync(entry);
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.DownloadsByHour.Should().ContainKey(DateTime.UtcNow.Hour);
        stats.DownloadsByHour[DateTime.UtcNow.Hour].Should().Be(1);
    }

    [Fact]
    public async Task RecordCompletedDownloadAsync_UpdatesTotalActiveTime()
    {
        // Arrange
        var service = CreateService();
        _mockHistoryRepository.Setup(r => r.GetCompletedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DownloadHistoryEntry>());

        var entry = CreateHistoryEntry(true, 1000, 500);
        entry.ActiveDuration = TimeSpan.FromMinutes(5);

        // Act
        await service.RecordCompletedDownloadAsync(entry);
        var stats = await service.GetStatisticsAsync();

        // Assert
        stats.TotalActiveDownloadTime.Should().Be(TimeSpan.FromMinutes(5));
    }

    private static DownloadHistoryEntry CreateHistoryEntry(
        bool isSuccessful,
        long bytesDownloaded,
        long averageSpeed,
        long peakSpeed = 0,
        string fileName = "test.txt")
    {
        return new DownloadHistoryEntry
        {
            Id = Guid.NewGuid(),
            Url = "https://example.com/file.txt",
            FileName = fileName,
            FileSize = bytesDownloaded,
            DestinationDirectory = "/downloads",
            BytesDownloaded = bytesDownloaded,
            AverageSpeed = averageSpeed,
            PeakSpeed = peakSpeed,
            IsSuccessful = isSuccessful,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
    }
}
