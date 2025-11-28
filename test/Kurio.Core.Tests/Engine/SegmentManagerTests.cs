using Kurio.Core.Abstractions;
using Kurio.Core.Engine;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

using Moq;

namespace Kurio.Core.Tests.Engine;

public class SegmentManagerTests
{
    private readonly Mock<ILogger<SegmentManager>> _mockLogger;
    private readonly Mock<ISegmentVerifier> _mockSegmentVerifier;
    private readonly Mock<IStorageManager> _mockStorageManager;
    private readonly SegmentManager _segmentManager;

    public SegmentManagerTests()
    {
        _mockStorageManager = new Mock<IStorageManager>();
        _mockSegmentVerifier = new Mock<ISegmentVerifier>();
        _mockLogger = new Mock<ILogger<SegmentManager>>();
        _segmentManager =
            new SegmentManager(_mockStorageManager.Object, _mockSegmentVerifier.Object, _mockLogger.Object);
    }

    [Fact]
    public void CalculateSegments_WithSmallFile_ShouldReturnSingleSegment()
    {
        // Arrange
        var fileSize = 512 * 1024; // 512 KB
        SegmentOptions options = new()
        {
            MaxConnections = 8, MinSegmentSize = 1024 * 1024 // 1 MB
        };

        // Act
        var config = _segmentManager.CalculateSegments(fileSize, true, options);

        // Assert
        Assert.Equal(1, config.SegmentCount);
        Assert.Equal(fileSize, config.FileSize);
        Assert.Single(config.Ranges);
        Assert.Equal(0, config.Ranges[0].Start);
        Assert.Equal(fileSize - 1, config.Ranges[0].End);
    }

    [Fact]
    public void CalculateSegments_WithLargeFile_ShouldCreateMultipleSegments()
    {
        // Arrange
        var fileSize = 10 * 1024 * 1024L; // 10 MB
        SegmentOptions options = new()
        {
            MaxConnections = 4, MinSegmentSize = 1024 * 1024 // 1 MB
        };

        // Act
        var config = _segmentManager.CalculateSegments(fileSize, true, options);

        // Assert
        Assert.Equal(4, config.SegmentCount);
        Assert.Equal(4, config.Ranges.Length);
        Assert.True(config.SupportsRanges);

        // Verify segment boundaries
        Assert.Equal(0, config.Ranges[0].Start);
        Assert.Equal(fileSize - 1, config.Ranges[^1].End);

        // Verify no gaps between segments
        for (var i = 0; i < config.Ranges.Length - 1; i++)
        {
            Assert.Equal(config.Ranges[i].End + 1, config.Ranges[i + 1].Start);
        }
    }

    [Fact]
    public void CalculateSegments_WithoutRangeSupport_ShouldReturnSingleSegment()
    {
        // Arrange
        var fileSize = 10 * 1024 * 1024L; // 10 MB
        SegmentOptions options = new() { MaxConnections = 8, MinSegmentSize = 1024 * 1024 };

        // Act
        var config = _segmentManager.CalculateSegments(fileSize, false, options);

        // Assert
        Assert.Equal(1, config.SegmentCount);
        Assert.False(config.SupportsRanges);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateSegments_WithInvalidFileSize_ShouldThrowArgumentException(long fileSize)
    {
        // Arrange
        SegmentOptions options = new();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _segmentManager.CalculateSegments(fileSize, true, options));
    }

    [Fact]
    public void CalculateSegments_ShouldLimitToMaxConnections()
    {
        // Arrange
        var fileSize = 100 * 1024 * 1024L; // 100 MB
        SegmentOptions options = new()
        {
            MaxConnections = 8, MinSegmentSize = 1024 * 1024 // 1 MB (would allow 100 segments)
        };

        // Act
        var config = _segmentManager.CalculateSegments(fileSize, true, options);

        // Assert
        Assert.Equal(8, config.SegmentCount); // Limited by MaxConnections
    }
}
