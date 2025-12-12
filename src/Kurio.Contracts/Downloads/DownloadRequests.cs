namespace KuriousLabs.Kurio.Contracts.Downloads;

public sealed record AddDownloadRequest
{
    public string Url { get; init; } = string.Empty;

    public string? FileName { get; init; }

    public string? Category { get; init; }

    public string? DestinationPath { get; init; }

    public int? MaxConnections { get; init; }

    public string? Checksum { get; init; }

    public string? ChecksumAlgorithm { get; init; }

    public bool? UseProxy { get; init; }

    public string? ProxyAddress { get; init; }

    public int? SegmentSizeBytes { get; init; }
}

public sealed record ChangePriorityRequest
{
    public DownloadPriority Priority { get; init; } = DownloadPriority.Normal;
}

public sealed record DownloadFilterRequest
{
    public DownloadStateFilter States { get; init; } = DownloadStateFilter.All;

    public string? Category { get; init; }

    public int? Limit { get; init; }
}
