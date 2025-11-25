namespace Kurio.Core.Tests.Statistics;

using FluentAssertions;
using Kurio.Core.Statistics;

public class SpeedCalculatorTests
{
    [Fact]
    public void Constructor_WithInvalidWindowSize_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => new SpeedCalculator(0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithValidWindowSize_CreatesInstance()
    {
        // Act
        var calculator = new SpeedCalculator(5);

        // Assert
        calculator.CurrentSpeed.Should().Be(0);
        calculator.AverageSpeed.Should().Be(0);
        calculator.PeakSpeed.Should().Be(0);
    }

    [Fact]
    public void RecordSample_FirstSample_DoesNotCalculateSpeed()
    {
        // Arrange
        var calculator = new SpeedCalculator();

        // Act
        calculator.RecordSample(1000, DateTime.UtcNow);

        // Assert
        calculator.CurrentSpeed.Should().Be(0);
        calculator.AverageSpeed.Should().Be(0);
    }

    [Fact]
    public void RecordSample_TwoSamples_CalculatesCurrentSpeed()
    {
        // Arrange
        var calculator = new SpeedCalculator();
        var time1 = DateTime.UtcNow;
        var time2 = time1.AddSeconds(1);

        // Act
        calculator.RecordSample(0, time1);
        calculator.RecordSample(1000, time2); // 1000 bytes in 1 second = 1000 B/s

        // Assert
        calculator.CurrentSpeed.Should().Be(1000);
    }

    [Fact]
    public void RecordSample_MultipleSamples_CalculatesRollingAverage()
    {
        // Arrange
        var calculator = new SpeedCalculator(3);
        var baseTime = DateTime.UtcNow;

        // Act
        calculator.RecordSample(0, baseTime);
        calculator.RecordSample(1000, baseTime.AddSeconds(1)); // 1000 B/s
        calculator.RecordSample(3000, baseTime.AddSeconds(2)); // 2000 B/s current, 1500 B/s average

        // Assert
        calculator.CurrentSpeed.Should().Be(2000);
        calculator.AverageSpeed.Should().Be(1500); // 3000 bytes over 2 seconds
    }

    [Fact]
    public void RecordSample_UpdatesPeakSpeed()
    {
        // Arrange
        var calculator = new SpeedCalculator();
        var baseTime = DateTime.UtcNow;

        // Act
        calculator.RecordSample(0, baseTime);
        calculator.RecordSample(5000, baseTime.AddSeconds(1)); // 5000 B/s - peak
        calculator.RecordSample(6000, baseTime.AddSeconds(2)); // 1000 B/s

        // Assert
        calculator.PeakSpeed.Should().Be(5000);
    }

    [Fact]
    public void Pause_PreventsSampleRecording()
    {
        // Arrange
        var calculator = new SpeedCalculator();
        var baseTime = DateTime.UtcNow;
        calculator.RecordSample(0, baseTime);
        calculator.RecordSample(1000, baseTime.AddSeconds(1));
        var initialSpeed = calculator.CurrentSpeed;

        // Act
        calculator.Pause();
        calculator.RecordSample(5000, baseTime.AddSeconds(2)); // Should be ignored

        // Assert
        calculator.CurrentSpeed.Should().Be(initialSpeed);
    }

    [Fact]
    public void Resume_AllowsSampleRecordingAgain()
    {
        // Arrange
        var calculator = new SpeedCalculator();
        var baseTime = DateTime.UtcNow;
        calculator.RecordSample(0, baseTime);
        calculator.Pause();
        calculator.Resume();

        // Act
        calculator.RecordSample(1000, baseTime.AddSeconds(1));

        // Assert
        calculator.CurrentSpeed.Should().Be(1000);
    }

    [Fact]
    public void PauseAndResume_TracksPausedDuration()
    {
        // Arrange
        var calculator = new SpeedCalculator();

        // Act
        calculator.Pause();
        Thread.Sleep(50); // Wait 50ms
        calculator.Resume();

        // Assert
        calculator.TotalPausedDurationMs.Should().BeGreaterOrEqualTo(40); // Account for timing variability
    }

    [Fact]
    public void Reset_ClearsAllValues()
    {
        // Arrange
        var calculator = new SpeedCalculator();
        var baseTime = DateTime.UtcNow;
        calculator.RecordSample(0, baseTime);
        calculator.RecordSample(1000, baseTime.AddSeconds(1));
        calculator.Pause();
        calculator.Resume();

        // Act
        calculator.Reset();

        // Assert
        calculator.CurrentSpeed.Should().Be(0);
        calculator.AverageSpeed.Should().Be(0);
        calculator.PeakSpeed.Should().Be(0);
        calculator.TotalPausedDurationMs.Should().Be(0);
    }

    [Fact]
    public void RecordSample_MaintainsWindowSize()
    {
        // Arrange
        var calculator = new SpeedCalculator(3);
        var baseTime = DateTime.UtcNow;

        // Act - record 5 samples, window size is 3
        calculator.RecordSample(0, baseTime);
        calculator.RecordSample(1000, baseTime.AddSeconds(1));
        calculator.RecordSample(2000, baseTime.AddSeconds(2));
        calculator.RecordSample(3000, baseTime.AddSeconds(3));
        calculator.RecordSample(4000, baseTime.AddSeconds(4));

        // Assert - average should be based on last 3 samples (2000->4000 over 2 seconds)
        calculator.AverageSpeed.Should().Be(1000); // 2000 bytes over 2 seconds
    }
}
