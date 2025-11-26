# PRD: Kurio Web Service Architecture

**Status:** Draft  
**Version:** 1.0  
**Last Updated:** November 26, 2025  
**Owner:** Engineering Team  

---

## Executive Summary

Transform Kurio from a library-based download manager into a client-server architecture where the download engine runs as a standalone web service. This enables multiple UI clients (TUI, GUI, Web UI, browser extensions) to connect to a centralized download service, providing better resource management, remote access capabilities, and real-time progress updates.

---

## Background

### Current Architecture
- Download engine runs in-process within each client application
- Progress updates use `IObservable<DownloadProgress>` (Reactive Extensions)
- Single-threaded segment downloads with custom retry logic
- One pre-allocated file with concurrent writes at different offsets
- State persistence for pause/resume functionality

### Problems with Current Approach
1. **Tight coupling**: Engine lifecycle tied to UI process
2. **Resource duplication**: Each client needs its own engine instance
3. **No remote management**: Cannot control downloads from different devices
4. **Limited progress streaming**: `IObservable` not ideal for network streaming
5. **File corruption issues**: Concurrent writes sometimes cause data corruption
6. **Custom retry logic**: Reinventing the wheel instead of using proven solutions

---

## Goals

### Primary Goals
1. **Separation of Concerns**: Decouple download engine from UI clients
2. **Multi-client Support**: Allow multiple UIs to connect to same engine
3. **Real-time Updates**: Stream progress updates efficiently to connected clients
4. **Remote Management**: Enable controlling downloads from anywhere
5. **Improved Reliability**: Use industry-standard resilience patterns
6. **Better Performance**: Optimize concurrent operations and file I/O

### Non-Goals
- Cloud-hosted service (this is for local/network deployments)
- Multi-user authentication (single-user or trusted network for v1)
- Distributed downloads across multiple servers

---

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│            Kurio.Server (ASP.NET Core)                  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         REST API Layer                            │  │
│  │  - Download CRUD operations                       │  │
│  │  - Queue management                               │  │
│  │  - Configuration endpoints                        │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         Real-time Layer                           │  │
│  │  - SignalR Hub (bidirectional)                    │  │
│  │  - SSE Endpoint (server-to-client streaming)      │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         Background Services                       │  │
│  │  - DownloadEngine (IHostedService)                │  │
│  │  - Progress Broadcaster                           │  │
│  │  - State Persistence Worker                       │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │         Core Layer                                │  │
│  │  - Kurio.Core (existing engine)                   │  │
│  │  - Enhanced with IAsyncEnumerable                 │  │
│  │  - Polly-based resilience                         │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
              ↑              ↑              ↑
              │              │              │
    ┌─────────┴──┐   ┌──────┴──────┐   ┌──┴──────────┐
    │  Kurio.Cli │   │ Kurio.GUI   │   │  Web UI     │
    │   (TUI)    │   │  (Desktop)  │   │  (Browser)  │
    └────────────┘   └─────────────┘   └─────────────┘
              ↑              ↑              ↑
         HTTP/WS        HTTP/WS        HTTP/SSE
```

### Component Responsibilities

#### Kurio.Server
- **ASP.NET Core Minimal API** or **MVC** project
- Hosts the download engine as a background service
- Exposes REST API for download operations
- Provides real-time updates via SignalR/SSE
- Handles authentication and authorization (future)
- Manages cross-origin requests for web clients

#### Kurio.Core (Enhanced)
- **IAsyncEnumerable** for progress streaming
- **Polly** for resilience and retry policies
- **Per-segment file storage** option (configurable)
- **Segment-level checksums** for integrity verification
- **Improved concurrent write handling**

#### Client Applications
- **Kurio.Cli**: TUI client using Spectre.Console
- **Kurio.GUI**: Future desktop GUI client
- **Web UI**: Browser-based interface
- **Browser Extensions**: Chrome/Firefox/Edge integration

---

## Technical Specifications

### Phase 1: Foundation Improvements

#### 1.1 Migrate to Polly for Resilience

**Objective**: Replace custom retry logic with Polly policies.

**Implementation Details**:
- Remove `IRetryHandler` and `RetryHandler` classes
- Add Polly to `Kurio.Core` dependencies
- Create `ResiliencePolicyFactory` for building policies
- Update `SegmentManager` to use Polly policies
- Integrate with existing `CircuitBreaker` implementation
- Add telemetry hooks for monitoring

**Polly Policies to Implement**:
```csharp
// Retry policy with exponential backoff
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            logger.LogWarning("Retry {RetryCount} after {Delay}s due to {Exception}", 
                retryCount, timeSpan.TotalSeconds, exception.GetType().Name);
        });

