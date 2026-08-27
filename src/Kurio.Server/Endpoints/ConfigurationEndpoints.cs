using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Configuration;
using KuriousLabs.Kurio.Server.Models;

using Microsoft.AspNetCore.Http.HttpResults;

namespace KuriousLabs.Kurio.Server.Endpoints;

/// <summary>
///     Minimal-API endpoints for reading and updating configuration settings.
/// </summary>
internal static class ConfigurationEndpoints
{
    /// <summary>
    ///     Maps the <c>/api/config</c> endpoints onto the supplied route builder.
    /// </summary>
    /// <param name="endpoints">The route builder to map onto.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/config")
            .WithTags("Configuration");

        group.MapGet("/speed-limit", GetSpeedLimit)
            .WithName("GetSpeedLimit")
            .Produces<SpeedLimitResponse>(StatusCodes.Status200OK);

        group.MapPut("/speed-limit", UpdateSpeedLimitAsync)
            .WithName("UpdateSpeedLimit")
            .Produces<SpeedLimitResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", GetConfiguration)
            .WithName("GetConfiguration")
            .Produces<KurioConfiguration>(StatusCodes.Status200OK);

        return endpoints;
    }

    /// <summary>
    ///     Gets the current speed limit configuration.
    /// </summary>
    private static Ok<SpeedLimitResponse> GetSpeedLimit(
        IConfigurationService configService,
        ISpeedLimiter speedLimiter)
    {
        var config = configService.GetConfiguration();
        SpeedLimitResponse response = new()
        {
            Enabled = config.Network.BandwidthLimit.Enabled,
            MaxDownloadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxDownloadSpeed,
            MaxUploadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxUploadSpeed,
            CurrentLimitBytesPerSecond = speedLimiter.MaxBytesPerSecond
        };

        return TypedResults.Ok(response);
    }

    /// <summary>
    ///     Updates the speed limit configuration and applies it to the running limiter.
    /// </summary>
    private static async Task<IResult> UpdateSpeedLimitAsync(
        UpdateSpeedLimitRequest request,
        IConfigurationService configService,
        ISpeedLimiter speedLimiter,
        ILogger<ConfigurationEndpointsLogCategory> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.MaxDownloadSpeedBytesPerSecond < 0)
            {
                return ApiResults.Problem(
                    httpContext,
                    "Download speed limit cannot be negative",
                    StatusCodes.Status400BadRequest,
                    "Invalid speed limit");
            }

            if (request.MaxUploadSpeedBytesPerSecond < 0)
            {
                return ApiResults.Problem(
                    httpContext,
                    "Upload speed limit cannot be negative",
                    StatusCodes.Status400BadRequest,
                    "Invalid speed limit");
            }

            var config = configService.GetConfiguration();
            config.Network.BandwidthLimit.Enabled = request.Enabled;
            config.Network.BandwidthLimit.MaxDownloadSpeed = request.MaxDownloadSpeedBytesPerSecond;
            config.Network.BandwidthLimit.MaxUploadSpeed = request.MaxUploadSpeedBytesPerSecond;

            await configService.UpdateConfigurationAsync(c =>
            {
                c.Network.BandwidthLimit.Enabled = request.Enabled;
                c.Network.BandwidthLimit.MaxDownloadSpeed = request.MaxDownloadSpeedBytesPerSecond;
                c.Network.BandwidthLimit.MaxUploadSpeed = request.MaxUploadSpeedBytesPerSecond;
            }, cancellationToken).ConfigureAwait(false);

            // Update the running speed limiter immediately (applied to active downloads without restart)
            var newMaxSpeed = request.Enabled ? request.MaxDownloadSpeedBytesPerSecond : 0;
            speedLimiter.UpdateMaxSpeed(newMaxSpeed);

            logger.LogSpeedLimitUpdated(
                request.Enabled,
                request.MaxDownloadSpeedBytesPerSecond,
                request.MaxUploadSpeedBytesPerSecond);

            SpeedLimitResponse response = new()
            {
                Enabled = config.Network.BandwidthLimit.Enabled,
                MaxDownloadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxDownloadSpeed,
                MaxUploadSpeedBytesPerSecond = config.Network.BandwidthLimit.MaxUploadSpeed,
                CurrentLimitBytesPerSecond = request.Enabled ? request.MaxDownloadSpeedBytesPerSecond : 0
            };

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogSpeedLimitUpdateFailed(ex);
            // Never echo unexpected exception details to the client
            return ApiResults.Problem(
                httpContext,
                "An unexpected error occurred while updating the speed limit.",
                StatusCodes.Status500InternalServerError,
                "Failed to update speed limit");
        }
    }

    /// <summary>
    ///     Gets the full configuration.
    /// </summary>
    private static Ok<KurioConfiguration> GetConfiguration(IConfigurationService configService)
    {
        var config = configService.GetConfiguration();
        return TypedResults.Ok(config);
    }
}
