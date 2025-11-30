namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Configuration for retry behavior on transient failures.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    ///     Gets or sets the retry strategy to use.
    /// </summary>
    public RetryStrategy Strategy { get; set; } = RetryStrategy.ExponentialBackoff;

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the initial delay before the first retry.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Gets or sets the multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    ///     Gets or sets whether to use jitter to randomize delays.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    ///     Gets or sets the error categories that should trigger a retry.
    /// </summary>
    public HashSet<DownloadErrorCategory> RetriableCategories { get; set; } = new()
    {
        DownloadErrorCategory.Network, DownloadErrorCategory.RateLimiting
    };

    /// <summary>
    ///     Gets the default retry policy.
    /// </summary>
    public static RetryPolicy Default => new();
}
