using KuriousLabs.Kurio.Cli.UI;
using Spectre.Console;

namespace KuriousLabs.Kurio.Cli;

/// <summary>
/// Main CLI application coordinator.
/// </summary>
public sealed class KurioCliApplication : IDisposable
{
    private readonly MainMenu _mainMenu;
    private readonly CancellationTokenSource _cts;
    private bool _disposed;
    
    public KurioCliApplication(MainMenu mainMenu)
    {
        _mainMenu = mainMenu ?? throw new ArgumentNullException(nameof(mainMenu));
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Runs the CLI application.
    /// </summary>
    public async Task RunAsync()
    {
        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        ShowWelcomeBanner();
        
        await _mainMenu.ShowAsync(_cts.Token);
    }

    private static void ShowWelcomeBanner()
    {
        AnsiConsole.Clear();
        
        var banner = new FigletText("Kurio")
            .Centered()
            .Color(Color.Blue);
        
        AnsiConsole.Write(banner);
        
        AnsiConsole.MarkupLine("[dim]Download Manager - Version 1.8.0[/]");
        AnsiConsole.WriteLine();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _disposed = true;
    }
}
