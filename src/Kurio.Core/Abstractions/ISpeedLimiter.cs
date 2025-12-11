namespace KuriousLabs.Kurio.Core.Abstractions;

/// <summary>
///     Interface for bandwidth throttling using the standard .NET rate limiting API.
/// </summary>
public interface ISpeedLimiter : IAsyncDisposable
{
    /// <summary>
    ///     Gets a value indicating whether speed limiting is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Gets the maximum bytes per second limit.
    /// </summary>
    long MaxBytesPerSecond { get; }

    /// <summary>
    ///     Throttles the operation to respect the configured speed limit.
    /// </summary>
    /// <param name="bytesRequested">Number of bytes to be transferred.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the throttling delay.</returns>
    Task ThrottleAsync(int bytesRequested, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates the maximum speed limit dynamically. Can be called at runtime.
    /// </summary>
    /// <param name="newMaxBytesPerSecond">New maximum bytes per second (0 = unlimited).</param>
    void UpdateMaxSpeed(long newMaxBytesPerSecond);
}
