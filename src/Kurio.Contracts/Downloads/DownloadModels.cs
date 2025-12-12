using System.Text.Json.Serialization;

namespace KuriousLabs.Kurio.Contracts.Downloads;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadState
{
    Created,
    Queued,
    Analyzing,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}

[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadStateFilter
{
    None = 0,
    Created = 1 << 0,
    Queued = 1 << 1,
    Analyzing = 1 << 2,
    Downloading = 1 << 3,
    Paused = 1 << 4,
    Completed = 1 << 5,
    Failed = 1 << 6,
    Cancelled = 1 << 7,
    Active = Queued | Analyzing | Downloading,
    All = Created | Queued | Analyzing | Downloading | Paused | Completed | Failed | Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DownloadErrorCategory
{
    Network,
    Http,
    DiskIo,
    Protocol,
    ResourceNotFound,
    Authentication,
    RateLimiting,
    Unknown
}

public sealed record DownloadFailureInfo
{
    public DownloadErrorCategory Category { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Details { get; init; }

    public int? StatusCode { get; init; }

    public int? RetryCount { get; init; }

    public string? Source { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

public sealed record DownloadSummary
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string? Category { get; init; }

    public long? TotalBytes { get; init; }

    public long DownloadedBytes { get; init; }

    public double? PercentComplete { get; init; }

    public DownloadState State { get; init; }

    public long? BytesPerSecond { get; init; }

    public TimeSpan? EstimatedTimeRemaining { get; init; }

    public int ActiveConnections { get; init; }

    public DownloadPriority Priority { get; init; } = DownloadPriority.Normal;

    public bool HasChecksum { get; init; }

    public DownloadFailureInfo? LastError { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastUpdatedAt { get; init; }

    public string? DestinationPath { get; init; }
}

public sealed record DownloadProgressUpdate
{
    public Guid Id { get; init; }

    public long DownloadedBytes { get; init; }

    public long? TotalBytes { get; init; }

    public double? PercentComplete { get; init; }

    public long? BytesPerSecond { get; init; }

    public TimeSpan? EstimatedTimeRemaining { get; init; }

    public int ActiveConnections { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}

public sealed record DownloadStatusChange
{
    public Guid Id { get; init; }

    public DownloadState PreviousState { get; init; }

    public DownloadState CurrentState { get; init; }

    public string? Reason { get; init; }

    public string? Message { get; init; }

    public DownloadFailureInfo? Failure { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}

public sealed record DownloadSubscriptionRequest
{
    public DownloadStateFilter States { get; init; } = DownloadStateFilter.All;

    public string? Category { get; init; }

    public int? Limit { get; init; }

    public bool IncludeCompleted { get; init; }
}
