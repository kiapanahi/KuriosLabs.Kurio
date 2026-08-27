# System design and on-disk persistence

> **Status:** verified against the working tree at `e1723ee` + the uncommitted minimal-API migration,
> `Directory.Build.props` `<Version>1.19.0</Version>`, on 2026-08-27.
> Everything below was read out of the source, not inferred from the README.
> `Kurio.Core` is untouched by the in-flight migration, so the storage and persistence sections apply verbatim
> to the last committed state as well.

This document answers two questions: how the containers fit together, and what exactly lands on disk when a
file is downloaded.

## 1. Container view

```text
                        ┌───────────────────────────────────────────┐
                        │           Kurio.AppHost (Aspire)          │
                        │   orchestrates Server + Web + dashboard   │
                        └────────────────┬──────────────────────────┘
                                         │ launches
              ┌──────────────────────────┴───────────────────────────┐
              ▼                                                      ▼
┌──────────────────────────────┐   HTTP/REST      ┌─────────────────────────────┐
│      Kurio.Web (Blazor)      │ ───────────────► │      Kurio.Server           │
│  KurioApiClient              │ ◄─────────────── │  Endpoints/  (minimal APIs) │
│  HubClientFactory            │   SignalR × 3    │  /hubs/downloads|queue|stats│
│  ConnectionStateService      │ ◄─────────────── │  GET /api/downloads/stream  │
└──────────────┬───────────────┘   SSE            │        (SSE, minimal API)   │
               │                                  └──────────────┬──────────────┘
               │ references                                      │ references
               ▼                                                 ▼
        ┌────────────────────┐                     ┌──────────────────────────┐
        │  Kurio.Contracts   │◄────────────────────│       Kurio.Core         │
        │  DTOs + hub ifaces │                     │   the actual engine      │
        └────────────────────┘                     └──────────────────────────┘

  ✗ Kurio.Web has NO reference to Kurio.Core — enforced by project refs, not convention.
```

The Web→Core non-reference is the load-bearing architectural decision in the repo. It is what makes the
"headless engine" claim structural rather than aspirational: the Blazor dashboard cannot reach into engine
internals, so any future client (CLI, TUI, mobile) gets exactly the access the dashboard has. The cost is DTO
fan-out — several "add a download" shapes exist because the boundary is crossed by hand-written mappers rather
than shared types.

Endpoint registration lives in `src/Kurio.Server/Program.cs`:

```text
  app.MapDownloadEndpoints();        app.MapHub<DownloadHub>("/hubs/downloads");
  app.MapQueueEndpoints();           app.MapHub<QueueHub>("/hubs/queue");
  app.MapStatsEndpoints();           app.MapHub<StatsHub>("/hubs/stats");
  app.MapConfigurationEndpoints();
  app.MapProgressStreamEndpoints();  // GET /api/downloads/stream
```

## 2. Inside Kurio.Core

```text
                     AddKurioDownloadEngine()   ← composition root
                              │
          ┌───────────────────┼────────────────────┬──────────────────┐
          ▼                   ▼                    ▼                  ▼
  ┌───────────────┐  ┌─────────────────┐  ┌────────────────┐  ┌──────────────┐
  │ DownloadEngine│  │ SegmentManager  │  │ StorageManager │  │JsonState     │
  │  (singleton)  │  │                 │  │                │  │Persistence   │
  └───────┬───────┘  └────────┬────────┘  └───────┬────────┘  └──────┬───────┘
          │                   │                   │                  │
   owns   │            splits │            files  │           JSON   │
   state  │            & pulls│            & moves│           state  │
          │                   │                   │                  │
          │            ┌──────▼──────┐            │                  │
          │            │HttpProtocol │            │                  │
          │            │  Handler    │◄─ SpeedLimiter (process-wide) │
          │            └──────┬──────┘            │                  │
          │                   │                   │                  │
          ▼                   ▼                   ▼                  ▼
  ┌────────────────────────────────────────────────────────────────────────┐
  │  DownloadQueueManager   (MaxConcurrentDownloads = 3)                   │
  │  ResiliencePolicyFactory (Polly)  ·  SegmentVerifier (SHA-256)         │
  └────────────────────────────────────────────────────────────────────────┘
```

