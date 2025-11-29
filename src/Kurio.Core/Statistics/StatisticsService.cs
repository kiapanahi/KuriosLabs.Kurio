using System.Text.Json;
using System.Text.Json.Serialization;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging;

namespace Kurio.Core.Statistics;

/// <summary>
///     Provides download statistics calculation and persistence.
/// </summary>
public sealed class StatisticsService : IStatisticsService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly IDownloadHistoryRepository _historyRepository;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<StatisticsService> _logger;
    private readonly DateTime _sessionStartTime;
    private readonly string _statisticsFilePath;

    // Session counters (in-memory only)
    private long _sessionBytesDownloaded;
    private int _sessionCompletedDownloads;
    private int _sessionFailedDownloads;
    private DownloadStatistics? _statistics;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StatisticsService" /> class.
    /// </summary>
    /// <param name="statisticsDirectory">The directory where statistics are stored.</param>
    /// <param name="historyRepository">The download history repository.</param>
    /// <param name="logger">Logger instance.</param>
    public StatisticsService(
        string statisticsDirectory,
        IDownloadHistoryRepository historyRepository,
        ILogger<StatisticsService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statisticsDirectory);

        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statisticsFilePath = Path.Combine(statisticsDirectory, "statistics.json");
        _sessionStartTime = DateTime.UtcNow;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        EnsureDirectoryExists(statisticsDirectory);
    }

    /// <inheritdoc />
    public async Task<DownloadStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            // Update session statistics
            _statistics!.SessionBytesDownloaded = _sessionBytesDownloaded;
            _statistics.SessionCompletedDownloads = _sessionCompletedDownloads;
            _statistics.SessionFailedDownloads = _sessionFailedDownloads;
            _statistics.SessionStartedAt = _sessionStartTime;
            _statistics.LastUpdatedAt = DateTime.UtcNow;

            return _statistics;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RecordCompletedDownloadAsync(DownloadHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            // Update session counters
            _sessionBytesDownloaded += entry.BytesDownloaded;
            _sessionCompletedDownloads++;

            // Update all-time statistics
            _statistics!.AllTimeBytesDownloaded += entry.BytesDownloaded;
            _statistics.AllTimeCompletedDownloads++;

            // Update peak speed
            if (entry.PeakSpeed > _statistics.PeakDownloadSpeed)
            {
                _statistics.PeakDownloadSpeed = entry.PeakSpeed;
            }

            // Update active download time
            if (entry.ActiveDuration.HasValue)
            {
                _statistics.TotalActiveDownloadTime += entry.ActiveDuration.Value;
            }

            // Update file type counts
            UpdateFileTypeCounts(entry.FileExtension);

            // Update download time statistics
            if (entry.CompletedAt.HasValue)
            {
                UpdateDownloadsByHour(entry.CompletedAt.Value.Hour);
            }

            // Recalculate average speed
            await RecalculateAverageSpeedAsync(cancellationToken).ConfigureAwait(false);

            _statistics.LastUpdatedAt = DateTime.UtcNow;
            await SaveStatisticsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogCompletedDownloadRecorded(entry.FileName);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task RecordFailedDownloadAsync(DownloadHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

            // Update session counters
            _sessionFailedDownloads++;

            // Update all-time statistics
            _statistics!.AllTimeFailedDownloads++;
            _statistics.LastUpdatedAt = DateTime.UtcNow;

            await SaveStatisticsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogFailedDownloadRecorded(entry.FileName);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetSessionStatisticsAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _sessionBytesDownloaded = 0;
            _sessionCompletedDownloads = 0;
            _sessionFailedDownloads = 0;

            _logger.LogSessionStatisticsReset();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, object>> ExportStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);

        return new Dictionary<string, object>
        {
            ["sessionBytesDownloaded"] = stats.SessionBytesDownloaded,
            ["allTimeBytesDownloaded"] = stats.AllTimeBytesDownloaded,
            ["sessionCompletedDownloads"] = stats.SessionCompletedDownloads,
            ["sessionFailedDownloads"] = stats.SessionFailedDownloads,
            ["allTimeCompletedDownloads"] = stats.AllTimeCompletedDownloads,
            ["allTimeFailedDownloads"] = stats.AllTimeFailedDownloads,
            ["averageDownloadSpeed"] = stats.AverageDownloadSpeed,
            ["peakDownloadSpeed"] = stats.PeakDownloadSpeed,
            ["totalActiveDownloadTimeSeconds"] = stats.TotalActiveDownloadTime.TotalSeconds,
            ["fileTypeCounts"] = stats.FileTypeCounts,
            ["downloadsByHour"] = stats.DownloadsByHour,
            ["lastUpdatedAt"] = stats.LastUpdatedAt.ToString("O"),
            ["sessionStartedAt"] = stats.SessionStartedAt.ToString("O")
        };
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_statistics != null)
        {
            return;
        }

        if (!File.Exists(_statisticsFilePath))
        {
            _statistics = new DownloadStatistics { CreatedAt = DateTime.UtcNow, SessionStartedAt = _sessionStartTime };
            return;
        }

        try
        {
            await using var fileStream = File.OpenRead(_statisticsFilePath);
            _statistics = await JsonSerializer.DeserializeAsync<DownloadStatistics>(
                fileStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            _statistics ??=
                new DownloadStatistics { CreatedAt = DateTime.UtcNow, SessionStartedAt = _sessionStartTime };
            _logger.LogStatisticsLoaded(_statisticsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogStatisticsLoadFailed(ex, _statisticsFilePath);
            _statistics = new DownloadStatistics { CreatedAt = DateTime.UtcNow, SessionStartedAt = _sessionStartTime };
        }
    }

    private async Task SaveStatisticsAsync(CancellationToken cancellationToken)
    {
        if (_statistics == null)
        {
            return;
        }

        try
        {
            var tempFilePath = $"{_statisticsFilePath}.tmp";
            await using var fileStream = File.Create(tempFilePath);
            await JsonSerializer.SerializeAsync(fileStream, _statistics, _jsonOptions, cancellationToken).ConfigureAwait(false);
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            fileStream.Close();

            File.Move(tempFilePath, _statisticsFilePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogStatisticsSaveFailed(ex, _statisticsFilePath);
            throw;
        }
    }

    private void UpdateFileTypeCounts(string fileExtension)
    {
        if (string.IsNullOrEmpty(fileExtension))
        {
            return;
        }

        Dictionary<string, int> counts = new(_statistics!.FileTypeCounts);
        counts.TryGetValue(fileExtension, out var count);
        counts[fileExtension] = count + 1;
        _statistics.FileTypeCounts = counts;
    }

    private void UpdateDownloadsByHour(int hour)
    {
        Dictionary<int, int> hourCounts = new(_statistics!.DownloadsByHour);
        hourCounts.TryGetValue(hour, out var count);
        hourCounts[hour] = count + 1;
        _statistics.DownloadsByHour = hourCounts;
    }

    private async Task RecalculateAverageSpeedAsync(CancellationToken cancellationToken)
    {
        var completedDownloads =
            await _historyRepository.GetCompletedAsync(cancellationToken).ConfigureAwait(false);
        if (completedDownloads.Count == 0)
        {
            return;
        }

        var totalSpeed = completedDownloads.Sum(d => d.AverageSpeed);
        _statistics!.AverageDownloadSpeed = totalSpeed / completedDownloads.Count;
    }

    private void EnsureDirectoryExists(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogStatisticsDirectoryCreated(directory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogStatisticsDirectoryCreationFailed(ex, directory);
            throw;
        }
    }
}
