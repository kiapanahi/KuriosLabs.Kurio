using System.ComponentModel.DataAnnotations;

namespace KuriousLabs.Kurio.Server.Models;

/// <summary>
///     Request to update speed limit configuration.
/// </summary>
public sealed class UpdateSpeedLimitRequest
{
    /// <summary>
    ///     Enable or disable bandwidth limiting.
    /// </summary>
    [Required]
    public bool Enabled { get; set; }

    /// <summary>
    ///     Maximum download speed in bytes per second (0 = unlimited).
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MaxDownloadSpeedBytesPerSecond { get; set; }

    /// <summary>
    ///     Maximum upload speed in bytes per second (0 = unlimited).
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MaxUploadSpeedBytesPerSecond { get; set; }
}

/// <summary>
///     Response containing current speed limit configuration.
/// </summary>
public sealed class SpeedLimitResponse
{
    /// <summary>
    ///     Whether bandwidth limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Maximum download speed in bytes per second (0 = unlimited).
    /// </summary>
    public long MaxDownloadSpeedBytesPerSecond { get; set; }

    /// <summary>
    ///     Maximum upload speed in bytes per second (0 = unlimited).
    /// </summary>
    public long MaxUploadSpeedBytesPerSecond { get; set; }

    /// <summary>
    ///     Current effective download speed limit in bytes per second.
    /// </summary>
    public long CurrentLimitBytesPerSecond { get; set; }
}
