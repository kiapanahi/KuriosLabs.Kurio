namespace Kurio.Core.Models;

/// <summary>
/// Contains metadata about a remote resource.
/// </summary>
public sealed class ResourceMetadata
{
    /// <summary>
    /// Gets or sets the ETag header value.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Gets or sets the Last-Modified timestamp.
    /// </summary>
    public DateTimeOffset? LastModified { get; set; }

    /// <summary>
    /// Gets or sets the Content-Type header.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long ContentLength { get; set; }

    /// <summary>
    /// Gets or sets whether the server supports range requests.
    /// </summary>
    public bool SupportsRanges { get; set; }

    /// <summary>
    /// Gets or sets the suggested file name from Content-Disposition header.
    /// </summary>
    public string? SuggestedFileName { get; set; }

    /// <summary>
    /// Gets or sets additional custom headers.
    /// </summary>
    public IDictionary<string, string> AdditionalHeaders { get; set; } = new Dictionary<string, string>();
}
