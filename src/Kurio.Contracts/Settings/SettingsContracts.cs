namespace KuriousLabs.Kurio.Contracts.Settings;

public sealed record SettingsSummary
{
    public bool ProxyEnabled { get; init; }

    public string? ProxyAddress { get; init; }

    public int MaxConcurrentDownloads { get; init; }

    public int DefaultConnectionsPerDownload { get; init; }

    public int? DefaultSegmentSizeBytes { get; init; }

    public string? DefaultCategory { get; init; }
}

public sealed record SettingsUpdateRequest
{
    public bool? ProxyEnabled { get; init; }

    public string? ProxyAddress { get; init; }

    public int? MaxConcurrentDownloads { get; init; }

    public int? DefaultConnectionsPerDownload { get; init; }

    public int? DefaultSegmentSizeBytes { get; init; }

    public string? DefaultCategory { get; init; }
}
