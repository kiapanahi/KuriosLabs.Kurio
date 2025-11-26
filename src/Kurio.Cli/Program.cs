using Kurio.Core;
using KuriousLabs.Kurio.Cli;
using KuriousLabs.Kurio.Cli.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace KuriousLabs.Kurio.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var host = CreateHostBuilder(args).Build();
            
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
                // Register Kurio Core services
                services.AddKurioDownloadEngine();
                
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
