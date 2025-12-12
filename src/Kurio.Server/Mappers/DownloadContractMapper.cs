using KuriousLabs.Kurio.Core.Abstractions;
using CoreModels = KuriousLabs.Kurio.Core.Models;
using ContractDownloads = KuriousLabs.Kurio.Contracts.Downloads;

namespace KuriousLabs.Kurio.Server.Mappers;

public static class DownloadContractMapper
{
    public static ContractDownloads.DownloadSummary ToContract(this IDownloadTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new ContractDownloads.DownloadSummary
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

    public static ContractDownloads.DownloadProgressUpdate ToProgressUpdate(this CoreModels.DownloadProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return new ContractDownloads.DownloadProgressUpdate
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

    public static CoreModels.DownloadStateFilter ToCoreFilter(this ContractDownloads.DownloadStateFilter filter)
    {
        if (filter == ContractDownloads.DownloadStateFilter.All)
        {
            return CoreModels.DownloadStateFilter.All;
        }

        if (filter == ContractDownloads.DownloadStateFilter.None)
        {
            return CoreModels.DownloadStateFilter.None;
        }

        CoreModels.DownloadStateFilter mapped = 0;

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Created))
        {
            mapped |= CoreModels.DownloadStateFilter.Created;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Queued))
        {
            mapped |= CoreModels.DownloadStateFilter.Queued;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Analyzing))
        {
            mapped |= CoreModels.DownloadStateFilter.Analyzing;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Downloading))
        {
            mapped |= CoreModels.DownloadStateFilter.Downloading;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Paused))
        {
            mapped |= CoreModels.DownloadStateFilter.Paused;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Completed))
        {
            mapped |= CoreModels.DownloadStateFilter.Completed;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Failed))
        {
            mapped |= CoreModels.DownloadStateFilter.Failed;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Cancelled))
        {
            mapped |= CoreModels.DownloadStateFilter.Cancelled;
        }

        if (filter.HasFlag(ContractDownloads.DownloadStateFilter.Active))
        {
            mapped |= CoreModels.DownloadStateFilter.Active;
        }

        return mapped;
    }

    private static ContractDownloads.DownloadFailureInfo? MapError(CoreModels.DownloadError? error)
    {
        if (error is null)
        {
            return null;
        }

        return new ContractDownloads.DownloadFailureInfo
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

    private static ContractDownloads.DownloadErrorCategory MapErrorCategory(CoreModels.DownloadErrorCategory category)
    {
        return category switch
        {
            CoreModels.DownloadErrorCategory.Network => ContractDownloads.DownloadErrorCategory.Network,
            CoreModels.DownloadErrorCategory.Http => ContractDownloads.DownloadErrorCategory.Http,
            CoreModels.DownloadErrorCategory.DiskIo => ContractDownloads.DownloadErrorCategory.DiskIo,
            CoreModels.DownloadErrorCategory.Protocol => ContractDownloads.DownloadErrorCategory.Protocol,
            CoreModels.DownloadErrorCategory.ResourceNotFound => ContractDownloads.DownloadErrorCategory.ResourceNotFound,
            CoreModels.DownloadErrorCategory.Authentication => ContractDownloads.DownloadErrorCategory.Authentication,
            CoreModels.DownloadErrorCategory.RateLimiting => ContractDownloads.DownloadErrorCategory.RateLimiting,
            _ => ContractDownloads.DownloadErrorCategory.Unknown
        };
    }

    internal static ContractDownloads.DownloadPriority MapPriority(CoreModels.DownloadPriority priority)
    {
        return priority switch
        {
            CoreModels.DownloadPriority.Low => ContractDownloads.DownloadPriority.Low,
            CoreModels.DownloadPriority.Normal => ContractDownloads.DownloadPriority.Normal,
            CoreModels.DownloadPriority.High => ContractDownloads.DownloadPriority.High,
            CoreModels.DownloadPriority.Critical => ContractDownloads.DownloadPriority.Critical,
            _ => ContractDownloads.DownloadPriority.Normal
        };
    }

    public static CoreModels.DownloadPriority ToCorePriority(this ContractDownloads.DownloadPriority priority)
    {
        return priority switch
        {
            ContractDownloads.DownloadPriority.Low => CoreModels.DownloadPriority.Low,
            ContractDownloads.DownloadPriority.Normal => CoreModels.DownloadPriority.Normal,
            ContractDownloads.DownloadPriority.High => CoreModels.DownloadPriority.High,
            ContractDownloads.DownloadPriority.Critical => CoreModels.DownloadPriority.Critical,
            _ => CoreModels.DownloadPriority.Normal
        };
    }

    private static ContractDownloads.DownloadState MapState(CoreModels.DownloadState state)
    {
        return state switch
        {
            CoreModels.DownloadState.Created => ContractDownloads.DownloadState.Created,
            CoreModels.DownloadState.Queued => ContractDownloads.DownloadState.Queued,
            CoreModels.DownloadState.Analyzing => ContractDownloads.DownloadState.Analyzing,
            CoreModels.DownloadState.Downloading => ContractDownloads.DownloadState.Downloading,
            CoreModels.DownloadState.Paused => ContractDownloads.DownloadState.Paused,
            CoreModels.DownloadState.Completed => ContractDownloads.DownloadState.Completed,
            CoreModels.DownloadState.Failed => ContractDownloads.DownloadState.Failed,
            CoreModels.DownloadState.Cancelled => ContractDownloads.DownloadState.Cancelled,
            _ => ContractDownloads.DownloadState.Created
        };
    }
}
