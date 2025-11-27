# Kurio.Avalonia

A cross-platform desktop application for Kurio Download Manager built with [Avalonia UI](https://avaloniaui.net/).

## Overview

Kurio.Avalonia is a modern, cross-platform desktop client for the Kurio download manager. It provides a rich graphical user interface for managing downloads with features like:

- 📥 Download queue management
- ⏸️ Pause, resume, and cancel downloads
- 📊 Real-time statistics and monitoring
- ⚙️ Configurable settings
- 🎨 Modern, responsive UI with Fluent Design
- 🖥️ Cross-platform support (Windows, macOS, Linux)

## Features

### Download Management
- View all downloads in a data grid with progress indicators
- Add new downloads with customizable settings
- Control individual downloads (pause, resume, cancel, remove)
- Real-time progress updates

### Statistics
- Total downloads count
- Completed and failed downloads tracking
- Total data downloaded
- Average download speed
- Active downloads monitoring

### Settings
- Configure maximum concurrent downloads
- Set default number of segments for downloads
- Specify default save location
- Enable/disable automatic download start
- Set download speed limits

## Architecture

The application follows the MVVM (Model-View-ViewModel) pattern using ReactiveUI:

### Project Structure

```
Kurio.Avalonia/
├── ViewModels/          # Application view models
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs
│   ├── DownloadListViewModel.cs
│   ├── AddDownloadViewModel.cs
│   ├── SettingsViewModel.cs
│   └── StatisticsViewModel.cs
├── Views/               # XAML views
│   ├── MainWindow.axaml
│   ├── DownloadListView.axaml
│   ├── AddDownloadView.axaml
│   ├── SettingsView.axaml
│   └── StatisticsView.axaml
├── App.axaml           # Application root
├── Program.cs          # Entry point
└── appsettings.json    # Configuration
```

## Dependencies

- **Avalonia**: Cross-platform UI framework (v11.2.2)
- **Avalonia.Desktop**: Desktop platform support
- **Avalonia.Themes.Fluent**: Fluent design theme
- **Avalonia.ReactiveUI**: MVVM framework integration
- **Kurio.Core**: Core download manager logic
- **Microsoft.AspNetCore.SignalR.Client**: Real-time communication with server

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- Visual Studio 2022, VS Code, or JetBrains Rider

### Building the Project

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build src/Kurio.Avalonia/Kurio.Avalonia.csproj

# Run the application
dotnet run --project src/Kurio.Avalonia/Kurio.Avalonia.csproj
```

### Configuration

Configure the server connection in `appsettings.json`:

```json
{
  "ServerUrl": "http://localhost:5000"
}
```

## Development

### Running in Development Mode

```bash
dotnet watch --project src/Kurio.Avalonia/Kurio.Avalonia.csproj
```

### Hot Reload

Avalonia supports hot reload for XAML changes. Make changes to `.axaml` files and they will be reflected immediately in the running application.

## Platform-Specific Notes

### Windows
- Requires .NET Desktop Runtime
- Full Fluent Design theme support

### macOS
- Requires .NET Runtime
- Native menu integration
- Supports both Intel and Apple Silicon

### Linux
- Requires .NET Runtime and X11 or Wayland
- Install dependencies: `sudo apt-get install libx11-dev libxrandr-dev` (Ubuntu/Debian)

## Roadmap

### Phase 1 (Current)
- ✅ Basic UI structure with navigation
- ✅ Download list view with sample data
- ✅ Add download form
- ✅ Settings page
- ✅ Statistics view

### Phase 2 (Next)
- [ ] Integrate with Kurio.Core API client
- [ ] Real-time download progress updates via SignalR
- [ ] File browser dialog integration
- [ ] Drag-and-drop URL support
- [ ] System tray integration

### Phase 3 (Future)
- [ ] Download categories and filtering
- [ ] Scheduler for timed downloads
- [ ] Browser extension integration
- [ ] Themes and customization
- [ ] Keyboard shortcuts
- [ ] Download history with search

## Contributing

Follow the project's contribution guidelines in the main repository. When working on the Avalonia UI:

1. Follow MVVM pattern consistently
2. Use ReactiveUI for property notifications and commands
3. Keep views simple - business logic belongs in ViewModels
4. Write clean, maintainable XAML
5. Test on multiple platforms when possible

## License

This project is part of Kurio and follows the same license as the main repository.

## Resources

- [Avalonia Documentation](https://docs.avaloniaui.net/)
- [ReactiveUI Documentation](https://www.reactiveui.net/)
- [Avalonia Samples](https://github.com/AvaloniaUI/Avalonia.Samples)
- [Avalonia Community](https://github.com/AvaloniaCommunity)
