# Kurio.Core

Core download engine library for Kurio download manager, providing robust multithreaded download capabilities with pause/resume support.

## Features

- 🚀 **Multi-threaded Downloads**: Automatic file segmentation with parallel connections
- ⏸️ **Pause/Resume**: Reliable download state management
- 🔄 **HTTP/HTTPS Support**: Built-in protocol handler with range request support
- 📊 **Real-time Progress**: Reactive progress tracking via `IObservable<T>`
- 🔌 **Extensible Architecture**: Plugin-ready protocol handlers and storage managers
- 🧵 **Thread-safe**: Concurrent download management with configurable limits
- 💾 **Smart Storage**: Automatic file naming conflict resolution
- ✅ **Tested**: Comprehensive unit test coverage

## Installation

```bash
dotnet add package Kurio.Core
```

## Quick Start

### 1. Configure Services

```csharp
using Kurio.Core;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Use default configuration (~/.kurio/temp and ~/.kurio/state)
services.AddKurioDownloadEngine();

// Or specify custom directories
services.AddKurioDownloadEngine(
    tempDirectory: "/path/to/temp",
    stateDirectory: "/path/to/state",
    maxConcurrentDownloads: 3
);

var serviceProvider = services.BuildServiceProvider();
```

### 2. Create a Download

```csharp
using Kurio.Core.Abstractions;
using Kurio.Core.Models;

var downloadEngine = serviceProvider.GetRequiredService<IDownloadEngine>();

var options = new DownloadOptions
{
    DestinationDirectory = "/path/to/downloads",
    FileName = "myfile.zip", // Optional, will be inferred from URL if omitted
    MaxConnections = 8,
    MinSegmentSize = 1024 * 1024, // 1 MB
    FileNamingPolicy = FileNamingPolicy.AutoRename
};

var task = await downloadEngine.AddDownloadAsync(
    new Uri("https://example.com/file.zip"),
    options
);

Console.WriteLine($"Download created with ID: {task.Id}");
```

### 3. Start and Monitor Download

```csharp
// Subscribe to progress updates
downloadEngine.ProgressUpdates.Subscribe(progress =>
{
    Console.WriteLine($"Progress: {progress.Percentage:F2}%");
    Console.WriteLine($"Speed: {progress.BytesPerSecond / 1024} KB/s");
    Console.WriteLine($"ETA: {progress.EstimatedTimeRemaining}");
});

// Start the download
await downloadEngine.StartDownloadAsync(task.Id);

// Wait for completion (in real app, you'd use progress updates)
while (task.State == DownloadState.Downloading || task.State == DownloadState.Queued)
{
    await Task.Delay(100);
}

if (task.State == DownloadState.Completed)
{
    Console.WriteLine($"Download completed successfully!");
}
```

### 4. Pause and Resume

```csharp
// Pause download
await downloadEngine.PauseDownloadAsync(task.Id);
Console.WriteLine("Download paused");

// Resume later
await downloadEngine.ResumeDownloadAsync(task.Id);
Console.WriteLine("Download resumed");
```

### 5. Cancel Download

```csharp
// Cancel and keep partial files
await downloadEngine.CancelDownloadAsync(task.Id, removePartialFiles: false);

// Or cancel and remove partial files
await downloadEngine.CancelDownloadAsync(task.Id, removePartialFiles: true);
```

## Architecture

### Core Components

```
┌────────────────────────────────────────────────────────────┐
│                     IDownloadEngine                        │
│  Main orchestrator for all download operations             │
└────────────────────────┬───────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         │               │               │
         ▼               ▼               ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│IProtocol    │  │IStorage     │  │ISegment     │
│Handler      │  │Manager      │  │Manager      │
│             │  │             │  │             │
│HTTP/HTTPS   │  │File Ops     │  │Multi-thread │
│FTP/FTPS     │  │Temp Files   │  │Segments     │
└─────────────┘  └─────────────┘  └─────────────┘
```

### Key Interfaces

- **`IDownloadEngine`**: Main API for download management
- **`IDownloadTask`**: Represents an individual download
- **`IProtocolHandler`**: Abstract protocol operations (HTTP, FTP, etc.)
- **`IStorageManager`**: File system operations
- **`ISegmentManager`**: Multi-threaded segmentation

## Configuration Options

### DownloadOptions

```csharp
var options = new DownloadOptions
{
    // Required
    DestinationDirectory = "/path/to/downloads",
    
    // Optional
    FileName = "custom-name.zip",
    MaxConnections = 8,              // Parallel connections
    MinSegmentSize = 1024 * 1024,    // 1 MB minimum per segment
    FileNamingPolicy = FileNamingPolicy.AutoRename,
    
    // HTTP specific
    UserAgent = "Kurio/1.0",
    TimeoutSeconds = 30,
    FollowRedirects = true,
    MaxRedirects = 5,
    ValidateCertificate = true,
    
    // Authentication
    Credentials = "username:password", // Basic auth
    
    // Custom headers
    Headers = new Dictionary<string, string>
    {
        ["X-Custom-Header"] = "value"
    },
    
    // Verification
    ExpectedChecksum = "sha256-hash",
    ChecksumAlgorithm = "SHA256"
};
```

### File Naming Policies

- **`AutoRename`**: Adds numeric suffix (e.g., `file(1).zip`)
- **`Overwrite`**: Replaces existing file
- **`Skip`**: Throws exception if file exists
- **`Prompt`**: Not supported (requires UI)

## Multi-threaded Downloads

The engine automatically:

