using Spectre.Console;

namespace KuriousLabs.Kurio.Cli.UI;

/// <summary>
///     View for managing application settings.
/// </summary>
public sealed class SettingsView
{
    /// <summary>
    ///     Shows the settings view.
    /// </summary>
    public Task ShowAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.Clear();
        ShowHeader();

        AnsiConsole.MarkupLine("[yellow]Settings management is not yet implemented.[/]");
        AnsiConsole.MarkupLine("This feature will be added in future releases.");

        AnsiConsole.MarkupLine("\n[dim]Planned settings:[/]");
        AnsiConsole.MarkupLine("  • Max concurrent downloads");
        AnsiConsole.MarkupLine("  • Default download directory");
        AnsiConsole.MarkupLine("  • Network settings (bandwidth limits, proxy)");
        AnsiConsole.MarkupLine("  • File naming policies");
        AnsiConsole.MarkupLine("  • Auto-start behavior");

        AnsiConsole.MarkupLine("\n[dim]Press any key to return...[/]");
        Console.ReadKey(true);

        return Task.CompletedTask;
    }

    private static void ShowHeader()
    {
        Rule rule = new("[blue]Settings[/]") { Justification = Justify.Left };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }
}
