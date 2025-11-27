# Bug Fix: File Corruption After Pause/Resume

## Issue

When pausing and resuming a download, the resulting file was corrupted (e.g., ZIP files would fail to extract) even though the file size was correct. This occurred specifically when:

1. A download was started with multiple segments
2. The download was paused while in progress
3. The download was resumed
4. The file appeared complete but was corrupted

## Root Cause

The bug was in `SegmentManager.cs` in the `DownloadSegmentAsync` method. There were two critical issues:

### Issue 1: Premature State Update (Primary Bug)

**Location:** Line 337-348 in the progress callback

**The Problem:**

```csharp
Progress<long> segmentProgress = new(bytesRead =>
{
    long totalForSegment = initialBytesDownloaded + bytesRead;
    state.BytesDownloaded = totalForSegment;  // ❌ PREMATURE UPDATE
    // ...
});
```

**What Happened:**

1. During download, bytes are read into a `MemoryStream` buffer
2. As bytes are read, the progress callback fires and updates `state.BytesDownloaded`
3. If download is cancelled/paused, the state shows bytes were "downloaded"
4. BUT: These bytes were never written to disk! They only existed in the memory buffer
5. On resume, the code calculates: `remainingStart = state.StartByte + state.BytesDownloaded`
6. This skips bytes that were counted but never persisted, creating gaps in the file

**Example:**

- Segment 0: Bytes 0-16,592,372 (16.5 MB)
- Downloaded 3.3 MB into memory buffer → `state.BytesDownloaded = 3,349,829`
- **Pause occurs** → Memory buffer discarded, bytes never written!
- State persisted shows 3,349,829 bytes downloaded
- **Resume** → Starts downloading from byte 3,349,829
- **Result** → Bytes 0-3,349,828 are empty (zeros from pre-allocated file)
- **ZIP file corrupted** → Missing first 3.3 MB of data

### Issue 2: Incorrect Checksum for Resumed Segments

**Location:** Line 379-380 (before fix)

**The Problem:**

```csharp
string checksum = await _segmentVerifier.ComputeChecksumAsync(buffer, "SHA256", cancellationToken);
state.Checksum = SegmentChecksum.Create("SHA256", checksum);
```

When resuming, `buffer` contained only the newly downloaded bytes (the remainder of the segment), not the entire segment. This meant:

- Initial download: Checksum of first chunk stored
- Resume: Checksum of second chunk overwrites it
- Verification: Tries to verify entire segment against partial checksum → Fails

## The Fix

### Fix 1: Defer State Update Until After Write

**Changed:**

```csharp
Progress<long> segmentProgress = new(bytesRead =>
{
    // bytesRead is the total bytes read in this download session (in memory, not yet written to disk)
    // We report progress for UI updates but do NOT update state.BytesDownloaded here
    // because if download is cancelled before write, we would have incorrect resume position
    long totalForSegment = initialBytesDownloaded + bytesRead;

    progress?.Report(new SegmentProgress
    {
        SegmentIndex = state.SegmentIndex,
        BytesDownloaded = totalForSegment,  // Report for UI
        Status = SegmentStatus.Downloading,
        Timestamp = DateTime.UtcNow
    });
    // Note: state.BytesDownloaded is NOT updated here!
});

// ... download happens ...

// Write to disk first
await _storageManager.WriteSegmentAsync(...);

// ONLY AFTER successful write, update the persisted bytes count
state.BytesDownloaded = initialBytesDownloaded + downloadedBytes;
```

**Key Changes:**

- Progress callback reports bytes for UI updates but doesn't modify `state.BytesDownloaded`
- `state.BytesDownloaded` is only updated AFTER successful write to disk
- If download is cancelled before write, state remains at last persisted position
- Resume will correctly start from where data was actually written

### Fix 2: Compute Checksum of Entire Segment

**Changed:**

```csharp
// Compute checksum for the ENTIRE segment from the file (not just the newly written part)
// This is important for resume scenarios where we need to verify the complete segment
using (FileStream fileStream = new(tempFilePath, FileMode.Open, FileAccess.Read, ...))
{
    fileStream.Seek(state.StartByte, SeekOrigin.Begin);
    byte[] segmentData = new byte[state.TotalSize];
    
    // Read entire segment
    int totalRead = 0;
    while (totalRead < state.TotalSize)
    {
        int bytesRead = await fileStream.ReadAsync(
            segmentData.AsMemory(totalRead, (int)(state.TotalSize - totalRead)),
            cancellationToken);
        
        if (bytesRead == 0)
            throw new InvalidOperationException(...);
        
        totalRead += bytesRead;
    }
    
    // Compute checksum of complete segment
    string checksum = await _segmentVerifier.ComputeChecksumAsync(segmentData, "SHA256", cancellationToken);
    state.Checksum = SegmentChecksum.Create("SHA256", checksum);
}
```

**Key Changes:**

- After writing, read the ENTIRE segment back from the file
- Compute checksum of the complete segment (including previously written data for resumes)
- This ensures verification works correctly for resumed downloads

## Testing

To verify the fix:

1. Run the test script: `./test/scripts/test-pause-resume.sh`
2. The script will:
   - Create a download
   - Wait and then pause it
   - Show bytes downloaded from state file
   - Prompt to resume
   - Resume the download
3. After completion, verify the downloaded file:
   - Check file size matches expected
   - For ZIP files: `unzip -t <filename>` should succeed
   - For other files: Compare checksum with original

## Impact

**Before Fix:**

- ❌ Resumed downloads produced corrupted files
- ❌ File size correct but content missing/wrong
- ❌ ZIP files wouldn't extract
- ❌ Checksums failed

**After Fix:**

- ✅ Resumed downloads produce valid files
- ✅ All bytes correctly written
- ✅ ZIP files extract successfully
- ✅ Checksums match

## Files Modified

- `src/Kurio.Core/Engine/SegmentManager.cs`:
  - Modified `DownloadSegmentAsync` method
  - Line ~337: Removed premature state update from progress callback
  - Line ~384: Moved state update to after successful write
  - Line ~390-420: Added complete segment checksum computation from file

## Related Issues

This fix resolves the core issue with pause/resume functionality. Related improvements could include:

1. Add integration tests for pause/resume scenarios
2. Add checksum verification for entire file (not just segments)
3. Consider incremental checksum updates during download
4. Add telemetry to track pause/resume success rates

## Version

- **Fixed in:** v1.0.0 (pending)
- **Date:** November 27, 2025
- **Severity:** Critical (data corruption)
- **Priority:** High
