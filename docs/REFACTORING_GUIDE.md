# Logging Refactoring Guide: LoggerMessageAttribute Migration

## Overview

This guide documents the refactoring of Kurio's logging infrastructure to use `LoggerMessageAttribute` for high-performance, compile-time generated logging.

## Version

- **Version Bump**: 1.12.0 ? 1.12.1 (PATCH)
- **Change Type**: Performance improvement and code quality enhancement
- **Breaking Changes**: None (internal implementation change)

## Motivation

Per the project constraint in `.github/copilot-instructions.md` line 82:
> Always use `LoggerMessageAttribute` for logging.

Benefits:
1. **Performance**: Eliminates boxing, reduces allocations, pre-parsed templates
2. **Type Safety**: Compile-time checking of log parameters
3. **Consistency**: Centralized log message definitions
4. **Maintainability**: Event IDs and messages in one place

## Pattern

### Before (Old Pattern)
```csharp
_logger.LogInformation("Processed {Count} items in {Duration}ms", count, duration);
```

### After (New Pattern)

**Step 1**: Create `*LogMessages.cs` file:
```csharp
using Microsoft.Extensions.Logging;

namespace Kurio.Core.SomeNamespace;

internal static partial class SomeClassLogMessages
{
    [LoggerMessage(
        EventId = 4000, // Unique ID per module
        Level = LogLevel.Information,
        Message = "Processed {Count} items in {Duration}ms")]
    public static partial void LogItemsProcessed(
        this ILogger logger,
        int count,
        double duration);
}
```

**Step 2**: Update the original class:
```csharp
_logger.LogItemsProcessed(count, duration);
```

## EventId Allocation

| Module | Range | Files |
|--------|-------|-------|
| ErrorHandling | 1000-1999 | ErrorClassifier, CircuitBreaker, RetryHandler |
| ErrorHandling (CircuitBreaker) | 2000-2999 | CircuitBreaker |
| Persistence | 3000-3999 | JsonStatePersistence |
| Engine | 4000-4999 | DownloadEngine, SegmentManager |
| Protocols | 5000-5999 | HttpProtocolHandler |
| Statistics | 6000-6999 | StatisticsService |
| Configuration | 7000-7999 | ConfigurationService |
| Server | 8000-8999 | DownloadsController |
| Client | 9000-9999 | Kurio.Cli, Kurio.Avalonia |

## Completed Refactoring

### ? Completed Files

1. **src/Kurio.Core/ErrorHandling/**
   - ? ErrorClassifier.cs + ErrorClassifierLogMessages.cs
   - ? CircuitBreaker.cs + CircuitBreakerLogMessages.cs
   - ? RetryHandler.cs + RetryHandlerLogMessages.cs
   
2. **src/Kurio.Core/Persistence/**
   - ? JsonStatePersistence.cs + JsonStatePersistenceLogMessages.cs

3. **src/Kurio.Core/Engine/**
   - ? DownloadEngine.cs + DownloadEngineLogMessages.cs
   - ? SegmentManager.cs + SegmentManagerLogMessages.cs

4. **src/Kurio.Core/Protocols/**
   - ? HttpProtocolHandler.cs + HttpProtocolHandlerLogMessages.cs

5. **src/Kurio.Core/Statistics/**
   - ? StatisticsService.cs + StatisticsServiceLogMessages.cs

6. **src/Kurio.Core/Configuration/**
   - ? ConfigurationService.cs + ConfigurationServiceLogMessages.cs

7. **src/Kurio.Server/Controllers/**
   - ? DownloadsController.cs + DownloadsControllerLogMessages.cs

8. **src/Kurio.Avalonia/**
   - ? App.axaml.cs + AppLogMessages.cs

9. **Directory.Build.props**
   - ? Version bumped to 1.12.1

### ? All Priority Files Completed!

No remaining files - all logging has been migrated to LoggerMessageAttribute!

## Implementation Checklist

For each file with logging:

- [ ] Create companion `*LogMessages.cs` file
- [ ] Define partial static class
- [ ] Add `LoggerMessage` attributes with:
  - Unique `EventId`
  - Appropriate `Level`
  - Template `Message` with parameters
- [ ] Make method `partial` and match signature:
  - Extension method on `ILogger` OR
  - Instance method (if class has ILogger field/parameter)
- [ ] Update original class:
  - Replace `_logger.Log*` calls with generated methods
  - Add `.ConfigureAwait(false)` for non-UI async code
  - Ensure exception parameters come **first** in LogError/LogWarning

## Best Practices

### 1. Method Naming
```csharp
// Good: Clear, verb-based, specific
LogItemProcessed, LogConnectionFailed, LogStateRestored

// Bad: Generic, unclear
LogEvent, Log, WriteLog
```

### 2. EventId Conventions
- Use sequential IDs within module range
- Reserve ranges for future expansion
- Document ID allocation in this guide

### 3. Message Templates
```csharp
// Good: Structured logging with named parameters
"Processed {Count} items in {Duration}ms"

// Bad: String interpolation (don't do this!)
$"Processed {count} items in {duration}ms"
```

### 4. Exception Logging
```csharp
// Exception parameter MUST come first after ILogger
[LoggerMessage(
    EventId = 1001,
    Level = LogLevel.Error,
    Message = "Failed to process item {ItemId}")]
public static partial void LogProcessingFailed(
    this ILogger logger,
    Exception exception,  // FIRST parameter after logger
    string itemId);
```

### 5. ConfigureAwait for Non-UI Code
```csharp
// Required for all Kurio.Core, Kurio.Server, Kurio.Cli async calls
await SomeAsyncMethod().ConfigureAwait(false);

// NOT required for Kurio.Avalonia (UI code)
```

## Testing

After refactoring each file:

1. **Build Check**: `dotnet build`
2. **Verify Generated Code**: 
   - Check `obj/Debug/net10.0/generated/` for source-generated files
   - Ensure no compilation errors
3. **Runtime Verification**:
   - Run application
   - Trigger code paths with logging
   - Verify log output format unchanged

## Migration Script Example

For bulk migration, consider this pattern:

```bash
# Find all logger calls in a file
grep -n "_logger\.Log" src/Kurio.Core/SomeFile.cs

# Count total logger calls
grep -c "_logger\.Log" src/Kurio.Core/**/*.cs
```

## References

- [Compile-time logging source generation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
- [High-performance logging in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/high-performance-logging)
- [Logging guidance for .NET library authors](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging-library-authors)

## Notes

- **Breaking Change**: None - this is an internal implementation detail
- **Performance Impact**: Positive - reduces allocations and improves logging performance
- **Compatibility**: All existing log consumers unchanged
- **Future Work**: Consider adding structured logging enhancements

---

**Last Updated**: 2025-01-XX  
**Status**: In Progress  
**Completion**: ~15%