// Circuit breaker policy
var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromSeconds(30));

// Timeout policy
var timeoutPolicy = Policy.TimeoutAsync(TimeSpan.FromMinutes(5));

// Combined policy
var resiliencePolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
```

**Configuration Model**:
```csharp
public class ResiliencePolicyOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public int InitialDelaySeconds { get; set; } = 1;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
    public int TimeoutMinutes { get; set; } = 5;
    public bool EnableJitter { get; set; } = true;
}
```

**Migration Tasks**:
- [ ] Add `Polly` NuGet package to `Directory.Packages.props`
- [ ] Create `ResiliencePolicyFactory` class
- [ ] Create `ResiliencePolicyOptions` configuration model
- [ ] Update `SegmentManager.DownloadSegmentWithRetryAsync` to use Polly
- [ ] Remove `RetryHandler` and `IRetryHandler` classes
- [ ] Update `RetryPolicy` model to configure Polly policies
- [ ] Add unit tests for policy behaviors
- [ ] Update documentation

---

#### 1.2 Fix Concurrent Write Issues

**Objective**: Ensure file integrity during multi-segment downloads.

**Root Cause Analysis**:
Current implementation uses `FileStream` with `FileShare.Write`, but:
- No explicit synchronization between concurrent writes
- Potential race conditions in buffer flushing
- No verification that bytes were written correctly

**Solution Strategy**:

**Option A: Enhanced Single-File Approach** (Recommended for Phase 1)
```csharp
public class StorageManager
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    
    public async Task WriteSegmentAsync(
        string filePath,
        long offset,
        byte[] data,
        int count,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,  // Changed from FileShare.Write
                bufferSize: 81920, // 80KB buffer
                options: FileOptions.WriteThrough | FileOptions.Asynchronous);

            fileStream.Seek(offset, SeekOrigin.Begin);
            await fileStream.WriteAsync(data.AsMemory(0, count), cancellationToken);
            await fileStream.FlushAsync(cancellationToken);
            
            // Verify write succeeded
            fileStream.Seek(offset, SeekOrigin.Begin);
            byte[] verifyBuffer = new byte[Math.Min(4096, count)];
            int bytesRead = await fileStream.ReadAsync(verifyBuffer, cancellationToken);
            
            if (!data.AsSpan(0, bytesRead).SequenceEqual(verifyBuffer.AsSpan(0, bytesRead)))
            {
                throw new IOException($"Write verification failed at offset {offset}");
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
```

**Option B: Per-Segment Files** (Configurable Alternative)
```csharp
public async Task<string> CreateSegmentFileAsync(
    Guid taskId,
    int segmentIndex,
    long segmentSize,
    CancellationToken cancellationToken)
{
    string taskDirectory = Path.Combine(_tempDirectory, taskId.ToString());
    Directory.CreateDirectory(taskDirectory);
    
    string segmentFilePath = Path.Combine(taskDirectory, $"segment_{segmentIndex:D4}.part");
    
    await using var fileStream = new FileStream(
        segmentFilePath,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        4096,
        FileOptions.Asynchronous);
    
    fileStream.SetLength(segmentSize);
    
    return segmentFilePath;
}

public async Task MergeSegmentFilesAsync(
    Guid taskId,
    string finalPath,
    int segmentCount,
    CancellationToken cancellationToken)
{
    await using var outputStream = new FileStream(
        finalPath,
        FileMode.Create,
        FileAccess.Write,
        FileShare.None,
        bufferSize: 1048576, // 1MB buffer for fast merge
        options: FileOptions.Asynchronous | FileOptions.SequentialScan);
    
    for (int i = 0; i < segmentCount; i++)
    {
        string segmentPath = GetSegmentFilePath(taskId, i);
        await using var inputStream = new FileStream(
            segmentPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1048576,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        
        await inputStream.CopyToAsync(outputStream, cancellationToken);
    }
}
```

**Configuration**:
```csharp
public class StorageOptions
{
    public StorageMode Mode { get; set; } = StorageMode.SingleFile;
    public bool VerifyWrites { get; set; } = true;
    public int WriteBufferSize { get; set; } = 81920; // 80KB
}

public enum StorageMode
{
    SingleFile,      // One file with concurrent writes (serialized)
    PerSegmentFiles  // One file per segment, merged at end
}
```

**Implementation Tasks**:
- [ ] Add `StorageOptions` configuration model
- [ ] Implement serialized write lock in `StorageManager`
- [ ] Add write verification logic
- [ ] Implement per-segment file storage mode
- [ ] Implement segment merge functionality
- [ ] Add configuration option to choose storage mode
- [ ] Add unit tests for both storage modes
- [ ] Add integration tests with simulated failures
- [ ] Measure and document performance differences

---

#### 1.3 Add Segment-Level Checksums

**Objective**: Verify integrity of each segment before and after writing.

**Implementation**:
```csharp
public class SegmentChecksum
{
    public int SegmentIndex { get; set; }
    public string Algorithm { get; set; } = "SHA256";
    public string Hash { get; set; } = string.Empty;
    public long ByteCount { get; set; }
    public DateTime ComputedAt { get; set; }
}

public interface ISegmentVerifier
{
    Task<SegmentChecksum> ComputeChecksumAsync(
        byte[] data,
        int count,
        int segmentIndex,
        CancellationToken cancellationToken = default);
    
    Task<bool> VerifySegmentAsync(
        string filePath,
        long offset,
        long length,
        SegmentChecksum expectedChecksum,
        CancellationToken cancellationToken = default);
}

public class SegmentVerifier : ISegmentVerifier
{
    public async Task<SegmentChecksum> ComputeChecksumAsync(
        byte[] data,
        int count,
        int segmentIndex,
        CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        byte[] hash = await Task.Run(() => 
            sha256.ComputeHash(data, 0, count), cancellationToken);
        
        return new SegmentChecksum
        {
            SegmentIndex = segmentIndex,
            Algorithm = "SHA256",
            Hash = Convert.ToBase64String(hash),
            ByteCount = count,
            ComputedAt = DateTime.UtcNow
        };
    }
    
    public async Task<bool> VerifySegmentAsync(
        string filePath,
        long offset,
        long length,
        SegmentChecksum expectedChecksum,
        CancellationToken cancellationToken = default)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous);
        
        fileStream.Seek(offset, SeekOrigin.Begin);
        
        byte[] buffer = new byte[length];
        int totalRead = 0;
        
        while (totalRead < length)
        {
            int read = await fileStream.ReadAsync(
                buffer.AsMemory(totalRead, (int)length - totalRead),
                cancellationToken);
            
            if (read == 0) break;
            totalRead += read;
        }
        
        var actualChecksum = await ComputeChecksumAsync(
            buffer, totalRead, expectedChecksum.SegmentIndex, cancellationToken);
        
        return actualChecksum.Hash == expectedChecksum.Hash;
    }
}
```

**Update SegmentState**:
```csharp
public class SegmentState
{
    // ... existing properties ...
    public SegmentChecksum? Checksum { get; set; }
}
```

**Integration with SegmentManager**:
```csharp
private async Task DownloadSegmentAsync(
    // ... parameters ...
)
{
    // ... download logic ...
    
    // Compute checksum before writing
    var checksum = await _segmentVerifier.ComputeChecksumAsync(
        buffer, buffer.Length, state.SegmentIndex, cancellationToken);
    
    state.Checksum = checksum;
    
    // Write to disk
    await _storageManager.WriteSegmentAsync(
        tempFilePath, range.Start, buffer, buffer.Length, cancellationToken);
    
    // Verify written data
    bool isValid = await _segmentVerifier.VerifySegmentAsync(
        tempFilePath, range.Start, range.Length, checksum, cancellationToken);
    
    if (!isValid)
    {
        throw new IOException(
            $"Segment {state.SegmentIndex} checksum verification failed after write");
    }
    
    // ... rest of logic ...
}
```

**Implementation Tasks**:
- [ ] Create `SegmentChecksum` model
- [ ] Create `ISegmentVerifier` interface and implementation
- [ ] Add `Checksum` property to `SegmentState`
- [ ] Update `SegmentManager` to compute checksums during download
- [ ] Update `SegmentManager` to verify checksums after writing
- [ ] Add checksum verification during resume
- [ ] Persist checksums in `DownloadTaskState`
- [ ] Add configuration option to enable/disable verification
- [ ] Add unit tests for checksum computation
- [ ] Add integration tests for verification logic

---

### Phase 2: Modernization

#### 2.1 Migrate to IAsyncEnumerable

**Objective**: Replace `IObservable<DownloadProgress>` with `IAsyncEnumerable<DownloadProgress>`.

**Benefits**:
- Native C# async/await support
- Built-in backpressure handling
- Better cancellation token integration
- Perfect for streaming over HTTP (SSE)
- No external library dependency (System.Linq.Async is optional)

**Updated Interface**:
```csharp
public interface IDownloadEngine
{
    // Replace this:
    // IObservable<DownloadProgress> ProgressUpdates { get; }
    
