# Kurio

A cross-platform download manager built with modern .NET, providing advanced features for managing and accelerating downloads across multiple protocols.

## Features

### Core Functionality

- **Pause and Resume Downloads** - Full control over your download queue
- **Multi-Protocol Support** - HTTP, HTTPS, and FTP protocols
- **Download Scheduling** - Queue management with customizable scheduling
- **Large File Support** - Handle downloads of any size efficiently
- **Multiple Simultaneous Downloads** - Download multiple files concurrently

### Performance

- **Automatic File Segmentation** - Split files for faster downloads
- **Multi-Threaded Downloading** - Utilize multiple connections for accelerated speeds
- **Download Acceleration** - Optimize bandwidth usage with multiple connections
- **Proxy Server Support** - Route downloads through proxy servers

### Integration

- **Browser Integration** - Seamless integration with Chrome, Firefox, and Edge
- **Clipboard Link Capturing** - Automatically detect download links from clipboard
- **File Hosting Services** - Support for popular file hosting platforms
- **Plugin System** - Extend functionality with plugins and extensions

### Organization

- **Download Categories** - Organize downloads with customizable categories
- **Download History** - Track and review past downloads
- **Statistics** - Detailed download statistics and analytics
- **Import/Export** - Import and export download lists

### User Experience

- **Cross-Platform** - Works on Windows, macOS, and Linux
- **Web UI** - Blazor-based web interface
- **Streaming Media Support** - Download streaming media content
- **Automatic Updates** - Stay up-to-date with the latest features and security patches

### Quality & Security

- **Artifact Verification** - Verify downloads using checksums
- **Open Source** - Community-driven development
- **Extensive Documentation** - Comprehensive guides and tutorials

## Getting Started

### Prerequisites

- .NET 10.0 or later
- Supported operating systems: Windows, macOS, Linux

Key constraints and guidelines:
- Always use minimal APIs for ASP.NET Core components.
- Use centralized package management via NuGet.
- Use `System.Threading.Lock` instead of `SemaphoreSlim(1, 1)` for lock objects.
- Use `LoggerMessageAttribute` for logging.
- For non-UI services (API, Core, CLI), use `.ConfigureAwait(false)` for all async calls.

### Installation

```bash
# Clone the repository
git clone https://github.com/kiapanahi/KuriosLabs.Kurio.git

# Navigate to the project directory
cd KuriosLabs.Kurio

# Restore and build the solution
dotnet restore
dotnet build

# Run the server (REST API + SignalR)
dotnet run --project src/Kurio.Server

# Run the web UI (Blazor WebAssembly/Server per project settings)
dotnet run --project src/Kurio.Web

# Optional: run the Aspire AppHost (if using .NET Aspire)
dotnet run --project src/Kurio.AppHost
```

## Documentation

- See [docs/](docs/) for guides and PRDs.
- See [.github/copilot-instructions.md](.github/copilot-instructions.md) for development rules and workflows.

## Project Structure

```text
.
├── src/
│   ├── Kurio.Contracts/       # Shared contracts (DTOs, Hubs, Settings, Queue)
│   ├── Kurio.Core/            # Core download engine and services
│   ├── Kurio.Server/          # ASP.NET Core REST API + SignalR (minimal APIs)
│   ├── Kurio.Web/             # Blazor-based web UI
│   ├── Kurio.AppHost/         # .NET Aspire application host
│   └── Kurio.ServiceDefaults/ # Shared service defaults
├── test/                    # Unit and integration tests
└── .github/                 # GitHub workflows and templates
```

## Contributing

We welcome contributions from the community! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

Workflow essentials:
- For any new feature or bug fix, create a PRD in `docs/prd/` outlining requirements and specifications.
- From the PRD, generate user stories and tasks in GitHub Issues.
- Write unit and integration tests where applicable.
- Versioning is mandatory: update `Directory.Build.props` BEFORE committing any feature or fix.
	- MAJOR (X.0.0): breaking changes
	- MINOR (x.Y.0): new features, backward compatible
	- PATCH (x.y.Z): bug fixes, minor improvements
	- Include a separate commit: `chore: bump version to X.Y.Z`
- Git: follow Gitflow, use feature branches, Conventional Commits, and always open a PR.

## Technology Stack

- **Language**: C#
- **Framework**: .NET 10.0+
- **Platform**: Cross-platform (Windows, macOS, Linux)

## License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.

## Branches

- Default branch: `main`
- Current working branch may vary; see repository for active branches. Example: `remove-redundant-projects`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a history of changes and releases.

## Support

For issues, questions, or feature requests, use the [GitHub Issues](https://github.com/kiapanahi/KuriosLabs.Kurio/issues) page.
