using System.Text.Json;
using System.Text.Json.Serialization;
using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using Microsoft.Extensions.Logging;

namespace Kurio.Core.Persistence;

/// <summary>
/// Implements state persistence using JSON files.
/// </summary>
public sealed class JsonStatePersistence : IStatePersistence
{
    private readonly ILogger<JsonStatePersistence> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStatePersistence"/> class.
    /// </summary>
    /// <param name="stateDirectory">The directory where state files are stored.</param>
    /// <param name="logger">Logger instance.</param>
    public JsonStatePersistence(string stateDirectory, ILogger<JsonStatePersistence> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateDirectory);

        StateDirectory = stateDirectory;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureStateDirectoryExists();
    }

    /// <inheritdoc />
    public string StateDirectory { get; }

    /// <inheritdoc />
    public async Task SaveStateAsync(DownloadTaskState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var filePath = GetStateFilePath(state.TaskId);
        state.LastUpdateAt = DateTime.UtcNow;

        try
        {
            // Write to a temporary file first for atomic operation
            var tempFilePath = $"{filePath}.tmp";
            await using var fileStream = File.Create(tempFilePath);
            await JsonSerializer.SerializeAsync(fileStream, state, _jsonOptions, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();

            // Atomic move
            File.Move(tempFilePath, filePath, overwrite: true);

            _logger.LogDebug("Saved state for task {TaskId} to {FilePath}", state.TaskId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save state for task {TaskId}", state.TaskId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DownloadTaskState?> LoadStateAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var filePath = GetStateFilePath(taskId);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("State file not found for task {TaskId}", taskId);
            return null;
        }

        try
        {
            await using var fileStream = File.OpenRead(filePath);
            var state = await JsonSerializer.DeserializeAsync<DownloadTaskState>(fileStream, _jsonOptions, cancellationToken);

            _logger.LogDebug("Loaded state for task {TaskId} from {FilePath}", taskId, filePath);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load state for task {TaskId} from {FilePath}", taskId, filePath);

            // Move corrupted file to backup
            try
            {
                var backupPath = $"{filePath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(filePath, backupPath);
                _logger.LogWarning("Moved corrupted state file to {BackupPath}", backupPath);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "Failed to backup corrupted state file");
            }

            return null;
        }
    }

    /// <inheritdoc />
    public async Task DeleteStateAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var filePath = GetStateFilePath(taskId);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug("State file not found for task {TaskId}, nothing to delete", taskId);
            return;
        }

        try
        {
            await Task.Run(() => File.Delete(filePath), cancellationToken);
            _logger.LogDebug("Deleted state for task {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete state for task {TaskId}", taskId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadTaskState>> LoadAllStatesAsync(CancellationToken cancellationToken = default)
    {
        var states = new List<DownloadTaskState>();

        try
        {
            var stateFiles = Directory.GetFiles(StateDirectory, "*.json");
            _logger.LogInformation("Found {Count} state files in {Directory}", stateFiles.Length, StateDirectory);

            foreach (var filePath in stateFiles)
            {
                try
                {
                    await using var fileStream = File.OpenRead(filePath);
                    var state = await JsonSerializer.DeserializeAsync<DownloadTaskState>(fileStream, _jsonOptions, cancellationToken);

                    if (state != null)
                    {
                        states.Add(state);
                        _logger.LogDebug("Loaded state from {FilePath}", filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load state from {FilePath}, skipping", filePath);
                }
            }

            _logger.LogInformation("Successfully loaded {Count} download states", states.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load states from directory {Directory}", StateDirectory);
        }

        return states;
    }

    private string GetStateFilePath(Guid taskId)
    {
        return Path.Combine(StateDirectory, $"{taskId}.json");
    }

    private void EnsureStateDirectoryExists()
    {
        try
        {
            if (!Directory.Exists(StateDirectory))
            {
                Directory.CreateDirectory(StateDirectory);
                _logger.LogInformation("Created state directory at {Directory}", StateDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create state directory at {Directory}", StateDirectory);
            throw;
        }
    }
}
