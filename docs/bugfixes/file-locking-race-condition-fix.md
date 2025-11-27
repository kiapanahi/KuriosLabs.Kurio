# Bug Fix: File Locking Race Condition

## Issue

When downloading files with multiple segments, random failures occurred with the error:

```text
The process cannot access the file because it is being used by another process.
```

This happened intermittently during concurrent segment downloads.

## Root Cause

The original implementation used `StorageMode.SingleFile`, where all segments wrote to the same pre-allocated `download.part` file. While the `StorageManager` had per-file locking (`_fileLocks` dictionary), multiple segments competing for write access to the same file caused:

1. **Lock contention**: Segments waiting for locks to write their data
2. **File handle conflicts**: OS-level file locking despite application-level semaphores
3. **Race conditions**: Timing issues when multiple threads accessed the same file handle

**Example Scenario:**

- 8 segments downloading concurrently
- All trying to write to `~/.kurio/temp/<task-id>/download.part`
- Segment 0 writes at offset 0
- Segment 1 tries to write at offset 16MB but file handle is locked
- Result: "File is being used by another process" error

## The Fix

Changed the storage mode from `SingleFile` to `PerSegmentFiles`, where each segment writes to its own independent file.

### Changes Made

1. **Storage Mode Configuration** (`ServiceCollectionExtensions.cs`)
   - Changed default from `StorageMode.SingleFile` to `StorageMode.PerSegmentFiles`
   - Eliminates file locking contention

2. **Segment File Writing** (`SegmentManager.cs`)
   - Each segment now writes to `segment_####.part` (e.g., `segment_0000.part`, `segment_0001.part`)
   - No locking needed since files are independent
   - Supports resume by seeking within individual segment files

3. **Segment State Tracking** (`SegmentState.cs`)
   - Added `SegmentFilePath` property to track per-segment file locations
   - Used for resume and verification

4. **Merge Step** (`DownloadEngine.cs`)
   - Added merge step after all segments complete
   - Calls `StorageManager.MergeSegmentFilesAsync()` to combine segments into final file
   - Works for both new downloads and resumed downloads

5. **Checksum Verification** (`SegmentManager.cs`)
   - Updated to read from individual segment files instead of single file
   - Offset is 0 for per-segment files (each file contains only its segment)

### How It Works Now

**Download Flow:**

```text
1. Create download task
2. Calculate segments (e.g., 8 segments)
3. Download each segment to its own file:
   - segment_0000.part (bytes 0-16MB)
   - segment_0001.part (bytes 16MB-32MB)
   - segment_0002.part (bytes 32MB-48MB)
   - ...
   - segment_0007.part (bytes 112MB-127MB)
4. Merge all segment files into download.part
5. Move download.part to final destination
6. Cleanup segment files
```

**Resume Flow:**

```text
1. Load segment states from disk
2. Identify incomplete segments
3. Resume downloading incomplete segments (append to their files)
4. Merge all segment files into download.part
5. Move to final destination
6. Cleanup
```

### Benefits

✅ **No file locking contention** - Each segment has its own file
✅ **Better concurrency** - Segments can write simultaneously without blocking
✅ **Simpler code** - No need for complex locking mechanisms
✅ **Supports resume** - Individual segment files preserved on pause
✅ **OS-agnostic** - Works consistently across Windows, macOS, Linux

### Tradeoffs

**Pros:**

- Eliminates race conditions
- Better performance for concurrent writes
- Simpler synchronization

**Cons:**

- Additional merge step required (but fast - sequential read/write)
- More files in temp directory (cleaned up automatically)
- Slightly more disk I/O (one extra pass to merge)

The merge step is very fast (sequential I/O) and the benefits far outweigh the minimal overhead.

## Testing

To verify the fix:

1. Run the test script: `./test/scripts/test-pause-resume.sh`
2. Monitor the temp directory during download:

   ```bash
   watch -n 0.5 'ls -lh ~/.kurio/temp/<task-id>/'
   ```

3. You should see individual segment files being created
4. After completion, segment files are merged and cleaned up

The "file being used by another process" error should no longer occur.

## Files Modified

- `src/Kurio.Core/ServiceCollectionExtensions.cs`:
  - Changed default storage mode to `PerSegmentFiles`

- `src/Kurio.Core/Engine/SegmentManager.cs`:
  - Modified `DownloadSegmentAsync` to write to per-segment files
  - Updated checksum computation to read from segment files
  - Updated verification to use segment files

- `src/Kurio.Core/Models/SegmentState.cs`:
  - Added `SegmentFilePath` property

- `src/Kurio.Core/Engine/DownloadEngine.cs`:
  - Added merge step after segment downloads complete
  - Added merge step after resumed downloads complete

## Related Issues

This fix resolves:

- File locking errors during concurrent downloads
- "File is being used by another process" exceptions
- Race conditions in multi-segment downloads

This complements the previous fix for pause/resume corruption, providing a more robust download system.

## Version

- **Fixed in:** v1.0.0 (pending)
- **Date:** November 27, 2025
- **Severity:** High (download failures)
- **Priority:** High