1. Checks server support for range requests
2. Calculates optimal number of segments
3. Downloads segments in parallel
4. Merges segments into final file

```
File: 10 MB, MaxConnections: 4

Segment 0: [0 MB     - 2.5 MB]  ████████
Segment 1: [2.5 MB   - 5 MB  ]  ████████
Segment 2: [5 MB     - 7.5 MB]  ████████
Segment 3: [7.5 MB   - 10 MB ]  ████████
```

## State Management

Download states:

1. **Created**: Initial state
2. **Queued**: Waiting to start
3. **Analyzing**: Checking metadata
4. **Downloading**: Active download
5. **Paused**: User paused
6. **Completed**: Successfully finished
7. **Failed**: Error occurred
8. **Canceled**: User canceled

## Error Handling

```csharp
var task = await downloadEngine.AddDownloadAsync(url, options);
await downloadEngine.StartDownloadAsync(task.Id);

if (task.State == DownloadState.Failed)
{
    Console.WriteLine($"Error: {task.LastError?.Message}");
    Console.WriteLine($"Recoverable: {task.LastError?.IsRecoverable}");
    Console.WriteLine($"Retry count: {task.RetryCount}");
}
```

## Query Downloads

```csharp
// Get single download
var task = downloadEngine.GetDownload(taskId);

// Get all active downloads
var active = downloadEngine.GetDownloads(DownloadStateFilter.Active);

// Get completed downloads
var completed = downloadEngine.GetDownloads(DownloadStateFilter.Completed);

// Get failed or cancelled
var failed = downloadEngine.GetDownloads(
    DownloadStateFilter.Failed | DownloadStateFilter.Cancelled
);

// Get all downloads
var all = downloadEngine.GetDownloads(DownloadStateFilter.All);
```

## Testing

Run the test suite:

```bash
dotnet test test/Kurio.Core.Tests
```

## Protocol Handlers

The protocol abstraction layer allows extending Kurio with support for additional protocols beyond HTTP/HTTPS.

### Using Protocol Handlers

```csharp
// Get protocol handler factory
var factory = serviceProvider.GetRequiredService<IProtocolHandlerFactory>();

// Check if a URL is supported
var url = new Uri("https://example.com/file.zip");
bool isSupported = factory.IsSupported(url); // true

// Get appropriate handler for a URL
IProtocolHandler handler = factory.GetHandler(url);
Console.WriteLine($"Using handler for: {string.Join(", ", handler.SupportedSchemes)}");
```

### Built-in Handlers

#### HttpProtocolHandler

Supports `http` and `https` schemes with:
- Range request support detection
- Automatic decompression (gzip, deflate)
- Custom headers and authentication
- Timeout and redirect configuration
- SSL/TLS certificate validation

```csharp
// Protocol handler operations are typically called by the engine,
// but you can use them directly if needed
var options = new DownloadOptions
{
    DestinationDirectory = "/downloads",
    UserAgent = "Kurio/1.0",
    TimeoutSeconds = 30
};

// Check if server supports range requests
bool supportsRanges = await handler.SupportsRangeRequestsAsync(url, options);

// Get file metadata
var metadata = await handler.GetMetadataAsync(url, options);
Console.WriteLine($"Size: {metadata.ContentLength} bytes");
Console.WriteLine($"Type: {metadata.ContentType}");
Console.WriteLine($"ETag: {metadata.ETag}");
Console.WriteLine($"Filename: {metadata.SuggestedFileName}");

// Download a byte range
var range = new ByteRange(0, 1023); // First 1KB
using var stream = new FileStream("partial.dat", FileMode.Create);
await handler.DownloadRangeAsync(url, range, stream, options);
```

### Creating Custom Protocol Handlers

Implement `IProtocolHandler` to add support for new protocols:

```csharp
public class FtpProtocolHandler : IProtocolHandler
{
    public IReadOnlySet<string> SupportedSchemes => 
        new HashSet<string> { "ftp", "ftps" };

    public async Task<bool> SupportsRangeRequestsAsync(
        Uri url, 
        DownloadOptions options,
        CancellationToken cancellationToken = default)
    {
        // Check if FTP server supports REST command
        // ...
    }

    // Implement other interface methods...
}
```

Register your custom handler:

```csharp
services.AddSingleton<IProtocolHandler, FtpProtocolHandler>();
services.AddKurioDownloadEngine();
```

The factory will automatically discover and register all `IProtocolHandler` implementations.

## Architecture Decisions

Based on the [Core Engine Architecture PRD](../docs/prd/core-engine-architecture.md):

- **HttpClient via IHttpClientFactory**: Prevents socket exhaustion
- **Protocol abstraction layer**: Extensible design for multiple protocols
- **Factory pattern**: Automatic protocol selection based on URL scheme
- **Concurrent file writes**: `FileShare.Write` for parallel segments
- **Pre-allocated files**: Better disk performance
- **Reactive progress**: `System.Reactive` for event streaming
- **Dependency injection**: Full DI support for testing and extensibility

## Roadmap

- [ ] State persistence (JSON serialization)
- [ ] FTP/FTPS protocol handler
- [ ] Checksum verification
- [ ] Retry logic with exponential backoff
- [ ] Download scheduling
- [ ] Bandwidth throttling
- [ ] Browser integration

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for guidelines.

## License

See [LICENSE](../../LICENSE) file for details.

## Related

- [Product Requirements Document](../docs/prd/core-engine-architecture.md)
- [Issue #4](https://github.com/kiapanahi/KuriosLabs.Kurio/issues/4) - Core Engine Architecture
- [Main Project README](../../README.md)
