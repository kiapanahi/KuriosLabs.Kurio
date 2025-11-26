using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Kurio.Core.Configuration;

/// <summary>
/// Default implementation of IConfigurationService
/// </summary>
public sealed class ConfigurationService : IConfigurationService, IDisposable
{
    private readonly string _configFilePath;
    private readonly ILogger<ConfigurationService> _logger;
    private readonly ConfigurationValidator _validator;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private KurioConfiguration _currentConfiguration;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

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

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var updatedConfig = CloneConfiguration(_currentConfiguration);
            updateAction(updatedConfig);

            var validationResult = _validator.Validate(updatedConfig);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => $"{e.PropertyPath}: {e.Message}"));
                throw new InvalidOperationException($"Configuration validation failed: {errors}");
            }

            await SaveConfigurationAsync(updatedConfig, cancellationToken);
            _currentConfiguration = updatedConfig;
            
            ConfigurationChanged?.Invoke(this, CloneConfiguration(_currentConfiguration));
            _logger.LogInformation("Configuration updated successfully");
        }
        finally
        {
            _lock.Release();
        }
    }

    public ConfigurationValidationResult ValidateConfiguration(KurioConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return _validator.Validate(config);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var defaultConfig = new KurioConfiguration();
            await SaveConfigurationAsync(defaultConfig, cancellationToken);
            _currentConfiguration = defaultConfig;
            
            ConfigurationChanged?.Invoke(this, CloneConfiguration(_currentConfiguration));
            _logger.LogInformation("Configuration reset to defaults");
        }
        finally
        {
            _lock.Release();
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

        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        _logger.LogInformation("Configuration exported to {FilePath}", filePath);
    }

    public async Task ImportConfigurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var config = JsonSerializer.Deserialize<KurioConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize configuration");

        var validationResult = _validator.Validate(config);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => $"{e.PropertyPath}: {e.Message}"));
            throw new InvalidOperationException($"Imported configuration is invalid: {errors}");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await SaveConfigurationAsync(config, cancellationToken);
            _currentConfiguration = config;
            
            ConfigurationChanged?.Invoke(this, CloneConfiguration(_currentConfiguration));
            _logger.LogInformation("Configuration imported from {FilePath}", filePath);
        }
        finally
        {
            _lock.Release();
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
                        _logger.LogInformation("Configuration loaded from {FilePath}", _configFilePath);
                        return config;
                    }
                    
                    _logger.LogWarning("Loaded configuration is invalid, using defaults. Errors: {Errors}",
                        string.Join(", ", validationResult.Errors.Select(e => e.Message)));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration from {FilePath}, using defaults", _configFilePath);
        }

        var defaultConfig = new KurioConfiguration();
        
        try
        {
            SaveConfigurationAsync(defaultConfig, CancellationToken.None).Wait();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save default configuration");
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
        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);
    }

    private static KurioConfiguration CloneConfiguration(KurioConfiguration config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        return JsonSerializer.Deserialize<KurioConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException("Failed to clone configuration");
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}
