using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using KuriousLabs.Kurio.Avalonia.Services;
using KuriousLabs.Kurio.Avalonia.ViewModels;
using KuriousLabs.Kurio.Avalonia.Views;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KuriousLabs.Kurio.Avalonia;

public class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", false)
            .Build();

        // Setup DI
        ServiceCollection services = new();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var serverUrl = configuration["ServerUrl"] ?? "http://localhost:5205";
        services.AddHttpClient<IKurioApiClient, KurioApiClient>(client =>
        {
            client.BaseAddress = new Uri(serverUrl);
        });
        services.AddSingleton<IKurioApiClient>(sp =>
        {
            var httpClient = sp.GetRequiredService<HttpClient>();
            var logger = sp.GetRequiredService<ILogger<KurioApiClient>>();
            return new KurioApiClient(httpClient, logger, serverUrl);
        });

        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var apiClient = Services.GetRequiredService<IKurioApiClient>();
            desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel(apiClient) };

            // Connect to server on startup
            _ = Task.Run(async () =>
            {
                try
                {
                    await apiClient.ConnectAsync();
                }
                catch (Exception ex)
                {
                    var logger = Services.GetRequiredService<ILogger<App>>();
                    logger.LogWarning(ex, "Failed to connect to server on startup");
                }
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
