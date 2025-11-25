using Kurio.Core.Models;

namespace Kurio.Core.Abstractions;

/// <summary>
/// Categorizes exceptions into error categories and determines recovery actions.
/// </summary>
public interface IErrorClassifier
{
    /// <summary>
    /// Classifies an exception into a download error.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>A download error with category and recovery information.</returns>
    DownloadError Classify(Exception exception);

    /// <summary>
    /// Determines if an error is transient and retriable.
    /// </summary>
    /// <param name="error">The error to check.</param>
    /// <returns>True if the error is transient; otherwise, false.</returns>
    bool IsTransient(DownloadError error);

    /// <summary>
    /// Gets the recommended recovery action for an error.
    /// </summary>
    /// <param name="error">The error to evaluate.</param>
    /// <returns>The recommended recovery action.</returns>
    ErrorRecoveryAction GetRecoveryAction(DownloadError error);
}
