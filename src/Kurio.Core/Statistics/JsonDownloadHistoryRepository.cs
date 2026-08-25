using System.Text.Json;
using System.Text.Json.Serialization;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Core.Statistics;

/// <summary>
///     Implements download history storage using JSON files.
/// </summary>
public sealed class JsonDownloadHistoryRepository : IDownloadHistoryRepository
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly string _historyFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<JsonDownloadHistoryRepository> _logger;
    private List<DownloadHistoryEntry> _entries = [];
    private bool _isLoaded;

    /// <summary>
    ///     Initializes a new instance of the <see cref="JsonDownloadHistoryRepository" /> class.
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

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            _entries.Add(entry);
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogHistoryEntryAdded(entry.Id, entry.FileName);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DownloadHistoryEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
    public async Task<IReadOnlyList<DownloadHistoryEntry>> GetCompletedAsync(
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
            return await GetAllAsync(cancellationToken).ConfigureAwait(false);
        }

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
    public async Task<int> CleanupOldEntriesAsync(TimeSpan retentionPeriod,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - retentionPeriod;

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var originalCount = _entries.Count;
            _entries = _entries.Where(e => (e.CompletedAt ?? e.CreatedAt) >= cutoff).ToList();
            var deletedCount = originalCount - _entries.Count;

            if (deletedCount > 0)
            {
                await SaveAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogOldEntriesCleanedUp(deletedCount, cutoff);
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
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var entry = _entries.Find(e => e.Id == id);
            if (entry == null)
            {
                return false;
            }

            _entries.Remove(entry);
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogHistoryEntryDeleted(id);
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
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _entries.Clear();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogAllHistoryCleared();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
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
            var fileStream = File.OpenRead(_historyFilePath);
            await using (fileStream.ConfigureAwait(false))
            {
                var entries = await JsonSerializer.DeserializeAsync<List<DownloadHistoryEntry>>(
                    fileStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
                _entries = entries ?? [];
                _isLoaded = true;
                _logger.LogHistoryEntriesLoaded(_entries.Count, _historyFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogHistoryLoadFailed(ex, _historyFilePath);
            _entries = [];
            _isLoaded = true;

            // Backup corrupted file
            try
            {
                var backupPath = $"{_historyFilePath}.corrupted.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(_historyFilePath, backupPath);
                _logger.LogCorruptedHistoryFileBackedUp(backupPath);
            }
            catch (Exception moveEx)
            {
                _logger.LogHistoryBackupFailed(moveEx);
            }
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tempFilePath = $"{_historyFilePath}.tmp";
            var fileStream = File.Create(tempFilePath);
            await using (fileStream.ConfigureAwait(false))
            {
                await JsonSerializer.SerializeAsync(fileStream, _entries, _jsonOptions, cancellationToken).ConfigureAwait(false);
                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                fileStream.Close();

                File.Move(tempFilePath, _historyFilePath, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogHistorySaveFailed(ex, _historyFilePath);
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
                _logger.LogHistoryDirectoryCreated(directory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogHistoryDirectoryCreationFailed(ex, directory);
            throw;
        }
    }
}
