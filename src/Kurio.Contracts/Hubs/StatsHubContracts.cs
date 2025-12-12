using KuriousLabs.Kurio.Contracts.Stats;

namespace KuriousLabs.Kurio.Contracts.Hubs;

public interface IStatsHub
{
    Task SubscribeStatsAsync();

    Task UnsubscribeStatsAsync();

    Task RequestStatsSnapshotAsync();
}

public interface IStatsClient
{
    Task StatsSnapshotAsync(StatsSnapshot snapshot);

    Task StatsUpdatedAsync(StatsSnapshot snapshot);

    Task AlertRaisedAsync(Alert alert);
}
