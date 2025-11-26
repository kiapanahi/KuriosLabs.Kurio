using Kurio.Core.Models;

namespace Kurio.Core.Configuration;

/// <summary>
/// Fluent builder for creating download options
/// </summary>
public sealed class DownloadOptionsBuilder
{
    private readonly DownloadOptions _options = new() { DestinationDirectory = string.Empty };

    /// <summary>
    /// Sets the maximum number of concurrent connections
    /// </summary>
    public DownloadOptionsBuilder WithMaxConnections(int maxConnections)
    {
        if (maxConnections < 1 || maxConnections > 32)
            throw new ArgumentOutOfRangeException(nameof(maxConnections), "Must be between 1 and 32");

        _options.MaxConnections = maxConnections;
        return this;
    }

    /// <summary>
    /// Sets the destination directory and optional filename
    /// </summary>
    public DownloadOptionsBuilder WithDestination(string directory, string? fileName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _options.DestinationDirectory = directory;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            _options.FileName = fileName;
        }
        return this;
    }

    /// <summary>
    /// Sets the download priority
    /// </summary>
    public DownloadOptionsBuilder WithPriority(DownloadPriority priority)
    {
        _options.Priority = priority;
        return this;
    }

    /// <summary>
    /// Adds custom HTTP headers
    /// </summary>
    public DownloadOptionsBuilder WithHeaders(Dictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(headers);
        _options.Headers = new Dictionary<string, string>(headers);
        return this;
    }

    /// <summary>
    /// Adds a single custom HTTP header
    /// </summary>
    public DownloadOptionsBuilder WithHeader(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        _options.Headers ??= new Dictionary<string, string>();
        _options.Headers[name] = value;
        return this;
    }

    /// <summary>
    /// Sets basic authentication credentials
    /// </summary>
    public DownloadOptionsBuilder WithAuthentication(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        _options.Authentication = new AuthenticationOptions
        {
            Type = AuthenticationType.Basic,
            Username = username,
            Password = password
        };
        return this;
    }

    /// <summary>
    /// Sets bearer token authentication
    /// </summary>
    public DownloadOptionsBuilder WithBearerToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        _options.Authentication = new AuthenticationOptions
        {
            Type = AuthenticationType.Bearer,
            Token = token
        };
        return this;
    }

    /// <summary>
    /// Sets custom retry policy
    /// </summary>
    public DownloadOptionsBuilder WithRetryPolicy(int maxRetries, TimeSpan initialDelay)
    {
        if (maxRetries < 0 || maxRetries > 10)
            throw new ArgumentOutOfRangeException(nameof(maxRetries), "Must be between 0 and 10");

        _options.RetryPolicy = new RetryPolicy
        {
            MaxRetryAttempts = maxRetries,
            InitialDelay = initialDelay
        };
        return this;
    }

    /// <summary>
    /// Sets the download category
    /// </summary>
    public DownloadOptionsBuilder WithCategory(string category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        _options.Category = category;
        return this;
    }

    /// <summary>
    /// Adds tags to the download
    /// </summary>
    public DownloadOptionsBuilder WithTags(params string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _options.Tags = [.. tags.Where(t => !string.IsNullOrWhiteSpace(t))];
        return this;
    }

    /// <summary>
    /// Sets expected checksum for verification
    /// </summary>
    public DownloadOptionsBuilder WithChecksum(ChecksumAlgorithm algorithm, string expectedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        _options.Checksum = new ChecksumOptions
        {
            Algorithm = algorithm,
            ExpectedHash = expectedHash
        };
        return this;
    }

    /// <summary>
    /// Sets bandwidth limit for this download
    /// </summary>
    public DownloadOptionsBuilder WithBandwidthLimit(long maxBytesPerSecond)
    {
        if (maxBytesPerSecond <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytesPerSecond), "Must be greater than 0");

        _options.MaxDownloadSpeed = maxBytesPerSecond;
        return this;
    }

    /// <summary>
    /// Sets the file naming policy for conflicts
    /// </summary>
    public DownloadOptionsBuilder WithNamingPolicy(FileNamingPolicy policy)
    {
        _options.NamingPolicy = policy;
        return this;
    }

    /// <summary>
    /// Builds and returns the download options
    /// </summary>
    public DownloadOptions Build()
    {
        return _options;
    }

    /// <summary>
    /// Creates a new builder instance
    /// </summary>
    public static DownloadOptionsBuilder Create() => new();
}

/// <summary>
/// Authentication options for downloads
/// </summary>
public sealed class AuthenticationOptions
{
    /// <summary>
    /// Authentication type
    /// </summary>
    public AuthenticationType Type { get; set; }

    /// <summary>
    /// Username (for Basic authentication)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password (for Basic authentication)
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Token (for Bearer authentication)
    /// </summary>
    public string? Token { get; set; }
}

/// <summary>
/// Authentication type
/// </summary>
public enum AuthenticationType
{
    None,
    Basic,
    Bearer,
    Digest
}

/// <summary>
/// Checksum verification options
/// </summary>
public sealed class ChecksumOptions
{
    /// <summary>
    /// Checksum algorithm to use
    /// </summary>
    public ChecksumAlgorithm Algorithm { get; set; }

    /// <summary>
    /// Expected hash value (hexadecimal string)
    /// </summary>
    public string ExpectedHash { get; set; } = string.Empty;
}
