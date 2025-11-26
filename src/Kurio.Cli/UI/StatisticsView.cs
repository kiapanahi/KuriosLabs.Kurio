using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using Spectre.Console;

namespace KuriousLabs.Kurio.Cli.UI;

/// <summary>
/// View for displaying download statistics.
/// </summary>
public sealed class StatisticsView
{
    private readonly IDownloadEngine _downloadEngine;
    private readonly IStatisticsService? _statisticsService;

    public StatisticsView(
        IDownloadEngine downloadEngine,
        IStatisticsService? statisticsService = null)
    {
        _downloadEngine = downloadEngine ?? throw new ArgumentNullException(nameof(downloadEngine));
        _statisticsService = statisticsService;
    }

    /// <summary>
    /// Shows the statistics view.
    /// </summary>
    public Task ShowAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        ShowHeader();

        var downloads = _downloadEngine.GetDownloads(DownloadStateFilter.All).ToList();

        var activeCount = downloads.Count(d => d.State == DownloadState.Downloading);
        var queuedCount = downloads.Count(d => d.State == DownloadState.Queued);
        var pausedCount = downloads.Count(d => d.State == DownloadState.Paused);
        var completedCount = downloads.Count(d => d.State == DownloadState.Completed);
        var failedCount = downloads.Count(d => d.State == DownloadState.Failed);

        var totalBytesDownloaded = downloads.Sum(d => d.Progress.BytesDownloaded);
        var totalBytes = downloads.Sum(d => d.Progress.TotalBytes);
        var currentSpeed = downloads
            .Where(d => d.State == DownloadState.Downloading)
            .Sum(d => d.Progress.BytesPerSecond);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[blue]Metric[/]"))
            .AddColumn(new TableColumn("[blue]Value[/]"));

        table.AddRow("📥 Total Downloads", downloads.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        table.AddRow("✅ Completed", $"[green]{completedCount}[/]");
        table.AddRow("⬇️  Active", $"[green]{activeCount}[/]");
        table.AddRow("⏳ Queued", $"[yellow]{queuedCount}[/]");
        table.AddRow("⏸️  Paused", $"[blue]{pausedCount}[/]");
        table.AddRow("❌ Failed", $"[red]{failedCount}[/]");
        table.AddEmptyRow();
        table.AddRow("💾 Total Downloaded", FormatSize(totalBytesDownloaded));
        table.AddRow("📦 Total Size", FormatSize(totalBytes));
        table.AddRow("⚡ Current Speed", FormatSpeed(currentSpeed));

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[dim]Press any key to return...[/]");
        Console.ReadKey(true);

        return Task.CompletedTask;
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
            return "[dim]0 B[/]";
        }

        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            _ => $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB"
        };
    }

    private static void ShowHeader()
    {
        var rule = new Rule("[blue]Statistics[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
}
