# Product Requirements Document: Configuration and Storage System

**Version:** 1.0  
**Date:** November 26, 2025  
**Status:** Draft  
**Author:** Kurio Development Team  
**Related Issues:
** [#17](https://github.com/kiapanahi/KuriosLabs.Kurio/issues/17), [#16](https://github.com/kiapanahi/KuriosLabs.Kurio/issues/16)

---

## Executive Summary

This document defines the requirements for Kurio's configuration management and storage systems. These foundational
components enable flexible user customization while ensuring safe, reliable file management across all supported
platforms.

---

## 1. Goals and Objectives

### Primary Goals

- **Configuration System**: Provide flexible, hierarchical configuration supporting global and per-download settings
- **Storage Management**: Implement reliable file system operations with atomic guarantees and cross-platform
  compatibility
- **User Experience**: Enable easy customization while maintaining sensible defaults
- **Data Safety**: Ensure no data loss or corruption during file operations

### Success Criteria

- Configuration changes persist correctly across application restarts
- All file operations are atomic and safe from corruption
- Settings validation prevents invalid configurations
- Cross-platform compatibility on Windows, macOS, and Linux
- Zero data loss during file moves and cleanup operations

---

## 2. Configuration System (Issue #17)

### 2.1 Configuration Hierarchy

```
System Defaults
    ↓
User Configuration File (~/.kurio/config.json)
    ↓
Environment Variables (KURIO_*)
    ↓
Command-Line Arguments
    ↓
Per-Download Settings
```

### 2.2 Global Settings Schema

```json
{
  "version": "1.0",
  "downloads": {
    "defaultDirectory": "~/Downloads",
    "tempDirectory": "~/.kurio/temp",
    "maxConcurrentDownloads": 3,
    "maxConnectionsPerDownload": 8,
    "minSegmentSize": 1048576,
    "segmentBufferSize": 8192,
    "autoStart": true,
    "fileNamingPolicy": "appendNumber",
    "cleanupIncompleteOnExit": false
  },
  "network": {
    "timeout": 30,
    "retryPolicy": {
      "maxRetries": 3,
      "initialDelaySeconds": 1,
      "maxDelaySeconds": 60,
      "backoffMultiplier": 2.0
    },
    "bandwidthLimit": {
      "enabled": false,
      "maxDownloadSpeed": 0,
      "maxUploadSpeed": 0
    },
    "userAgent": "Kurio/1.0",
    "followRedirects": true,
    "maxRedirects": 5,
    "validateCertificates": true
  },
  "verification": {
    "autoVerify": true,
    "checksumAlgorithm": "SHA256",
    "failOnMismatch": true
  },
  "storage": {
    "checkDiskSpace": true,
    "minimumFreeSpace": 104857600,
    "categorization": {
      "enabled": true,
      "autoCategorizeBymimeType": true,
      "customRules": []
    }
  },
  "logging": {
    "level": "Information",
    "logDirectory": "~/.kurio/logs",
    "maxLogFiles": 10,
    "maxLogSizeBytes": 10485760
  }
}
```

### 2.3 Per-Download Settings

Per-download settings override global settings:

```csharp
public class DownloadOptions
{
    // Override global concurrent connection limit
    public int? MaxConnections { get; set; }
    
    // Custom destination directory
    public string? DestinationDirectory { get; set; }
    
    // Custom filename
    public string? FileName { get; set; }
    
    // File naming policy override
    public FileNamingPolicy? NamingPolicy { get; set; }
    
    // Priority in queue
    public DownloadPriority Priority { get; set; } = DownloadPriority.Normal;
    
    // Custom HTTP headers
    public Dictionary<string, string>? Headers { get; set; }
    
    // Authentication
    public AuthenticationOptions? Authentication { get; set; }
    
    // Custom retry policy
    public RetryPolicy? RetryPolicy { get; set; }
    
    // Category/tags
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    
    // Checksum verification
    public ChecksumOptions? Checksum { get; set; }
    
    // Bandwidth limit override
    public long? MaxDownloadSpeed { get; set; }
}
```

### 2.4 Configuration Interfaces

```csharp
/// <summary>
/// Service for managing application configuration
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current configuration
    /// </summary>
    KurioConfiguration GetConfiguration();
    
    /// <summary>
    /// Updates configuration settings
    /// </summary>
    Task UpdateConfigurationAsync(
        Action<KurioConfiguration> updateAction,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates configuration
    /// </summary>
    ValidationResult ValidateConfiguration(KurioConfiguration config);
    
    /// <summary>
    /// Resets configuration to defaults
    /// </summary>
    Task ResetToDefaultsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Exports configuration to file
    /// </summary>
    Task ExportConfigurationAsync(
        string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Imports configuration from file
    /// </summary>
    Task ImportConfigurationAsync(
        string filePath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Observable stream of configuration changes
    /// </summary>
    IObservable<KurioConfiguration> ConfigurationChanged { get; }
}

/// <summary>
/// Builder for creating download options with fluent API
/// </summary>
public interface IDownloadOptionsBuilder
{
    IDownloadOptionsBuilder WithMaxConnections(int maxConnections);
    IDownloadOptionsBuilder WithDestination(string directory, string? fileName = null);
    IDownloadOptionsBuilder WithPriority(DownloadPriority priority);
    IDownloadOptionsBuilder WithHeaders(Dictionary<string, string> headers);
    IDownloadOptionsBuilder WithAuthentication(string username, string password);
    IDownloadOptionsBuilder WithBearerToken(string token);
    IDownloadOptionsBuilder WithRetryPolicy(int maxRetries, TimeSpan initialDelay);
    IDownloadOptionsBuilder WithCategory(string category);
    IDownloadOptionsBuilder WithTags(params string[] tags);
    IDownloadOptionsBuilder WithChecksum(ChecksumAlgorithm algorithm, string expectedHash);
    IDownloadOptionsBuilder WithBandwidthLimit(long maxBytesPerSecond);
    DownloadOptions Build();
}
```

### 2.5 Configuration Validation Rules

| Setting                   | Validation Rule              | Default Value |
|---------------------------|------------------------------|---------------|
| MaxConcurrentDownloads    | 1 ≤ x ≤ 20                   | 3             |
| MaxConnectionsPerDownload | 1 ≤ x ≤ 32                   | 8             |
| MinSegmentSize            | 512 KB ≤ x ≤ 100 MB          | 1 MB          |
| SegmentBufferSize         | 4 KB ≤ x ≤ 1 MB              | 8 KB          |
| Timeout                   | 5 ≤ x ≤ 300 seconds          | 30            |
| MaxRetries                | 0 ≤ x ≤ 10                   | 3             |
| InitialDelaySeconds       | 0.5 ≤ x ≤ 60 seconds         | 1             |
| BackoffMultiplier         | 1.0 ≤ x ≤ 5.0                | 2.0           |
| MaxLogFiles               | 1 ≤ x ≤ 100                  | 10            |
| DefaultDirectory          | Must be valid, writable path | ~/Downloads   |
| TempDirectory             | Must be valid, writable path | ~/.kurio/temp |
| MinimumFreeSpace          | 10 MB ≤ x                    | 100 MB        |

### 2.6 Environment Variable Overrides

Support environment variables with `KURIO_` prefix:

```bash
KURIO_DOWNLOADS__DEFAULTDIRECTORY=/custom/path
KURIO_DOWNLOADS__MAXCONCURRENTDOWNLOADS=5
KURIO_NETWORK__TIMEOUT=60
KURIO_LOGGING__LEVEL=Debug
```

### 2.7 Configuration Migration

Support configuration version upgrades:

```csharp
public interface IConfigurationMigrator
{
    /// <summary>
    /// Migrates configuration from old version to current
    /// </summary>
    Task<KurioConfiguration> MigrateAsync(
        string configJson,
        string fromVersion,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if migration is needed
    /// </summary>
    bool NeedsMigration(string version);
}
```

---

## 3. Storage and File Management (Issue #16)

### 3.1 Storage Manager Interface

```csharp
/// <summary>
/// Manages file system operations for downloads
/// </summary>
public interface IStorageManager
{
    /// <summary>
    /// Creates a temporary file for download
    /// </summary>
    Task<TempFileHandle> CreateTempFileAsync(
        Guid taskId,
        string fileName,
        long expectedSize,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Writes data to specific offset in temp file
    /// </summary>
    Task WriteAtOffsetAsync(
        TempFileHandle handle,
        long offset,
        byte[] buffer,
        int count,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Atomically moves temp file to final destination
    /// </summary>
    Task<string> CommitDownloadAsync(
        TempFileHandle handle,
        string destinationDirectory,
        string fileName,
        FileNamingPolicy namingPolicy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks available disk space at path
    /// </summary>
    Task<long> GetAvailableDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if there's enough space for download
    /// </summary>
    Task<bool> HasSufficientSpaceAsync(
        string path,
        long requiredBytes,
        long minimumFreeSpaceBuffer,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes temp file
    /// </summary>
    Task DeleteTempFileAsync(
        TempFileHandle handle,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up orphaned temp files
    /// </summary>
    Task CleanupOrphanedFilesAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Resolves filename conflicts
    /// </summary>
    string ResolveFileNameConflict(
        string directory,
        string fileName,
        FileNamingPolicy policy);
    
    /// <summary>
    /// Sanitizes filename for current platform
    /// </summary>
    string SanitizeFileName(string fileName);
    
    /// <summary>
    /// Gets appropriate directory for category
    /// </summary>
    string GetCategoryDirectory(string baseDirectory, string? category);
}

/// <summary>
/// Handle for temporary download file
/// </summary>
public class TempFileHandle : IDisposable
{
    public Guid TaskId { get; }
    public string FilePath { get; }
    public FileStream Stream { get; }
    public long ExpectedSize { get; }
    
    public void Dispose();
}
```

### 3.2 File Naming Policies

```csharp
public enum FileNamingPolicy
{
    /// <summary>
    /// Overwrites existing file
    /// </summary>
    Overwrite,
    
    /// <summary>
    /// Appends number: file.txt, file (1).txt, file (2).txt
    /// </summary>
    AppendNumber,
    
    /// <summary>
    /// Appends timestamp: file-20251126-143022.txt
    /// </summary>
    AppendTimestamp,
    
    /// <summary>
    /// Fails if file exists
    /// </summary>
    FailIfExists,
    
    /// <summary>
    /// Skips download if file exists
    /// </summary>
    SkipIfExists
}
```

### 3.3 Platform-Specific Paths

```csharp
public interface IPlatformPathProvider
{
    /// <summary>
    /// Gets default downloads directory for platform
    /// </summary>
    string GetDefaultDownloadsDirectory();
    
    /// <summary>
    /// Gets application data directory
    /// </summary>
    string GetAppDataDirectory();
    
    /// <summary>
    /// Gets temp directory
    /// </summary>
    string GetTempDirectory();
    
    /// <summary>
    /// Expands path with environment variables and user home
    /// </summary>
    string ExpandPath(string path);
    
    /// <summary>
    /// Gets invalid characters for filenames on this platform
    /// </summary>
    char[] GetInvalidFileNameChars();
    
    /// <summary>
    /// Checks if path is valid for platform
    /// </summary>
    bool IsValidPath(string path);
}
```

**Platform Defaults:**

| Platform | Downloads Directory       | App Data                              | Temp                     |
|----------|---------------------------|---------------------------------------|--------------------------|
| Windows  | `%USERPROFILE%\Downloads` | `%APPDATA%\Kurio`                     | `%TEMP%\Kurio`           |
| macOS    | `~/Downloads`             | `~/Library/Application Support/Kurio` | `~/Library/Caches/Kurio` |
| Linux    | `~/Downloads`             | `~/.config/kurio`                     | `/tmp/kurio`             |

### 3.4 Atomic File Operations

Atomic move algorithm:

```
1. Verify source file exists and is complete
2. Generate final destination path (handle naming conflicts)
3. Create destination directory if needed
4. If same filesystem:
     - Use atomic File.Move with overwrite flag
   Else:
     - Copy to temp file in destination directory
     - Verify copy integrity (size, optionally hash)
     - Delete source
     - Rename temp to final name
5. Update metadata/permissions if needed
6. Cleanup temp files on any failure
```

### 3.5 Disk Space Management

```csharp
public interface IDiskSpaceMonitor
{
    /// <summary>
    /// Continuously monitors disk space
    /// </summary>
    IObservable<DiskSpaceInfo> MonitorDiskSpace(string path, TimeSpan interval);
    
    /// <summary>
    /// Gets current disk space info
    /// </summary>
    Task<DiskSpaceInfo> GetDiskSpaceAsync(
        string path,
        CancellationToken cancellationToken = default);
}

public record DiskSpaceInfo(
    string Path,
    long TotalBytes,
    long AvailableBytes,
    long FreeBytes,
    double UsagePercentage);
```

**Disk Space Checks:**

1. **Before Download**: Check if `fileSize + minimumFreeSpace` available
2. **During Download**: Monitor periodically (every 10 seconds)
3. **On Low Space**: Pause download, notify user
4. **Critical Low Space**: Pause all downloads

### 3.6 Category-Based Organization

```csharp
public interface ICategoryManager
{
    /// <summary>
    /// Auto-categorizes file by MIME type or extension
    /// </summary>
    string? AutoCategorize(string fileName, string? mimeType);
    
    /// <summary>
    /// Gets directory for category
    /// </summary>
    string GetCategoryDirectory(string baseDirectory, string category);
    
    /// <summary>
    /// Registers custom categorization rule
    /// </summary>
    void RegisterRule(CategoryRule rule);
}

public class CategoryRule
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string[]? Extensions { get; set; }
    public string[]? MimeTypes { get; set; }
    public string? UrlPattern { get; set; }
}
```

**Default Categories:**

| Category  | Extensions                                          | MIME Types        |
|-----------|-----------------------------------------------------|-------------------|
| Documents | `.pdf`, `.doc`, `.docx`, `.txt`, `.md`              | `application/pdf` |
| Images    | `.jpg`, `.png`, `.gif`, `.svg`                      | `image/*`         |
| Videos    | `.mp4`, `.mkv`, `.avi`, `.mov`                      | `video/*`         |
| Audio     | `.mp3`, `.flac`, `.wav`, `.ogg`                     | `audio/*`         |
| Archives  | `.zip`, `.tar`, `.gz`, `.7z`, `.rar`                | `application/zip` |
| Software  | `.exe`, `.msi`, `.dmg`, `.deb`, `.rpm`, `.appimage` | `application/x-*` |
| Code      | `.cs`, `.js`, `.py`, `.java`, `.go`                 | `text/x-*`        |

### 3.7 Temp File Cleanup

```csharp
public interface ITempFileCleanupService
{
    /// <summary>
    /// Scans for orphaned temp files on startup
    /// </summary>
    Task<OrphanedFilesInfo> ScanForOrphanedFilesAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up orphaned files older than threshold
    /// </summary>
    Task<CleanupResult> CleanupOrphanedFilesAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleans up temp files for specific task
    /// </summary>
    Task CleanupTaskFilesAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}

public record OrphanedFilesInfo(
    int FileCount,
    long TotalBytes,
    List<OrphanedFile> Files);

public record OrphanedFile(
    string Path,
    long Size,
    DateTime LastModified,
    Guid? TaskId);

public record CleanupResult(
    int FilesDeleted,
    long BytesFreed,
    int FailedDeletions);
```

**Cleanup Strategy:**

- **On Startup**: Scan temp directory for files older than 7 days
- **Periodic**: Run cleanup daily (configurable)
- **On Task Cancel**: Immediately delete temp files
- **On Task Complete**: Delete temp files after successful move
- **Manual**: User-triggered cleanup via CLI/UI

---

## 4. Implementation Plan

### Phase 1: Configuration System (Issue #17)

**Week 1-2**

1. Define configuration models and schema
2. Implement `IConfigurationService` with JSON persistence
3. Add configuration validation logic
4. Implement environment variable overrides
5. Create fluent `DownloadOptionsBuilder`
6. Write unit tests for configuration management

**Deliverables:**

- Configuration models and interfaces
- Configuration service implementation
- Validation logic
- Unit tests (>80% coverage)
- Configuration schema documentation

### Phase 2: Storage Management (Issue #16)

**Week 3-4**

1. Implement `IPlatformPathProvider` for all platforms
2. Implement `IStorageManager` with atomic operations
3. Add disk space monitoring
4. Implement file naming conflict resolution
5. Create category-based organization
6. Add temp file cleanup service
7. Write comprehensive tests

**Deliverables:**

- Storage manager implementation
- Platform-specific path handling
- Atomic file operations
- Disk space monitoring
- Category management
- Cleanup service
- Cross-platform integration tests

### Phase 3: Integration

**Week 5**

1. Integrate configuration with download engine
2. Integrate storage manager with download tasks
3. Update existing code to use new configuration
4. End-to-end integration testing
5. Documentation updates

---

## 5. Testing Strategy

### 5.1 Configuration Tests

- **Unit Tests**: Configuration validation, defaults, merging
- **Integration Tests**: File persistence, environment variable overrides
- **Migration Tests**: Version upgrade scenarios
- **Validation Tests**: Invalid configuration rejection

### 5.2 Storage Tests

- **Unit Tests**: Path sanitization, naming conflicts, categorization
- **Integration Tests**: Atomic operations, disk space checks
- **Platform Tests**: All operations on Windows, macOS, Linux
- **Stress Tests**: Large files, low disk space, concurrent operations
- **Recovery Tests**: Orphaned file cleanup, crash recovery

### 5.3 Test Coverage Goals

- Unit tests: >85%
- Integration tests: All critical paths
- Platform tests: All three platforms
- Edge cases: All error conditions

---

## 6. Non-Functional Requirements

### 6.1 Performance

- Configuration load time: < 100ms
- File operation overhead: < 10ms per operation
- Disk space check: < 50ms
- Orphaned file scan: < 1 second per 1000 files

### 6.2 Reliability

- Zero configuration corruption
- Zero file corruption during moves
- 100% atomic operation success (or rollback)
- Graceful degradation on disk full

### 6.3 Compatibility

- All platforms: Windows 10+, macOS 12+, modern Linux
- All filesystems: NTFS, APFS, ext4, Btrfs, ZFS
- Long path support on Windows
- Unicode filename support

### 6.4 Security

- Configuration file permissions: User-only read/write
- No sensitive data in plaintext (future: encrypt credentials)
- Secure temp file creation (unpredictable names)
- Path traversal prevention

---

## 7. Success Metrics

### Development Metrics

- Code coverage: >85%
- Zero critical bugs in file operations
- All platform tests passing
- Documentation complete

### User Experience Metrics

- Configuration changes persist correctly: 100%
- File operations succeed without corruption: 100%
- Disk space warnings before failures: 100%
- Appropriate defaults for all settings

---

## 8. Future Enhancements

### Configuration

- Remote configuration profiles
- Configuration presets (torrenting, slow connection, etc.)
- Per-site configuration rules
- Configuration backup/sync

### Storage

- Cloud storage backends (S3, Google Drive)
- Deduplication
- Compression for archived downloads
- Advanced categorization with ML

---

## 9. References

- [.NET Configuration](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [File System Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/io/)
- [Atomic File Operations](https://en.wikipedia.org/wiki/Atomic_operation)

---

## Appendix A: Configuration File Example

See Section 2.2 for complete schema.

## Appendix B: Storage Directory Structure

```
~/.kurio/
├── config.json                 # User configuration
├── state/                      # Download state files
│   └── {taskId}.json
├── temp/                       # Temporary downloads
│   └── {taskId}/
│       ├── download.part
│       └── state.json
├── logs/                       # Application logs
│   ├── kurio-20251126.log
│   └── kurio-20251125.log
└── history.db                  # Download history (future)
```

---

**Document Status**: ✅ Ready for Implementation  
**Next Steps**: Create branch → Implement → Test → PR
