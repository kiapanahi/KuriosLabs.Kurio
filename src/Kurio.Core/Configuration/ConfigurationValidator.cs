namespace Kurio.Core.Configuration;

/// <summary>
/// Validates configuration settings
/// </summary>
public sealed class ConfigurationValidator
{
    /// <summary>
    /// Validates the entire configuration object
    /// </summary>
    public ConfigurationValidationResult Validate(KurioConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = new List<ConfigurationError>();

        ValidateDownloadSettings(config.Downloads, errors);
        ValidateNetworkSettings(config.Network, errors);
        ValidateStorageSettings(config.Storage, errors);
        ValidateLoggingSettings(config.Logging, errors);

        return errors.Count == 0
            ? ConfigurationValidationResult.Success()
            : new ConfigurationValidationResult { Errors = errors };
    }

    private static void ValidateDownloadSettings(DownloadSettings settings, List<ConfigurationError> errors)
    {
        if (settings.MaxConcurrentDownloads < 1 || settings.MaxConcurrentDownloads > 20)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.MaxConcurrentDownloads",
                Message = "Must be between 1 and 20",
                CurrentValue = settings.MaxConcurrentDownloads,
                ExpectedConstraint = "1 ≤ x ≤ 20"
            });
        }

        if (settings.MaxConnectionsPerDownload < 1 || settings.MaxConnectionsPerDownload > 32)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.MaxConnectionsPerDownload",
                Message = "Must be between 1 and 32",
                CurrentValue = settings.MaxConnectionsPerDownload,
                ExpectedConstraint = "1 ≤ x ≤ 32"
            });
        }

        const long minSegmentSize = 512 * 1024; // 512 KB
        const long maxSegmentSize = 100 * 1024 * 1024; // 100 MB
        if (settings.MinSegmentSize < minSegmentSize || settings.MinSegmentSize > maxSegmentSize)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.MinSegmentSize",
                Message = "Must be between 512 KB and 100 MB",
                CurrentValue = settings.MinSegmentSize,
                ExpectedConstraint = $"{minSegmentSize} ≤ x ≤ {maxSegmentSize}"
            });
        }

        const int minBufferSize = 4 * 1024; // 4 KB
        const int maxBufferSize = 1024 * 1024; // 1 MB
        if (settings.SegmentBufferSize < minBufferSize || settings.SegmentBufferSize > maxBufferSize)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.SegmentBufferSize",
                Message = "Must be between 4 KB and 1 MB",
                CurrentValue = settings.SegmentBufferSize,
                ExpectedConstraint = $"{minBufferSize} ≤ x ≤ {maxBufferSize}"
            });
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultDirectory))
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.DefaultDirectory",
                Message = "Cannot be empty",
                CurrentValue = settings.DefaultDirectory,
                ExpectedConstraint = "Non-empty path"
            });
        }

        if (string.IsNullOrWhiteSpace(settings.TempDirectory))
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.TempDirectory",
                Message = "Cannot be empty",
                CurrentValue = settings.TempDirectory,
                ExpectedConstraint = "Non-empty path"
            });
        }

        var validPolicies = new[] { "overwrite", "appendNumber", "appendTimestamp", "failIfExists", "skipIfExists" };
        if (!validPolicies.Contains(settings.FileNamingPolicy.ToLowerInvariant()))
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Downloads.FileNamingPolicy",
                Message = "Invalid file naming policy",
                CurrentValue = settings.FileNamingPolicy,
                ExpectedConstraint = string.Join(", ", validPolicies)
            });
        }
    }

    private static void ValidateNetworkSettings(NetworkSettings settings, List<ConfigurationError> errors)
    {
        if (settings.TimeoutSeconds < 5 || settings.TimeoutSeconds > 300)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Network.TimeoutSeconds",
                Message = "Must be between 5 and 300",
                CurrentValue = settings.TimeoutSeconds,
                ExpectedConstraint = "5 ≤ x ≤ 300"
            });
        }

        if (settings.MaxRedirects < 0 || settings.MaxRedirects > 10)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Network.MaxRedirects",
                Message = "Must be between 0 and 10",
                CurrentValue = settings.MaxRedirects,
                ExpectedConstraint = "0 ≤ x ≤ 10"
            });
        }

        ValidateRetryPolicy(settings.RetryPolicy, errors);
    }

    private static void ValidateRetryPolicy(RetryPolicySettings settings, List<ConfigurationError> errors)
    {
        if (settings.MaxRetries < 0 || settings.MaxRetries > 10)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Network.RetryPolicy.MaxRetries",
                Message = "Must be between 0 and 10",
                CurrentValue = settings.MaxRetries,
                ExpectedConstraint = "0 ≤ x ≤ 10"
            });
        }

        if (settings.InitialDelaySeconds < 0.5 || settings.InitialDelaySeconds > 60)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Network.RetryPolicy.InitialDelaySeconds",
                Message = "Must be between 0.5 and 60",
                CurrentValue = settings.InitialDelaySeconds,
                ExpectedConstraint = "0.5 ≤ x ≤ 60"
            });
        }

        if (settings.MaxDelaySeconds < 1 || settings.MaxDelaySeconds > 300)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Network.RetryPolicy.MaxDelaySeconds",
                Message = "Must be between 1 and 300",
                CurrentValue = settings.MaxDelaySeconds,
                ExpectedConstraint = "1 ≤ x ≤ 300"
            });
        }

        if (settings.BackoffMultiplier < 1.0 || settings.BackoffMultiplier > 5.0)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Network.RetryPolicy.BackoffMultiplier",
                Message = "Must be between 1.0 and 5.0",
                CurrentValue = settings.BackoffMultiplier,
                ExpectedConstraint = "1.0 ≤ x ≤ 5.0"
            });
        }
    }

    private static void ValidateStorageSettings(StorageSettings settings, List<ConfigurationError> errors)
    {
        const long minFreeSpace = 10 * 1024 * 1024; // 10 MB
        if (settings.MinimumFreeSpace < minFreeSpace)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Storage.MinimumFreeSpace",
                Message = "Must be at least 10 MB",
                CurrentValue = settings.MinimumFreeSpace,
                ExpectedConstraint = $">= {minFreeSpace}"
            });
        }
    }

    private static void ValidateLoggingSettings(LoggingSettings settings, List<ConfigurationError> errors)
    {
        var validLevels = new[] { "trace", "debug", "information", "warning", "error", "critical" };
        if (!validLevels.Contains(settings.Level.ToLowerInvariant()))
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Logging.Level",
                Message = "Invalid logging level",
                CurrentValue = settings.Level,
                ExpectedConstraint = string.Join(", ", validLevels)
            });
        }

        if (settings.MaxLogFiles < 1 || settings.MaxLogFiles > 100)
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Logging.MaxLogFiles",
                Message = "Must be between 1 and 100",
                CurrentValue = settings.MaxLogFiles,
                ExpectedConstraint = "1 ≤ x ≤ 100"
            });
        }

        if (string.IsNullOrWhiteSpace(settings.LogDirectory))
        {
            errors.Add(new ConfigurationError
            {
                PropertyPath = "Logging.LogDirectory",
                Message = "Cannot be empty",
                CurrentValue = settings.LogDirectory,
                ExpectedConstraint = "Non-empty path"
            });
        }
    }
}