    // With this:
    IAsyncEnumerable<DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        CancellationToken cancellationToken = default);
}
```

**Implementation**:
```csharp
public class DownloadEngine : IDownloadEngine
{
    private readonly Channel<DownloadProgress> _progressChannel = 
        Channel.CreateUnbounded<DownloadProgress>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
    
    public async IAsyncEnumerable<DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var progress in _progressChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (taskId == null || progress.TaskId == taskId)
            {
                yield return progress;
            }
        }
    }
    
    private void PublishProgress(DownloadProgress progress)
    {
        _progressChannel.Writer.TryWrite(progress);
    }
}
```

**Update DownloadProgress Model**:
```csharp
public class DownloadProgress
{
    public Guid TaskId { get; set; }  // Add this
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double PercentComplete => TotalBytes > 0 
        ? (double)BytesDownloaded / TotalBytes * 100 
        : 0;
    public long BytesPerSecond { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
    public int ActiveConnections { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Client Consumption** (TUI example):
```csharp
// Old way with IObservable
_engine.ProgressUpdates.Subscribe(progress => 
{
    UpdateUI(progress);
});

// New way with IAsyncEnumerable
await foreach (var progress in _engine.StreamProgressAsync(taskId, cancellationToken))
{
    UpdateUI(progress);
}
```

**Implementation Tasks**:
- [ ] Replace `IObservable` with `IAsyncEnumerable` in `IDownloadEngine`
- [ ] Implement `Channel<DownloadProgress>` for progress streaming
- [ ] Add `TaskId` to `DownloadProgress` model
- [ ] Update all progress publishing points in `DownloadEngine`
- [ ] Update all progress publishing points in `SegmentManager`
- [ ] Remove System.Reactive dependency from `Kurio.Core`
- [ ] Update `Kurio.Cli` to consume `IAsyncEnumerable`
- [ ] Add cancellation support for progress streaming
- [ ] Add unit tests for progress streaming
- [ ] Update documentation

---

#### 2.2 Per-Segment File Storage (Configurable)

**Objective**: Make storage mode configurable between single-file and per-segment-file approaches.

This was covered in section 1.2 (Option B). Implementation tasks are the same.

**Additional Considerations**:
- Add performance benchmarks comparing both modes
- Document when to use each mode
- Consider memory-mapped files for very large files
- Add automatic cleanup of segment files on failure

---

### Phase 3: Web Service Implementation

#### 3.1 Create Kurio.Server Project

**Objective**: Create ASP.NET Core web service hosting the download engine.

**Project Structure**:
```
src/Kurio.Server/
├── Program.cs                      # Application entry point
├── Kurio.Server.csproj            # Project file
├── appsettings.json               # Configuration
├── appsettings.Development.json   # Dev configuration
├── Controllers/
│   ├── DownloadsController.cs     # REST API
│   └── HealthController.cs        # Health checks
├── Hubs/
│   └── DownloadHub.cs             # SignalR hub
├── Services/
│   ├── DownloadEngineHostedService.cs  # Background service
│   └── ProgressBroadcaster.cs          # Progress distribution
├── Models/
│   ├── AddDownloadRequest.cs
│   ├── DownloadResponse.cs
│   └── ErrorResponse.cs
└── Middleware/
    └── ErrorHandlingMiddleware.cs
```

**Program.cs Setup**:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Add Kurio.Core services
builder.Services.AddKurioCore(builder.Configuration);

// Add hosted service
builder.Services.AddHostedService<DownloadEngineHostedService>();
builder.Services.AddSingleton<ProgressBroadcaster>();

// Add CORS for web clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebClients", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite default
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthorization();

app.MapControllers();
app.MapHub<DownloadHub>("/hubs/downloads");

// SSE endpoint
app.MapGet("/api/downloads/stream", async (
    IDownloadEngine engine,
    CancellationToken cancellationToken) =>
{
    var response = Results.Stream(async stream =>
    {
        using var writer = new StreamWriter(stream);
        await foreach (var progress in engine.StreamProgressAsync(null, cancellationToken))
        {
            var json = JsonSerializer.Serialize(progress);
            await writer.WriteLineAsync($"data: {json}\n");
            await writer.FlushAsync();
        }
    }, "text/event-stream");
    
    return response;
});

app.Run();
```

**Implementation Tasks**:
- [ ] Create `Kurio.Server` project
- [ ] Add required NuGet packages
- [ ] Implement `Program.cs` with minimal API setup
- [ ] Configure dependency injection
- [ ] Add CORS configuration
- [ ] Add Swagger/OpenAPI documentation
- [ ] Create health check endpoints
- [ ] Add structured logging configuration
- [ ] Configure app settings
- [ ] Add Docker support

---

#### 3.2 Implement REST API

**Objective**: Full CRUD REST API for download operations.

**API Endpoints**:

```csharp
[ApiController]
[Route("api/downloads")]
public class DownloadsController : ControllerBase
{
    private readonly IDownloadEngine _engine;
    private readonly ILogger<DownloadsController> _logger;
    
    [HttpPost]
    [ProducesResponseType(typeof(DownloadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DownloadResponse>> AddDownload(
        [FromBody] AddDownloadRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _engine.AddDownloadAsync(
            new Uri(request.Url),
            request.ToDownloadOptions(),
            cancellationToken);
        
        var response = DownloadResponse.FromTask(task);
        return CreatedAtAction(nameof(GetDownload), new { id = task.Id }, response);
    }
    
    [HttpGet]
    [ProducesResponseType(typeof(List<DownloadResponse>), StatusCodes.Status200OK)]
    public ActionResult<List<DownloadResponse>> GetDownloads(
        [FromQuery] DownloadStateFilter filter = DownloadStateFilter.All)
    {
        var downloads = _engine.GetDownloads(filter)
            .Select(DownloadResponse.FromTask)
            .ToList();
        
        return Ok(downloads);
    }
    
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DownloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DownloadResponse> GetDownload(Guid id)
    {
        var task = _engine.GetDownload(id);
        if (task == null)
            return NotFound();
        
        return Ok(DownloadResponse.FromTask(task));
    }
    
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartDownload(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _engine.StartDownloadAsync(id, cancellationToken);
        return NoContent();
    }
    
    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PauseDownload(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _engine.PauseDownloadAsync(id, cancellationToken);
        return NoContent();
    }
    
    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResumeDownload(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _engine.ResumeDownloadAsync(id, cancellationToken);
        return NoContent();
    }
    
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelDownload(
        Guid id,
        [FromQuery] bool removeFiles = false,
        CancellationToken cancellationToken)
    {
        await _engine.CancelDownloadAsync(id, removeFiles, cancellationToken);
        return NoContent();
    }
    
    [HttpPost("{id:guid}/priority")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ChangePriority(
        Guid id,
        [FromBody] ChangePriorityRequest request)
    {
        var success = _engine.ChangePriority(id, request.Priority);
        if (!success)
            return NotFound();
        
        return NoContent();
    }
    
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(QueueStatistics), StatusCodes.Status200OK)]
    public ActionResult<QueueStatistics> GetStatistics()
    {
        var (active, queued) = _engine.GetQueueStatistics();
        return Ok(new QueueStatistics
        {
            ActiveDownloads = active,
            QueuedDownloads = queued,
            TotalDownloads = _engine.GetDownloads(DownloadStateFilter.All).Count()
        });
    }
}
```

**Request/Response Models**:
```csharp
public record AddDownloadRequest(
    string Url,
    string? FileName = null,
    string? DestinationDirectory = null,
    int? MaxConnections = null,
    DownloadPriority Priority = DownloadPriority.Normal);

public record DownloadResponse(
    Guid Id,
    string Url,
    string FileName,
    long FileSize,
    DownloadState State,
    DownloadPriority Priority,
    DownloadProgressDto Progress,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public record ErrorResponse(
    string Message,
    string? Details = null,
    string? TraceId = null);
```

**Implementation Tasks**:
- [ ] Create `DownloadsController` with all CRUD operations
- [ ] Create request/response DTOs
- [ ] Add input validation with FluentValidation
- [ ] Implement error handling middleware
- [ ] Add API versioning support
- [ ] Configure response compression
- [ ] Add rate limiting
- [ ] Add OpenAPI/Swagger annotations
- [ ] Create Postman/Insomnia collection
- [ ] Write API integration tests

---

#### 3.3 Add SignalR and SSE Support

**Objective**: Real-time progress updates via SignalR (bidirectional) and SSE (server-to-client).

**SignalR Hub**:
```csharp
public class DownloadHub : Hub
{
    private readonly IDownloadEngine _engine;
    private readonly ILogger<DownloadHub> _logger;
    
    public async Task SubscribeToProgress(Guid? taskId = null)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} subscribed to progress for task {TaskId}",
            connectionId, taskId?.ToString() ?? "all");
        
        // Note: Progress streaming handled by ProgressBroadcaster
        await Clients.Caller.SendAsync("Subscribed", taskId);
    }
    
    public async Task UnsubscribeFromProgress()
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} unsubscribed from progress", connectionId);
        
        await Clients.Caller.SendAsync("Unsubscribed");
    }
    
    public async Task<DownloadResponse> GetDownload(Guid id)
    {
        var task = _engine.GetDownload(id);
        if (task == null)
            throw new HubException($"Download {id} not found");
        
        return DownloadResponse.FromTask(task);
    }
}
```

**Progress Broadcaster** (Background Service):
```csharp
public class ProgressBroadcaster : BackgroundService
{
    private readonly IDownloadEngine _engine;
    private readonly IHubContext<DownloadHub> _hubContext;
    private readonly ILogger<ProgressBroadcaster> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Progress broadcaster started");
        
