using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Avalonia.Services;

public record AddDownloadRequest(
    string Url,
    string? SavePath = null,
    int Segments = 8,
    bool StartImmediately = true,
    DownloadPriority Priority = DownloadPriority.Normal);

public record DownloadResponse(
    Guid Id,
    string Url,
    string? FileName,
    DownloadState State,
    long? TotalBytes,
    long DownloadedBytes,
    double? Speed,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorMessage);

public record DownloadProgressDto(
    Guid DownloadId,
    long DownloadedBytes,
    long? TotalBytes,
    double Speed,
    double ProgressPercentage,
    DownloadState State);

public record QueueStatistics(
    int TotalDownloads,
    int ActiveDownloads,
    int CompletedDownloads,
    int FailedDownloads,
    long TotalBytesDownloaded,
    double AverageSpeed);

public record UpdateSpeedLimitRequest(
    bool Enabled,
    long MaxDownloadSpeedBytesPerSecond,
    long MaxUploadSpeedBytesPerSecond);

public record SpeedLimitResponse(
    bool Enabled,
    long MaxDownloadSpeedBytesPerSecond,
    long MaxUploadSpeedBytesPerSecond,
    long CurrentLimitBytesPerSecond);

public enum DownloadStateFilter
{
    All,
    Active,
    Completed,
    Failed,
    Paused
}
