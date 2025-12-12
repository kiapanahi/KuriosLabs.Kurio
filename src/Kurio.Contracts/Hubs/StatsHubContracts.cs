using KuriousLabs.Kurio.Contracts.Stats;

namespace KuriousLabs.Kurio.Contracts.Hubs;

public interface IStatsHub
{
    Task SubscribeStatsAsync(CancellationToken cancellationToken = default);

    Task UnsubscribeStatsAsync(CancellationToken cancellationToken = default);

    Task RequestStatsSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IStatsClient
{
    Task StatsSnapshotAsync(StatsSnapshot snapshot);

    Task StatsUpdatedAsync(StatsSnapshot snapshot);

    Task AlertRaisedAsync(Alert alert);
}