        await foreach (var progress in _engine.StreamProgressAsync(null, stoppingToken))
        {
            try
            {
                // Broadcast to all connected SignalR clients
                await _hubContext.Clients.All.SendAsync(
                    "ProgressUpdate",
                    progress,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting progress");
            }
        }
    }
}
```

**SSE Endpoint** (already shown in Program.cs):
```csharp
app.MapGet("/api/downloads/stream", async (
    IDownloadEngine engine,
    Guid? taskId,
    CancellationToken cancellationToken) =>
{
    return Results.Stream(async stream =>
    {
        using var writer = new StreamWriter(stream);
        await writer.WriteLineAsync("retry: 10000\n");
        
        await foreach (var progress in engine.StreamProgressAsync(taskId, cancellationToken))
        {
            var json = JsonSerializer.Serialize(progress, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await writer.WriteLineAsync($"event: progress");
            await writer.WriteLineAsync($"data: {json}");
            await writer.WriteLineAsync();
            await writer.FlushAsync();
        }
    }, "text/event-stream");
});
```

**Client Examples**:

**SignalR (TypeScript)**:
```typescript
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
    .withUrl('http://localhost:5000/hubs/downloads')
    .withAutomaticReconnect()
    .build();

connection.on('ProgressUpdate', (progress) => {
    console.log('Progress:', progress);
    updateProgressBar(progress);
});

await connection.start();
await connection.invoke('SubscribeToProgress', null); // null = all tasks
```

**SSE (JavaScript)**:
```javascript
const eventSource = new EventSource('http://localhost:5000/api/downloads/stream');

eventSource.addEventListener('progress', (event) => {
    const progress = JSON.parse(event.data);
    console.log('Progress:', progress);
    updateProgressBar(progress);
});

eventSource.onerror = (error) => {
    console.error('SSE error:', error);
};
```

**Implementation Tasks**:
- [ ] Create `DownloadHub` SignalR hub
- [ ] Create `ProgressBroadcaster` background service
- [ ] Implement SSE endpoint
- [ ] Add connection state management
- [ ] Implement graceful disconnection handling
- [ ] Add reconnection logic
- [ ] Create TypeScript client library
- [ ] Create JavaScript client library
- [ ] Write real-time communication tests
- [ ] Add performance monitoring for broadcasts

---

#### 3.4 Update CLI to Client Mode

**Objective**: Refactor `Kurio.Cli` to connect to `Kurio.Server` instead of hosting engine in-process.

**Architecture Changes**:
```
Before:
Kurio.Cli → IDownloadEngine (in-process)

After:
Kurio.Cli → KurioApiClient → HTTP/SignalR → Kurio.Server → IDownloadEngine
```

**API Client**:
```csharp
public interface IKurioApiClient
{
    Task<DownloadResponse> AddDownloadAsync(
        AddDownloadRequest request,
        CancellationToken cancellationToken = default);
    
    Task<List<DownloadResponse>> GetDownloadsAsync(
        DownloadStateFilter filter = DownloadStateFilter.All,
        CancellationToken cancellationToken = default);
    
    Task<DownloadResponse?> GetDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default);
    
    Task StartDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task PauseDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task ResumeDownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task CancelDownloadAsync(Guid id, bool removeFiles = false, CancellationToken cancellationToken = default);
    
    IAsyncEnumerable<DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        CancellationToken cancellationToken = default);
}

