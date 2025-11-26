namespace KuriousLabs.Kurio.Server.Models;

/// <summary>
/// Response model for API errors.
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional detailed error information.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Optional trace ID for debugging.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; init; }
}
