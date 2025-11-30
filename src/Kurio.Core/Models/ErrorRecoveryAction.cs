namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Defines the recovery action to take for an error.
/// </summary>
public enum ErrorRecoveryAction
{
    /// <summary>
    ///     Retry the operation automatically.
    /// </summary>
    Retry,

    /// <summary>
    ///     Fall back to single-threaded download.
    /// </summary>
    FallbackToSingleThread,

    /// <summary>
    ///     Pause the download and notify the user.
    /// </summary>
    Pause,

    /// <summary>
    ///     Fail the download permanently.
    /// </summary>
    Fail,

    /// <summary>
    ///     Ignore the error and continue.
    /// </summary>
    Ignore
}
