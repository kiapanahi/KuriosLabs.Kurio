# Kurio Project Overview

## What is Kurio?

Kurio is a cross-platform download manager built with .NET that provides advanced features for managing and accelerating downloads. It supports pause/resume, multi-protocol downloads (HTTP/HTTPS/FTP), automatic file segmentation, multi-threaded downloading, and extensive customization options.

## Project Structure

```
├── src/
│   ├── Kurio.Core/          # Core download engine library
│   ├── Kurio.Server/        # ASP.NET Core REST API + SignalR hub
│   ├── Kurio.Cli/           # Terminal User Interface (TUI)
│   ├── Kurio.Avalonia/      # Cross-platform desktop GUI
│   ├── Kurio.AppHost/       # .NET Aspire application host
│   └── Kurio.ServiceDefaults/ # Shared service defaults
├── test/
│   └── Kurio.Core.Tests/    # Unit and integration tests
└── docs/                     # Documentation (see DOCS.md)
```

## Components

### Kurio.Core
Core download engine library providing:
- Multi-threaded downloads with automatic file segmentation
- Pause/resume with reliable state management
- HTTP/HTTPS protocol support with range requests
- Real-time progress tracking via `IObservable<T>`
- Extensible architecture for protocol handlers and storage managers
- Thread-safe concurrent download management
- Segment-level SHA256 checksum verification

**Key Features:**
- Configurable max concurrent downloads and connections (1-32)
- Smart storage with automatic conflict resolution
- Resilient connection handling with exponential backoff
- Comprehensive test coverage

### Kurio.Server
ASP.NET Core web service exposing the download engine via:
- **REST API**: Full CRUD operations for downloads, queue management, statistics
- **SignalR Hub**: Bidirectional real-time communication at `/hubs/downloads`
- **Server-Sent Events (SSE)**: Simple server-to-client streaming at `/api/downloads/stream`
- **OpenAPI/Swagger**: Auto-generated API documentation
- **Health Checks**: Monitoring endpoint at `/health`

**Endpoints:**
- `POST /api/downloads` - Add download
- `GET /api/downloads` - List all downloads
- `POST /api/downloads/{id}/start|pause|resume` - Control downloads
- `DELETE /api/downloads/{id}` - Cancel download
- `POST /api/downloads/pause-all` - Pause all active
- `GET /api/downloads/statistics` - Queue statistics

**Configuration:**
- Default port: 5000 (HTTP), 5001 (HTTPS)
- CORS support for web clients
- Graceful shutdown (auto-pauses active downloads)

### Kurio.Cli
Modern Terminal User Interface (TUI) built with Spectre.Console:
- Interactive menu system with arrow key navigation
- Real-time progress bars and status indicators
- Download queue management (start, pause, resume, cancel, reorder)
- Statistics dashboard (totals, speeds, counts)
- Cross-platform support (Windows, macOS, Linux)

**Menu Options:**
1. Downloads - View and manage downloads
2. Add Download - Create new download with options
3. Statistics - View download metrics
4. Settings - Configure application (coming soon)
5. Exit - Quit application

### Kurio.Avalonia
Cross-platform desktop application built with Avalonia UI:
- Modern, responsive UI with Fluent Design theme
- MVVM architecture using ReactiveUI
- Download queue management with data grid
- Real-time statistics and monitoring
- Configurable settings (concurrent downloads, segments, save location, speed limits)

**Views:**
- Main window with tabbed interface
- Download list with progress indicators
- Add download dialog with customization
- Settings panel for configuration
- Statistics dashboard

### Kurio.AppHost
.NET Aspire application host for orchestrating services in development and production environments.

## Technology Stack

- **Language**: C# (latest features)
- **Framework**: .NET 10.0+
- **UI Frameworks**: Avalonia UI (desktop), Spectre.Console (TUI)
- **Web Framework**: ASP.NET Core (minimal APIs only)
- **Real-time**: SignalR, Server-Sent Events
- **Testing**: xUnit with comprehensive coverage
- **Resilience**: Polly library
- **Configuration**: Microsoft.Extensions.Configuration
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection

## Getting Started

### Prerequisites
- .NET 10.0 SDK or later
- Supported OS: Windows, macOS, Linux

### Build and Run

```powershell
# Clone repository
git clone https://github.com/kiapanahi/KuriosLabs.Kurio.git
cd KuriosLabs.Kurio

# Build entire solution
dotnet build

# Run specific component
dotnet run --project src/Kurio.Server    # Web API
dotnet run --project src/Kurio.Cli       # Terminal UI
dotnet run --project src/Kurio.Avalonia  # Desktop GUI
```

### Configuration

All components use `appsettings.json` for configuration:

```json
{
  "Kurio": {
    "Storage": {
      "DefaultDestination": "~/Downloads",
      "TempDirectory": "~/Downloads/.kurio/temp",
      "Mode": "SingleFile",
      "VerifyWrites": true
    },
    "Engine": {
      "MaxConcurrentDownloads": 3,
      "DefaultMaxConnections": 8
    }
  }
}
```

## Key Features Across All Components

### Performance
- Multi-threaded downloads with parallel segment processing
- Automatic bandwidth optimization
- Configurable connection pooling
- Download acceleration via multiple connections

### Reliability
- Connection resilience with automatic retry (exponential backoff)
- Stall detection (30s timeout)
- Segment-level checksums for data integrity
- State persistence for crash recovery
- Graceful shutdown handling

### User Experience
- Real-time progress updates across all UIs
- Flexible queue management
- Detailed statistics and history
- Cross-platform native experience
- Browser integration capability

### Quality
- Comprehensive unit and integration tests
- Type-safe logging with `LoggerMessageAttribute`
- Modern async/await patterns with `ConfigureAwait(false)`
- Semantic versioning
- Extensive API documentation

## Development Guidelines

- **Version Control**: Gitflow workflow, Conventional Commits
- **Versioning**: Update `Directory.Build.props` before PRs (MANDATORY)
- **Package Management**: Centralized in `Directory.Packages.props`
- **Async**: Always use `.ConfigureAwait(false)` in non-UI code
- **Locking**: Use `System.Threading.Lock` (not `SemaphoreSlim(1,1)`)
- **Testing**: Maintain >80% code coverage target
- **Documentation**: Keep inline with code changes

## Contributing

See `CONTRIBUTING.md` for contribution guidelines and `DOCS.md` for technical documentation.

## License

See `LICENSE` file for licensing information.
