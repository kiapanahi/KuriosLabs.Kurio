namespace KuriousLabs.Kurio.Core.Models;

/// <summary>
///     Defines how segments are stored during download.
/// </summary>
public enum StorageMode
{
    /// <summary>
    ///     All segments write to a single pre-allocated file with synchronization (default).
    ///     Safer but slightly slower due to locking.
    /// </summary>
    SingleFile,

    /// <summary>
    ///     Each segment writes to its own file, merged at completion.
    ///     Faster concurrent writes but requires merge step.
    /// </summary>
    PerSegmentFiles
}
