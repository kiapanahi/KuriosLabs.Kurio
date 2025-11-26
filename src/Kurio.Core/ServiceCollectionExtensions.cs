using Kurio.Core.Abstractions;
using Kurio.Core.Engine;
using Kurio.Core.ErrorHandling;
using Kurio.Core.Models;
using Kurio.Core.Persistence;
using Kurio.Core.Protocols;
using Kurio.Core.Queue;
using Kurio.Core.Statistics;
using Kurio.Core.Storage;
using Kurio.Core.Verification;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kurio.Core;

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
                new StorageManager(tempDirectory, stateDirectory));

            // Register state persistence
            services.AddSingleton<IStatePersistence>(sp =>
            {
                ILogger<JsonStatePersistence> logger = sp.GetRequiredService<ILogger<JsonStatePersistence>>();
                return new JsonStatePersistence(stateDirectory, logger);
            });

            // Register segment manager
            services.AddTransient<ISegmentManager, SegmentManager>();

            // Register checksum verifier
            services.AddSingleton<IChecksumVerifier, ChecksumVerifier>();

            // Register error handling services
            services.AddSingleton<IRetryHandler, RetryHandler>();
            services.AddSingleton<IErrorClassifier, ErrorClassifier>();
            services.AddSingleton<CircuitBreakerFactory>(sp =>
            {
                ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new CircuitBreakerFactory(CircuitBreakerPolicy.Default, loggerFactory);
            });

            // Register protocol handlers
            services.AddSingleton<IProtocolHandler, HttpProtocolHandler>();

            // Register protocol handler factory
            services.AddSingleton<IProtocolHandlerFactory>(sp =>
            {
                IEnumerable<IProtocolHandler> handlers = sp.GetServices<IProtocolHandler>();
                return new ProtocolHandlerFactory(handlers);
            });

            // Configure HttpClient for downloads
            services.AddHttpClient("KurioDownloader", client => { client.DefaultRequestHeaders.Add("Accept", "*/*"); })
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
            string historyDirectory = Path.Combine(stateDirectory, "history");
            services.AddSingleton<IDownloadHistoryRepository>(sp =>
            {
                ILogger<JsonDownloadHistoryRepository> logger =
                    sp.GetRequiredService<ILogger<JsonDownloadHistoryRepository>>();
                return new JsonDownloadHistoryRepository(historyDirectory, logger);
            });

            // Register statistics service
            services.AddSingleton<IStatisticsService>(sp =>
            {
                IDownloadHistoryRepository historyRepository = sp.GetRequiredService<IDownloadHistoryRepository>();
                ILogger<StatisticsService> logger = sp.GetRequiredService<ILogger<StatisticsService>>();
                return new StatisticsService(stateDirectory, historyRepository, logger);
            });

            // Register download engine as singleton
            services.AddSingleton<IDownloadEngine>(sp =>
            {
                IProtocolHandler protocolHandler = sp.GetRequiredService<IProtocolHandler>();
                IStorageManager storageManager = sp.GetRequiredService<IStorageManager>();
                ISegmentManager segmentManager = sp.GetRequiredService<ISegmentManager>();
                IStatePersistence statePersistence = sp.GetRequiredService<IStatePersistence>();
                IDownloadQueueManager queueManager = sp.GetRequiredService<IDownloadQueueManager>();

                return new DownloadEngine(
                    protocolHandler,
                    storageManager,
                    segmentManager,
                    statePersistence,
                    queueManager,
                    maxConcurrentDownloads);
            });

            return services;
        }

        return services;
    }

    /// <summary>
    ///     Adds the Kurio download engine services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKurioDownloadEngine(this IServiceCollection services)
    {
        string homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string tempDirectory = Path.Combine(homeDirectory, ".kurio", "temp");
        string stateDirectory = Path.Combine(homeDirectory, ".kurio", "state");

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
        services.AddSingleton<Configuration.IPlatformPathProvider, Configuration.PlatformPathProvider>();
        
        services.AddSingleton<Configuration.IConfigurationService>(sp =>
        {
            var pathProvider = sp.GetRequiredService<Configuration.IPlatformPathProvider>();
            var logger = sp.GetRequiredService<ILogger<Configuration.ConfigurationService>>();
            
            var defaultPath = Path.Combine(
                pathProvider.GetAppDataDirectory(),
                "config.json");
            
            return new Configuration.ConfigurationService(
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
        services.AddSingleton<Storage.IPlatformPathProvider, Storage.PlatformPathProvider>();
        
        services.AddSingleton<Storage.ITempFileCleanupService>(sp =>
        {
            var pathProvider = sp.GetRequiredService<Storage.IPlatformPathProvider>();
            var logger = sp.GetRequiredService<ILogger<Storage.TempFileCleanupService>>();
            
            return new Storage.TempFileCleanupService(
                pathProvider.GetTempDirectory(),
                logger);
        });

        return services;
    }
}
