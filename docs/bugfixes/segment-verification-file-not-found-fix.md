# Bug Fix: Segment Verification File Not Found Error

## Issue Description

When downloading files without pausing (continuous download from start to finish), the download would complete successfully for all segments, but the final merge step would fail with the error:

```text
FileNotFoundException: Could not find file '/Users/k/.kurio/temp/{taskId}/download.part'
```

### Symptoms

- All segment files (`segment_0000.part`, `segment_0001.part`, etc.) were downloaded successfully to disk
- Each segment showed `status: "Completed"` with 100% of bytes downloaded
- The final merged file was never created in the destination directory
- The download task state showed `"Failed"` despite all segments being complete

### Root Cause

The bug was in the `VerifySegmentBoundariesAsync` method in `SegmentManager.cs` (line 478). This method was attempting to verify the download by checking if the merged file (`download.part`) exists:

```csharp
FileInfo fileInfo = new(tempFilePath); // This throws FileNotFoundException
if (fileInfo.Length != config.FileSize)
{
    // ...
}
```

However, when using per-segment file architecture, the `download.part` file **does not exist** until after the merge step. The segments are stored as individual files:

- `segment_0000.part`
- `segment_0001.part`
- `segment_0002.part`
- etc.

The verification was happening **before** the merge, causing the download to fail even though all segments were successfully downloaded.

## Solution

Updated `VerifySegmentBoundariesAsync` to verify segment files directly instead of expecting a merged file:

### Changes Made

1. **Removed premature file size check**: Removed the check that attempted to verify `download.part` before it exists

2. **Added segment file verification**: For each segment, verify:
   - The segment file exists (either from `state.SegmentFilePath` or constructed path)
   - The segment file size matches the expected `state.TotalSize`
   - Accumulate total bytes across all segments

3. **Added total size verification**: After checking all segments, verify that the sum of all segment sizes equals the expected file size

### New Verification Logic

```csharp
// Verify segment files exist and have correct sizes
string? tempDir = Path.GetDirectoryName(tempFilePath);
long totalBytesVerified = 0;

foreach (SegmentState state in config.States)
{
    string segmentFilePath = state.SegmentFilePath ?? 
        Path.Combine(tempDir, $"segment_{state.SegmentIndex:D4}.part");

    if (!File.Exists(segmentFilePath))
    {
        throw new FileNotFoundException($"Segment file not found: {segmentFilePath}");
    }

    FileInfo segmentInfo = new(segmentFilePath);
    if (segmentInfo.Length != state.TotalSize)
    {
        throw new InvalidOperationException(
            $"Segment {state.SegmentIndex} size mismatch. Expected: {state.TotalSize}, Got: {segmentInfo.Length}");
    }

    totalBytesVerified += segmentInfo.Length;
}

// Verify total size matches expected file size
if (totalBytesVerified != config.FileSize)
{
    throw new InvalidOperationException(
        $"Total downloaded size mismatch. Expected: {config.FileSize}, Got: {totalBytesVerified}");
}
```

## Impact

- **Downloads without pause**: Now work correctly from start to finish
- **Downloads with pause/resume**: Continue to work as before (this code path wasn't affected)
- **Verification robustness**: More accurate verification that checks actual segment files rather than a non-existent merged file

## Testing

Test the fix using the pause-resume test script without pausing:

```bash
cd test/scripts
./test-pause-resume.sh
```

The download should now:

1. Download all segments successfully
2. Pass segment boundary verification
3. Merge all segments into the final file
4. Complete with `state: "Completed"`

## Related Files

- `src/Kurio.Core/Engine/SegmentManager.cs` - Fixed method: `VerifySegmentBoundariesAsync`
- `src/Kurio.Core/Engine/DownloadEngine.cs` - Calls the verification and merge logic
- `src/Kurio.Core/Storage/StorageManager.cs` - Handles the merge operation

## Version

Fixed in version: 1.10.1

## See Also

- [Pause/Resume Corruption Fix](./pause-resume-corruption-fix.md)
- [State Calculation Fix](./state-calculation-fix.md)
- [File Locking Race Condition Fix](./file-locking-race-condition-fix.md)
