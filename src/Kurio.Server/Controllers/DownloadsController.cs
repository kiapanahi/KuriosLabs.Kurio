using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using KuriousLabs.Kurio.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace KuriousLabs.Kurio.Server.Controllers;

/// <summary>
/// API controller for managing downloads.
/// </summary>
[ApiController]
[Route("api/downloads")]
[Produces("application/json")]
public class DownloadsController : ControllerBase
{
    private readonly IDownloadEngine _engine;
    private readonly ILogger<DownloadsController> _logger;

    public DownloadsController(
        IDownloadEngine engine,
        ILogger<DownloadsController> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Adds a new download to the queue.
    /// </summary>
    /// <param name="request">Download details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created download details.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DownloadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DownloadResponse>> AddDownload(
        [FromBody] AddDownloadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            {
                return BadRequest(new ErrorResponse
                {
                    Message = "Invalid URL",
                    Details = $"The URL '{request.Url}' is not valid",
                    StatusCode = StatusCodes.Status400BadRequest
                });
            }

            var task = await _engine.AddDownloadAsync(
                uri,
                request.ToDownloadOptions(),
                cancellationToken);

            // Set priority if different from default
            if (request.Priority != DownloadPriority.Normal)
            {
                _engine.ChangePriority(task.Id, request.Priority);
            }

            var response = DownloadResponse.FromTask(task);
            return CreatedAtAction(nameof(GetDownload), new { id = task.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding download for URL: {Url}", request.Url);
            return BadRequest(new ErrorResponse
            {
                Message = "Failed to add download",
                Details = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Gets all downloads with optional filtering.
    /// </summary>
    /// <param name="filter">State filter (All, Active, Completed, Failed).</param>
    /// <returns>List of downloads.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<DownloadResponse>), StatusCodes.Status200OK)]
    public ActionResult<List<DownloadResponse>> GetDownloads(
        [FromQuery] DownloadStateFilter filter)
    {
        var downloads = _engine.GetDownloads(filter)
            .Select(DownloadResponse.FromTask)
            .ToList();

        return Ok(downloads);
    }

    /// <summary>
    /// Gets a specific download by ID.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <returns>Download details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DownloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public ActionResult<DownloadResponse> GetDownload(Guid id)
    {
        var task = _engine.GetDownload(id);
        if (task == null)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Download not found",
                Details = $"No download with ID {id} exists",
                StatusCode = StatusCodes.Status404NotFound
            });
        }

        return Ok(DownloadResponse.FromTask(task));
    }

    /// <summary>
    /// Starts a queued download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartDownload(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.StartDownloadAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot start download {Id}", id);
            return BadRequest(new ErrorResponse
            {
                Message = "Cannot start download",
                Details = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Download {Id} not found", id);
            return NotFound(new ErrorResponse
            {
                Message = "Download not found",
                Details = ex.Message,
                StatusCode = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Pauses an active download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PauseDownload(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.PauseDownloadAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot pause download {Id}", id);
            return BadRequest(new ErrorResponse
            {
                Message = "Cannot pause download",
                Details = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Download {Id} not found", id);
            return NotFound(new ErrorResponse
            {
                Message = "Download not found",
                Details = ex.Message,
                StatusCode = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Resumes a paused download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResumeDownload(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _engine.ResumeDownloadAsync(id, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot resume download {Id}", id);
            return BadRequest(new ErrorResponse
            {
                Message = "Cannot resume download",
                Details = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Download {Id} not found", id);
            return NotFound(new ErrorResponse
            {
                Message = "Download not found",
                Details = ex.Message,
                StatusCode = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Cancels a download and optionally removes partial files.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="removeFiles">Whether to remove partial files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelDownload(
        Guid id,
        CancellationToken cancellationToken,
        [FromQuery] bool removeFiles = false)
    {
        try
        {
            await _engine.CancelDownloadAsync(id, removeFiles, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Download {Id} not found", id);
            return NotFound(new ErrorResponse
            {
                Message = "Download not found",
                Details = ex.Message,
                StatusCode = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// Changes the priority of a queued download.
    /// </summary>
    /// <param name="id">Download ID.</param>
    /// <param name="request">New priority.</param>
    [HttpPost("{id:guid}/priority")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public IActionResult ChangePriority(Guid id, [FromBody] ChangePriorityRequest request)
    {
        var success = _engine.ChangePriority(id, request.Priority);
        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Message = "Download not found or not queued",
                Details = $"Cannot change priority for download {id}",
                StatusCode = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Pauses all active downloads.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of downloads paused.</returns>
    [HttpPost("pause-all")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> PauseAll(CancellationToken cancellationToken)
    {
        var count = await _engine.PauseAllAsync(cancellationToken);
        return Ok(count);
    }

    /// <summary>
    /// Clears all completed downloads from the queue.
    /// </summary>
    [HttpPost("clear-completed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult ClearCompleted()
    {
        _engine.ClearCompleted();
        return NoContent();
    }

    /// <summary>
    /// Gets download queue statistics.
    /// </summary>
    /// <returns>Queue statistics.</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(QueueStatistics), StatusCodes.Status200OK)]
    public ActionResult<QueueStatistics> GetStatistics()
    {
        var (active, queued) = _engine.GetQueueStatistics();
        var allDownloads = _engine.GetDownloads(DownloadStateFilter.All).ToList();

        return Ok(new QueueStatistics
        {
            ActiveDownloads = active,
            QueuedDownloads = queued,
            TotalDownloads = allDownloads.Count,
            CompletedDownloads = allDownloads.Count(d => d.State == DownloadState.Completed),
            FailedDownloads = allDownloads.Count(d => d.State == DownloadState.Failed)
        });
    }
}
