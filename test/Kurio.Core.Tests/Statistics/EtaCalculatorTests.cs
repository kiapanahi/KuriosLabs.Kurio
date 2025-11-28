using FluentAssertions;

using Kurio.Core.Statistics;

namespace Kurio.Core.Tests.Statistics;

public class EtaCalculatorTests
{
    [Fact]
    public void Constructor_WithNullSpeedCalculator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new EtaCalculator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetEtaFromCurrentSpeed_WithZeroBytesRemaining_ReturnsZero()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetEtaFromCurrentSpeed(0);

        // Assert
        eta.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetEtaFromCurrentSpeed_WithNegativeBytesRemaining_ReturnsZero()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetEtaFromCurrentSpeed(-100);

        // Assert
        eta.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetEtaFromCurrentSpeed_WithZeroSpeed_ReturnsNull()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetEtaFromCurrentSpeed(1000);

        // Assert
        eta.Should().BeNull();
    }

    [Fact]
    public void GetEtaFromCurrentSpeed_CalculatesCorrectly()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        var baseTime = DateTime.UtcNow;
        speedCalculator.RecordSample(0, baseTime);
        speedCalculator.RecordSample(1000, baseTime.AddSeconds(1)); // 1000 B/s

        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetEtaFromCurrentSpeed(5000); // 5000 bytes at 1000 B/s = 5 seconds

        // Assert
        eta.Should().NotBeNull();
        eta!.Value.TotalSeconds.Should().BeApproximately(5, 0.1);
    }

    [Fact]
    public void GetEtaFromAverageSpeed_WithZeroBytesRemaining_ReturnsZero()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetEtaFromAverageSpeed(0);

        // Assert
        eta.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void GetEtaFromAverageSpeed_WithZeroAverageSpeed_ReturnsNull()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        EtaCalculator etaCalculator = new(speedCalculator);

        // Only one sample - no average calculated yet
        speedCalculator.RecordSample(1000, DateTime.UtcNow);

        // Act
        var eta = etaCalculator.GetEtaFromAverageSpeed(1000);

        // Assert
        eta.Should().BeNull();
    }

    [Fact]
    public void GetEtaFromAverageSpeed_CalculatesCorrectly()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        var baseTime = DateTime.UtcNow;
        speedCalculator.RecordSample(0, baseTime);
        speedCalculator.RecordSample(2000, baseTime.AddSeconds(2)); // 1000 B/s average

        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetEtaFromAverageSpeed(3000); // 3000 bytes at 1000 B/s = 3 seconds

        // Assert
        eta.Should().NotBeNull();
        eta!.Value.TotalSeconds.Should().BeApproximately(3, 0.1);
    }

    [Fact]
    public void GetBestEta_PreferAverageSpeedWhenAvailable()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        var baseTime = DateTime.UtcNow;
        speedCalculator.RecordSample(0, baseTime);
        speedCalculator.RecordSample(1000, baseTime.AddSeconds(1)); // Current: 1000 B/s
        speedCalculator.RecordSample(2000, baseTime.AddSeconds(2)); // Current: 1000 B/s, Avg: 1000 B/s

        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetBestEta(2000);

        // Assert
        eta.Should().NotBeNull();
        eta!.Value.TotalSeconds.Should().BeApproximately(2, 0.1);
    }

    [Fact]
    public void GetBestEta_FallsBackToCurrentSpeedWhenAverageNotAvailable()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        var baseTime = DateTime.UtcNow;
        speedCalculator.RecordSample(0, baseTime);
        speedCalculator.RecordSample(1000, baseTime.AddSeconds(1)); // Only current speed available

        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetBestEta(2000); // 2000 bytes at 1000 B/s = 2 seconds

        // Assert
        eta.Should().NotBeNull();
        eta!.Value.TotalSeconds.Should().BeApproximately(2, 0.1);
    }

    [Fact]
    public void GetBestEta_WithZeroBytesRemaining_ReturnsZero()
    {
        // Arrange
        SpeedCalculator speedCalculator = new();
        EtaCalculator etaCalculator = new(speedCalculator);

        // Act
        var eta = etaCalculator.GetBestEta(0);

        // Assert
        eta.Should().Be(TimeSpan.Zero);
    }
}
