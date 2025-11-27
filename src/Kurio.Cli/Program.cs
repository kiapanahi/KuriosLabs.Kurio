using KuriousLabs.Kurio.Cli;
using KuriousLabs.Kurio.Cli.Client;
using KuriousLabs.Kurio.Cli.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KuriousLabs.Kurio.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args).Build();
            
            // Get API client and connect to server
            var apiClient = host.Services.GetRequiredService<IKurioApiClient>();
            var serverUrl = host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Kurio:ServerUrl"] 
                ?? "http://localhost:5205";
            
            try
            {
                AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .Start($"Connecting to Kurio server at {serverUrl}...", ctx =>
                    {
                        apiClient.ConnectAsync().GetAwaiter().GetResult();
                    });
                
                AnsiConsole.MarkupLine("[green]✓[/] Connected to server");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed to connect to Kurio server at {serverUrl}[/]");
                AnsiConsole.WriteException(ex);
                return 1;
            }
            
            // Get the main application and run it
            var app = host.Services.GetRequiredService<KurioCliApplication>();
            await app.RunAsync();
            
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Get server URL from configuration
                var serverUrl = context.Configuration["Kurio:ServerUrl"] ?? "http://localhost:5205";
                
                // Register HTTP client with JSON options matching the server
                services.AddHttpClient<IKurioApiClient>(client =>
                {
                    client.BaseAddress = new Uri(serverUrl);
                    client.Timeout = TimeSpan.FromSeconds(30);
                })
                .ConfigureHttpClient((sp, client) =>
                {
                    // Configure JSON serialization options to match server
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                });
                
                // Register API client as singleton
                services.AddSingleton<IKurioApiClient>(sp =>
                {
                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var httpClient = httpClientFactory.CreateClient(typeof(IKurioApiClient).FullName!);
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KurioApiClient>>();
                    return new KurioApiClient(httpClient, logger, serverUrl);
                });
                
                // Register CLI application
                services.AddSingleton<KurioCliApplication>();
                
                // Register UI components
                services.AddSingleton<MainMenu>();
                services.AddSingleton<DownloadListView>();
                services.AddSingleton<AddDownloadView>();
                services.AddSingleton<StatisticsView>();
                services.AddSingleton<SettingsView>();
            });
    }
}
