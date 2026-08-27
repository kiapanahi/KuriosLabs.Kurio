using System.Text.Json;
using System.Text.Json.Serialization;

using KuriousLabs.Kurio.Core.Abstractions;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Server-sent events endpoint that streams download progress to browser clients.
/// </summary>
internal static class ProgressStreamEndpoints
{
    // Cached, reused JsonSerializerOptions (CA1869: avoid allocating a new instance - and losing
    // its JsonTypeInfo cache - on every streamed message).
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static IEndpointRouteBuilder MapProgressStreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/downloads/stream", (
                IDownloadEngine engine,
                Guid? taskId,
                CancellationToken cancellationToken) => Results.Stream(async stream =>
            {
                try
                {
                    await using StreamWriter writer = new(stream);
                    writer.AutoFlush = true;

                    await writer.WriteLineAsync("retry: 10000\n").ConfigureAwait(false);

                    await foreach (var progress in engine.StreamProgressAsync(taskId, cancellationToken).ConfigureAwait(false))
                    {
                        var json = JsonSerializer.Serialize(progress, SseJsonOptions);

                        await writer.WriteLineAsync("event: progress").ConfigureAwait(false);
                        await writer.WriteLineAsync($"data: {json}").ConfigureAwait(false);
                        await writer.WriteLineAsync().ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Client disconnected or the request was aborted mid-stream; nothing left to write to.
                }
                catch (IOException)
                {
                    // The underlying connection was closed mid-write (e.g. broken pipe); nothing left to write to.
                }
            }, "text/event-stream"))
            .WithName("StreamProgress")
            .WithTags("Progress")
            .Produces(200, contentType: "text/event-stream");

        return endpoints;
    }
}
