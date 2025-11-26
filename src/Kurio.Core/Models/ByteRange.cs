namespace Kurio.Core.Models;

/// <summary>
///     Represents a byte range for partial downloads.
/// </summary>
/// <param name="Start">Starting byte position (inclusive).</param>
/// <param name="End">Ending byte position (inclusive).</param>
public readonly record struct ByteRange(long Start, long End)
{
    /// <summary>
    ///     Gets the length of this byte range.
    /// </summary>
    public long Length => End - Start + 1;

    /// <summary>
    ///     Creates a byte range from start position and length.
    /// </summary>
    public static ByteRange FromLength(long start, long length)
    {
        return new ByteRange(start, start + length - 1);
    }

    /// <summary>
    ///     Returns the HTTP Range header value for this byte range.
    /// </summary>
    public override string ToString()
    {
        return $"bytes={Start}-{End}";
    }
}
