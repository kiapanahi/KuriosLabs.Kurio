# Domain Model Architecture

**Version:** 1.0  
**Date:** November 25, 2025  
**Status:** Approved  
**Related Issue:** [#5](https://github.com/kiapanahi/KuriosLabs.Kurio/issues/5)

---

## Overview

This document describes the domain model and core abstractions that form the foundation of the Kurio download manager. The architecture follows SOLID principles with a clear separation of concerns through well-defined interfaces and cohesive domain entities.

---

## 1. Core Interfaces

### 1.1 Class Diagram - Core Abstractions

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             Download Engine                                  │
│                                                                             │
│  ┌─────────────────┐     uses      ┌───────────────────┐                    │
│  │ IDownloadEngine │──────────────▶│  IProtocolHandler │                    │
│  │                 │               │                   │                    │
│  │ + AddDownload() │               │ + GetMetadata()   │                    │
│  │ + StartDownload │               │ + DownloadRange() │                    │
│  │ + PauseDownload │               │ + GetFileSize()   │                    │
│  │ + ResumeDownload│               │ + SupportsRange() │                    │
│  │ + CancelDownload│               └───────────────────┘                    │
│  │ + GetDownloads()│                        △                               │
│  │ + Progress      │                        │                               │
│  └────────┬────────┘               ┌────────┴────────┐                      │
│           │                        │                 │                      │
│           │                 ┌──────┴──────┐  ┌───────┴───────┐              │
│           │ manages         │HttpProtocol │  │ FtpProtocol   │              │
│           ▼                 │Handler      │  │ Handler (TBD) │              │
│  ┌─────────────────┐        └─────────────┘  └───────────────┘              │
│  │ IDownloadTask   │                                                        │
│  │                 │                                                        │
│  │ + Id            │                                                        │
│  │ + Url           │                                                        │
│  │ + State         │                                                        │
│  │ + Progress      │◀──────────┐                                            │
│  │ + Options       │           │                                            │
│  │ + Metadata      │           │                                            │
│  └────────┬────────┘           │                                            │
│           │                    │                                            │
│           │ uses               │ updates                                    │
│           ▼                    │                                            │
│  ┌─────────────────┐     ┌─────┴───────────┐                                │
│  │ IStorageManager │     │ ISegmentManager │                                │
│  │                 │     │                 │                                │
│  │ + CreateTemp()  │     │ + Calculate()   │                                │
│  │ + WriteSegment()│◀────│ + Download()    │                                │
│  │ + Commit()      │     │ + Resume()      │                                │
│  │ + Cleanup()     │     └─────────────────┘                                │
│  │ + GetDiskSpace()│                                                        │
│  └─────────────────┘                                                        │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 IDownloadEngine

The main orchestrator for all download operations. Manages the download queue, coordinates between components, and provides observable progress updates.

**Location:** `src/Kurio.Core/Abstractions/IDownloadEngine.cs`

```csharp
public interface IDownloadEngine
{
    // Queue Operations
    Task<IDownloadTask> AddDownloadAsync(Uri url, DownloadOptions options, CancellationToken ct);
    
    // Download Control
    Task StartDownloadAsync(Guid taskId, CancellationToken ct);
    Task PauseDownloadAsync(Guid taskId, CancellationToken ct);
    Task ResumeDownloadAsync(Guid taskId, CancellationToken ct);
    Task CancelDownloadAsync(Guid taskId, bool removePartialFiles, CancellationToken ct);
    
    // Query Operations
    IDownloadTask? GetDownload(Guid taskId);
    IEnumerable<IDownloadTask> GetDownloads(DownloadStateFilter filter);
    
    // Progress Streaming
    IObservable<DownloadProgress> ProgressUpdates { get; }
}
```

**Responsibilities:**
- Download lifecycle management (add, start, pause, resume, cancel)
- Concurrent download limits enforcement
- Progress aggregation and streaming via Reactive Extensions
- Task state transitions

### 1.3 IDownloadTask

Represents an individual download task with all associated metadata and state.

**Location:** `src/Kurio.Core/Abstractions/IDownloadTask.cs`

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

**Responsibilities:**
- Immutable download identification (Id, Url)
- Mutable state tracking (State, Progress, Metadata)
- Configuration holding (Options)
- Error and retry tracking

### 1.4 IProtocolHandler

Abstraction for protocol-specific download operations, allowing support for multiple protocols (HTTP, HTTPS, FTP, etc.).

**Location:** `src/Kurio.Core/Abstractions/IProtocolHandler.cs`

```csharp
public interface IProtocolHandler
{
    IReadOnlySet<string> SupportedSchemes { get; }
    
    Task<bool> SupportsRangeRequestsAsync(Uri url, DownloadOptions options, CancellationToken ct);
    Task<long> GetFileSizeAsync(Uri url, DownloadOptions options, CancellationToken ct);
    Task DownloadRangeAsync(Uri url, ByteRange range, Stream destination, 
                           DownloadOptions options, IProgress<long>? progress, CancellationToken ct);
    Task<ResourceMetadata> GetMetadataAsync(Uri url, DownloadOptions options, CancellationToken ct);
}
```

**Responsibilities:**
- Protocol-specific network operations
- Range request support detection
- Metadata retrieval (ETag, Last-Modified, Content-Type)
- Segment downloading

### 1.5 ISegmentManager

Manages download segmentation for multi-threaded parallel downloads.

**Location:** `src/Kurio.Core/Abstractions/ISegmentManager.cs`

```csharp
public interface ISegmentManager
{
    SegmentConfiguration CalculateSegments(long fileSize, bool supportsRanges, SegmentOptions options);
    
    Task DownloadSegmentsAsync(IProtocolHandler handler, Uri url, SegmentConfiguration config,
                               string tempFilePath, DownloadOptions options, 
                               IProgress<SegmentProgress>? progress, CancellationToken ct);
    
    Task ResumeSegmentsAsync(IProtocolHandler handler, Uri url, SegmentConfiguration config,
                             SegmentState[] segmentStates, string tempFilePath, 
                             DownloadOptions options, IProgress<SegmentProgress>? progress, 
                             CancellationToken ct);
}
```

**Responsibilities:**
- Optimal segment calculation based on file size and connection limits
- Parallel segment download orchestration
- Resume support for incomplete segments

### 1.6 IStorageManager

Manages file system operations for downloads, including temporary file handling and atomic commits.

**Location:** `src/Kurio.Core/Abstractions/IStorageManager.cs`

```csharp
public interface IStorageManager
{
    Task<string> CreateTemporaryFileAsync(Guid taskId, string fileName, long fileSize, CancellationToken ct);
    Task WriteSegmentAsync(string filePath, long offset, byte[] data, int count, CancellationToken ct);
    Task<string> CommitDownloadAsync(string tempFilePath, string destDir, string fileName, 
                                     FileNamingPolicy policy, CancellationToken ct);
    Task<long> GetAvailableDiskSpaceAsync(string path, CancellationToken ct);
    Task CleanupTemporaryFilesAsync(Guid taskId, CancellationToken ct);
}
```

**Responsibilities:**
- Temporary file creation with pre-allocation
- Concurrent segment writing at specific offsets
- Atomic file commit with naming conflict resolution
- Disk space verification
- Cleanup of temporary files

---

## 2. Domain Entities

### 2.1 Class Diagram - Domain Entities

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Domain Entities                                    │
│                                                                             │
│  ┌──────────────────┐        ┌─────────────────┐                            │
│  │  DownloadOptions │        │ ResourceMetadata│                            │
│  │                  │        │                 │                            │
│  │ + DestinationDir │        │ + ETag          │                            │
│  │ + FileName       │        │ + LastModified  │                            │
│  │ + MaxConnections │        │ + ContentType   │                            │
│  │ + MinSegmentSize │        │ + ContentLength │                            │
│  │ + Headers        │        │ + SupportsRanges│                            │
│  │ + UserAgent      │        │ + SuggestedName │                            │
│  │ + Timeout        │        └─────────────────┘                            │
│  │ + Checksum       │                                                       │
│  └──────────────────┘                                                       │
│                                                                             │
│  ┌──────────────────┐        ┌─────────────────┐                            │
│  │ DownloadProgress │        │  DownloadError  │                            │
│  │                  │        │                 │                            │
│  │ + BytesDownloaded│        │ + Message       │                            │
│  │ + TotalBytes     │        │ + ExceptionType │                            │
│  │ + Percentage     │        │ + StackTrace    │                            │
│  │ + BytesPerSecond │        │ + Timestamp     │                            │
│  │ + ETA            │        │ + IsRecoverable │                            │
│  │ + ActiveConns    │        └─────────────────┘                            │
│  └──────────────────┘                                                       │
│                                                                             │
│  ┌──────────────────┐        ┌─────────────────┐                            │
│  │ ByteRange        │        │SegmentOptions   │                            │
│  │ (record struct)  │        │                 │                            │
│  │                  │        │ + MaxConnections│                            │
│  │ + Start          │        │ + MinSegmentSize│                            │
│  │ + End            │        │ + BufferSize    │                            │
│  │ + Length         │        └─────────────────┘                            │
│  └──────────────────┘                                                       │
│                                                                             │
│  ┌──────────────────────────────────────────────┐                           │
│  │           SegmentConfiguration               │                           │
│  │                                              │                           │
│  │ + FileSize: long                             │                           │
│  │ + SegmentCount: int                          │                           │
│  │ + SupportsRanges: bool                       │                           │
│  │ + Ranges: ByteRange[]                        │◀───contains──┐            │
│  │ + States: SegmentState[]                     │◀───contains──┤            │
│  └──────────────────────────────────────────────┘              │            │
│                                                                │            │
│  ┌──────────────────┐        ┌─────────────────┐               │            │
│  │   SegmentState   │        │ SegmentProgress │               │            │
│  │                  │        │                 │               │            │
│  │ + SegmentIndex   │        │ + SegmentIndex  │───────────────┘            │
│  │ + StartByte      │        │ + BytesDownload │                            │
│  │ + EndByte        │        │ + Status        │                            │
│  │ + BytesDownloaded│        │ + Timestamp     │                            │
│  │ + Status         │        └─────────────────┘                            │
│  │ + StartedAt      │                                                       │
│  │ + CompletedAt    │                                                       │
│  │ + RetryCount     │                                                       │
│  │ + TotalSize      │                                                       │
│  │ + IsComplete     │                                                       │
│  └──────────────────┘                                                       │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Download Metadata Entities

#### DownloadOptions

Configuration for a download task.

**Location:** `src/Kurio.Core/Models/DownloadOptions.cs`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DestinationDirectory` | `string` | required | Target directory for downloaded file |
| `FileName` | `string?` | null | Override filename (inferred from URL if not provided) |
| `MaxConnections` | `int` | 8 | Maximum parallel connections |
| `MinSegmentSize` | `long` | 1 MB | Minimum bytes per segment |
| `FileNamingPolicy` | `FileNamingPolicy` | AutoRename | Conflict resolution strategy |
| `Headers` | `IDictionary<string, string>` | empty | Custom HTTP headers |
| `Credentials` | `string?` | null | Basic auth (username:password) |
| `UserAgent` | `string` | "Kurio/1.0" | User-Agent header value |
| `TimeoutSeconds` | `int` | 30 | Request timeout |
| `FollowRedirects` | `bool` | true | Follow HTTP redirects |
| `MaxRedirects` | `int` | 5 | Maximum redirect count |
| `ValidateCertificate` | `bool` | true | SSL certificate validation |
| `ExpectedChecksum` | `string?` | null | Checksum for verification |
| `ChecksumAlgorithm` | `string?` | null | Algorithm (SHA256, MD5) |

#### ResourceMetadata

Metadata about the remote resource retrieved from server.

**Location:** `src/Kurio.Core/Models/ResourceMetadata.cs`

| Property | Type | Description |
|----------|------|-------------|
| `ETag` | `string?` | HTTP ETag header for cache validation |
| `LastModified` | `DateTimeOffset?` | Last-Modified timestamp |
| `ContentType` | `string?` | MIME type of the resource |
| `ContentLength` | `long` | File size in bytes |
| `SupportsRanges` | `bool` | Whether server supports range requests |
| `SuggestedFileName` | `string?` | Filename from Content-Disposition |
| `AdditionalHeaders` | `IDictionary<string, string>` | Non-standard headers |

### 2.3 Progress Tracking Entities

#### DownloadProgress

Tracks overall download progress.

**Location:** `src/Kurio.Core/Models/DownloadProgress.cs`

| Property | Type | Description |
|----------|------|-------------|
| `BytesDownloaded` | `long` | Total bytes received |
| `TotalBytes` | `long` | Total file size (0 if unknown) |
| `Percentage` | `double` | Completion percentage (0-100) |
| `BytesPerSecond` | `long` | Current download speed |
| `EstimatedTimeRemaining` | `TimeSpan?` | ETA to completion |
| `ActiveConnections` | `int` | Number of active segments |
| `Timestamp` | `DateTime` | Time of this progress update |

#### SegmentProgress

Progress for an individual download segment.

**Location:** `src/Kurio.Core/Models/SegmentProgress.cs`

| Property | Type | Description |
|----------|------|-------------|
| `SegmentIndex` | `int` | Zero-based segment identifier |
| `BytesDownloaded` | `long` | Bytes downloaded for this segment |
| `Status` | `SegmentStatus` | Current segment status |
| `Timestamp` | `DateTime` | Time of this progress update |

### 2.4 State Representations

#### DownloadState

Enumeration of possible download task states.

**Location:** `src/Kurio.Core/Models/DownloadState.cs`

```
Created → Queued → Analyzing → Downloading → Completed
                      ↓              ↓
                    Failed       Paused → (Resume) → Queued
                                    ↓
                                Cancelled
```

| Value | Description |
|-------|-------------|
| `Created` | Task created but not queued |
| `Queued` | Waiting in queue to start |
| `Analyzing` | Retrieving metadata (size, range support) |
| `Downloading` | Actively transferring data |
| `Paused` | Paused by user or system |
| `Completed` | Successfully finished |
| `Failed` | Terminated due to error |
| `Cancelled` | Cancelled by user |

#### SegmentState

State tracking for an individual download segment.

**Location:** `src/Kurio.Core/Models/SegmentState.cs`

| Property | Type | Description |
|----------|------|-------------|
| `SegmentIndex` | `int` | Zero-based segment identifier |
| `StartByte` | `long` | Start position (inclusive) |
| `EndByte` | `long` | End position (inclusive) |
| `BytesDownloaded` | `long` | Bytes completed for segment |
| `Status` | `SegmentStatus` | Segment download status |
| `StartedAt` | `DateTime?` | When segment started |
| `CompletedAt` | `DateTime?` | When segment completed |
| `RetryCount` | `int` | Number of retry attempts |
| `TotalSize` | `long` | Calculated segment size |
| `IsComplete` | `bool` | Whether segment is finished |

#### SegmentStatus

Status enumeration for segments.

**Location:** `src/Kurio.Core/Models/SegmentStatus.cs`

| Value | Description |
|-------|-------------|
| `Pending` | Not yet started |
| `Downloading` | Currently transferring |
| `Paused` | Paused |
| `Completed` | Successfully finished |
| `Failed` | Error occurred |

### 2.5 Configuration Models

#### SegmentConfiguration

Complete configuration for a segmented download.

**Location:** `src/Kurio.Core/Models/SegmentConfiguration.cs`

| Property | Type | Description |
|----------|------|-------------|
| `FileSize` | `long` | Total file size |
| `SegmentCount` | `int` | Number of segments |
| `SupportsRanges` | `bool` | Range request support |
| `Ranges` | `ByteRange[]` | Byte ranges for each segment |
| `States` | `SegmentState[]` | State for each segment |

#### ByteRange

Value type representing a byte range for partial downloads.

**Location:** `src/Kurio.Core/Models/ByteRange.cs`

```csharp
public readonly record struct ByteRange(long Start, long End)
{
    public long Length => End - Start + 1;
    public static ByteRange FromLength(long start, long length);
    public override string ToString() => $"bytes={Start}-{End}";
}
```

### 2.6 Error Handling

#### DownloadError

Error information for failed downloads.

**Location:** `src/Kurio.Core/Models/DownloadError.cs`

| Property | Type | Description |
|----------|------|-------------|
| `Message` | `string` | Human-readable error message |
| `ExceptionType` | `string?` | .NET exception type name |
| `StackTrace` | `string?` | Stack trace for debugging |
| `Timestamp` | `DateTime` | When error occurred |
| `IsRecoverable` | `bool` | Whether retry is possible |

### 2.7 Enumerations

#### DownloadPriority

Priority levels for queue ordering.

**Location:** `src/Kurio.Core/Models/DownloadPriority.cs`

| Value | Level | Description |
|-------|-------|-------------|
| `Low` | 0 | Background downloads |
| `Normal` | 1 | Default priority |
| `High` | 2 | User-requested priority |
| `Critical` | 3 | Immediate start if possible |

#### DownloadStateFilter

Flags for filtering downloads by state.

**Location:** `src/Kurio.Core/Models/DownloadStateFilter.cs`

| Flag | Description |
|------|-------------|
| `None` | No filter (matches nothing) |
| `Created` | Include created downloads |
| `Queued` | Include queued downloads |
| `Analyzing` | Include analyzing downloads |
| `Downloading` | Include active downloads |
| `Paused` | Include paused downloads |
| `Completed` | Include completed downloads |
| `Failed` | Include failed downloads |
| `Cancelled` | Include cancelled downloads |
| `Active` | Queued \| Analyzing \| Downloading |
| `All` | All states |

#### FileNamingPolicy

Conflict resolution for file names.

**Location:** `src/Kurio.Core/Models/FileNamingPolicy.cs`

| Value | Description |
|-------|-------------|
| `Overwrite` | Replace existing file |
| `AutoRename` | Add numeric suffix (e.g., file(1).txt) |
| `Skip` | Skip if file exists |
| `Prompt` | Prompt user (requires UI integration) |

---

## 3. Relationships

### 3.1 Component Relationship Diagram

```
                              ┌─────────────────┐
                              │ Download Engine │
                              │  (IDownloadEngine)
                              └───────┬─────────┘
                                      │
               ┌──────────────────────┼──────────────────────┐
               │                      │                      │
               ▼                      ▼                      ▼
    ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
    │  Download Task  │    │ Protocol Handler│    │ Storage Manager │
    │  (IDownloadTask)│    │(IProtocolHandler)│    │(IStorageManager)│
    └────────┬────────┘    └────────┬────────┘    └─────────────────┘
             │                      │                      ▲
             │                      │                      │
             │                      ▼                      │
             │             ┌─────────────────┐             │
             │             │ Segment Manager │─────────────┘
             │             │(ISegmentManager)│  writes segments
             │             └────────┬────────┘
             │                      │
             ▼                      ▼
    ┌─────────────────┐    ┌─────────────────┐
    │ Domain Entities │    │  Segment State  │
    │ - Progress      │    │  - ByteRange    │
    │ - Options       │    │  - SegmentState │
    │ - Metadata      │    │  - SegmentConfig│
    │ - Error         │    └─────────────────┘
    └─────────────────┘
```

### 3.2 Key Relationships

#### Download Queue ↔ Task Management

```
IDownloadEngine
    │
    ├── AddDownloadAsync() → creates → IDownloadTask
    │
    ├── GetDownload(Guid) → retrieves → IDownloadTask
    │
    ├── GetDownloads(filter) → queries → IEnumerable<IDownloadTask>
    │
    └── ProgressUpdates → streams → IObservable<DownloadProgress>
```

- **Relationship Type:** One-to-Many (Engine manages multiple Tasks)
- **Lifecycle:** Tasks are created by the engine and tracked by ID
- **Querying:** Tasks can be filtered by state using `DownloadStateFilter`

#### Task ↔ Protocol Handlers

```
IDownloadTask                    IProtocolHandler
    │                                   │
    ├── Url (scheme) ────────────────▶ SupportedSchemes
    │                                   │
    ├── Options ─────────────────────▶ GetMetadataAsync()
    │                                   │
    └── Metadata ◀───────────────────── returns ResourceMetadata
```

- **Relationship Type:** Many-to-Many (Tasks use handlers based on URL scheme)
- **Selection:** Handler selected by matching `Url.Scheme` to `SupportedSchemes`
- **Data Flow:** Options flow to handler; Metadata flows back to task

#### Task ↔ Segments

```
IDownloadTask
    │
    ├── FileSize ────────────────────▶ ISegmentManager.CalculateSegments()
    │                                          │
    │                                          ▼
    │                                  SegmentConfiguration
    │                                          │
    │                                          ├── Ranges: ByteRange[]
    │                                          │
    │                                          └── States: SegmentState[]
    │                                                      │
    └── Progress ◀─────────── aggregates ─────────────────┘
```

- **Relationship Type:** One-to-Many (Task has multiple Segments)
- **Segmentation:** `ISegmentManager` calculates optimal segments based on file size
- **Progress Aggregation:** Task progress aggregates individual segment progress

#### Engine ↔ Storage Manager

```
IDownloadEngine
    │
    └── ExecuteDownload() uses:
            │
            ├── IStorageManager.CreateTemporaryFileAsync()
            │       └── Allocates space for segmented writing
            │
            ├── IStorageManager.GetAvailableDiskSpaceAsync()
            │       └── Validates sufficient space before download
            │
            ├── IStorageManager.CommitDownloadAsync()
            │       └── Atomic move from temp to destination
            │
            └── IStorageManager.CleanupTemporaryFilesAsync()
                    └── Cleanup on cancel/failure
```

- **Relationship Type:** Dependency (Engine depends on Storage)
- **Temp Files:** Each task gets isolated temp directory
- **Atomic Operations:** Commit ensures complete files only

---

## 4. Implementation Details

### 4.1 Concrete Implementations

| Interface | Implementation | Location |
|-----------|----------------|----------|
| `IDownloadEngine` | `DownloadEngine` | `src/Kurio.Core/Engine/DownloadEngine.cs` |
| `IDownloadTask` | `DownloadTask` | `src/Kurio.Core/Engine/DownloadTask.cs` |
| `IProtocolHandler` | `HttpProtocolHandler` | `src/Kurio.Core/Protocols/HttpProtocolHandler.cs` |
| `ISegmentManager` | `SegmentManager` | `src/Kurio.Core/Engine/SegmentManager.cs` |
| `IStorageManager` | `StorageManager` | `src/Kurio.Core/Storage/StorageManager.cs` |

### 4.2 Dependency Injection

Services are registered via `ServiceCollectionExtensions.AddKurioDownloadEngine()`:

```csharp
services.AddSingleton<IStorageManager, StorageManager>();
services.AddTransient<ISegmentManager, SegmentManager>();
services.AddSingleton<IProtocolHandler, HttpProtocolHandler>();
services.AddSingleton<IDownloadEngine, DownloadEngine>();
```

### 4.3 Thread Safety

- `DownloadEngine`: Uses `ConcurrentDictionary` for task storage
- `StorageManager`: Uses `FileShare.Write` for concurrent segment writing
- `SegmentManager`: Each segment downloads in parallel via `Task.WhenAll`

---

## 5. Design Principles

### 5.1 SOLID Compliance

| Principle | Implementation |
|-----------|----------------|
| **Single Responsibility** | Each interface has one clear purpose (e.g., `IStorageManager` only handles file operations) |
| **Open/Closed** | New protocols can be added by implementing `IProtocolHandler` without modifying existing code |
| **Liskov Substitution** | All implementations are interchangeable with their interfaces |
| **Interface Segregation** | Small, focused interfaces (5 core interfaces vs. one monolithic interface) |
| **Dependency Inversion** | High-level `IDownloadEngine` depends on abstractions, not concrete implementations |

### 5.2 Design Patterns Used

| Pattern | Usage |
|---------|-------|
| **Strategy** | `IProtocolHandler` allows swapping protocol implementations |
| **Observer** | `IObservable<DownloadProgress>` for progress streaming |
| **Factory** | `IHttpClientFactory` for HTTP client creation |
| **Template Method** | Download execution flow in `DownloadEngine` |

---

## 6. Future Extensibility

### 6.1 Planned Extensions

- **FTP Protocol Handler**: Implement `IProtocolHandler` for FTP/FTPS
- **State Persistence**: Add `IStatePersistence` for pause/resume across restarts
- **Checksum Verification**: Add `IChecksumVerifier` for post-download validation
- **Retry Manager**: Add `IRetryManager` for smart retry policies

### 6.2 Extension Points

```
IProtocolHandler
    └── FtpProtocolHandler (planned)
    └── SftpProtocolHandler (future)
    └── CloudStorageHandler (future)

IStorageManager
    └── EncryptedStorageManager (future)
    └── CloudStorageManager (future)
```

---

## Appendix A: File Locations

```
src/Kurio.Core/
├── Abstractions/
│   ├── IDownloadEngine.cs
│   ├── IDownloadTask.cs
│   ├── IProtocolHandler.cs
│   ├── ISegmentManager.cs
│   └── IStorageManager.cs
├── Models/
│   ├── ByteRange.cs
│   ├── DownloadError.cs
│   ├── DownloadOptions.cs
│   ├── DownloadPriority.cs
│   ├── DownloadProgress.cs
│   ├── DownloadState.cs
│   ├── DownloadStateFilter.cs
│   ├── FileNamingPolicy.cs
│   ├── ResourceMetadata.cs
│   ├── SegmentConfiguration.cs
│   ├── SegmentOptions.cs
│   ├── SegmentProgress.cs
│   ├── SegmentState.cs
│   └── SegmentStatus.cs
├── Engine/
│   ├── DownloadEngine.cs
│   ├── DownloadTask.cs
│   └── SegmentManager.cs
├── Protocols/
│   └── HttpProtocolHandler.cs
├── Storage/
│   └── StorageManager.cs
└── ServiceCollectionExtensions.cs
```

---

**Document Status**: ✅ Approved  
**Related Documents**: 
- [Core Engine Architecture PRD](../prd/core-engine-architecture.md)
