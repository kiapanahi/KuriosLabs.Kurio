# Kurio CLI - Terminal User Interface

A modern, cross-platform Terminal User Interface (TUI) for the Kurio download manager.

## Features

### Current Features (v1.8.0)

- **Interactive Menu System**: Easy navigation with arrow keys and Enter
- **Download Management**:
  - Add new downloads with customizable options
  - View all downloads with status and progress
  - Start, pause, resume, and cancel downloads
  - Move downloads up/down in queue
  - Clear completed downloads
  - Pause all active downloads
- **Real-time Progress Display**:
  - Visual progress bars
  - Download speed (B/s, KB/s, MB/s, GB/s)
  - File size formatting
  - Status indicators (Queued, Downloading, Paused, Completed, Failed)
- **Statistics Dashboard**:
  - Total downloads count
  - Active, queued, paused, completed, and failed counts
  - Total bytes downloaded
  - Current download speed
- **Cross-platform**: Works on Windows, macOS, and Linux

### Coming Soon

- Real-time live updates with background service
- Keyboard shortcuts (F1-help, F5-refresh, etc.)
- Help screen
- Download details view
- Settings configuration UI
- Enhanced error notifications

## Installation

```bash
# Build the project
dotnet build src/Kurio.Cli/Kurio.Cli.csproj

# Run the CLI
dotnet run --project src/Kurio.Cli/Kurio.Cli.csproj
```

## Usage

### Main Menu

When you start Kurio CLI, you'll see the main menu with the following options:

1. **📥 Downloads**: View and manage your downloads
2. **➕ Add Download**: Add a new download
3. **📊 Statistics**: View download statistics
4. **⚙️  Settings**: Configure application settings (coming soon)
5. **❌ Exit**: Exit the application

### Adding a Download

1. Select "➕ Add Download" from the main menu
2. Enter the download URL
3. Specify the destination directory (or use default)
4. Optionally specify a custom file name
5. Set the maximum number of connections (default: 8)
6. Choose whether to start the download immediately

### Managing Downloads

In the Downloads view, you can:

- **▶️  Start Selected**: Start a queued download
- **⏸️  Pause Selected**: Pause an active download
- **🔄 Resume Selected**: Resume a paused download
- **❌ Cancel Selected**: Cancel a download (with option to remove partial files)
- **⬆️  Move Up**: Move a download higher in the queue
- **⬇️  Move Down**: Move a download lower in the queue
- **🔄 Refresh**: Refresh the download list
- **🗑️  Clear Completed**: Remove completed downloads from the list
- **⏸️  Pause All**: Pause all active downloads
- **⬅️  Back**: Return to main menu

## Architecture

The Kurio CLI is built with:

- **Spectre.Console**: Modern TUI library for rich console output
- **Microsoft.Extensions.Hosting**: Dependency injection and hosting
- **Kurio.Core**: Core download engine

### Project Structure

```text
Kurio.Cli/
├── Program.cs                 # Application entry point
├── KurioCliApplication.cs    # Main application coordinator
└── UI/
    ├── MainMenu.cs           # Main navigation menu
    ├── DownloadListView.cs   # Download management view
    ├── AddDownloadView.cs    # Add download form
    ├── StatisticsView.cs     # Statistics dashboard
    └── SettingsView.cs       # Settings configuration (placeholder)
```

## Development

### Building

```bash
dotnet build src/Kurio.Cli/Kurio.Cli.csproj
```

### Running

```bash
dotnet run --project src/Kurio.Cli/Kurio.Cli.csproj
```

### Testing

```bash
# Unit tests (when available)
dotnet test test/Kurio.Cli.Tests/
```

## Dependencies

- **Spectre.Console** 0.49.1: Terminal UI library
- **Microsoft.Extensions.Hosting** 10.0.0: Hosting and DI
- **Kurio.Core**: Core download engine

## Contributing

This is part of the Kurio project. See the main [CONTRIBUTING.md](../../CONTRIBUTING.md) for guidelines.

## License

See [LICENSE](../../LICENSE) file in the root directory.
