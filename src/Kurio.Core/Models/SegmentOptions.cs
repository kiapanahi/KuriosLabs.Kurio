namespace Kurio.Core.Models;

/// <summary>
///     Options for configuring segment behavior.
/// </summary>
public sealed class SegmentOptions
{
    /// <summary>
    ///     Gets or sets the maximum number of connections per download.
    /// </summary>
    public int MaxConnections { get; set; } = 8;

    /// <summary>
    ///     Gets or sets the minimum size in bytes for creating a segment.
    /// </summary>
    public long MinSegmentSize { get; set; } = 1024 * 1024; // 1 MB

    /// <summary>
    ///     Gets or sets the buffer size for reading segment data.
    /// </summary>
    public int BufferSize { get; set; } = 8192; // 8 KB
}
