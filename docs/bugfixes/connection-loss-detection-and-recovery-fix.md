# Bug Fix: Connection Loss Detection and Recovery

## Date
2025-11-28

## Severity
**Critical**

## Summary
When network connection is lost during a download (e.g., WiFi turned off), the download engine fails to detect the connection loss, does not log timeout exceptions, and eventually fails the download entirely instead of pausing or recovering. This breaks the connection resilience feature.

## Problem Description

### Observed Behavior
1. Download starts normally with multiple segments
2. When WiFi is turned off:
   - NO timeout exceptions are logged
   - Segments remain in "Downloading" state indefinitely
   - No bandwidth usage (download actually stalled)
   - Status API reports "Downloading" (incorrect state)
3. When WiFi is turned back on:
   - Download does not auto-resume
   - Manual resume fails: "cannot resume a task that is not paused"
4. Manual pause causes download to fail with error:
   - "Received an unexpected EOF or 0 bytes from the transport stream."
   - Error classified as NOT recoverable

### Root Causes

#### 1. IOException Not Properly Caught by Resilience Policy
The `ResiliencePolicyFactory.IsTransientNetworkError()` method only catches IOExceptions with specific message patterns:
```csharp
IOException ioEx when
    ioEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
    ioEx.Message.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
    ioEx.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase) =>
    true,
```

The actual error message is: **"Received an unexpected EOF or 0 bytes from the transport stream."**
This message doesn't match any of the patterns, so the exception is not considered transient and retryable.

#### 2. HttpClient Timeout Not Effective for Connection Loss
The `HttpClient.Timeout` setting only applies to the overall request, not to data transfer stalls. When WiFi is disconnected:
- The socket doesn't immediately throw an exception
- `ReadAsync()` blocks indefinitely waiting for data
- No timeout is triggered
- Segments appear to be "Downloading" but are actually hung

#### 3. Stall Detection Not Working
The stall detection logic in `HttpProtocolHandler.DownloadRangeAsync()`:
```csharp
// Check for stall
var timeSinceLastData = DateTime.UtcNow - lastDataReceivedAt;
if (timeSinceLastData.TotalSeconds > stallTimeoutSeconds)
{
    throw new TimeoutException(...);
}
```

This check only runs AFTER `ReadAsync()` returns, but when connection is lost, `ReadAsync()` blocks and never returns, so the check never executes.

#### 4. No Async Cancellation/Timeout on Stream Read
The `ReadAsync()` call has a cancellation token but no independent timeout mechanism that would fire if the read operation hangs.

#### 5. Error Recovery Action Set to "Fail"
In the state.json, the error shows:
```json
"isRecoverable": false,
"recoveryAction": "Fail"
```

This suggests the error categorization logic in `ErrorHandling/ErrorClassifier.cs` is not properly detecting EOF/connection errors as transient.

## Technical Analysis

### Error Flow
1. Network disconnects → Socket connection breaks
2. `HttpClient` internally tries to read from socket
3. .NET throws IOException: "Received an unexpected EOF..."
4. Exception bubbles up through `DownloadRangeAsync()`
5. `SegmentManager.DownloadSegmentWithRetryAsync()` catches it
6. Resilience policy `ShouldHandle` evaluates → returns FALSE (doesn't match pattern)
7. Exception is not retried, bubbles up to `DownloadSegmentsAsync()`
8. All segments fail with AggregateException
9. `DownloadEngine.ExecuteDownloadAsync()` catches AggregateException
10. Error is classified as non-recoverable
11. Task state set to "Failed"

### Expected Flow
1. Network disconnects → IOException thrown
2. Resilience policy recognizes as transient error
3. Retry mechanism activates with backoff
4. If retries exhausted, task should be paused (not failed)
5. Connection monitoring should detect network loss
6. When network returns, task should auto-resume

## Solution

### 1. Expand IOException Pattern Matching
Update `IsTransientNetworkError()` to catch EOF and other connection-related IOExceptions:

```csharp
// IO exceptions related to network
IOException ioEx when
    ioEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
    ioEx.Message.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
    ioEx.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase) ||
    ioEx.Message.Contains("EOF", StringComparison.OrdinalIgnoreCase) ||
    ioEx.Message.Contains("transport stream", StringComparison.OrdinalIgnoreCase) ||
    ioEx.InnerException is SocketException =>
    true,
```

### 2. Implement Per-Read Operation Timeout
Add a timeout mechanism that wraps each `ReadAsync()` call:

```csharp
using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(stallTimeoutSeconds));
using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

try
{
    bytesRead = await responseStream.ReadAsync(buffer, linkedCts.Token);
    lastDataReceivedAt = DateTime.UtcNow;
}
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
{
    throw new TimeoutException($"No data received for {stallTimeoutSeconds} seconds");
}
```

### 3. Update Error Classifier
Ensure `ErrorClassifier.ClassifyError()` marks EOF and transport errors as transient:

```csharp
when ex is IOException ioEx &&
     (ioEx.Message.Contains("EOF", StringComparison.OrdinalIgnoreCase) ||
      ioEx.Message.Contains("transport stream", StringComparison.OrdinalIgnoreCase))
    => ErrorClassification.Transient(ErrorCategory.Network, recoveryAction: RecoveryAction.Retry),
```

### 4. Implement Graceful Pause on Retries Exhausted
When all retries are exhausted for a network error, instead of failing:
- Set task state to "Paused"
- Mark error as recoverable with recovery action "WaitAndRetry"
- Allow manual or automatic resume

### 5. Add Connection Health Monitoring (Future Enhancement)
Implement active network monitoring:
- Periodically ping health check endpoints
- Detect connection loss proactively
- Auto-pause downloads before they fail
- Auto-resume when connection returns

## Testing Plan

### Test Case 1: WiFi Disconnect During Download
1. Start a large file download
2. Wait for segments to start (check bandwidth usage)
3. Turn off WiFi
4. Verify timeout exceptions are logged within 30-60 seconds
5. Verify task is paused (not failed)
6. Turn on WiFi
7. Verify task auto-resumes or can be manually resumed
8. Verify download completes successfully

### Test Case 2: Intermittent Connection
1. Start download
2. Turn WiFi off for 10 seconds
3. Turn WiFi back on
4. Repeat 2-3 times
5. Verify download eventually completes
6. Verify retries are logged

### Test Case 3: Extended Connection Loss
1. Start download
2. Turn off WiFi for 5 minutes
3. Verify task is paused (not failed) after retry exhaustion
4. Turn on WiFi
5. Manually resume download
6. Verify download completes

## Impact
- **User Experience**: Major improvement - downloads no longer fail permanently on temporary connection issues
- **Reliability**: Critical fix for connection resilience feature
- **Performance**: Minimal - adds per-read timeout checks

## Related Files
- `src/Kurio.Core/Resilience/ResiliencePolicyFactory.cs`
- `src/Kurio.Core/Protocols/HttpProtocolHandler.cs`
- `src/Kurio.Core/ErrorHandling/ErrorClassifier.cs`
- `src/Kurio.Core/Engine/SegmentManager.cs`
- `src/Kurio.Core/Engine/DownloadEngine.cs`

## Version Impact
This is a **PATCH** version bump (bug fix).

