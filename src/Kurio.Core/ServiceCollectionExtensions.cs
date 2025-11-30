using Kurio.Core.Queue;

using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Configuration;
using KuriousLabs.Kurio.Core.Engine;
using KuriousLabs.Kurio.Core.ErrorHandling;
using KuriousLabs.Kurio.Core.Models;
using KuriousLabs.Kurio.Core.Persistence;
using KuriousLabs.Kurio.Core.Protocols;
using KuriousLabs.Kurio.Core.Resilience;
using KuriousLabs.Kurio.Core.Statistics;
using KuriousLabs.Kurio.Core.Storage;
using KuriousLabs.Kurio.Core.Verification;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuriousLabs.Kurio.Core;

/// <summary>
///     Extension methods for configuring Kurio download engine services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the Kurio download engine services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="tempDirectory">The directory for temporary download files.</param>
    /// <param name="stateDirectory">The directory for state persistence files.</param>
    /// <param name="maxConcurrentDownloads">Maximum number of concurrent downloads (default: 3).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKurioDownloadEngine(
        this IServiceCollection services,
        string tempDirectory,
        string stateDirectory,
        int maxConcurrentDownloads = 3)
    {
        // Register storage manager as singleton with configured directories
        services.AddSingleton<IStorageManager>(sp =>
        {
            // Configure default storage options - can be overridden via configuration
            StorageOptions storageOptions = new()
            {
                Mode = StorageMode.PerSegmentFiles, // Use per-segment files to avoid file locking contention
                VerifyWrites = false, // Disabled by default for performance
                WriteBufferSize = 81920, // 80KB
                CleanupSegmentFiles = true
            };

            return new StorageManager(tempDirectory, stateDirectory, null, storageOptions);
        });

        // Register state persistence
        services.AddSingleton<IStatePersistence>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<JsonStatePersistence>>();
            return new JsonStatePersistence(stateDirectory, logger);
        });

        // Register segment verifier for checksum operations
        services.AddSingleton<ISegmentVerifier, SegmentVerifier>();

        // Register segment manager with resilience support
        services.AddTransient<ISegmentManager>(sp =>
        {
            var storageManager = sp.GetRequiredService<IStorageManager>();
            var segmentVerifier = sp.GetRequiredService<ISegmentVerifier>();
            var logger = sp.GetService<ILogger<SegmentManager>>();
            var resiliencePolicyFactory = sp.GetRequiredService<ResiliencePolicyFactory>();
            var connectionOptions = sp.GetRequiredService<IOptions<ConnectionResilienceOptions>>().Value;

            return new SegmentManager(
                storageManager,
                segmentVerifier,
                logger,
                resiliencePolicyFactory,
                connectionOptions);
        });

        // Register checksum verifier
        services.AddSingleton<IChecksumVerifier, ChecksumVerifier>();

        // Register resilience services (Polly-based)
        services.AddSingleton<ResiliencePolicyFactory>();

        // Register connection resilience options
        services.AddOptions<ConnectionResilienceOptions>()
            .Configure(options =>
            {
                // Set default values
                options.MaxRetryAttempts = 5;
                options.InitialRetryDelaySeconds = 2;
                options.MaxRetryDelaySeconds = 60;
                options.NetworkHealthCheckIntervalSeconds = 30;
                options.StallDetectionTimeoutSeconds = 30;
                options.EnableConnectionMonitoring = true;
                options.EnableAdaptiveBackoff = true;
                options.EnableCircuitBreaker = true;
            });

        // Register connection health monitor
        services.AddSingleton<IConnectionHealthMonitor, ConnectionHealthMonitor>();

        // Configure HttpClient for health checks
        services.AddHttpClient("KurioHealthCheck", client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("User-Agent", "Kurio-HealthCheck/1.0");
        });

        // Register error handling services
        services.AddSingleton<IErrorClassifier, ErrorClassifier>();

        // Register protocol handlers
        services.AddSingleton<IProtocolHandler, HttpProtocolHandler>();

        // Register protocol handler factory
        services.AddSingleton<IProtocolHandlerFactory>(sp =>
        {
            var handlers = sp.GetServices<IProtocolHandler>();
            return new ProtocolHandlerFactory(handlers);
        });

        // Configure HttpClient for downloads
        services.AddHttpClient("KurioDownloader", client => client.DefaultRequestHeaders.Add("Accept", "*/*"))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                MaxConnectionsPerServer = 8,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            });

        // Register queue manager
        services.AddSingleton<IDownloadQueueManager>(sp =>
            new DownloadQueueManager { MaxConcurrentDownloads = maxConcurrentDownloads });

        // Register progress tracker
        services.AddSingleton<IProgressTracker, ProgressTracker>();

        // Register download history repository
        var historyDirectory = Path.Combine(stateDirectory, "history");
        services.AddSingleton<IDownloadHistoryRepository>(sp =>
        {
            var logger =
                sp.GetRequiredService<ILogger<JsonDownloadHistoryRepository>>();
            return new JsonDownloadHistoryRepository(historyDirectory, logger);
        });

        // Register statistics service
        services.AddSingleton<IStatisticsService>(sp =>
        {
            var historyRepository = sp.GetRequiredService<IDownloadHistoryRepository>();
            var logger = sp.GetRequiredService<ILogger<StatisticsService>>();
            return new StatisticsService(stateDirectory, historyRepository, logger);
        });

        // Register download engine as singleton
        services.AddSingleton<IDownloadEngine>(sp =>
        {
            var protocolHandler = sp.GetRequiredService<IProtocolHandler>();
            var storageManager = sp.GetRequiredService<IStorageManager>();
            var segmentManager = sp.GetRequiredService<ISegmentManager>();
            var statePersistence = sp.GetRequiredService<IStatePersistence>();
            var queueManager = sp.GetRequiredService<IDownloadQueueManager>();
            var logger = sp.GetRequiredService<ILogger<DownloadEngine>>();

            return new DownloadEngine(
                protocolHandler,
                storageManager,
                segmentManager,
                statePersistence,
                logger,
                queueManager,
                maxConcurrentDownloads);
        });

        return services;
    }

    /// <summary>
    ///     Adds the Kurio download engine services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKurioDownloadEngine(this IServiceCollection services)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tempDirectory = Path.Combine(homeDirectory, ".kurio", "temp");
        var stateDirectory = Path.Combine(homeDirectory, ".kurio", "state");

        return services.AddKurioDownloadEngine(tempDirectory, stateDirectory);
    }

    /// <summary>
    ///     Adds configuration management services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configFilePath">Path to configuration file (optional, uses default if not specified).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKurioConfiguration(
        this IServiceCollection services,
        string? configFilePath = null)
    {
        services.AddSingleton<IPlatformPathProvider, PlatformPathProvider>();

        services.AddSingleton<IConfigurationService>(sp =>
        {
            var pathProvider = sp.GetRequiredService<IPlatformPathProvider>();
            var logger = sp.GetRequiredService<ILogger<ConfigurationService>>();

            var defaultPath = Path.Combine(
                pathProvider.GetAppDataDirectory(),
                "config.json");

            return new ConfigurationService(
                configFilePath ?? defaultPath,
                logger);
        });

        return services;
    }

    /// <summary>
    ///     Adds storage and file management services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKurioStorage(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformPathProvider, PlatformPathProvider>();

        services.AddSingleton<ITempFileCleanupService>(sp =>
        {
            var pathProvider = sp.GetRequiredService<IPlatformPathProvider>();
            var logger = sp.GetRequiredService<ILogger<TempFileCleanupService>>();

            return new TempFileCleanupService(
                pathProvider.GetTempDirectory(),
                logger);
        });

        return services;
    }
}
