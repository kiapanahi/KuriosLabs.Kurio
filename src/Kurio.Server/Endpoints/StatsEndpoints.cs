using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.Http.HttpResults;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Aggregated download statistics endpoints. Minimal-API replacement for the former
///     <c>StatsController</c>; the routes, metadata and response payloads are unchanged.
/// </summary>
internal static class StatsEndpoints
{
    /// <summary>
    ///     Maps <c>GET /api/stats</c> onto the supplied route builder.
    /// </summary>
    /// <param name="endpoints">The route builder to map the statistics endpoints onto.</param>
    /// <returns>The same <paramref name="endpoints" /> instance, to allow chaining.</returns>
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/stats", GetStatsAsync)
            .WithName("GetStats")
            .WithTags("Stats")
            .Produces<StatsSnapshot>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>
    ///     Returns a point-in-time snapshot combining persisted download statistics with the
    ///     live active/queued counts held by the queue manager.
    /// </summary>
    private static async Task<Ok<StatsSnapshot>> GetStatsAsync(
        IStatisticsService statisticsService,
        IDownloadQueueManager queueManager,
        CancellationToken cancellationToken)
    {
        var stats = await statisticsService.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(stats.ToContract(queueManager));
    }
}
