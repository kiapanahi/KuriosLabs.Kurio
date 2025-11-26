using Kurio.Core.Abstractions;
using Kurio.Core.Models;
using Spectre.Console;

namespace KuriousLabs.Kurio.Cli.UI;

/// <summary>
/// View for adding new downloads.
/// </summary>
public sealed class AddDownloadView
{
    private readonly IDownloadEngine _downloadEngine;

    public AddDownloadView(IDownloadEngine downloadEngine)
    {
        _downloadEngine = downloadEngine ?? throw new ArgumentNullException(nameof(downloadEngine));
    }

    /// <summary>
    /// Shows the add download view.
    /// </summary>
    public async Task ShowAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        ShowHeader();

        var url = AnsiConsole.Ask<string>("[blue]Enter download URL:[/]");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            AnsiConsole.MarkupLine("[red]Invalid URL format![/]");
            AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
            Console.ReadKey(true);
            return;
        }

        var destinationDirectory = AnsiConsole.Ask(
            "[blue]Destination directory:[/]",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Downloads");

        var fileName = AnsiConsole.Ask<string>(
            "[blue]File name (leave empty to auto-detect):[/]",
            string.Empty);

        var maxConnections = AnsiConsole.Ask(
            "[blue]Max connections:[/]",
            8);

        var options = new DownloadOptions
        {
            DestinationDirectory = destinationDirectory,
            FileName = string.IsNullOrWhiteSpace(fileName) ? null : fileName,
            MaxConnections = maxConnections
        };

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Adding download...", async ctx =>
                {
                    var task = await _downloadEngine.AddDownloadAsync(uri, options, cancellationToken);
                    AnsiConsole.MarkupLine($"[green]✓ Download added: {task.FileName ?? "Unknown"}[/]");
                    
                    if (AnsiConsole.Confirm("Start download now?"))
                    {
                        ctx.Status("Starting download...");
                        await _downloadEngine.StartDownloadAsync(task.Id, cancellationToken);
                        AnsiConsole.MarkupLine("[green]✓ Download started![/]");
                    }
                });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AnsiConsole.WriteException(ex);
        }

        AnsiConsole.MarkupLine("\n[dim]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    private static void ShowHeader()
    {
        var rule = new Rule("[blue]Add Download[/]")
        {
            Justification = Justify.Left
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
}
