namespace Kurio.Core.Tests.Models;

using Kurio.Core.Models;

using Xunit;

public class ByteRangeTests
{
    [Fact]
    public void Constructor_ShouldSetStartAndEnd()
    {
        // Arrange & Act
        var range = new ByteRange(0, 99);

        // Assert
        Assert.Equal(0, range.Start);
        Assert.Equal(99, range.End);
    }

    [Fact]
    public void Length_ShouldCalculateCorrectly()
    {
        // Arrange
        var range = new ByteRange(0, 99);

        // Act
        var length = range.Length;

        // Assert
        Assert.Equal(100, length);
    }

    [Fact]
    public void FromLength_ShouldCreateCorrectRange()
    {
        // Arrange & Act
        var range = ByteRange.FromLength(100, 50);

        // Assert
        Assert.Equal(100, range.Start);
        Assert.Equal(149, range.End);
        Assert.Equal(50, range.Length);
    }

    [Fact]
    public void ToString_ShouldReturnHttpRangeFormat()
    {
        // Arrange
        var range = new ByteRange(0, 99);

        // Act
        var result = range.ToString();

        // Assert
        Assert.Equal("bytes=0-99", result);
    }
}