public class KurioApiClient : IKurioApiClient
{
    private readonly HttpClient _httpClient;
    private readonly HubConnection _hubConnection;
    private readonly Channel<DownloadProgress> _progressChannel;
    
    public KurioApiClient(HttpClient httpClient, string serverUrl)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(serverUrl);
        
        _progressChannel = Channel.CreateUnbounded<DownloadProgress>();
        
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/hubs/downloads")
            .WithAutomaticReconnect()
            .Build();
        
        _hubConnection.On<DownloadProgress>("ProgressUpdate", progress =>
        {
            _progressChannel.Writer.TryWrite(progress);
        });
    }
    
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _hubConnection.StartAsync(cancellationToken);
        await _hubConnection.InvokeAsync("SubscribeToProgress", null, cancellationToken);
    }
    
    public async Task<DownloadResponse> AddDownloadAsync(
        AddDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/downloads",
            request,
            cancellationToken);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DownloadResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }
    
    public async IAsyncEnumerable<DownloadProgress> StreamProgressAsync(
        Guid? taskId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var progress in _progressChannel.Reader.ReadAllAsync(cancellationToken))
        {
            if (taskId == null || progress.TaskId == taskId)
            {
                yield return progress;
            }
        }
    }
    
    // ... other methods ...
}
```

**Updated Program.cs**:
```csharp
var builder = Host.CreateApplicationBuilder(args);

