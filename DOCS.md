# Kurio Documentation

## Architecture

### Domain Model
- Core abstractions: `IDownloadEngine`, `IDownloadTask`, `IProtocolHandler`, `IStorageManager`, `ISegmentManager`
- SOLID principles with clear separation of concerns
- Hybrid download engine: Native .NET HttpClient-based with extensibility for future aria2 integration

### System Components
- **Queue Management**: Task prioritization and concurrency control
- **Protocol Handlers**: HTTP/HTTPS/FTP support
- **Storage**: Per-segment file storage mode (eliminates locking contention)
- **Task Execution**: Multi-threaded segment downloading
- **Progress Tracking**: `IObservable<DownloadProgress>` (migrating to `IAsyncEnumerable`)
- **State Persistence**: JSON-based resume capability
- **Resilience**: Polly library for retry policies and circuit breakers

### Client-Server Architecture
- ASP.NET Core server with minimal APIs
- REST API for CRUD operations
- SignalR/SSE for real-time progress updates
- Multiple UI clients: Avalonia (desktop), TUI (terminal), web (planned)

## Features

### Core Capabilities
- Multi-segment downloads with parallel connections (1-32 configurable)
- Pause/resume with byte-level precision
- Automatic file segmentation and merge
- Connection resilience with exponential backoff (max 5 retries, 2s-60s)
- Stall detection (30s timeout per read operation)
- Segment-level SHA256 checksum verification
- Real-time progress streaming

### Configuration
- Hierarchical: defaults → user config → environment variables → CLI arguments
- JSON-based settings with per-download option overrides
- FluentValidation for declarative validation rules
- Global and per-download settings support

### User Interfaces
- **TUI**: Spectre.Console-based interactive terminal with queue management
- **Avalonia**: Cross-platform desktop GUI with ReactiveUI
- **Server**: ASP.NET Core REST API + SignalR hub

## Bug Fixes

- **File Locking**: Fixed race conditions with per-segment storage mode
- **Pause/Resume Corruption**: Fixed premature state updates causing data gaps
- **Segment Verification**: Fixed verification timing (now checks segments before merge)
- **State Calculation**: Fixed live progress aggregation during active downloads
- **Connection Loss**: Expanded error detection patterns for EOF and transport errors

## Requirements

### Platform
- C# with latest language features
- .NET 10.0 or later
- Cross-platform: Windows, macOS, Linux

### Constraints
- Always use `LoggerMessageAttribute` for logging
- Use `.ConfigureAwait(false)` for non-UI async calls
- Use `System.Threading.Lock` instead of `SemaphoreSlim(1,1)`
- Semantic versioning in `Directory.Build.props`
- **MANDATORY**: Update version before creating pull requests
- Centralized package management via `Directory.Packages.props`
- ASP.NET Core: minimal APIs only
- Git: 50/72 rule, Gitflow workflow, Conventional Commits

### Logging EventId Ranges
- ErrorHandling: 1000-2999
- Persistence: 3000-3999
- Engine: 4000-4999
- Protocols: 5000-5999
- Statistics: 6000-6999
- Configuration: 7000-7999
- Server: 8000-8999
- Client: 9000-9999

## Implementation Phases

### Phase 1 - Foundation (4-7 weeks)
- Migrate to Polly for resilience
- Fix concurrent write issues
- Segment-level checksums
- Connection resilience enhancements

### Phase 2 - Modernization (1-2 weeks)
- Migrate `IObservable` to `IAsyncEnumerable` for streaming

### Phase 3 - Web Service (4-6 weeks)
- Create `Kurio.Server` ASP.NET Core service
- Implement REST API endpoints
- Add SignalR hub for real-time updates
- Background services for engine hosting
