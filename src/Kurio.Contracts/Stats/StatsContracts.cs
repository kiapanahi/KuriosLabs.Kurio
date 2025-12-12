using System.Text.Json.Serialization;

namespace KuriousLabs.Kurio.Contracts.Stats;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public sealed record Alert
{
    public AlertSeverity Severity { get; init; } = AlertSeverity.Info;

    public string Message { get; init; } = string.Empty;

    public Guid? DownloadId { get; init; }

    public string? Source { get; init; }

    public DateTimeOffset OccurredAt { get; init; }
}

public sealed record StatsSnapshot
{
    public int ActiveCount { get; init; }

    public int QueuedCount { get; init; }

    public int CompletedCount { get; init; }

    public int FailedCount { get; init; }

    public long CurrentThroughputBytesPerSecond { get; init; }

    public long AverageThroughputBytesPerSecond { get; init; }

    public long TotalBytesDownloaded { get; init; }

    public DateTimeOffset? StartedAt { get; init; }
}
