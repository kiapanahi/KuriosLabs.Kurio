using Kurio.Core.Abstractions;
using Spectre.Console;

namespace KuriousLabs.Kurio.Cli.UI;

/// <summary>
/// Main menu for the Kurio TUI.
/// </summary>
public sealed class MainMenu
{
    private readonly IDownloadEngine _downloadEngine;
    private readonly DownloadListView _downloadListView;
    private readonly AddDownloadView _addDownloadView;
    private readonly StatisticsView _statisticsView;
    private readonly SettingsView _settingsView;

    public MainMenu(
        IDownloadEngine downloadEngine,
        DownloadListView downloadListView,
        AddDownloadView addDownloadView,
        StatisticsView statisticsView,
        SettingsView settingsView)
    {
        _downloadEngine = downloadEngine ?? throw new ArgumentNullException(nameof(downloadEngine));
        _downloadListView = downloadListView ?? throw new ArgumentNullException(nameof(downloadListView));
        _addDownloadView = addDownloadView ?? throw new ArgumentNullException(nameof(addDownloadView));
        _statisticsView = statisticsView ?? throw new ArgumentNullException(nameof(statisticsView));
        _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
    }

    /// <summary>
    /// Shows the main menu and handles navigation.
    /// </summary>
    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.Clear();
            ShowHeader();

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[blue]Select an option:[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "📥 Downloads",
                        "➕ Add Download",
                        "📊 Statistics",
                        "⚙️  Settings",
                        "❌ Exit"
                    }));

            try
            {
                await HandleChoiceAsync(choice, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AnsiConsole.WriteException(ex);
                AnsiConsole.MarkupLine("\n[red]Press any key to continue...[/]");
                Console.ReadKey(true);
            }
            
            if (choice == "❌ Exit")
            {
                break;
            }
        }
    }

    private async Task HandleChoiceAsync(string choice, CancellationToken cancellationToken)
    {
        switch (choice)
        {
            case "📥 Downloads":
                await _downloadListView.ShowAsync(cancellationToken);
                break;
            case "➕ Add Download":
                await _addDownloadView.ShowAsync(cancellationToken);
                break;
            case "📊 Statistics":
                await _statisticsView.ShowAsync(cancellationToken);
                break;
            case "⚙️  Settings":
                await _settingsView.ShowAsync(cancellationToken);
                break;
            case "❌ Exit":
                if (AnsiConsole.Confirm("Are you sure you want to exit?"))
                {
                    var (active, queued) = _downloadEngine.GetQueueStatistics();
                    if (active > 0)
                    {
                        if (AnsiConsole.Confirm($"[yellow]{active} downloads are still active. Pause them before exiting?[/]"))
                        {
                            await _downloadEngine.PauseAllAsync(cancellationToken);
                            AnsiConsole.MarkupLine("[green]All downloads paused.[/]");
                        }
                    }
                }
                break;
        }
    }

    private static void ShowHeader()
    {
        var rule = new Rule("[blue]Kurio Download Manager[/]")
        {
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
}
