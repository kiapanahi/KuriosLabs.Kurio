namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Defines when and how checksum verification should occur.
/// </summary>
public enum VerificationMode
{
    /// <summary>
    ///     No verification performed.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Verify after download completes (default).
    /// </summary>
    PostDownload = 1,

    /// <summary>
    ///     Calculate checksum during download streaming.
    /// </summary>
    Streaming = 2,

    /// <summary>
    ///     Both streaming and post-download verification.
    /// </summary>
    Both = 3
}
