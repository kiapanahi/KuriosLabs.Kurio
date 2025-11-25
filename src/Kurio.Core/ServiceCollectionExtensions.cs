namespace Kurio.Core;

using Microsoft.Extensions.DependencyInjection;
using Kurio.Core.Abstractions;
using Kurio.Core.Engine;
using Kurio.Core.Protocols;
using Kurio.Core.Storage;

/// <summary>
/// Extension methods for configuring Kurio download engine services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Kurio download engine services to the dependency injection container.
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

        // Register segment manager
        services.AddTransient<ISegmentManager, SegmentManager>();

        // Register protocol handlers
        services.AddSingleton<IProtocolHandler, HttpProtocolHandler>();
        
        // Register protocol handler factory
        services.AddSingleton<IProtocolHandlerFactory>(sp =>
        {
            var handlers = sp.GetServices<IProtocolHandler>();
            return new ProtocolHandlerFactory(handlers);
        });

        // Configure HttpClient for downloads
        services.AddHttpClient("KurioDownloader", client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "*/*");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 8,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        });

        // Register download engine as singleton
        services.AddSingleton<IDownloadEngine>(sp =>
        {
            var protocolHandler = sp.GetRequiredService<IProtocolHandler>();
            var storageManager = sp.GetRequiredService<IStorageManager>();
            var segmentManager = sp.GetRequiredService<ISegmentManager>();

            return new DownloadEngine(
                protocolHandler,
                storageManager,
                segmentManager,
                maxConcurrentDownloads);
        });

        return services;
    }

    /// <summary>
    /// Adds the Kurio download engine services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKurioDownloadEngine(this IServiceCollection services)
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tempDirectory = Path.Combine(homeDirectory, ".kurio", "temp");
        var stateDirectory = Path.Combine(homeDirectory, ".kurio", "state");

        return AddKurioDownloadEngine(services, tempDirectory, stateDirectory);
    }
}
