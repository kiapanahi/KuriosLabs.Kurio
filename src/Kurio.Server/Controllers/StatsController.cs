using KuriousLabs.Kurio.Contracts.Stats;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Mappers;

using Microsoft.AspNetCore.Mvc;

namespace KuriousLabs.Kurio.Server.Controllers;

[ApiController]
[Route("api/stats")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;
    private readonly IDownloadQueueManager _queueManager;

    public StatsController(
        IStatisticsService statisticsService,
        IDownloadQueueManager queueManager)
    {
        _statisticsService = statisticsService;
        _queueManager = queueManager;
    }

    [HttpGet]
    [ProducesResponseType(typeof(StatsSnapshot), StatusCodes.Status200OK)]
    public async Task<ActionResult<StatsSnapshot>> GetStatsAsync(CancellationToken cancellationToken)
    {
        var stats = await _statisticsService.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = stats.ToContract(_queueManager);
        return Ok(snapshot);
    }
}
