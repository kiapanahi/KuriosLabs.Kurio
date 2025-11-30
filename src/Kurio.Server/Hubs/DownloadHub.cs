using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server.Models;

using Microsoft.AspNetCore.SignalR;

namespace KuriousLabs.Kurio.Server.Hubs;

/// <summary>
///     SignalR hub for real-time download updates and operations.
/// </summary>
public class DownloadHub : Hub
{
    private readonly IDownloadEngine _engine;
    private readonly ILogger<DownloadHub> _logger;

    public DownloadHub(
        IDownloadEngine engine,
        ILogger<DownloadHub> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    ///     Subscribe to progress updates for a specific task or all tasks.
    /// </summary>
    /// <param name="taskId">Optional task ID to filter progress updates. If null, receives all progress.</param>
    public async Task SubscribeToProgress(Guid? taskId = null)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} subscribed to progress for task {TaskId}",
            connectionId, taskId?.ToString() ?? "all");

        // Note: Progress streaming is handled by ProgressBroadcaster
        await Clients.Caller.SendAsync("Subscribed", taskId);
    }

    /// <summary>
    ///     Unsubscribe from progress updates.
    /// </summary>
    public async Task UnsubscribeFromProgress()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} unsubscribed from progress", connectionId);

        await Clients.Caller.SendAsync("Unsubscribed");
    }

    /// <summary>
    ///     Get download details for a specific task.
    /// </summary>
    /// <param name="id">Download task ID.</param>
    /// <returns>Download details.</returns>
    public async Task<DownloadResponse> GetDownload(Guid id)
    {
        var task = _engine.GetDownload(id);
        if (task == null)
        {
            throw new HubException($"Download {id} not found");
        }

        return await Task.FromResult(DownloadResponse.FromTask(task));
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