`DownloadEngine` hands `SegmentManager` an `IProtocolHandler` and a temp path; `SegmentManager` opens the file
streams itself and lets the handler write **straight into them**. There is no in-memory staging buffer of the
payload at any point.

## 3. Lifecycle of one download

```text
 AddDownloadAsync ──► DownloadTask{Id=GUID, State=Created}
        │
        ▼
   Enqueue ─────► DownloadQueueManager (≤3 in flight)
        │
        ▼
 ExecuteDownloadAsync                                 DownloadEngine.cs:423
 ┌──────────────────────────────────────────────────────────────────────┐
 │ 1. State=Analyzing                                                   │
 │    HEAD/GET → ResourceMetadata{ContentLength, SupportsRanges,        │
 │                                SuggestedFileName}                    │
 │ 2. GetAvailableDiskSpaceAsync → throw if < FileSize                  │
 │ 3. CreateTemporaryFileAsync   → mkdir ~/.kurio/temp/{GUID}/          │
 │                                 returns .../download.part  (NOT made)│
 │ 4. CalculateSegments(size, supportsRanges, {MaxConn=8, MinSeg=1MB})  │
 │ 5. State=Downloading  ──► SaveTaskStateAsync()   ← first state write │
 │ 6. DownloadSegmentsAsync  ══► N concurrent segment tasks             │
 │       └─ every progress tick: aggregate, push to progress channel    │
 │       └─ every ≥5 s:         fire-and-forget SaveTaskStateAsync      │
 │ 7. VerifySegmentBoundariesAsync  (no gaps/overlaps, sizes, total)    │
 │ 8. MergeSegmentFilesAsync → download.part                            │
 │ 9. CommitDownloadAsync    → File.Move into destination directory     │
 │10. DeleteStateAsync + CleanupTemporaryFilesAsync                     │
 └──────────────────────────────────────────────────────────────────────┘
```

On failure the engine records a `DownloadError`, increments `RetryCount`, marks the queue entry failed, and
saves state — the temp directory is deliberately left in place. On cancel with `removePartialFiles: true`, the
temp directory and the state file are both removed.

## 4. On-disk layout

Two roots, fixed as hardcoded DI defaults (`ServiceCollectionExtensions.cs:210-211`) and **not** read from
`appsettings.json`:

```text
$HOME/
├── .kurio/
│   ├── temp/                              ← tempDirectory
│   │   └── 8f3c1a92-....-4d21/            ← one dir per task GUID
│   │        ├── segment_0000.part         ← pre-sized, written concurrently
│   │        ├── segment_0001.part
│   │        ├── ...
│   │        ├── segment_0007.part
│   │        └── download.part             ← appears only at merge time
│   │
│   └── state/                             ← stateDirectory
│        ├── 8f3c1a92-....-4d21.json       ← resume state, one per live task
│        ├── 8f3c1a92-....-4d21.json.tmp   ← transient, during atomic save
│        ├── <guid>.json.corrupted.20260827…  ← quarantined bad state
│        └── history/
│             └── download_history.json    ← ⚠ written by nothing (see §9)
│
├── Downloads/                             ← DestinationDirectory (default)
│   └── ubuntu-24.04.iso                   ← final, after File.Move
│
└── Library/Application Support/Kurio/     ← macOS; %APPDATA%\Kurio on Windows
     └── config.json                       ← separate config system, speed limit only
```

`PlatformPathProvider` computes proper per-OS locations and is used for `config.json`, but the engine's
temp/state directories bypass it — they are `Path.Combine(homeDirectory, ".kurio", ...)` on every platform,
including Windows.

## 5. Segment-to-byte mapping

`CalculateSegments` with the defaults `MaxConnections = 8`, `MinSegmentSize = 1 MB`, for a 100 MB file:

