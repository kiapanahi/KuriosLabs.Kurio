using KuriousLabs.Kurio.Contracts.Downloads;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;

namespace KuriousLabs.Kurio.Server.Mappers;

public static class DownloadContractMapper
{
    public static DownloadSummary ToContract(this IDownloadTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new DownloadSummary
        {
            Id = task.Id,
            Name = task.FileName,
            Url = task.Url.ToString(),
            Category = task.Options.Category,
            TotalBytes = task.FileSize > 0 ? task.FileSize : null,
            DownloadedBytes = task.Progress.BytesDownloaded,
            PercentComplete = task.Progress.TotalBytes > 0
                ? task.Progress.Percentage
                : null,
            State = MapState(task.State),
            BytesPerSecond = task.Progress.BytesPerSecond,
            EstimatedTimeRemaining = task.Progress.EstimatedTimeRemaining,
            ActiveConnections = task.Progress.ActiveConnections,
            Priority = MapPriority(task.Priority),
            HasChecksum = task.Options.Checksum is not null
                || !string.IsNullOrWhiteSpace(task.Options.ExpectedChecksum),
            LastError = MapError(task.LastError),
            CreatedAt = task.CreatedAt,
            LastUpdatedAt = task.Progress.Timestamp,
            DestinationPath = task.Options.DestinationDirectory
        };
    }

    public static DownloadProgressUpdate ToProgressUpdate(this DownloadProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new DownloadProgressUpdate
        {
            Id = progress.TaskId,
            DownloadedBytes = progress.BytesDownloaded,
            TotalBytes = progress.TotalBytes > 0 ? progress.TotalBytes : null,
            PercentComplete = progress.TotalBytes > 0
                ? progress.Percentage
                : null,
            BytesPerSecond = progress.BytesPerSecond,
            EstimatedTimeRemaining = progress.EstimatedTimeRemaining,
            ActiveConnections = progress.ActiveConnections,
            Timestamp = progress.Timestamp
        };
    }

    public static KuriousLabs.Kurio.Core.Models.DownloadStateFilter ToCoreFilter(this DownloadStateFilter filter)
    {
        if (filter == DownloadStateFilter.All)
        {
            return KuriousLabs.Kurio.Core.Models.DownloadStateFilter.All;
        }

        if (filter == DownloadStateFilter.None)
        {
            return KuriousLabs.Kurio.Core.Models.DownloadStateFilter.None;
        }

        KuriousLabs.Kurio.Core.Models.DownloadStateFilter mapped = 0;

        if (filter.HasFlag(DownloadStateFilter.Created))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Created;
        }

        if (filter.HasFlag(DownloadStateFilter.Queued))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Queued;
        }

        if (filter.HasFlag(DownloadStateFilter.Analyzing))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Analyzing;
        }

        if (filter.HasFlag(DownloadStateFilter.Downloading))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Downloading;
        }

        if (filter.HasFlag(DownloadStateFilter.Paused))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Paused;
        }

        if (filter.HasFlag(DownloadStateFilter.Completed))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Completed;
        }

        if (filter.HasFlag(DownloadStateFilter.Failed))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Failed;
        }

        if (filter.HasFlag(DownloadStateFilter.Cancelled))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Cancelled;
        }

        if (filter.HasFlag(DownloadStateFilter.Active))
        {
            mapped |= KuriousLabs.Kurio.Core.Models.DownloadStateFilter.Active;
        }

        return mapped;
    }

    private static DownloadFailureInfo? MapError(DownloadError? error)
    {
        if (error is null)
        {
            return null;
        }

        return new DownloadFailureInfo
        {
            Category = MapErrorCategory(error.Category),
            Message = error.UserFriendlyMessage ?? error.Message,
            Details = error.StackTrace,
            StatusCode = error.HttpStatusCode,
            RetryCount = null,
            Source = error.ExceptionType,
            OccurredAt = error.Timestamp
        };
    }

    private static DownloadErrorCategory MapErrorCategory(Core.Models.DownloadErrorCategory category)
    {
        return category switch
        {
            Core.Models.DownloadErrorCategory.Network => DownloadErrorCategory.Network,
            Core.Models.DownloadErrorCategory.Http => DownloadErrorCategory.Http,
            Core.Models.DownloadErrorCategory.DiskIo => DownloadErrorCategory.DiskIo,
            Core.Models.DownloadErrorCategory.Protocol => DownloadErrorCategory.Protocol,
            Core.Models.DownloadErrorCategory.ResourceNotFound => DownloadErrorCategory.ResourceNotFound,
            Core.Models.DownloadErrorCategory.Authentication => DownloadErrorCategory.Authentication,
            Core.Models.DownloadErrorCategory.RateLimiting => DownloadErrorCategory.RateLimiting,
            _ => DownloadErrorCategory.Unknown
        };
    }

    internal static DownloadPriority MapPriority(Core.Models.DownloadPriority priority)
    {
        return priority switch
        {
            Core.Models.DownloadPriority.Low => DownloadPriority.Low,
            Core.Models.DownloadPriority.Normal => DownloadPriority.Normal,
            Core.Models.DownloadPriority.High => DownloadPriority.High,
            Core.Models.DownloadPriority.Critical => DownloadPriority.Critical,
            _ => DownloadPriority.Normal
        };
    }

    public static Core.Models.DownloadPriority ToCorePriority(this DownloadPriority priority)
    {
        return priority switch
        {
            DownloadPriority.Low => Core.Models.DownloadPriority.Low,
            DownloadPriority.Normal => Core.Models.DownloadPriority.Normal,
            DownloadPriority.High => Core.Models.DownloadPriority.High,
            DownloadPriority.Critical => Core.Models.DownloadPriority.Critical,
            _ => Core.Models.DownloadPriority.Normal
        };
    }

    private static DownloadState MapState(Core.Models.DownloadState state)
    {
        return state switch
        {
            Core.Models.DownloadState.Created => DownloadState.Created,
            Core.Models.DownloadState.Queued => DownloadState.Queued,
            Core.Models.DownloadState.Analyzing => DownloadState.Analyzing,
            Core.Models.DownloadState.Downloading => DownloadState.Downloading,
            Core.Models.DownloadState.Paused => DownloadState.Paused,
            Core.Models.DownloadState.Completed => DownloadState.Completed,
            Core.Models.DownloadState.Failed => DownloadState.Failed,
            Core.Models.DownloadState.Cancelled => DownloadState.Cancelled,
            _ => DownloadState.Created
        };
    }
}
