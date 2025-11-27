namespace Kurio.Core.Statistics;

/// <summary>
///     Calculates estimated time remaining for downloads.
/// </summary>
public sealed class EtaCalculator
{
    private readonly SpeedCalculator _speedCalculator;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EtaCalculator" /> class.
    /// </summary>
    /// <param name="speedCalculator">The speed calculator to use for speed measurements.</param>
    public EtaCalculator(SpeedCalculator speedCalculator)
    {
        _speedCalculator = speedCalculator ?? throw new ArgumentNullException(nameof(speedCalculator));
    }

    /// <summary>
    ///     Gets the estimated time remaining based on the current speed.
    /// </summary>
    /// <param name="bytesRemaining">The number of bytes remaining to download.</param>
    /// <returns>The estimated time remaining, or null if it cannot be calculated.</returns>
    public TimeSpan? GetEtaFromCurrentSpeed(long bytesRemaining)
    {
        if (bytesRemaining <= 0)
        {
            return TimeSpan.Zero;
        }

        var currentSpeed = _speedCalculator.CurrentSpeed;
        if (currentSpeed <= 0)
        {
            return null;
        }

        var secondsRemaining = (double)bytesRemaining / currentSpeed;
        return TimeSpan.FromSeconds(secondsRemaining);
    }

    /// <summary>
    ///     Gets the estimated time remaining based on the average speed.
    /// </summary>
    /// <param name="bytesRemaining">The number of bytes remaining to download.</param>
    /// <returns>The estimated time remaining, or null if it cannot be calculated.</returns>
    public TimeSpan? GetEtaFromAverageSpeed(long bytesRemaining)
    {
        if (bytesRemaining <= 0)
        {
            return TimeSpan.Zero;
        }

        var averageSpeed = _speedCalculator.AverageSpeed;
        if (averageSpeed <= 0)
        {
            return null;
        }

        var secondsRemaining = (double)bytesRemaining / averageSpeed;
        return TimeSpan.FromSeconds(secondsRemaining);
    }

    /// <summary>
    ///     Gets the most accurate ETA estimate, preferring average speed when available.
    ///     Falls back to current speed if average is not yet available.
    /// </summary>
    /// <param name="bytesRemaining">The number of bytes remaining to download.</param>
    /// <returns>The estimated time remaining, or null if it cannot be calculated.</returns>
    public TimeSpan? GetBestEta(long bytesRemaining)
    {
        if (bytesRemaining <= 0)
        {
            return TimeSpan.Zero;
        }

        // Prefer average speed as it's more stable
        var avgEta = GetEtaFromAverageSpeed(bytesRemaining);
        if (avgEta.HasValue)
        {
            return avgEta;
        }

        // Fall back to current speed
        return GetEtaFromCurrentSpeed(bytesRemaining);
    }
}
