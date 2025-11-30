namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Defines the retry strategy for handling transient failures.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    ///     No retry attempts.
    /// </summary>
    None,

    /// <summary>
    ///     Exponential backoff with jitter.
    /// </summary>
    ExponentialBackoff,

    /// <summary>
    ///     Linear delay between retries.
    /// </summary>
    Linear,

    /// <summary>
    ///     Fixed delay between retries.
    /// </summary>
    Fixed
}
