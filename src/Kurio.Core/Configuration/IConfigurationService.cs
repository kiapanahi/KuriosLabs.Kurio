namespace Kurio.Core.Configuration;

/// <summary>
/// Service for managing application configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current configuration snapshot
    /// </summary>
    KurioConfiguration GetConfiguration();

    /// <summary>
    /// Updates configuration settings and persists changes
    /// </summary>
    /// <param name="updateAction">Action to modify configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateConfigurationAsync(
        Action<KurioConfiguration> updateAction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration object
    /// </summary>
    /// <param name="config">Configuration to validate</param>
    /// <returns>Validation result with any errors</returns>
    ConfigurationValidationResult ValidateConfiguration(KurioConfiguration config);

    /// <summary>
    /// Resets configuration to default values
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports configuration to a file
    /// </summary>
    /// <param name="filePath">Destination file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExportConfigurationAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports configuration from a file
    /// </summary>
    /// <param name="filePath">Source file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ImportConfigurationAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when configuration changes
    /// </summary>
    event EventHandler<KurioConfiguration>? ConfigurationChanged;
}

/// <summary>
/// Result of configuration validation
/// </summary>
public sealed class ConfigurationValidationResult
{
    /// <summary>
    /// Whether the configuration is valid
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<ConfigurationError> Errors { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    public static ConfigurationValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with errors
    /// </summary>
    public static ConfigurationValidationResult Failure(params ConfigurationError[] errors)
    {
        return new ConfigurationValidationResult { Errors = [.. errors] };
    }
}

/// <summary>
/// Represents a configuration validation error
/// </summary>
public sealed class ConfigurationError
{
    /// <summary>
    /// Path to the configuration property (e.g., "Downloads.MaxConcurrentDownloads")
    /// </summary>
    public string PropertyPath { get; init; } = string.Empty;

    /// <summary>
    /// Error message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Current value that failed validation
    /// </summary>
    public object? CurrentValue { get; init; }

    /// <summary>
    /// Expected value or constraint
    /// </summary>
    public string? ExpectedConstraint { get; init; }
}