```text
  idealCount  = min(8, 104857600 / 1048576 = 100)  = 8
  segmentSize = 104857600 / 8                      = 13 107 200 B
  the last segment absorbs the remainder (end = fileSize - 1)

  logical file  0 ──────────────────────────────────────────► 104857599
                │        │        │        │        │        │        │
   segment idx  0        1        2        3        4    …   6        7
   byte range   0        13107200 26214400 39321600 52428800 78643200 91750400
                └─13.1MB─┴─13.1MB─┴─13.1MB─┴─13.1MB─┴─13.1MB─┴─13.1MB─┴─13.1MB┘

  ON DISK, each range is its OWN file starting at offset 0:

   segment_0000.part  [0 .. 13107199]   ← HTTP Range: bytes=0-13107199
   segment_0001.part  [0 .. 13107199]   ← HTTP Range: bytes=13107200-26214399
   ...                                       (file offset ≠ logical offset)
   segment_0007.part  [0 .. 13107199]   ← HTTP Range: bytes=91750400-104857599
```

This is the crux of `StorageMode.PerSegmentFiles`: **the logical byte offset is not the file offset**. Each
segment file is a standalone blob whose position in the final file is implied only by its `_NNNN` index.
Segment files are pre-sized with `SetLength(segmentSize)` before writing, so a partially-downloaded segment
file is already full-length on disk with a zero tail.

If the server does not support ranges, or the file is smaller than `MinSegmentSize`, `CalculateSegments`
returns a single segment covering `[0, fileSize-1]` with `SupportsRanges = false`.

### Merge

```text
  segment_0000.part ─┐
  segment_0001.part ─┤
  segment_0002.part ─┼──► sequential CopyToAsync, 1 MB buffer ──► download.part
        ...          ─┤    (FileOptions.SequentialScan)
  segment_0007.part ─┘         then delete each segment file

  SPECIAL CASE segmentCount == 1:
  segment_0000.part ──── File.Move ────► download.part     (rename, no copy —
                                                            avoids duplicating
                                                            a multi-GB payload)
```

### Commit

```text
  ~/.kurio/temp/{GUID}/download.part
            │
            │  File.Move(temp, dest, overwrite: policy == Overwrite)
            ▼
  ~/Downloads/ubuntu-24.04.iso
                       ▲
                       └─ FileNamingPolicy decides the name:
                          Overwrite  → clobber
                          AutoRename → ubuntu-24.04(1).iso, (2)…
                          Skip       → throw InvalidOperationException
                          Prompt     → throw NotSupportedException (core has no UI)
```

Per-segment files trade disk cost for concurrency simplicity — the DI comment says it plainly: *"Use
per-segment files to avoid file locking contention."* The price is a full sequential re-write of the entire
payload at merge time, so a 100 GB download performs roughly 200 GB of disk writes and needs 2× free space.
The pre-flight check in step 2 only verifies `FileSize`, and it checks the **destination** volume while the
temp files land under `$HOME`.

## 6. State file anatomy

`~/.kurio/state/{GUID}.json` — camelCase, indented, enums as strings, nulls omitted:

```jsonc
{
  "version": "1.0",
  "taskId": "8f3c1a92-...-4d21",
  "url": "https://releases.ubuntu.com/…/ubuntu-24.04.iso",
  "fileName": "ubuntu-24.04.iso",
  "fileSize": 104857600,
  "destinationDirectory": "/Users/k/Downloads",
  "tempFilePath": "/Users/k/.kurio/temp/8f3c1a92-…/download.part",
  "state": "Paused",                       // DownloadState enum
  "priority": "Normal",
  "metadata": { "contentLength": 104857600, "supportsRanges": true },
  "segments": [
    { "segmentIndex": 0, "startByte": 0, "endByte": 13107199,
      "bytesDownloaded": 13107200, "status": "Completed",
      "checksum": { "algorithm": "SHA256", "hash": "A3F1…", "isVerified": false },
      "segmentFilePath": "/Users/k/.kurio/temp/8f3c1a92-…/segment_0000.part" },
    { "segmentIndex": 1, "startByte": 13107200, "endByte": 26214399,
      "bytesDownloaded": 4210688, "status": "Downloading" }   // ← resume point
  ],
  "createdAt": "…", "startedAt": "…", "lastUpdateAt": "…",
  "retryCount": 0, "lastError": null,
  "options": { }
}
```

