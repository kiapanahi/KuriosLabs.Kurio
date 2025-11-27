# State Calculation Fix: totalBytesDownloaded and completedPercent

## Problem Description

After implementing per-segment file storage, the `totalBytesDownloaded` field in the state file was not being calculated correctly during active downloads. The state file showed 0 bytes downloaded until segments completed, preventing accurate progress tracking.

Additionally, there was no `completedPercent` field in the state file for easy progress visualization.

## Root Cause

### Issue 1: Zero Progress During Active Download

The problem occurred due to a timing mismatch in state updates:

1. **SegmentManager Progress Callback** (line 360): 
   - Reported progress via `SegmentProgress` event with current `BytesDownloaded`
   - But did NOT update `state.BytesDownloaded` 

2. **State Update After Completion** (line 402):
   - Only updated `state.BytesDownloaded` AFTER segment completed and flushed to disk
   - This was correct for persistence but prevented live progress tracking

3. **DownloadEngine Aggregation** (line 463):
   - Calculated `totalDownloaded = segmentConfig.States.Sum(s => s.BytesDownloaded)`
   - Since individual segment states weren't updated during download, sum was always stale

### Issue 2: Missing Completion Percentage

The `DownloadTaskState` class had no calculated property for completion percentage, requiring clients to manually compute it from `TotalBytesDownloaded` and `FileSize`.

## Solution

### Fix 1: Update State During Progress

Modified the progress callback in `SegmentManager.DownloadSegmentAsync` to update `state.BytesDownloaded` for live aggregation:

```csharp
Progress<long> segmentProgress = new(bytesRead =>
{
    bytesWrittenThisSession = bytesRead;
    long totalForSegment = initialBytesDownloaded + bytesRead;

    // Update state for live progress aggregation (not persisted until completion)
    state.BytesDownloaded = totalForSegment;

    progress?.Report(new SegmentProgress
    {
        SegmentIndex = state.SegmentIndex,
        BytesDownloaded = totalForSegment,
        Status = SegmentStatus.Downloading,
        Timestamp = DateTime.UtcNow
    });
});
```

**Key Points:**
- `state.BytesDownloaded` is updated during progress for live aggregation
- The final confirmed value is still set after successful flush (line 402)
- This ensures both live progress tracking AND correct resume behavior

### Fix 2: Add CompletedPercent Property

Added a calculated property to `DownloadTaskState`:

```csharp
/// <summary>
///     Gets the completion percentage (0-100).
/// </summary>
public double CompletedPercent => FileSize > 0 
    ? Math.Round((double)TotalBytesDownloaded / FileSize * 100, 2) 
    : 0;
```

This provides:
- Automatic calculation based on current progress
- Rounded to 2 decimal places for readability
- Safe handling of zero file size edge case
- Consistent with serialization to state file

## Testing

Created `test/scripts/test-state-calculations.sh` to verify:

1. `totalBytesDownloaded` updates during active download (non-zero)
2. `completedPercent` is calculated during download
3. Values persist correctly after pause
4. Calculation formula matches expectations

## Impact

### Before Fix

State file during active download:
```json
{
  "fileSize": 10485760,
  "totalBytesDownloaded": 0,  // ❌ Always zero during download
  "segments": [
    {"bytesDownloaded": 0},   // ❌ Not updated until completion
    {"bytesDownloaded": 0},
    {"bytesDownloaded": 0}
  ]
}
```

### After Fix

State file during active download:
```json
{
  "fileSize": 10485760,
  "totalBytesDownloaded": 5242880,  // ✅ Shows real progress (50%)
  "completedPercent": 50.00,         // ✅ Auto-calculated
  "segments": [
    {"bytesDownloaded": 1747626},   // ✅ Updated during download
    {"bytesDownloaded": 1747627},
    {"bytesDownloaded": 1747627}
  ]
}
```

## Related Issues

- Works in conjunction with [pause-resume-corruption-fix.md](./pause-resume-corruption-fix.md)
- Ensures state reflects both live progress and persisted data correctly
- Maintains integrity of resume functionality

## Files Modified

- `src/Kurio.Core/Engine/SegmentManager.cs` - Update state during progress
- `src/Kurio.Core/Models/DownloadTaskState.cs` - Add CompletedPercent property
- `test/scripts/test-state-calculations.sh` - Verification test
- `test/scripts/README.md` - Documentation

## Verification

Run the test script to verify:

```bash
./test/scripts/test-state-calculations.sh
```

Expected output:
```
=== State Calculation Test ===
✓ Download created with ID: xxx
✓ totalBytesDownloaded is non-zero: 5242880 bytes
✓ completedPercent is calculated: 50.00%
✓ totalBytesDownloaded persisted after pause: 5242880 bytes
✓ completedPercent calculation is correct (within 0.1%)

=== All tests passed! ===
```
