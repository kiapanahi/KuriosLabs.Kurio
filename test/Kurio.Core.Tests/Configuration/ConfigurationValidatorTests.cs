using FluentAssertions;
using Kurio.Core.Configuration;

namespace Kurio.Core.Tests.Configuration;

public sealed class ConfigurationValidatorTests
{
    private readonly ConfigurationValidator _validator = new();

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsSuccess()
    {
        var config = CreateValidConfiguration();

        var result = _validator.Validate(config);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidMaxConcurrentDownloads_ReturnsError()
    {
        var config = CreateValidConfiguration();
        config.Downloads.MaxConcurrentDownloads = 0;

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].PropertyPath.Should().Be("Downloads.MaxConcurrentDownloads");
        result.Errors[0].Message.Should().Contain("between 1 and 20");
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        var config = CreateValidConfiguration();
        config.Downloads.MaxConcurrentDownloads = 0;
        config.Network.TimeoutSeconds = 1000;
        config.Storage.MinimumFreeSpace = 100;

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(3);
        result.Errors.Should().Contain(e => e.PropertyPath == "Downloads.MaxConcurrentDownloads");
        result.Errors.Should().Contain(e => e.PropertyPath == "Network.TimeoutSeconds");
        result.Errors.Should().Contain(e => e.PropertyPath == "Storage.MinimumFreeSpace");
    }

    [Fact]
    public void Validate_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Action act = () => _validator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_ErrorsContainCurrentValue()
    {
        var config = CreateValidConfiguration();
        config.Downloads.MaxConcurrentDownloads = 100;

        var result = _validator.Validate(config);

        result.Errors.Should().ContainSingle();
        result.Errors[0].CurrentValue.Should().Be(100);
    }

    [Fact]
    public void Validate_WithInvalidFileNamingPolicy_ReturnsError()
    {
        var config = CreateValidConfiguration();
        config.Downloads.FileNamingPolicy = "invalid";

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].PropertyPath.Should().Be("Downloads.FileNamingPolicy");
    }

    [Fact]
    public void Validate_WithEmptyDirectories_ReturnsErrors()
    {
        var config = CreateValidConfiguration();
        config.Downloads.DefaultDirectory = "";
        config.Downloads.TempDirectory = "";

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.PropertyPath == "Downloads.DefaultDirectory");
        result.Errors.Should().Contain(e => e.PropertyPath == "Downloads.TempDirectory");
    }

    [Fact]
    public void Validate_WithInvalidLogLevel_ReturnsError()
    {
        var config = CreateValidConfiguration();
        config.Logging.Level = "InvalidLevel";

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].PropertyPath.Should().Be("Logging.Level");
    }

    [Fact]
    public void Validate_WithInvalidRetryPolicy_ReturnsError()
    {
        var config = CreateValidConfiguration();
        config.Network.RetryPolicy.MaxRetries = 100;

        var result = _validator.Validate(config);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].PropertyPath.Should().Be("Network.RetryPolicy.MaxRetries");
    }

    private static KurioConfiguration CreateValidConfiguration()
    {
        return new KurioConfiguration
        {
            Downloads = new DownloadSettings
            {
                MaxConcurrentDownloads = 5,
                MaxConnectionsPerDownload = 8,
                MinSegmentSize = 1024 * 1024,
                SegmentBufferSize = 8192,
                DefaultDirectory = "~/Downloads",
                TempDirectory = "~/.kurio/temp",
                FileNamingPolicy = "appendNumber"
            },
            Network = new NetworkSettings
            {
                TimeoutSeconds = 30,
                MaxRedirects = 5,
                RetryPolicy = new RetryPolicySettings
                {
                    MaxRetries = 3,
                    InitialDelaySeconds = 1.0,
                    MaxDelaySeconds = 60.0,
                    BackoffMultiplier = 2.0
                }
            },
            Storage = new StorageSettings
            {
                MinimumFreeSpace = 100 * 1024 * 1024
            },
            Logging = new LoggingSettings
            {
                Level = "Information",
                MaxLogFiles = 10,
                LogDirectory = "~/.kurio/logs"
            }
        };
    }
}
