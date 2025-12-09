# Kurio

A powerful, cross-platform download manager built with .NET that provides advanced features for managing and
accelerating your downloads.

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
- **Command-Line Interface** - Powerful TUI for advanced users
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

### Installation

```bash
# Clone the repository
git clone https://github.com/kiapanahi/KuriosLabs.Kurio.git

# Navigate to the project directory
cd KuriosLabs.Kurio

# Build the project
dotnet build

# Run the application
dotnet run --project src/Kurio
```

## Documentation

- **[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)** - Detailed component descriptions and getting started guide
- **[DOCS.md](DOCS.md)** - Technical documentation, architecture, and development guidelines

## Project Structure

```text
.
├── src/
│   ├── Kurio.Core/          # Core download engine library
│   ├── Kurio.Server/        # ASP.NET Core REST API + SignalR
│   ├── Kurio.Cli/           # Terminal User Interface (TUI)
│   ├── Kurio.Avalonia/      # Cross-platform desktop GUI
│   ├── Kurio.AppHost/       # .NET Aspire application host
│   └── Kurio.ServiceDefaults/ # Shared service defaults
├── test/                    # Unit and integration tests
└── .github/                 # GitHub workflows and templates
```

## Contributing

We welcome contributions from the community! Please read our [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on how to
contribute to the project.

## Technology Stack

- **Language**: C#
- **Framework**: .NET 10.0+
- **Platform**: Cross-platform (Windows, macOS, Linux)

## License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.

## Documentation

For detailed documentation, please visit the [docs/](docs/) directory.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a history of changes and releases.

## Support

For issues, questions, or feature requests, please use
the [GitHub Issues](https://github.com/kiapanahi/KuriosLabs.Kurio/issues) page.
