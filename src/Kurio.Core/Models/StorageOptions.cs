namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Configuration options for storage management.
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    ///     Gets or sets the storage mode for multi-segment downloads.
    /// </summary>
    public StorageMode Mode { get; set; } = StorageMode.SingleFile;

    /// <summary>
    ///     Gets or sets whether to verify writes by reading back data.
    /// </summary>
    public bool VerifyWrites { get; set; } = true;

    /// <summary>
    ///     Gets or sets the write buffer size in bytes.
    /// </summary>
    public int WriteBufferSize { get; set; } = 81920; // 80KB

    /// <summary>
    ///     Gets or sets whether to cleanup segment files after merge.
    /// </summary>
    public bool CleanupSegmentFiles { get; set; } = true;

    /// <summary>
    ///     Gets the default storage options.
    /// </summary>
    public static StorageOptions Default => new();
}