Computed, never a serialized source of truth: `totalBytesDownloaded = Σ segments[].bytesDownloaded`,
`completedPercent`, and `canResume = state == Paused && metadata.supportsRanges && segments.Count > 0`.

Every state write is atomic-by-rename:

```text
  serialize ──► {GUID}.json.tmp ──► FlushAsync ──► Dispose (release handle)
                                                        │
                                          File.Move(tmp, {GUID}.json, overwrite: true)
```

A crash mid-serialize leaves a stray `.tmp`; the real state file is never torn. On load failure the file is
renamed to `.corrupted.<yyyyMMddHHmmss>` and `null` is returned rather than thrown — the download is
forgotten, not crashed on.

The same write-to-`.tmp` / flush / dispose / `File.Move` pattern appears in `JsonStatePersistence`,
`JsonDownloadHistoryRepository`, and in spirit at the final commit. It is the correct primitive:
`rename(2)` and `MoveFileEx` are atomic within a volume, so a reader never sees a half-written file. The
ordering detail that makes it work — `File.Move` only *after* `await using` has released the handle — is
called out in a comment at `JsonStatePersistence.cs:64`.

## 7. Write path and durability

```text
 HTTP socket
     │  read 80 KB chunk
     ▼
 handler: destination.WriteAsync(chunk)        HttpProtocolHandler.cs:203
     │        └─ lands in FileStream's 80 KB user-space buffer
     │
     ├─ progress.Report(totalBytesRead)        line 205  ◄── counts BUFFERED bytes
     │        └─ state.BytesDownloaded = initial + bytesRead   SegmentManager.cs:390
     │        └─ every ≥5 s → SaveTaskStateAsync() writes that number to JSON
     ▼
 (loop until range exhausted)
     │
     ▼
 segmentStream.FlushAsync()                    SegmentManager.cs:411
     │        └─ buffer → OS page cache  (no WriteThrough, no fsync)
     ▼
 position check: finalPos == initial + range.Length ? else throw
     ▼
 state.BytesDownloaded = initial + range.Length     line 429  ← authoritative value
 state.SegmentFilePath = …/segment_NNNN.part
     ▼
 re-open the segment file, stream SHA-256 over exactly TotalSize bytes
 (IncrementalHash + ArrayPool, 80 KB chunks — never buffers the segment)
     ▼
 state.Status = Completed
```

The durability ladder, weakest to strongest:

```text
  ┌───────────────────────────────────────────────────────────────────┐
  │ in-flight counter   → survives nothing                            │
  │ FileStream buffer   → survives nothing (≤80 KB in user space)     │
  │ after FlushAsync    → survives process kill; NOT power loss       │
  │ after state save    → survives restart, IF it agrees with the file│
  │ after File.Move     → survives everything the filesystem does     │
  └───────────────────────────────────────────────────────────────────┘
```

**Known soft spot.** The persisted `bytesDownloaded` counts bytes handed to the `FileStream`, not bytes
flushed. A 5-second state save that fires while ≤80 KB sits unflushed, followed by a hard kill or power loss,
persists a count ahead of the segment file's real content. Resume then seeks to that offset and writes past a
zero-filled hole. The boundary check in step 7 still passes (the sizes are right) and no SHA-256 was recorded
for the incomplete segment, so nothing detects it. A graceful pause is safe: `await using` disposes and
flushes the stream on cancellation.

## 8. Pause, resume, recovery

