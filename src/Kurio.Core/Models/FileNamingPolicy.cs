namespace Kurio.Core.Models;

/// <summary>
///     Policy for handling file name conflicts.
/// </summary>
public enum FileNamingPolicy
{
    /// <summary>
    ///     Overwrite existing file.
    /// </summary>
    Overwrite,

    /// <summary>
    ///     Add numeric suffix to avoid conflict (e.g., file(1).txt).
    /// </summary>
    AutoRename,

    /// <summary>
    ///     Skip download if file exists.
    /// </summary>
    Skip,

    /// <summary>
    ///     Prompt user for action (requires UI integration).
    /// </summary>
    Prompt
}
