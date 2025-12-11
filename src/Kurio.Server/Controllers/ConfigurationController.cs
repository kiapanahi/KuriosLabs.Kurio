using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Configuration;
using KuriousLabs.Kurio.Server.Models;

using Microsoft.AspNetCore.Mvc;

namespace KuriousLabs.Kurio.Server.Controllers;

/// <summary>
///     API controller for managing configuration settings.
/// </summary>
[ApiController]
[Route("api/config")]
[Produces("application/json")]
public class ConfigurationController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly ISpeedLimiter _speedLimiter;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IConfigurationService configService,
        ISpeedLimiter speedLimiter,
        ILogger<ConfigurationController> logger)
    {
        _configService = configService;
        _speedLimiter = speedLimiter;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the current speed limit configuration.
    /// </summary>
    /// <returns>Speed limit settings.</returns>
    [HttpGet("speed-limit")]
    [ProducesResponseType(typeof(SpeedLimitResponse), StatusCodes.Status200OK)]
    public ActionResult<SpeedLimitResponse> GetSpeedLimit()
    {
        var config = _configService.GetConfiguration();
        var response = new SpeedLimitResponse
        {
            Enabled = config.Network.BandwidthLimit.Enabled,
            MaxDownloadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxDownloadSpeed,
            MaxUploadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxUploadSpeed,
            CurrentLimitBytesPerSecond = _speedLimiter.MaxBytesPerSecond
        };

        return Ok(response);
    }

    /// <summary>
    ///     Updates the speed limit configuration.
    /// </summary>
    /// <param name="request">Speed limit settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated speed limit settings.</returns>
    [HttpPut("speed-limit")]
    [ProducesResponseType(typeof(SpeedLimitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SpeedLimitResponse>> UpdateSpeedLimit(
        [FromBody] UpdateSpeedLimitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.MaxDownloadSpeedBytesPerSecond < 0)
            {
                return Problem(
                    "Download speed limit cannot be negative",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid speed limit");
            }

            if (request.MaxUploadSpeedBytesPerSecond < 0)
            {
                return Problem(
                    "Upload speed limit cannot be negative",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid speed limit");
            }

            var config = _configService.GetConfiguration();
            config.Network.BandwidthLimit.Enabled = request.Enabled;
            config.Network.BandwidthLimit.MaxDownloadSpeed = request.MaxDownloadSpeedBytesPerSecond;
            config.Network.BandwidthLimit.MaxUploadSpeed = request.MaxUploadSpeedBytesPerSecond;

            await _configService.UpdateConfigurationAsync(c =>
            {
                c.Network.BandwidthLimit.Enabled = request.Enabled;
                c.Network.BandwidthLimit.MaxDownloadSpeed = request.MaxDownloadSpeedBytesPerSecond;
                c.Network.BandwidthLimit.MaxUploadSpeed = request.MaxUploadSpeedBytesPerSecond;
            }, cancellationToken).ConfigureAwait(false);

            // Update the running speed limiter immediately (applied to active downloads without restart)
            var newMaxSpeed = request.Enabled ? request.MaxDownloadSpeedBytesPerSecond : 0;
            _speedLimiter.UpdateMaxSpeed(newMaxSpeed);

            _logger.LogInformation(
                "Speed limit updated: Enabled={Enabled}, DownloadSpeed={DownloadSpeed} B/s, UploadSpeed={UploadSpeed} B/s (applied immediately to active downloads)",
                request.Enabled,
                request.MaxDownloadSpeedBytesPerSecond,
                request.MaxUploadSpeedBytesPerSecond);

            var response = new SpeedLimitResponse
            {
                Enabled = config.Network.BandwidthLimit.Enabled,
                MaxDownloadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxDownloadSpeed,
                MaxUploadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxUploadSpeed,
                CurrentLimitBytesPerSecond = request.Enabled ? request.MaxDownloadSpeedBytesPerSecond : 0
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update speed limit configuration");
            return Problem(
                ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to update speed limit");
        }
    }

    /// <summary>
    ///     Gets the full configuration.
    /// </summary>
    /// <returns>Complete configuration settings.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(KurioConfiguration), StatusCodes.Status200OK)]
    public ActionResult<KurioConfiguration> GetConfiguration()
    {
        var config = _configService.GetConfiguration();
        return Ok(config);
    }
}
