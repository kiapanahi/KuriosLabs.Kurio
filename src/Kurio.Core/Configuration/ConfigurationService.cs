using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Configuration;

/// <summary>
///     Default implementation of IConfigurationService
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _configFilePath;
    private readonly Lock _lock = new();
    private readonly ILogger<ConfigurationService> _logger;
    private readonly ConfigurationValidator _validator;
    private KurioConfiguration _currentConfiguration;

    public ConfigurationService(
        string configFilePath,
        ILogger<ConfigurationService> logger)
    {
        _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validator = new ConfigurationValidator();

        _currentConfiguration = LoadOrCreateConfiguration();
    }

    public event EventHandler<KurioConfiguration>? ConfigurationChanged;

    public KurioConfiguration GetConfiguration()
    {
        return CloneConfiguration(_currentConfiguration);
    }

    public async Task UpdateConfigurationAsync(
        Action<KurioConfiguration> updateAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updateAction);

        lock (_lock)
        {
            var updatedConfig = CloneConfiguration(_currentConfiguration);
            updateAction(updatedConfig);

            var validationResult = _validator.Validate(updatedConfig);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ",
                    validationResult.Errors.Select(e => $"{e.PropertyPath}: {e.Message}"));
                throw new InvalidOperationException($"Configuration validation failed: {errors}");
            }

            SaveConfigurationAsync(updatedConfig, cancellationToken).Wait(cancellationToken);
            _currentConfiguration = updatedConfig;

            ConfigurationChanged?.Invoke(this, CloneConfiguration(_currentConfiguration));
            _logger.LogConfigurationUpdated();
        }
    }

    public ConfigurationValidationResult ValidateConfiguration(KurioConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return _validator.Validate(config);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            KurioConfiguration defaultConfig = new();
            SaveConfigurationAsync(defaultConfig, cancellationToken).Wait(cancellationToken);
            _currentConfiguration = defaultConfig;

            ConfigurationChanged?.Invoke(this, CloneConfiguration(_currentConfiguration));
            _logger.LogConfigurationReset();
        }
    }

    public async Task ExportConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var config = GetConfiguration();
        var json = JsonSerializer.Serialize(config, JsonOptions);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
        _logger.LogConfigurationExported(filePath);
    }

    public async Task ImportConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var config = JsonSerializer.Deserialize<KurioConfiguration>(json, JsonOptions)
                     ?? throw new InvalidOperationException("Failed to deserialize configuration");

        var validationResult = _validator.Validate(config);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => $"{e.PropertyPath}: {e.Message}"));
            throw new InvalidOperationException($"Imported configuration is invalid: {errors}");
        }

        lock (_lock)
        {
            SaveConfigurationAsync(config, cancellationToken).Wait(cancellationToken);
            _currentConfiguration = config;

            ConfigurationChanged?.Invoke(this, CloneConfiguration(_currentConfiguration));
            _logger.LogConfigurationImported(filePath);
        }
    }

    private KurioConfiguration LoadOrCreateConfiguration()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<KurioConfiguration>(json, JsonOptions);

                if (config != null)
                {
                    var validationResult = _validator.Validate(config);
                    if (validationResult.IsValid)
                    {
                        _logger.LogConfigurationLoaded(_configFilePath);
                        return config;
                    }

                    _logger.LogConfigurationInvalid(
                        string.Join(", ", validationResult.Errors.Select(e => e.Message)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogConfigurationLoadFailed(ex, _configFilePath);
        }

        KurioConfiguration defaultConfig = new();

        try
        {
            SaveConfigurationAsync(defaultConfig, CancellationToken.None).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogDefaultConfigurationSaveFailed(ex);
        }

        return defaultConfig;
    }

    private async Task SaveConfigurationAsync(KurioConfiguration config, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken).ConfigureAwait(false);
    }

    private static KurioConfiguration CloneConfiguration(KurioConfiguration config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        return JsonSerializer.Deserialize<KurioConfiguration>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to clone configuration");
    }
}
