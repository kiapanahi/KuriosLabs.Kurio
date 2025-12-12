namespace KuriousLabs.Kurio.Web.Services;

public sealed class KurioServerOptions
{
    public Uri BaseUrl { get; init; } = new("https://localhost:5001");

    public HubEndpoints Hubs { get; init; } = new();

    public AuthenticationOptions? Authentication { get; init; }

    public sealed class HubEndpoints
    {
        public string Downloads { get; init; } = "/hubs/downloads";
        public string Queue { get; init; } = "/hubs/queue";
        public string Stats { get; init; } = "/hubs/stats";
    }

    public sealed class AuthenticationOptions
    {
        public bool Enabled { get; init; }
        public string? ApiKey { get; init; }
    }
}
