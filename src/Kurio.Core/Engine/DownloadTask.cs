namespace Kurio.Core.Engine;

using Kurio.Core.Abstractions;
using Kurio.Core.Models;

/// <summary>
/// Concrete implementation of a download task.
/// </summary>
internal sealed class DownloadTask : IDownloadTask
{
    private DownloadPriority _priority;

    /// <inheritdoc />
    public Guid Id { get; set; }

    /// <inheritdoc />
    public Uri Url { get; init; }

    /// <inheritdoc />
    public string FileName { get; set; }

    /// <inheritdoc />
    public long FileSize { get; set; }

    /// <inheritdoc />
    public DownloadState State { get; set; }

    /// <inheritdoc />
    public DownloadPriority Priority
    {
        get => _priority;
        set
        {
            if (_priority != value)
            {
                _priority = value;
                PriorityChanged?.Invoke(this, value);
            }
        }
    }

    /// <inheritdoc />
    public DownloadProgress Progress { get; set; }

    /// <inheritdoc />
    public DownloadOptions Options { get; init; }

    /// <inheritdoc />
    public ResourceMetadata Metadata { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAt { get; init; }

    /// <inheritdoc />
    public DateTime? StartedAt { get; set; }

    /// <inheritdoc />
    public DateTime? CompletedAt { get; set; }

    /// <inheritdoc />
    public DownloadError? LastError { get; set; }

    /// <inheritdoc />
    public int RetryCount { get; set; }

    /// <summary>
    /// Event raised when the priority changes.
    /// </summary>
    public event EventHandler<DownloadPriority>? PriorityChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadTask"/> class.
    /// </summary>
    public DownloadTask(Uri url, DownloadOptions options)
    {
        Id = Guid.NewGuid();
        Url = url ?? throw new ArgumentNullException(nameof(url));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        FileName = options.FileName ?? Path.GetFileName(url.LocalPath);
        State = DownloadState.Created;
        _priority = DownloadPriority.Normal;
        Progress = new DownloadProgress();
        Metadata = new ResourceMetadata();
        CreatedAt = DateTime.UtcNow;
    }
}