```text
  RUNNING ──── PauseDownloadAsync ─────────────────────────────────► PAUSED
               1. task.State = Paused      (set BEFORE cancel, so the
               2. queueManager.MarkAsPaused  catch block can distinguish
               3. cts.CancelAsync()          pause from failure)
               4. SaveTaskStateAsync()     ← segment files stay on disk

  PAUSED ───── ResumeDownloadAsync ────────────────────────────────► QUEUED
               ValidateResumeCapability → Enqueue → SaveTaskStateAsync
                        │
                        ▼  scheduler sees _resumingTasks[id] → ExecuteResumeAsync
               ┌────────────────────────────────────────────────────┐
               │ VerifyCompletedSegmentsAsync:                      │
               │   for each Completed segment with a checksum →     │
               │     re-hash the segment file, compare              │
               │     ✓ MarkAsVerified                               │
               │     ✗ MarkAsFailed, Status=Failed, bytes=0  (redo) │
               │                                                    │
               │ for each incomplete segment:                       │
               │   remainingStart = StartByte + BytesDownloaded     │
               │   Range: bytes={remainingStart}-{EndByte}          │
               │   open segment file OpenOrCreate,                  │
               │   Seek(BytesDownloaded) ← file-local offset        │
               └────────────────────────────────────────────────────┘
                        │
                        ▼   merge → commit → delete state → rm -rf temp/{GUID}

  PROCESS RESTART ──► RecoverPersistedStatesAsync()      DownloadEngine.cs:887
               LoadAllStatesAsync() globs state/*.json
                 → rebuild DownloadTask, SegmentConfiguration, tempFilePath
                 → re-register in _tasks / _segmentConfigs / _tempFilePaths
                 → tasks reappear in whatever state they were persisted in
                    (a killed-while-Downloading task comes back as
                     "Downloading" but nothing is running — it is not
                     auto-restarted, and CanResume is false because it
                     requires State == Paused)
```

## 9. Known gaps in this area

| Thing                                                               | Reality                                                                                                                                                                               |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `StorageMode.SingleFile`                                            | Never runs. DI hardcodes `PerSegmentFiles`, so the `SetLength` pre-allocation branch in `CreateTemporaryFileAsync` is dead.                                                           |
| `WriteSegmentAsync` / `CreateSegmentFileAsync` / `VerifyWriteAsync` | Zero production callers — `SegmentManager` opens its own `FileStream`s. Only `PauseResumeTests` and `SegmentManagerAdvancedTests` reach them. `VerifyWrites` is `false` in DI anyway. |
| `state/history/download_history.json`                               | `IDownloadHistoryRepository.AddAsync` has no callers anywhere in `src/`. The file is created on read, never populated — `/api/stats` reports over a permanently empty history.        |
| `Kurio:Storage` in `appsettings.json`                               | Dead. Temp/state paths come only from the hardcoded `$HOME/.kurio/*` defaults.                                                                                                        |
| `TempFileCleanupService`                                            | Scans `temp/*` by mtime with no cross-check against live or paused tasks — a long-paused download's segments look like orphans.                                                       |
| `File.Move` at commit                                               | A cheap rename on the same volume; a silent full copy (non-atomic, no partial-file cleanup) if `~/.kurio/temp` and the destination sit on different filesystems.                      |
| Disk-space pre-flight                                               | Checks `FileSize` on the destination volume only; `PerSegmentFiles` + merge actually needs `2 × FileSize`, and the segments live under `$HOME`.                                       |
| Buffered progress vs. flushed bytes                                 | See §7 — persisted `bytesDownloaded` can exceed what is on disk after an ungraceful stop.                                                                                             |

## Source index

| Concern                                  | File                                                                   |
| ---------------------------------------- | ---------------------------------------------------------------------- |
| Orchestration, state save/recover        | `src/Kurio.Core/Engine/DownloadEngine.cs`                              |
| Range math, segment download, checksums  | `src/Kurio.Core/Engine/SegmentManager.cs`                              |
| Temp files, merge, commit, naming policy | `src/Kurio.Core/Storage/StorageManager.cs`                             |
| Atomic JSON state                        | `src/Kurio.Core/Persistence/JsonStatePersistence.cs`                   |
| Persisted shape                          | `src/Kurio.Core/Models/DownloadTaskState.cs`, `Models/SegmentState.cs` |
| Storage knobs                            | `src/Kurio.Core/Models/StorageOptions.cs`, `Models/StorageMode.cs`     |
| Per-OS paths                             | `src/Kurio.Core/Storage/PlatformPathProvider.cs`                       |
| Wiring and defaults                      | `src/Kurio.Core/ServiceCollectionExtensions.cs`                        |
| Byte pump and progress reporting         | `src/Kurio.Core/Protocols/HttpProtocolHandler.cs`                      |
