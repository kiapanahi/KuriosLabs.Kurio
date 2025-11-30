using System.Globalization;

using KuriousLabs.Kurio.Cli.Client;
using KuriousLabs.Kurio.Core.Models;

using Spectre.Console;

namespace KuriousLabs.Kurio.Cli.UI;

/// <summary>
///     View for displaying and managing downloads.
/// </summary>
public sealed class DownloadListView
{
    private readonly IKurioApiClient _apiClient;

    public DownloadListView(IKurioApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    /// <summary>
    ///     Shows the download list view.
    /// </summary>
    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.Clear();
            ShowHeader();

            var downloads =
                await _apiClient.GetDownloadsAsync(DownloadStateFilter.All, cancellationToken);

            if (downloads.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No downloads yet. Add your first download![/]");
                AnsiConsole.MarkupLine("\n[dim]Press any key to return...[/]");
                Console.ReadKey(true);
                return;
            }

            await DisplayDownloadsAsync(downloads, cancellationToken);

            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("\n[blue]Actions:[/]")
                    .AddChoices("▶️  Start Selected", "⏸️  Pause Selected", "🔄 Resume Selected", "❌ Cancel Selected",
                        "⬆️  Move Up", "⬇️  Move Down", "🔄 Refresh", "🗑️  Clear Completed", "⏸️  Pause All",
                        "⬅️  Back"));

            if (choice == "⬅️  Back")
            {
                return;
            }

            await HandleActionAsync(choice, downloads, cancellationToken);
        }
    }

    private async Task DisplayDownloadsAsync(List<DownloadResponse> downloads, CancellationToken cancellationToken)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[blue]#[/]").Centered())
            .AddColumn(new TableColumn("[blue]Status[/]"))
            .AddColumn(new TableColumn("[blue]Name[/]"))
            .AddColumn(new TableColumn("[blue]Progress[/]"))
            .AddColumn(new TableColumn("[blue]Speed[/]"))
            .AddColumn(new TableColumn("[blue]Size[/]"));

        for (var i = 0; i < downloads.Count; i++)
        {
            var download = downloads[i];
            var progress = download.Progress;

            var statusIcon = GetStatusIcon(download.State);
            var progressBar = CreateProgressBar(progress?.PercentComplete ?? 0);
            var speed = FormatSpeed(progress?.BytesPerSecond ?? 0);
            var size = FormatSize(download.FileSize);

            table.AddRow(
                (i + 1).ToString(CultureInfo.InvariantCulture),
                statusIcon,
                Markup.Escape(download.FileName ?? "Unknown"),
                progressBar,
                speed,
                size
            );
        }

        AnsiConsole.Write(table);

        var stats = await _apiClient.GetStatisticsAsync(cancellationToken);
        AnsiConsole.MarkupLine(
            $"\n[dim]Active: {stats.ActiveDownloads} | Queued: {stats.QueuedDownloads} | Total: {downloads.Count}[/]");
    }

    private static string GetStatusIcon(DownloadState state)
    {
        return state switch
        {
            DownloadState.Queued => "[yellow]⏳ Queued[/]",
            DownloadState.Downloading => "[green]⬇️  Downloading[/]",
            DownloadState.Paused => "[blue]⏸️  Paused[/]",
            DownloadState.Completed => "[green]✅ Completed[/]",
            DownloadState.Failed => "[red]❌ Failed[/]",
            DownloadState.Cancelled => "[grey]🚫 Cancelled[/]",
            _ => "[dim]❓ Unknown[/]"
        };
    }

    private static string CreateProgressBar(double percent)
    {
        var completed = (int)(percent / 10);
        var remaining = 10 - completed;
        return $"[green]{'█' * completed}[/][dim]{'░' * remaining}[/] {percent:F1}%";
    }

    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond == 0)
        {
            return "[dim]--[/]";
        }

        return bytesPerSecond switch
        {
            < 1024 => $"{bytesPerSecond} B/s",
            < 1024 * 1024 => $"{bytesPerSecond / 1024.0:F1} KB/s",
            < 1024 * 1024 * 1024 => $"{bytesPerSecond / (1024.0 * 1024.0):F1} MB/s",
            _ => $"{bytesPerSecond / (1024.0 * 1024.0 * 1024.0):F1} GB/s"
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes == 0)
        {
            return "[dim]Unknown[/]";
        }

        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }

    private async Task HandleActionAsync(string action, List<DownloadResponse> downloads,
        CancellationToken cancellationToken)
    {
        try
        {
            switch (action)
            {
                case "🔄 Refresh":
                    // Just loop back to refresh
                    break;

                case "🗑️  Clear Completed":
                    await _apiClient.ClearCompletedAsync(cancellationToken);
                    AnsiConsole.MarkupLine("[green]Completed downloads cleared.[/]");
                    await Task.Delay(1000, cancellationToken);
                    break;

                case "⏸️  Pause All":
                    var paused = await _apiClient.PauseAllAsync(cancellationToken);
                    AnsiConsole.MarkupLine($"[green]Paused {paused} downloads.[/]");
                    await Task.Delay(1000, cancellationToken);
                    break;

                default:
                    var selected = SelectDownload(downloads);
                    if (selected != null)
                    {
                        await ExecuteDownloadActionAsync(action, selected, cancellationToken);
                    }

                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AnsiConsole.WriteException(ex);
            AnsiConsole.MarkupLine("\n[red]Press any key to continue...[/]");
            Console.ReadKey(true);
        }
    }

    private static DownloadResponse? SelectDownload(List<DownloadResponse> downloads)
    {
        var choices = downloads
            .Select((d, i) => $"{i + 1}. {d.FileName ?? "Unknown"}")
            .Concat(new[] { "Cancel" })
            .ToArray();

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[blue]Select a download:[/]")
                .PageSize(15)
                .AddChoices(choices));

        if (selection == "Cancel")
        {
            return null;
        }

        var index = int.Parse(selection.Split('.')[0], CultureInfo.InvariantCulture) - 1;
        return downloads[index];
    }

    private async Task ExecuteDownloadActionAsync(string action, DownloadResponse download,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "▶️  Start Selected":
                await _apiClient.StartDownloadAsync(download.Id, cancellationToken);
                AnsiConsole.MarkupLine("[green]Download started.[/]");
                break;

            case "⏸️  Pause Selected":
                await _apiClient.PauseDownloadAsync(download.Id, cancellationToken);
                AnsiConsole.MarkupLine("[green]Download paused.[/]");
                break;

            case "🔄 Resume Selected":
                await _apiClient.ResumeDownloadAsync(download.Id, cancellationToken);
                AnsiConsole.MarkupLine("[green]Download resumed.[/]");
                break;

            case "❌ Cancel Selected":
                if (AnsiConsole.Confirm("Remove partial files?"))
                {
                    await _apiClient.CancelDownloadAsync(download.Id, true, cancellationToken);
                }
                else
                {
                    await _apiClient.CancelDownloadAsync(download.Id, false, cancellationToken);
                }

                AnsiConsole.MarkupLine("[green]Download cancelled.[/]");
                break;

            case "⬆️  Move Up":
                AnsiConsole.MarkupLine("[yellow]Priority management via API not yet implemented.[/]");
                break;

            case "⬇️  Move Down":
                AnsiConsole.MarkupLine("[yellow]Priority management via API not yet implemented.[/]");
                break;
        }

        await Task.Delay(1000, cancellationToken);
    }

    private static void ShowHeader()
    {
        Rule rule = new("[blue]Downloads[/]") { Justification = Justify.Left };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
}
