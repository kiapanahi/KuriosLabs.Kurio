# Product Requirements Document: Core Download Engine Architecture

**Version:** 1.0  
**Date:** November 25, 2025  
**Status:** Draft  
**Author:** Kurio Development Team  
**Related Issue:** [#4](https://github.com/kiapanahi/KuriosLabs.Kurio/issues/4)

---

## Executive Summary

This document defines the architecture for Kurio's core download engine, implementing a hybrid approach that leverages
.NET's `HttpClient` with custom download management logic. The architecture prioritizes cross-platform compatibility,
extensibility, and future integration with high-performance backends like aria2.

---

## 1. Goals and Objectives

### Primary Goals

- Build a robust, native .NET download engine with no external dependencies
- Support multi-threaded downloads with file segmentation
- Implement reliable pause/resume functionality
- Enable multi-protocol support (HTTP/HTTPS/FTP)
- Maintain extensibility for future aria2 integration

### Success Criteria

- Download speeds competitive with commercial download managers
- 99.9% successful resume rate for interrupted downloads
- Cross-platform operation on Windows, macOS, and Linux
- Clean, maintainable codebase following SOLID principles
- Comprehensive test coverage (>80%)

---

## 2. Architecture Overview

### 2.1 High-Level Components

```plain
┌────────────────────────────────────────────────────────────┐
│                     Download Engine                        │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Download   │  │   Protocol   │  │   Storage    │      │
│  │   Queue      │  │   Handlers   │  │   Manager    │      │
│  │   Manager    │  │              │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Download   │  │   Segment    │  │   Progress   │      │
│  │   Task       │  │   Manager    │  │   Tracker    │      │
│  │   Executor   │  │              │  │              │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │    State     │  │   Retry      │  │  Checksum    │      │
│  │ Persistence  │  │   Manager    │  │  Verifier    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### 2.2 Component Responsibilities

| Component                  | Responsibility                                                     |
|----------------------------|--------------------------------------------------------------------|
| **Download Queue Manager** | Manages download queue, priorities, and concurrent download limits |
| **Protocol Handlers**      | Abstract protocol operations (HTTP/HTTPS/FTP)                      |
| **Storage Manager**        | Handles file system operations, temp files, and final destinations |
| **Download Task Executor** | Orchestrates individual download execution                         |
| **Segment Manager**        | Manages file segmentation and parallel downloads                   |
| **Progress Tracker**       | Tracks download progress, speed, and ETA                           |
| **State Persistence**      | Saves/loads download state for pause/resume                        |
| **Retry Manager**          | Handles errors and retry logic                                     |
| **Checksum Verifier**      | Validates file integrity post-download                             |

---

## 3. Core Abstractions

### 3.1 IDownloadEngine

The main orchestrator for all download operations.

```csharp
public interface IDownloadEngine
{
    /// <summary>
    /// Adds a new download to the queue.
    /// </summary>
    Task<DownloadTask> AddDownloadAsync(
        Uri url, 
        DownloadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a queued download.
    /// </summary>
    Task StartDownloadAsync(
        Guid taskId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses an active download.
    /// </summary>
    Task PauseDownloadAsync(
        Guid taskId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused download.
    /// </summary>
    Task ResumeDownloadAsync(
        Guid taskId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a download and optionally removes partial files.
    /// </summary>
    Task CancelDownloadAsync(
        Guid taskId, 
        bool removePartialFiles = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a download.
    /// </summary>
    DownloadTask? GetDownload(Guid taskId);

    /// <summary>
    /// Gets all downloads matching the filter.
    /// </summary>
    IEnumerable<DownloadTask> GetDownloads(DownloadStateFilter filter);

    /// <summary>
    /// Observable stream of download progress updates.
    /// </summary>
    IObservable<DownloadProgress> ProgressUpdates { get; }
}
```

### 3.2 IProtocolHandler

Abstraction for protocol-specific operations.

```csharp
public interface IProtocolHandler
{
    /// <summary>
    /// Supported protocol schemes (http, https, ftp, ftps).
    /// </summary>
    IReadOnlySet<string> SupportedSchemes { get; }

    /// <summary>
    /// Checks if the server supports range requests.
    /// </summary>
    Task<bool> SupportsRangeRequestsAsync(
        Uri url, 
        DownloadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total file size.
    /// </summary>
    Task<long> GetFileSizeAsync(
        Uri url, 
        DownloadOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a byte range from the resource.
    /// </summary>
    Task DownloadRangeAsync(
        Uri url,
        ByteRange range,
        Stream destination,
        DownloadOptions options,
        IProgress<long> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets resource metadata (ETag, Last-Modified, etc.).
    /// </summary>
    Task<ResourceMetadata> GetMetadataAsync(
        Uri url,
        DownloadOptions options,
        CancellationToken cancellationToken = default);
}
```

### 3.3 IStorageManager

Manages file system operations.

```csharp
public interface IStorageManager
{
    /// <summary>
    /// Creates a temporary file for partial download.
    /// </summary>
    Task<string> CreateTemporaryFileAsync(
        Guid taskId,
        string fileName,
        long fileSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes data to a specific offset in the file.
    /// </summary>
    Task WriteSegmentAsync(
        string filePath,
        long offset,
        byte[] data,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves temporary file to final destination atomically.
    /// </summary>
    Task<string> CommitDownloadAsync(
        string tempFilePath,
        string destinationDirectory,
        string fileName,
        FileNamingPolicy namingPolicy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks available disk space.
    /// </summary>
    Task<long> GetAvailableDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up temporary files for a task.
    /// </summary>
    Task CleanupTemporaryFilesAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
```

### 3.4 IDownloadTask

Represents an individual download.

```csharp
public interface IDownloadTask
{
    Guid Id { get; }
    Uri Url { get; }
    string FileName { get; }
    long FileSize { get; }
    DownloadState State { get; }
    DownloadPriority Priority { get; set; }
    DownloadProgress Progress { get; }
    DownloadOptions Options { get; }
    ResourceMetadata Metadata { get; }
    DateTime CreatedAt { get; }
    DateTime? StartedAt { get; }
    DateTime? CompletedAt { get; }
    DownloadError? LastError { get; }
    int RetryCount { get; }
}
```

### 3.5 ISegmentManager

Manages download segmentation.

```csharp
public interface ISegmentManager
{
    /// <summary>
    /// Calculates optimal segment configuration for a download.
    /// </summary>
    SegmentConfiguration CalculateSegments(
        long fileSize,
        bool supportsRanges,
        SegmentOptions options);

    /// <summary>
    /// Downloads all segments in parallel.
    /// </summary>
    Task DownloadSegmentsAsync(
        IProtocolHandler handler,
        Uri url,
        SegmentConfiguration config,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress> progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes downloading incomplete segments.
    /// </summary>
    Task ResumeSegmentsAsync(
        IProtocolHandler handler,
        Uri url,
        SegmentConfiguration config,
        SegmentState[] segmentStates,
        string tempFilePath,
        DownloadOptions options,
        IProgress<SegmentProgress> progress,
        CancellationToken cancellationToken = default);
}
```

---

## 4. Multi-Threaded Segmentation Strategy

### 4.1 Segmentation Algorithm

```plain
Input: fileSize, maxConnections, minSegmentSize
Output: segmentCount, segmentRanges[]

1. IF fileSize < minSegmentSize THEN
     segmentCount = 1
     RETURN [0, fileSize-1]

2. idealSegmentCount = MIN(maxConnections, fileSize / minSegmentSize)

3. segmentSize = FLOOR(fileSize / idealSegmentCount)

4. FOR i = 0 TO idealSegmentCount-1
     start = i * segmentSize
     end = (i == idealSegmentCount-1) ? fileSize-1 : (start + segmentSize - 1)
     segmentRanges[i] = [start, end]

5. RETURN segmentRanges
```

### 4.2 Segment Download Flow

```plain
┌─────────────────────────────────────────────────────────────┐
│                    Download Task Start                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          Check Server Range Support (HEAD request)          │
└──────────────────────┬──────────────────────────────────────┘
                       │
          ┌────────────┴────────────┐
          ▼                         ▼
  ┌───────────────┐       ┌───────────────────┐
  │  Supports     │       │  Does Not         │
  │  Ranges       │       │  Support Ranges   │
  └───────┬───────┘       └─────────┬─────────┘
          │                         │
          ▼                         ▼
  ┌───────────────┐       ┌───────────────────┐
  │  Calculate    │       │  Single-threaded  │
  │  Segments     │       │  Download         │
  └───────┬───────┘       └─────────┬─────────┘
          │                         │
          ▼                         │
  ┌───────────────┐                 │
  │  Create N     │                 │
  │  Parallel     │                 │
  │  Tasks        │                 │
  └───────┬───────┘                 │
          │                         │
          ▼                         │
  ┌───────────────┐                 │
  │  Download     │                 │
  │  Each Segment │                 │
  │  (Parallel)   │                 │
  └───────┬───────┘                 │
          │                         │
          └────────────┬────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                   Verify and Merge                          │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                      Complete                               │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 Concurrency Configuration

| Setting                     | Default | Description                     |
|-----------------------------|---------|---------------------------------|
| `MaxConcurrentDownloads`    | 3       | Max simultaneous downloads      |
| `MaxConnectionsPerDownload` | 8       | Max segments per download       |
| `MinSegmentSize`            | 1 MB    | Minimum size to create segment  |
| `SegmentBufferSize`         | 8 KB    | Buffer size for segment reading |

### 4.4 Segment State Tracking

Each segment maintains:

```csharp
public class SegmentState
{
    public int SegmentIndex { get; set; }
    public long StartByte { get; set; }
    public long EndByte { get; set; }
    public long BytesDownloaded { get; set; }
    public SegmentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
}
```

---

## 5. Pause/Resume Mechanism

### 5.1 State Persistence Format

Downloads are persisted as JSON files in the state directory:

```json
{
  "version": "1.0",
  "taskId": "550e8400-e29b-41d4-a716-446655440000",
  "url": "https://example.com/file.zip",
  "fileName": "file.zip",
  "fileSize": 104857600,
  "destinationDirectory": "/downloads",
  "state": "paused",
  "priority": "normal",
  "metadata": {
    "etag": "33a64df551425fcc55e4d42a148795d9f25f89d4",
    "lastModified": "2025-11-20T10:30:00Z",
    "contentType": "application/zip"
  },
  "segments": [
    {
      "segmentIndex": 0,
      "startByte": 0,
      "endByte": 26214399,
      "bytesDownloaded": 26214400,
      "status": "completed"
    },
    {
      "segmentIndex": 1,
      "startByte": 26214400,
      "endByte": 52428799,
      "bytesDownloaded": 15728640,
      "status": "paused"
    }
  ],
  "createdAt": "2025-11-25T08:00:00Z",
  "startedAt": "2025-11-25T08:01:00Z",
  "lastUpdateAt": "2025-11-25T08:15:30Z",
  "retryCount": 0,
  "options": {
    "maxConnections": 4,
    "headers": {
      "User-Agent": "Kurio/1.0"
    }
  }
}
```

### 5.2 Resume Validation

Before resuming, validate:

1. **ETag Match**: If server provides ETag, verify it matches
2. **Last-Modified**: If no ETag, check Last-Modified hasn't changed
3. **File Size**: Confirm file size is still the same
4. **Range Support**: Verify server still supports ranges

If validation fails:

- Log warning
- Offer to restart download from beginning
- Optionally keep partial files

### 5.3 Partial File Management

```plain
Temp Directory Structure:
/temp/downloads/
  ├── {taskId}/
  │   ├── download.part      # Partial download file
  │   └── state.json         # Download state
  └── orphaned/              # Recovery directory
```

On startup:

1. Scan temp directory
2. Load all state.json files
3. Identify orphaned downloads
4. Offer recovery or cleanup

---

## 6. Protocol Support

### 6.1 HTTP/HTTPS Implementation

**Technology**: `HttpClient` with `SocketsHttpHandler`

**Key Features**:

- Connection pooling via `SocketsHttpHandler`
- Custom headers support
- Authentication (Basic, Bearer, Digest via `HttpClientHandler`)
- Automatic decompression (gzip, deflate)
- Redirect handling (configurable)
- Timeout configuration
- Proxy support

**Range Request Example**:

```csharp
var request = new HttpRequestMessage(HttpMethod.Get, url);
request.Headers.Range = new RangeHeaderValue(startByte, endByte);
request.Headers.Add("User-Agent", "Kurio/1.0");

var response = await httpClient.SendAsync(
    request, 
    HttpCompletionOption.ResponseHeadersRead,
    cancellationToken);

response.EnsureSuccessStatusCode();

await using var stream = await response.Content.ReadAsStreamAsync();
// Write to file at offset...
```

### 6.2 FTP/FTPS Implementation

**Technology**: `FluentFTP` library (or similar)

**Key Features**:

- FTP and FTPS (Explicit/Implicit SSL)
- Authentication
- Passive and Active modes
- Binary transfer
- Resume support via REST command
- Directory operations

**Considerations**:

- Firewall/NAT traversal
- Server compatibility variations
- Fallback to active mode if passive fails

### 6.3 Future Protocol Extensibility

The `IProtocolHandler` abstraction allows adding:

- SFTP (SSH File Transfer Protocol)
- BitTorrent (via plugins)
- Cloud storage APIs (S3, Google Drive, etc.)
- Custom protocols via plugins

---

## 7. Technology Stack

### 7.1 Core Technologies

| Component         | Technology                               | Version  |
|-------------------|------------------------------------------|----------|
| **Runtime**       | .NET                                     | 10.0+    |
| **Language**      | C#                                       | 13.0+    |
| **HTTP Client**   | HttpClient                               | Built-in |
| **FTP Client**    | FluentFTP                                | Latest   |
| **DI Container**  | Microsoft.Extensions.DependencyInjection | Latest   |
| **Logging**       | Microsoft.Extensions.Logging             | Latest   |
| **Configuration** | Microsoft.Extensions.Configuration       | Latest   |
| **Serialization** | System.Text.Json                         | Built-in |

### 7.2 Supporting Libraries

| Purpose                 | Library         | Rationale                       |
|-------------------------|-----------------|---------------------------------|
| **Reactive Extensions** | System.Reactive | Progress updates stream         |
| **Async Utils**         | Nito.AsyncEx    | AsyncLock, AsyncCollection      |
| **Polly**               | Polly           | Retry policies, circuit breaker |
| **Checksums**           | Built-in        | System.Security.Cryptography    |

### 7.3 Dependency Injection Structure

```csharp
services.AddSingleton<IDownloadEngine, DownloadEngine>();
services.AddSingleton<IDownloadQueueManager, DownloadQueueManager>();
services.AddSingleton<IStorageManager, StorageManager>();
services.AddSingleton<IStatePersistence, JsonStatePersistence>();

// Protocol handlers
services.AddSingleton<IProtocolHandler, HttpProtocolHandler>();
services.AddSingleton<IProtocolHandler, FtpProtocolHandler>();
services.AddSingleton<IProtocolHandlerFactory, ProtocolHandlerFactory>();

// Per-download services
services.AddTransient<IDownloadTaskExecutor, DownloadTaskExecutor>();
services.AddTransient<ISegmentManager, SegmentManager>();
services.AddTransient<IProgressTracker, ProgressTracker>();
services.AddTransient<IChecksumVerifier, ChecksumVerifier>();
services.AddTransient<IRetryManager, RetryManager>();

// HttpClient with proper lifecycle
services.AddHttpClient("KurioDownloader")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        MaxConnectionsPerServer = 8
    });
```

---

## 8. Non-Functional Requirements

### 8.1 Performance

| Metric             | Target                  | Notes                          |
|--------------------|-------------------------|--------------------------------|
| **Download Speed** | 90% of network capacity | With 8 connections             |
| **Resume Time**    | < 1 second              | Time to resume paused download |
| **Memory Usage**   | < 100 MB per download   | Excluding file buffers         |
| **CPU Usage**      | < 10% average           | During active download         |
| **Startup Time**   | < 2 seconds             | Application launch             |

### 8.2 Reliability

- **Success Rate**: 99% for successful downloads on stable connections
- **Resume Success**: 99.9% for resumable downloads
- **Data Integrity**: 100% (via checksums)
- **Crash Recovery**: Automatic recovery from all state files

### 8.3 Scalability

- Support up to 100 concurrent downloads in queue
- Support up to 10 simultaneous active downloads
- Support files up to 100 GB
- Support download history of 10,000 entries

### 8.4 Security

- **TLS**: TLS 1.2+ for HTTPS
- **Certificate Validation**: Strict by default, configurable
- **Checksum Verification**: Mandatory for critical files
- **Credential Storage**: Encrypted using OS keychain
- **No Plaintext Passwords**: Use secure storage APIs

### 8.5 Compatibility

- **Platforms**: Windows 10+, macOS 12+, Linux (modern distros)
- **Architectures**: x64, ARM64
- **.NET Runtime**: Self-contained or framework-dependent
- **File Systems**: NTFS, APFS, ext4, etc.

---

## 9. Implementation Phases

### Phase 1: Foundation (Week 1-2)

- Define all interfaces
- Implement basic `HttpProtocolHandler`
- Implement `StorageManager`
- Set up DI container
- Basic unit tests

### Phase 2: Core Engine (Week 3-5)

- Implement `DownloadEngine`
- Implement `SegmentManager`
- Implement single-threaded download
- Add multi-threaded segmentation
- Integration tests

### Phase 3: Persistence (Week 6-7)

- Implement state persistence
- Add pause/resume logic
- Add recovery on startup
- Comprehensive resume tests

### Phase 4: Queue & Management (Week 8-9)

- Implement `DownloadQueueManager`
- Add priority management
- Add concurrent download limits
- Queue persistence

### Phase 5: Enhanced Features (Week 10-12)

- Add checksum verification
- Implement retry logic
- Add progress tracking
- Add FTP protocol support

### Phase 6: Polish & Testing (Week 13-14)

- Performance optimization
- Cross-platform testing
- Error handling improvements
- Documentation

---

## 10. Success Metrics

### Development Metrics

- Code coverage: >80%
- Build success rate: >95%
- Test pass rate: 100%
- No critical security vulnerabilities

### User Experience Metrics

- Download success rate: >99%
- Resume success rate: >99.9%
- Average speed: >90% of network capacity
- User satisfaction: >4.5/5 (future surveys)

### Performance Metrics

- Memory usage < 100 MB per download
- CPU usage < 10% average
- Startup time < 2 seconds
- Resume time < 1 second

---

## 11. Risks and Mitigations

| Risk                        | Impact | Probability | Mitigation                                     |
|-----------------------------|--------|-------------|------------------------------------------------|
| **Server incompatibility**  | High   | Medium      | Extensive testing, fallback to single-threaded |
| **File system limitations** | Medium | Low         | Platform-specific testing, error handling      |
| **Network instability**     | High   | Medium      | Robust retry logic, circuit breaker            |
| **Memory leaks**            | High   | Low         | Profiling, dispose patterns, testing           |
| **State corruption**        | High   | Low         | Atomic writes, validation, backups             |
| **Performance issues**      | Medium | Medium      | Benchmarking, profiling, optimization          |

---

## 12. Open Questions

1. **Buffer Sizes**: What are optimal buffer sizes for different network speeds?
2. **Segment Count**: Should segment count adapt dynamically based on speed?
3. **State Format**: JSON vs binary format for state persistence?
4. **Cleanup Policy**: When to delete old/failed download state files?
5. **Notification System**: How to notify users of download completion?

---

## 13. References

- [HTTP Range Requests (RFC 7233)](https://tools.ietf.org/html/rfc7233)
- [.NET HttpClient Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [Polly Resilience Framework](https://www.pollydocs.org/)

---

## Appendix A: State Machine Diagram

```plain
                    ┌─────────┐
                    │ Created │
                    └────┬────┘
                         │
                         ▼
                    ┌─────────┐
              ┌────▶│ Queued  │◀───┐
              │     └────┬────┘    │
              │          │         │
              │          ▼         │
              │     ┌─────────┐    │
              │     │Analyzing│    │
              │     └────┬────┘    │
              │          │         │
              │          ▼         │
              │   ┌────────────┐   │
              │   │Downloading │   │
              │   └─┬────────┬─┘   │
              │     │        │     │
              │     │        └────►│
       ┌──────┴──┐  │              │
       │ Paused  │◄─┘              │
       └────┬────┘                 │
            │                      │
            └──────────────────────┘
      ┌──────────┐         ┌──────────┐
      │ Failed   │         │Completed │
      └──────────┘         └──────────┘
      ┌──────────┐
      │Cancelled │
      └──────────┘
```

---

## Appendix B: Sample Configuration

```json
{
  "downloadEngine": {
    "maxConcurrentDownloads": 3,
    "maxConnectionsPerDownload": 8,
    "minSegmentSize": 1048576,
    "segmentBufferSize": 8192,
    "defaultDownloadDirectory": "~/Downloads",
    "tempDirectory": "~/.kurio/temp",
    "stateDirectory": "~/.kurio/state"
  },
  "httpOptions": {
    "userAgent": "Kurio/1.0",
    "timeout": 30,
    "followRedirects": true,
    "maxRedirects": 5,
    "validateCertificate": true
  },
  "retryPolicy": {
    "maxRetries": 3,
    "initialDelay": 1,
    "maxDelay": 60,
    "backoffMultiplier": 2.0
  },
  "verification": {
    "checksumAlgorithm": "SHA256",
    "autoVerify": true,
    "failOnMismatch": true
  }
}
```

---

**Document Status**: ✅ Ready for Review  
**Next Steps**: Team review → Approval → Begin implementation (Issue #5)
