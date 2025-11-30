using FluentValidation.TestHelper;

using Kurio.Core.Configuration;
using Kurio.Core.Configuration.Validators;

namespace Kurio.Core.Tests.Configuration;

public sealed class KurioConfigurationValidatorTests
{
    private readonly KurioConfigurationValidator _validator = new();

    [Fact]
    public void Validate_WhenAllSettingsValid_ShouldNotHaveErrors()
    {
        var config = new KurioConfiguration
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

        var result = _validator.TestValidate(config);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenDownloadsInvalid_ShouldHaveErrors()
    {
        var config = new KurioConfiguration
        {
            Downloads = new DownloadSettings
            {
                MaxConcurrentDownloads = 0 // Invalid
            }
        };

        var result = _validator.TestValidate(config);
        result.ShouldHaveValidationErrorFor("Downloads.MaxConcurrentDownloads");
    }

    [Fact]
    public void Validate_WhenNetworkInvalid_ShouldHaveErrors()
    {
        var config = new KurioConfiguration
        {
            Network = new NetworkSettings
            {
                TimeoutSeconds = 1 // Invalid
            }
        };

        var result = _validator.TestValidate(config);
        result.ShouldHaveValidationErrorFor("Network.TimeoutSeconds");
    }

    [Fact]
    public void Validate_WhenStorageInvalid_ShouldHaveErrors()
    {
        var config = new KurioConfiguration
        {
            Storage = new StorageSettings
            {
                MinimumFreeSpace = 100 // Too small
            }
        };

        var result = _validator.TestValidate(config);
        result.ShouldHaveValidationErrorFor("Storage.MinimumFreeSpace");
    }

    [Fact]
    public void Validate_WhenLoggingInvalid_ShouldHaveErrors()
    {
        var config = new KurioConfiguration
        {
            Logging = new LoggingSettings
            {
                Level = "invalid" // Invalid
            }
        };

        var result = _validator.TestValidate(config);
        result.ShouldHaveValidationErrorFor("Logging.Level");
    }

    [Fact]
    public void Validate_WhenMultipleSettingsInvalid_ShouldHaveMultipleErrors()
    {
        var config = new KurioConfiguration
        {
            Downloads = new DownloadSettings
            {
                MaxConcurrentDownloads = 0,
                DefaultDirectory = "" // Both invalid
            },
            Network = new NetworkSettings
            {
                TimeoutSeconds = 1000 // Invalid
            }
        };

        var result = _validator.TestValidate(config);
        result.ShouldHaveValidationErrorFor("Downloads.MaxConcurrentDownloads");
        result.ShouldHaveValidationErrorFor("Downloads.DefaultDirectory");
        result.ShouldHaveValidationErrorFor("Network.TimeoutSeconds");
    }
}
