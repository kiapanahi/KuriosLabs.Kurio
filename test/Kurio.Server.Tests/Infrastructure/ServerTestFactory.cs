using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Server;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using CoreModels = KuriousLabs.Kurio.Core.Models;

namespace Kurio.Server.Tests;

/// <summary>
///     Boots the real <see cref="Program" /> pipeline in-memory with the engine, queue and
///     statistics services swapped for fakes.
/// </summary>
/// <remarks>
///     Prefer creating a fresh instance per test class (or per test) over sharing one through
///     <c>IClassFixture</c>: the fakes are mutable and the host is built lazily on the first
///     <c>CreateClient()</c> call, so configuration applied after that point is ignored.
/// </remarks>
public sealed class ServerTestFactory : WebApplicationFactory<Program>
{
    private readonly List<Action<IServiceCollection>> _configurators = [];
    private readonly FakeQueueManager _queueManager = new();
    private readonly FakeStatisticsService _statisticsService = new();

    /// <summary>Mutates the fake queue manager that backs the queue endpoints.</summary>
    public ServerTestFactory WithQueue(Action<FakeQueueManager> configure)
    {
        configure(_queueManager);
        return this;
    }

    /// <summary>Mutates the statistics snapshot returned by the fake statistics service.</summary>
    public ServerTestFactory WithStats(Action<CoreModels.DownloadStatistics> configure)
    {
        configure(_statisticsService.Statistics);
        return this;
    }

    /// <summary>
    ///     Registers additional service overrides. These run after the default fakes, so a
    ///     later registration of the same service type wins.
    /// </summary>
    public ServerTestFactory WithServices(Action<IServiceCollection> configure)
    {
        _configurators.Add(configure);
        return this;
    }

    /// <summary>Replaces a single service with the supplied instance.</summary>
    public ServerTestFactory WithService<TService>(TService instance)
        where TService : class
    {
        return WithServices(services =>
        {
            services.RemoveAll<TService>();
            services.AddSingleton(instance);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDownloadEngine>();
            services.RemoveAll<IDownloadQueueManager>();
            services.RemoveAll<IStatisticsService>();

            services.AddSingleton<IDownloadEngine, FakeDownloadEngine>();
            services.AddSingleton<IDownloadQueueManager>(_queueManager);
            services.AddSingleton<IStatisticsService>(_statisticsService);

            foreach (var configure in _configurators)
            {
                configure(services);
            }
        });

        return base.CreateHost(builder);
    }
}
