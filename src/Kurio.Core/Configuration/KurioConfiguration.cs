namespace Kurio.Core.Configuration;

/// <summary>
///     Root configuration object for Kurio application
/// </summary>
public sealed class KurioConfiguration
{
    /// <summary>
    ///     Configuration schema version for migration support
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    ///     Download-related settings
    /// </summary>
    public DownloadSettings Downloads { get; set; } = new();

    /// <summary>
    ///     Network-related settings
    /// </summary>
    public NetworkSettings Network { get; set; } = new();

    /// <summary>
    ///     Verification settings
    /// </summary>
    public VerificationSettings Verification { get; set; } = new();

    /// <summary>
    ///     Storage settings
    /// </summary>
    public StorageSettings Storage { get; set; } = new();

    /// <summary>
    ///     Logging settings
    /// </summary>
    public LoggingSettings Logging { get; set; } = new();
}

/// <summary>
///     Download behavior settings
/// </summary>
public sealed class DownloadSettings
{
    /// <summary>
    ///     Default directory for downloads (supports ~ expansion)
    /// </summary>
    public string DefaultDirectory { get; set; } = "~/Downloads";

    /// <summary>
    ///     Temporary directory for partial downloads
    /// </summary>
    public string TempDirectory { get; set; } = "~/.kurio/temp";

    /// <summary>
    ///     Maximum number of concurrent downloads (1-20)
    /// </summary>
    public int MaxConcurrentDownloads { get; set; } = 3;

    /// <summary>
    ///     Maximum number of connections per download (1-32)
    /// </summary>
    public int MaxConnectionsPerDownload { get; set; } = 8;

    /// <summary>
    ///     Minimum segment size in bytes (512 KB - 100 MB)
    /// </summary>
    public long MinSegmentSize { get; set; } = 1_048_576; // 1 MB

    /// <summary>
    ///     Buffer size for segment reading (4 KB - 1 MB)
    /// </summary>
    public int SegmentBufferSize { get; set; } = 8192; // 8 KB

    /// <summary>
    ///     Auto-start downloads when added to queue
    /// </summary>
    public bool AutoStart { get; set; } = true;

    /// <summary>
    ///     File naming policy for conflicts
    /// </summary>
    public string FileNamingPolicy { get; set; } = "appendNumber";

    /// <summary>
    ///     Clean up incomplete downloads on application exit
    /// </summary>
    public bool CleanupIncompleteOnExit { get; set; } = false;
}

/// <summary>
///     Network configuration settings
/// </summary>
public sealed class NetworkSettings
{
    /// <summary>
    ///     Request timeout in seconds (5-300)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Retry policy configuration
    /// </summary>
    public RetryPolicySettings RetryPolicy { get; set; } = new();

    /// <summary>
    ///     Bandwidth limiting configuration
    /// </summary>
    public BandwidthLimitSettings BandwidthLimit { get; set; } = new();

    /// <summary>
    ///     User agent string for HTTP requests
    /// </summary>
    public string UserAgent { get; set; } = "Kurio/1.0";

    /// <summary>
    ///     Follow HTTP redirects
    /// </summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    ///     Maximum number of redirects to follow (1-10)
    /// </summary>
    public int MaxRedirects { get; set; } = 5;

    /// <summary>
    ///     Validate SSL/TLS certificates
    /// </summary>
    public bool ValidateCertificates { get; set; } = true;
}

/// <summary>
///     Retry policy configuration
/// </summary>
public sealed class RetryPolicySettings
{
    /// <summary>
    ///     Maximum number of retry attempts (0-10)
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    ///     Initial delay before first retry in seconds (0.5-60)
    /// </summary>
    public double InitialDelaySeconds { get; set; } = 1.0;

    /// <summary>
    ///     Maximum delay between retries in seconds (1-300)
    /// </summary>
    public double MaxDelaySeconds { get; set; } = 60.0;

    /// <summary>
    ///     Exponential backoff multiplier (1.0-5.0)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
///     Bandwidth limiting settings
/// </summary>
public sealed class BandwidthLimitSettings
{
    /// <summary>
    ///     Enable bandwidth limiting
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    ///     Maximum download speed in bytes per second (0 = unlimited)
    /// </summary>
    public long MaxDownloadSpeed { get; set; } = 0;

    /// <summary>
    ///     Maximum upload speed in bytes per second (0 = unlimited)
    /// </summary>
    public long MaxUploadSpeed { get; set; } = 0;
}

/// <summary>
///     File verification settings
/// </summary>
public sealed class VerificationSettings
{
    /// <summary>
    ///     Automatically verify checksums when available
    /// </summary>
    public bool AutoVerify { get; set; } = true;

    /// <summary>
    ///     Default checksum algorithm (MD5, SHA1, SHA256, SHA512)
    /// </summary>
    public string ChecksumAlgorithm { get; set; } = "SHA256";

    /// <summary>
    ///     Fail download on checksum mismatch
    /// </summary>
    public bool FailOnMismatch { get; set; } = true;
}

/// <summary>
///     Storage management settings
/// </summary>
public sealed class StorageSettings
{
    /// <summary>
    ///     Check disk space before starting downloads
    /// </summary>
    public bool CheckDiskSpace { get; set; } = true;

    /// <summary>
    ///     Minimum free space to maintain in bytes
    /// </summary>
    public long MinimumFreeSpace { get; set; } = 104_857_600; // 100 MB

    /// <summary>
    ///     File categorization settings
    /// </summary>
    public CategorizationSettings Categorization { get; set; } = new();
}

/// <summary>
///     Automatic file categorization settings
/// </summary>
public sealed class CategorizationSettings
{
    /// <summary>
    ///     Enable automatic categorization
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Auto-categorize by MIME type
    /// </summary>
    public bool AutoCategorizeByMimeType { get; set; } = true;

    /// <summary>
    ///     Custom categorization rules
    /// </summary>
    public List<CategoryRule> CustomRules { get; set; } = [];
}

/// <summary>
///     Custom categorization rule
/// </summary>
public sealed class CategoryRule
{
    /// <summary>
    ///     Rule name for identification
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Target category
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    ///     File extensions to match (e.g., ".pdf", ".doc")
    /// </summary>
    public List<string>? Extensions { get; set; }

    /// <summary>
    ///     MIME types to match (e.g., "application/pdf")
    /// </summary>
    public List<string>? MimeTypes { get; set; }

    /// <summary>
    ///     URL pattern to match (regex)
    /// </summary>
    public string? UrlPattern { get; set; }
}

/// <summary>
///     Logging configuration
/// </summary>
public sealed class LoggingSettings
{
    /// <summary>
    ///     Logging level (Trace, Debug, Information, Warning, Error, Critical)
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    ///     Directory for log files
    /// </summary>
    public string LogDirectory { get; set; } = "~/.kurio/logs";

    /// <summary>
    ///     Maximum number of log files to retain (1-100)
    /// </summary>
    public int MaxLogFiles { get; set; } = 10;

    /// <summary>
    ///     Maximum size per log file in bytes
    /// </summary>
    public long MaxLogSizeBytes { get; set; } = 10_485_760; // 10 MB
}