// Configure API client
var serverUrl = builder.Configuration["Kurio:ServerUrl"] ?? "http://localhost:5000";
builder.Services.AddHttpClient<IKurioApiClient, KurioApiClient>(client =>
{
    client.BaseAddress = new Uri(serverUrl);
});

// Add TUI application
builder.Services.AddSingleton<KurioCliApplication>();

var host = builder.Build();

// Connect to server
var apiClient = host.Services.GetRequiredService<IKurioApiClient>();
await apiClient.ConnectAsync();

// Run TUI
var app = host.Services.GetRequiredService<KurioCliApplication>();
await app.RunAsync();
```

**Configuration** (appsettings.json):
```json
{
  "Kurio": {
    "ServerUrl": "http://localhost:5000",
    "AutoConnect": true,
    "ReconnectDelay": "00:00:05"
  }
}
```

**Implementation Tasks**:
- [ ] Create `IKurioApiClient` interface
- [ ] Implement `KurioApiClient` with HTTP and SignalR
- [ ] Add connection state management
- [ ] Add automatic reconnection logic
- [ ] Update `KurioCliApplication` to use API client
- [ ] Update all UI views to use API client
- [ ] Add offline mode support (show cached data)
- [ ] Add connection status indicator in TUI
- [ ] Handle server unavailability gracefully
- [ ] Add configuration for server URL
- [ ] Write integration tests with test server

---

## Configuration

### Kurio.Server Configuration

**appsettings.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Kurio": "Debug"
    }
  },
  "AllowedHosts": "*",
  "Kurio": {
    "Storage": {
      "TempDirectory": "~/Downloads/.kurio/temp",
      "StateDirectory": "~/Downloads/.kurio/state",
      "DefaultDestination": "~/Downloads",
      "Mode": "SingleFile",
      "VerifyWrites": true
    },
    "Engine": {
      "MaxConcurrentDownloads": 3,
      "DefaultMaxConnections": 8,
      "MinSegmentSize": 10485760
    },
    "Resilience": {
      "MaxRetryAttempts": 3,
      "InitialDelaySeconds": 1,
      "CircuitBreakerThreshold": 5,
      "CircuitBreakerDurationSeconds": 30,
      "TimeoutMinutes": 5,
      "EnableJitter": true
    },
    "Server": {
      "EnableCors": true,
      "AllowedOrigins": ["http://localhost:5173"],
      "EnableSwagger": true,
      "EnableHealthChecks": true
    }
  }
}
```

