using System.Text.Json;
using System.Text.Json.Serialization;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Persistence;

/// <summary>
///     Implements state persistence using JSON files.
/// </summary>
public sealed class JsonStatePersistence : IStatePersistence
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<JsonStatePersistence> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonStatePersistence" /> class.
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
            var fileStream = File.Create(tempFilePath);
            await using (fileStream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(fileStream, state, _jsonOptions, cancellationToken).ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                fileStream.Close();

                // Atomic move
                File.Move(tempFilePath, filePath, true);

                _logger.LogStateSaved(state.TaskId, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogStateSaveFailed(ex, state.TaskId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DownloadTaskState?> LoadStateAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var filePath = GetStateFilePath(taskId);

        if (!File.Exists(filePath))
        {
            _logger.LogStateFileNotFound(taskId);
            return null;
        }

        try
        {
            var fileStream = File.OpenRead(filePath);
            await using (fileStream.ConfigureAwait(false))
            {
                var state =
                    await JsonSerializer.DeserializeAsync<DownloadTaskState>(fileStream, _jsonOptions, cancellationToken)
                        .ConfigureAwait(false);

                _logger.LogStateLoaded(taskId, filePath);
                return state;
            }
        }
        catch (Exception ex)
        {
            _logger.LogStateLoadFailed(ex, taskId, filePath);

            // Move corrupted file to backup
            try
            {
                var backupPath = $"{filePath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(filePath, backupPath);
                _logger.LogCorruptedStateFileBackedUp(backupPath);
            }
            catch (Exception moveEx)
            {
                _logger.LogBackupFailed(moveEx);
            }

            return null;
        }
    }

    /// <inheritdoc />
    public Task DeleteStateAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var filePath = GetStateFilePath(taskId);

        if (!File.Exists(filePath))
        {
            _logger.LogStateFileNotFoundForDelete(taskId);
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            File.Delete(filePath);
            _logger.LogStateDeleted(taskId);
        }
        catch (Exception ex)
        {
            _logger.LogStateDeleteFailed(ex, taskId);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadTaskState>> LoadAllStatesAsync(
        CancellationToken cancellationToken = default)
    {
        List<DownloadTaskState> states = new();

        try
        {
            var stateFiles = Directory.GetFiles(StateDirectory, "*.json");
            _logger.LogStateFilesFound(stateFiles.Length, StateDirectory);

            foreach (var filePath in stateFiles)
            {
                try
                {
                    var fileStream = File.OpenRead(filePath);
                    await using (fileStream.ConfigureAwait(false))
                    {
                        var state =
                            await JsonSerializer.DeserializeAsync<DownloadTaskState>(fileStream, _jsonOptions,
                                cancellationToken).ConfigureAwait(false);

                        if (state != null)
                        {
                            states.Add(state);
                            _logger.LogStateLoadedFromFile(filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogStateLoadFailedSkipping(ex, filePath);
                }
            }

            _logger.LogStatesLoaded(states.Count);
        }
        catch (Exception ex)
        {
            _logger.LogStatesLoadFailed(ex, StateDirectory);
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
                _logger.LogStateDirectoryCreated(StateDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogStateDirectoryCreationFailed(ex, StateDirectory);
            throw;
        }
    }
}
