using System.Text.Json;
using System.Text.Json.Serialization;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace Kurio.Core.Statistics;

/// <summary>
/// Implements download history storage using JSON files.
/// </summary>
public sealed class JsonDownloadHistoryRepository : IDownloadHistoryRepository
{
    private readonly string _historyFilePath;
    private readonly ILogger<JsonDownloadHistoryRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private List<DownloadHistoryEntry> _entries = [];
    private bool _isLoaded;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDownloadHistoryRepository"/> class.
    /// </summary>
    /// <param name="historyDirectory">The directory where history files are stored.</param>
    /// <param name="logger">Logger instance.</param>
    public JsonDownloadHistoryRepository(string historyDirectory, ILogger<JsonDownloadHistoryRepository> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historyDirectory);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _historyFilePath = Path.Combine(historyDirectory, "download_history.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureDirectoryExists(historyDirectory);
    }

    /// <inheritdoc />
    public async Task AddAsync(DownloadHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _entries.Add(entry);
            await SaveAsync(cancellationToken);
            _logger.LogDebug("Added history entry for download {Id}: {FileName}", entry.Id, entry.FileName);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DownloadHistoryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries.Find(e => e.Id == id);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadHistoryEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries.OrderByDescending(e => e.CompletedAt ?? e.CreatedAt).ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadHistoryEntry>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries
                .Where(e => (e.CompletedAt ?? e.CreatedAt) >= from && (e.CompletedAt ?? e.CreatedAt) <= to)
                .OrderByDescending(e => e.CompletedAt ?? e.CreatedAt)
                .ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadHistoryEntry>> GetCompletedAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries
                .Where(e => e.IsSuccessful)
                .OrderByDescending(e => e.CompletedAt)
                .ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadHistoryEntry>> GetFailedAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries
                .Where(e => !e.IsSuccessful)
                .OrderByDescending(e => e.CompletedAt ?? e.CreatedAt)
                .ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DownloadHistoryEntry>> SearchAsync(
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync(cancellationToken);
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var term = searchTerm.ToLowerInvariant();
            return _entries
                .Where(e => e.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                           e.Url.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.CompletedAt ?? e.CreatedAt)
                .ToList();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupOldEntriesAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - retentionPeriod;

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var originalCount = _entries.Count;
            _entries = _entries.Where(e => (e.CompletedAt ?? e.CreatedAt) >= cutoff).ToList();
            var deletedCount = originalCount - _entries.Count;

            if (deletedCount > 0)
            {
                await SaveAsync(cancellationToken);
                _logger.LogInformation("Cleaned up {Count} old history entries older than {Cutoff}", deletedCount, cutoff);
            }

            return deletedCount;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var entry = _entries.Find(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }

            _entries.Remove(entry);
            await SaveAsync(cancellationToken);
            _logger.LogDebug("Deleted history entry {Id}", id);
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _entries.Clear();
            await SaveAsync(cancellationToken);
            _logger.LogInformation("Cleared all download history");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _entries.Count;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        if (!File.Exists(_historyFilePath))
        {
            _entries = [];
            _isLoaded = true;
            return;
        }

        try
        {
            await using var fileStream = File.OpenRead(_historyFilePath);
            var entries = await JsonSerializer.DeserializeAsync<List<DownloadHistoryEntry>>(
                fileStream, _jsonOptions, cancellationToken);
            _entries = entries ?? [];
            _isLoaded = true;
            _logger.LogDebug("Loaded {Count} history entries from {Path}", _entries.Count, _historyFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history from {Path}, starting fresh", _historyFilePath);
            _entries = [];
            _isLoaded = true;

            // Backup corrupted file
            try
            {
                var backupPath = $"{_historyFilePath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(_historyFilePath, backupPath);
                _logger.LogWarning("Moved corrupted history file to {BackupPath}", backupPath);
            }
            catch (Exception moveEx)
            {
                _logger.LogError(moveEx, "Failed to backup corrupted history file");
            }
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tempFilePath = $"{_historyFilePath}.tmp";
            await using var fileStream = File.Create(tempFilePath);
            await JsonSerializer.SerializeAsync(fileStream, _entries, _jsonOptions, cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();

            File.Move(tempFilePath, _historyFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save history to {Path}", _historyFilePath);
            throw;
        }
    }

    private void EnsureDirectoryExists(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created history directory at {Directory}", directory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create history directory at {Directory}", directory);
            throw;
        }
    }
}