---

## Security Considerations

### Phase 1 (v1.0)
- Single-user, localhost deployment
- No authentication/authorization
- Trust localhost connections
- File system permissions only

### Future Phases
- [ ] Add API key authentication
- [ ] Add JWT-based authentication
- [ ] Add role-based authorization
- [ ] Add HTTPS enforcement
- [ ] Add rate limiting per client
- [ ] Add request validation
- [ ] Add CSRF protection
- [ ] Add input sanitization
- [ ] Add audit logging
- [ ] Add encrypted state storage

---

## Performance Requirements

### Latency
- API response time: < 100ms (p95)
- Progress update latency: < 50ms
- Download start time: < 1s

### Throughput
- Support 100+ concurrent downloads
- Handle 1000+ progress updates/second
- Support 50+ connected clients

### Resource Usage
- Memory: < 2GB for 100 downloads
- CPU: < 20% idle, < 80% during downloads
- Disk I/O: Maximize write throughput

---

## Testing Strategy

### Unit Tests
- All core business logic
- Policy configurations
- Checksum calculations
- Storage operations

### Integration Tests
- Full download workflows
- Pause/resume functionality
- API endpoints
- SignalR connections
- SSE streaming

### Performance Tests
- Load testing with 100+ downloads
- Stress testing with large files
- Endurance testing (24+ hours)
- Memory leak detection

