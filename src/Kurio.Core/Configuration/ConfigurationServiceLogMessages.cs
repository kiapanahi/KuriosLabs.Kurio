using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Configuration;

internal static partial class ConfigurationServiceLogMessages
{
    [LoggerMessage(
        EventId = 7000,
        Level = LogLevel.Information,
        Message = "Configuration loaded from {FilePath}")]
    public static partial void LogConfigurationLoaded(
        this ILogger logger,
        string filePath);

    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "Loaded configuration is invalid, using defaults. Errors: {Errors}")]
    public static partial void LogConfigurationInvalid(
        this ILogger logger,
        string errors);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Error,
        Message = "Failed to load configuration from {FilePath}, using defaults")]
    public static partial void LogConfigurationLoadFailed(
        this ILogger logger,
        Exception exception,
        string filePath);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Error,
        Message = "Failed to save default configuration")]
    public static partial void LogDefaultConfigurationSaveFailed(
        this ILogger logger,
        Exception exception);

    [LoggerMessage(
        EventId = 7004,
        Level = LogLevel.Information,
        Message = "Configuration updated successfully")]
    public static partial void LogConfigurationUpdated(
        this ILogger logger);

    [LoggerMessage(
        EventId = 7005,
        Level = LogLevel.Information,
        Message = "Configuration reset to defaults")]
    public static partial void LogConfigurationReset(
        this ILogger logger);

    [LoggerMessage(
        EventId = 7006,
        Level = LogLevel.Information,
        Message = "Configuration exported to {FilePath}")]
    public static partial void LogConfigurationExported(
        this ILogger logger,
        string filePath);

    [LoggerMessage(
        EventId = 7007,
        Level = LogLevel.Information,
        Message = "Configuration imported from {FilePath}")]
    public static partial void LogConfigurationImported(
        this ILogger logger,
        string filePath);
}
