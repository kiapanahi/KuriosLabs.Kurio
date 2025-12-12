using System.Linq;

using KuriousLabs.Kurio.Contracts.Queue;
using KuriousLabs.Kurio.Contracts.Hubs;
using KuriousLabs.Kurio.Server.Hubs;
using KuriousLabs.Kurio.Server.Mappers;
using ContractChangePriorityRequest = KuriousLabs.Kurio.Contracts.Downloads.ChangePriorityRequest;
using KuriousLabs.Kurio.Core.Abstractions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Controllers;

[ApiController]
[Route("api/queue")]
[Produces("application/json")]
public class QueueController : ControllerBase
{
    private readonly IDownloadQueueManager _queueManager;
    private readonly IHubContext<QueueHub, IQueueClient> _queueHubContext;
    private readonly ILogger<QueueController> _logger;

    public QueueController(
        IDownloadQueueManager queueManager,
        IHubContext<QueueHub, IQueueClient> queueHubContext,
        ILogger<QueueController> logger)
    {
        _queueManager = queueManager;
        _queueHubContext = queueHubContext;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<QueueItem>), StatusCodes.Status200OK)]
    public ActionResult<List<QueueItem>> GetQueue()
    {
        var snapshot = BuildSnapshot();
        return Ok(snapshot);
    }

    [HttpPost("{id:guid}/priority")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangePriorityAsync(
        Guid id,
        [FromBody] ContractChangePriorityRequest request,
        CancellationToken cancellationToken)
    {
        var success = _queueManager.ChangePriority(id, request.Priority.ToCorePriority());
        if (!success)
        {
            return Problem(
                $"Cannot change priority for download {id}",
                statusCode: StatusCodes.Status404NotFound,
                title: "Download not found or not queued");
        }

        await BroadcastSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/move-up")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveUpAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_queueManager.MoveUp(id))
        {
            return Problem(
                $"Cannot move download {id} up",
                statusCode: StatusCodes.Status404NotFound,
                title: "Download not found or already at top");
        }

        await BroadcastSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/move-down")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveDownAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_queueManager.MoveDown(id))
        {
            return Problem(
                $"Cannot move download {id} down",
                statusCode: StatusCodes.Status404NotFound,
                title: "Download not found or already at bottom");
        }

        await BroadcastSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/move-top")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToTopAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_queueManager.MoveToTop(id))
        {
            return Problem(
                $"Cannot move download {id} to top",
                statusCode: StatusCodes.Status404NotFound,
                title: "Download not found or already at top");
        }

        await BroadcastSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("{id:guid}/move-bottom")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MoveToBottomAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_queueManager.MoveToBottom(id))
        {
            return Problem(
                $"Cannot move download {id} to bottom",
                statusCode: StatusCodes.Status404NotFound,
                title: "Download not found or already at bottom");
        }

        await BroadcastSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private List<QueueItem> BuildSnapshot()
    {
        return _queueManager.GetQueuedTasks()
            .Select((task, index) => task.ToContract(index + 1))
            .ToList();
    }

    private async Task BroadcastSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = BuildSnapshot();
        await _queueHubContext.Clients.Group(QueueHub.GroupName)
            .QueueSnapshotAsync(snapshot)
            .ConfigureAwait(false);
    }
}