### End-to-End Tests
- TUI client connecting to server
- Web UI connecting to server
- Multiple clients simultaneously
- Network failure scenarios

---

## Documentation Requirements

- [ ] API documentation (OpenAPI/Swagger)
- [ ] Architecture decision records (ADRs)
- [ ] Deployment guide
- [ ] Configuration guide
- [ ] Client SDK documentation
- [ ] Performance tuning guide
- [ ] Troubleshooting guide
- [ ] Migration guide (from library to service)

---

## Deployment

### Local Development
```bash
# Start server
cd src/Kurio.Server
dotnet run

# Start CLI client
cd src/Kurio.Cli
dotnet run
```

### Docker Deployment
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Kurio.Server/Kurio.Server.csproj", "Kurio.Server/"]
COPY ["src/Kurio.Core/Kurio.Core.csproj", "Kurio.Core/"]
RUN dotnet restore "Kurio.Server/Kurio.Server.csproj"
COPY . .
WORKDIR "/src/Kurio.Server"
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Kurio.Server.dll"]
```

```bash
docker build -t kurio-server .
docker run -d -p 5000:5000 -v ~/Downloads:/downloads kurio-server
```

### systemd Service
```ini
[Unit]
Description=Kurio Download Manager Server
After=network.target

[Service]
Type=notify
ExecStart=/usr/bin/dotnet /opt/kurio/Kurio.Server.dll
Restart=on-failure
User=kurio
WorkingDirectory=/opt/kurio
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

---

## Migration Path

### From Library to Service

1. **Install server**: Deploy `Kurio.Server` on desired machine
2. **Migrate state**: Copy state files from `~/.kurio` to server
3. **Update clients**: Update CLI/GUI to connect to server
4. **Verify**: Test all functionality before removing old setup
5. **Cleanup**: Remove in-process engine code

---

## Success Metrics

- [ ] All Phase 1 tasks completed
- [ ] All Phase 2 tasks completed
- [ ] All Phase 3 tasks completed
- [ ] Zero data corruption issues
- [ ] API response time < 100ms (p95)
- [ ] Support 100+ concurrent downloads
- [ ] Memory usage < 2GB
- [ ] 95%+ test coverage
- [ ] Documentation complete
- [ ] Docker image available

---

## Timeline Estimate

- **Phase 1 (Foundation)**: 2-3 weeks
- **Phase 2 (Modernization)**: 1-2 weeks
- **Phase 3 (Web Service)**: 3-4 weeks
- **Total**: 6-9 weeks

---

## References

- [Polly Documentation](https://www.pollydocs.org/)
- [ASP.NET Core SignalR](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [Server-Sent Events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- [IAsyncEnumerable](https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams)
- [System.Threading.Channels](https://docs.microsoft.com/en-us/dotnet/api/system.threading.channels)

---

**End of PRD**
