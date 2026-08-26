# Download-manager landscape study

Synthesis of a 40-entry survey of download managers, engines, libraries, and integration standards, written to
inform Kurio's revival roadmap (Kurio.Core engine + Kurio.Server REST/SignalR + Kurio.Web Blazor, .NET 10).
Raw per-project notes: [raw-research-notes.json](raw-research-notes.json). Compiled 2026-08-26.

## Landscape overview

Duplicate research entries (aria2, wget2, Motrix, IDM, FDM were each studied from multiple angles) are merged
into one row per project.

| Project                    | Language         | Architecture model                                                                                   | Engine                          | Standout trait                                                                                                                   |
| -------------------------- | ---------------- | ---------------------------------------------------------------------------------------------------- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| aria2                      | C++              | Headless daemon; JSON-RPC/XML-RPC/WebSocket, zero UI code                                            | Own (HTTP/FTP/SFTP/BT/Metalink) | RPC-first design; versioned binary `.aria2` per-file control files with piece bitfields                                          |
| Motrix (v1 / v2 beta)      | JS/TS (Electron) | v1: GUI supervising bundled aria2c; v2: own core behind MDXP JSON-RPC, SQLite, QuickJS plugins       | aria2 (v1), own (v2)            | Engine supervision + startup reconciliation + user-facing self-diagnostics; v2 proves the "engine + open protocol" rewrite       |
| AriaNg                     | JS (static)      | Pure static web frontend, no backend at all; talks to aria2 RPC from the browser                     | None (remote aria2)             | A complete UI built on someone else's RPC API; delta-only status polling                                                         |
| Gopeed                     | Go + Flutter     | Single binary: engine + REST (`/api/v1`) + Goja JS extension VM; Bolt KV storage                     | Own (Fetcher/FetcherManager)    | Git-distributed extensions with manifest, lifecycle hooks (onResolve/onStart/onError/onDone), per-extension KV + typed settings  |
| Brisk                      | Dart (Flutter)   | Desktop app; in-process engine on isolates; companion browser extension                              | Own                             | Dynamic connection spawn/reuse (work stealing) + stalled-connection watchdog; M3U8 capture                                       |
| AB Download Manager        | Kotlin           | Gradle modules: engine core / read-only monitor / UIs / embedded REST (port 15151, OpenAPI)          | Own (coroutines)                | Atomic temp-then-rename JSON persistence, split metadata vs parts files; HLS first-class; typed resume-failure exceptions        |
| pyLoad                     | Python           | Single process; Flask web UI + `@Expose` HTTP API; SQLite                                            | Own                             | Four plugin base classes (Hoster/Crypter/Account/Hook) with regex URL dispatch; captcha pipeline                                 |
| JDownloader 2              | Java             | Thick client + MyJDownloader cloud-relay RPC (device polling)                                        | Own                             | End-to-end-encrypted relay RPC; two-stage LinkGrabber -> download queue pipeline; offline command queuing                        |
| Persepolis                 | Python (Qt)      | GUI + own engine (dropped aria2 XML-RPC); three SQLite DBs; native-messaging host                    | Own (requests) + yt-dlp         | Per-category queue threads with time windows and post-completion actions (shutdown/sleep)                                        |
| uGet (dormant)             | C (GTK)          | GUI over uglib with a C vtable plugin ABI matched by URL scheme                                      | libcurl + optional aria2 plugin | Backend-agnostic engine plugin ABI; 7x24 weekly speed-throttle grid; per-category retention limits                               |
| XDM                        | C#               | Shared XDM.Core + per-OS UIs; SQLite downloads table; loopback HTTP (127.0.0.1:8597) browser IPC     | Own (Chunk model) + yt-dlp      | Server-pushed extension filter rules via /sync; HLS/DASH media classification and de-dupe heuristics                             |
| Varia                      | Python (GTK4)    | Thin frontend spawning aria2c, driven via aria2p; local capture server                               | aria2 + yt-dlp + ffmpeg         | Unified queue mixing HTTP, torrent, and media jobs; pure orchestration layer                                                     |
| Parabolic                  | C# (.NET 10)     | Shared core project + GTK4 and WinUI heads; supervised subprocesses                                  | yt-dlp/ffmpeg/aria2c            | Executable discovery/validation, typed YtdlpException stderr mapping, RecoverableDownload replay records                         |
| DownThemAll!               | TypeScript       | WebExtension; transfer delegated to `browser.downloads`                                              | Browser                         | Renamer mask language, glob/regex/substring filters, per-domain concurrency limits, HEAD preroller, state normalization on save  |
| Downloader (bezzad)        | C#               | In-process library (DownloadService/DownloadConfiguration/DownloadPackage)                           | Own                             | Serializable DownloadPackage resume token; chunks write directly to final-file offsets; range-support fallback                   |
| FluentFTP                  | C#               | Embeddable FTP/FTPS client library, zero deps                                                        | n/a                             | Dialect-tolerant LIST parsing, TLS 1.3, FXP, full proxy taxonomy (HTTP/SOCKS4/4a/5)                                              |
| MonoTorrent                | C#               | Embeddable BitTorrent engine (ClientEngine/TorrentManager)                                           | Own                             | Fast-resume data (verified-piece bitmap); pluggable piece pickers; global vs per-job rate limits                                 |
| YoutubeDLSharp             | C#               | yt-dlp/ffmpeg process wrapper; OptionSet maps 1:1 to CLI flags                                       | yt-dlp                          | Self-bootstrapping binary download; probe (RunVideoDataFetch) separated from download                                            |
| IDM (closed)               | C/C++            | Native Windows app + browser bridge module                                                           | Own                             | Dynamic largest-segment bisection + idle-connection work stealing; sub-minute segment-map flush; visual segment map              |
| FDM (closed)               | C++              | Desktop app + browser extension                                                                      | Own + BT                        | `.fdmdownload` single-file incomplete marker with rename-on-verified-complete; switchable bandwidth modes                        |
| wget2                      | C                | libwget core + CLI                                                                                   | Own                             | `--chunk-size` splitting (size-driven, not count-driven); HTTP/2 multiplexing; Metalink XML + RFC 6249 header variant            |
| curl / libcurl             | C                | Easy/Multi two-tier API; per-origin connection pool                                                  | Own                             | Pool keyed by host+config with age/lifetime eviction; Happy Eyeballs; `--parallel-max-host` per-origin cap; `-C` resume contract |
| MeTube                     | Python + Angular | Single container: backend + SPA + WebSocket push                                                     | yt-dlp                          | Split queue.json/completed.json state; PUID/PGID/UMASK; URL_PREFIX and scoped CORS knobs                                         |
| qBittorrent                | C++              | App with embedded versioned WebUI API (`/api/v2/<ns>/<method>`)                                      | libtorrent                      | The de facto *arr integration contract: categories with savePath, content_path, rich state enum                                  |
| SABnzbd                    | Python           | Monolith; single-endpoint `?mode=` API + API key                                                     | Own (Usenet)                    | First-class history surface separate from live queue; category cascade defaults; post-processing enum                            |
| NZBGet                     | C++              | Single process; JSON/XML-RPC on one endpoint                                                         | Own (Usenet)                    | Three-tier credentials (full/restricted/add-only); per-pipeline-stage pause; built-in speed/disk diagnostics RPCs                |
| Sonarr/Radarr contract     | C#               | Poll download clients via per-client adapters                                                        | n/a                             | Contract = add-with-category, queue status, retained history with stable output path, remote path mapping                        |
| Metalink (RFC 5854/6249)   | Spec             | Mirror + hash metadata as .meta4 file or plain HTTP headers                                          | n/a                             | Per-piece hashes enable re-fetching only corrupted chunks; header variant needs no extra file                                    |
| .NET BCL primitives        | C#               | RandomAccess/File.OpenHandle, System.Threading.RateLimiting, System.IO.Pipelines, SocketsHttpHandler | n/a                             | Concurrent positional writes + zero-fill preallocation; first-party token bucket; explicit HTTP/2 connection policy knobs        |
| linuxserver.io conventions | Shell/s6         | Docker packaging standard (not an app)                                                               | n/a                             | PUID/PGID remap, single /config volume, baked-in HEALTHCHECK; what NAS users expect                                              |
| Browser handoff patterns   | Spec/JS          | Three transports: native-messaging stdio host, loopback HTTP + scoped CORS, cloud relay              | n/a                             | Loopback HTTP + pairing token is the lowest-friction fit for server-first apps (Gopeed/XDM model)                                |

## Architecture patterns

### 1. Headless engine + RPC contract + thin frontends is the converged endgame

aria2 spawned dozens of frontends (AriaNg is a *static HTML page* that is a complete UI) because the daemon has
zero UI code and a complete RPC surface. Gopeed, AB DM, and qBittorrent embed the same shape; Motrix threw away
its v1 Electron-wraps-aria2 design and rebuilt v2 around an open, versioned JSON-RPC protocol (MDXP) with
published schema types. Kurio already has the right skeleton (Core / Server / Web), but the API is not yet a
product: no published OpenAPI/contract package, no named lifecycle events, no capability discovery. Opinion:
this is Kurio's structural advantage over every desktop-first competitor — invest in the contract (versioned
DTOs in Kurio.Contracts published as the source of truth, OpenAPI file in the repo root like AB DM's
REST-API.yml, `onDownloadStarted/Completed/Failed`-style named SignalR events) and treat Blazor as client #1 of
N. Every feature must land in the API first and the dashboard second, or AriaNg-style third-party clients can
never exist.

### 2. Resume state is a per-download sidecar with HTTP validators, not one JSON blob

The mature engines converge on the same recipe: per-download control/state files (aria2's versioned binary
`.aria2` bitfields, AB DM's `{id}.json` metadata + separate `{id}.json` parts file, bezzad's DownloadPackage,
FDM's `.fdmdownload` marker), atomic write-temp-then-rename for every flush (AB DM's TransactionalFileSaver),
periodic flush during transfer (IDM sub-minute, aria2 save-interval), ETag/Last-Modified captured at start and
re-validated with If-Range on every resume (curl, wget2, aria2), and rename-to-final-name only after verification
(FDM). Opinion: a single queue-wide JSON snapshot is the most dangerous liability in Kurio's current design — a
crash mid-write can corrupt every download's state at once (aria2's docs warn about exactly this), and resuming
without validators can silently corrupt files. Split hot per-segment state from cold metadata, flush both
atomically on a timer, and make validator-checked resume the canonical semantics. This is unglamorous
table-stakes correctness that most home-grown managers get wrong.

### 3. External engines: fine as supervised leaf tools, fatal as load-bearing cores

Every project that made aria2 a hard dependency regretted it: Persepolis ripped out aria2 XML-RPC after crashes
kept killing sessions, uGet went dormant partly from that same fragility, Motrix v1's bundled-binary coupling
justified a ground-up rewrite, and Varia pays a heavy packaging tax bundling aria2c+yt-dlp+ffmpeg per OS. The
success stories (Parabolic, MeTube, XDM) wrap *domain-specific* tools (yt-dlp, ffmpeg) as supervised
subprocesses with typed error mapping and replay-based recovery — never as the primary transfer engine.
Opinion: Kurio.Core staying pure managed .NET is correct and should be a stated principle; the "aria2
integration layer" backlog item should be demoted to, at most, an optional `IDownloadBackend` plugin selected by
URL scheme (uGet's vtable model), and yt-dlp should arrive as a supervised subprocess job kind with process-replay
(not byte-offset) resume semantics.

### 4. Extension systems that survive are manifest + few lifecycle hooks + sandbox; regex plugin farms decay

pyLoad's regex-dispatched Hoster/Crypter plugin farm was brilliant until hosters churned URL schemes faster than
maintainers could patch, and its captcha/premium ecosystem rotted silently. JDownloader's closed, CDN-distributed
plugins block community contribution. The modern shape that works: Gopeed's declarative manifest.json + four
events (onResolve/onStart/onError/onDone) + Chrome-style URL match patterns + per-extension KV storage and typed
settings, distributed as plain git repos; Motrix v2 adds a permission schema and sandbox (QuickJS). Opinion: when
Kurio builds its plugin backlog item, copy Gopeed's shape (in .NET terms: `AssemblyLoadContext` or a JS/WASM
sandbox, manifest-declared hooks and URL patterns, git-based install), keep the hook count tiny, and never let
site-specific logic into Kurio.Core.

### 5. Browser capture: loopback HTTP + scoped CORS + pairing token beats native messaging for a server-first app

Three observed transports: native-messaging stdio hosts (FDM, Persepolis — require per-OS manifest/registry
installers), cloud relay (JDownloader — privacy SPOF outside user control), and plain HTTP to an
already-listening local server (Gopeed :9999, XDM 127.0.0.1:8597, AB DM :15151, Varia server.py). XDM even
abandoned its two earlier native-messaging generations for loopback HTTP. Kurio.Server is *already* a persistent
HTTP server, so the third option is nearly free. Opinion: build a `/api/capture` endpoint set with CORS scoped
to the extension origin plus a pairing token (JD's device-pairing idea, minus the cloud), and design the capture
payload (url, headers, cookies, referrer, suggested name/category) now since it doubles as the clipboard-watcher
and bookmarklet schema. Never ship `Access-Control-Allow-Origin: *`.

### 6. Ecosystem compatibility and packaging are features, not chores

SABnzbd/NZBGet/qBittorrent all keep an addressable history separate from the live queue, categories that carry
save paths and cascading defaults, and simple API-key auth — because the Sonarr/Radarr contract (add with
category, poll queue, import from retained history, remote path mapping) depends on exactly those. rdt-client
got the entire *arr ecosystem for free by emulating qBittorrent's API. Meanwhile MeTube/linuxserver.io define
what self-hosters expect from a container (PUID/PGID, /config volume, HEALTHCHECK, URL_PREFIX). Opinion: Kurio's
job model should grow `category`, `savePath`/`contentPath`, a normalized status enum, and history retention now,
so a qBittorrent-compat shim and a proper Docker image are mapping exercises later instead of redesigns.

## Feature matrix

| Feature                                             | Who has it                                                                               | Kurio                                                              |
| --------------------------------------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------ |
| Segmented multi-connection HTTP                     | aria2, IDM, FDM, XDM, Brisk, AB DM, Gopeed, wget2, curl, bezzad, Motrix                  | Implemented                                                        |
| Pause/resume                                        | Effectively all engines surveyed                                                         | Implemented                                                        |
| Dynamic segment rebalancing / work stealing         | IDM, Brisk                                                                               | Absent (static split; high-value gap)                              |
| Crash-safe per-segment sidecar state + validators   | aria2, AB DM, IDM, FDM, bezzad, curl (contract)                                          | Partial — JSON persistence exists; atomicity/validators unverified |
| Range-support probe with single-stream fallback     | curl, wget2, bezzad, DTA (preroller), Gopeed (/resolve)                                  | Absent/unverified                                                  |
| Checksum verification                               | aria2 (incl. per-piece via Metalink), wget2, Kurio                                       | Implemented (end-to-end)                                           |
| Speed limiting (global)                             | aria2, qBittorrent, AB DM, IDM, FDM, MonoTorrent, bezzad                                 | Implemented (token-style)                                          |
| Per-download / per-host limits and concurrency caps | curl (--parallel-max-host), DTA (per-domain), MonoTorrent, aria2                         | Absent                                                             |
| Speed-limit schedules / bandwidth profiles          | FDM (modes), uGet (7x24 grid), IDM                                                       | Absent                                                             |
| Queue/scheduler (time windows, post-actions)        | Persepolis, XDM, Brisk, AB DM, IDM, FDM, uGet                                            | Backlog                                                            |
| Multiple named queues                               | AB DM, Brisk, XDM, Persepolis (categories)                                               | Absent (single queue)                                              |
| Categories with save-path routing                   | qBittorrent, SABnzbd, Persepolis, uGet, pyLoad (packages)                                | Absent (README aspiration)                                         |
| History as separate queryable surface + retention   | SABnzbd, qBittorrent, Motrix v2, Parabolic, MeTube                                       | Absent                                                             |
| Remote API for third-party frontends                | aria2, Gopeed, AB DM, qBittorrent, SABnzbd, NZBGet, pyLoad, Motrix v2 (MDXP), JD (relay) | Implemented (REST+SignalR) but unversioned/unpublished             |
| Push events (WS/SignalR) vs polling                 | aria2 (WS notifications), MeTube, Kurio; most others poll                                | Implemented (progress); named lifecycle events missing             |
| Browser capture extension                           | IDM, FDM, XDM, Brisk, AB DM, Gopeed, DTA, JD, Varia, Parabolic, Persepolis, Motrix v2    | Backlog                                                            |
| Clipboard link watching                             | Brisk (global hotkey), IDM, FDM                                                          | Backlog                                                            |
| Bulk add / link filters / batch templates           | DTA (filters, batches, renamer), Brisk (selection capture), JD (LinkGrabber)             | Absent                                                             |
| Import/export of queue                              | aria2 (--save-session/--input-file), DTA (imex)                                          | Backlog                                                            |
| Multi-mirror / Metalink                             | aria2, wget2 (RFC 5854 + 6249)                                                           | Absent                                                             |
| FTP/SFTP                                            | aria2, uGet, FDM, wget2, curl, FluentFTP (lib)                                           | Backlog (FTP)                                                      |
| Proxy support                                       | aria2, IDM, FDM, curl, FluentFTP, AB DM (pluggable provider)                             | Backlog                                                            |
| BitTorrent                                          | aria2, qBittorrent, FDM, Varia (via aria2), MonoTorrent (lib)                            | Absent (not planned)                                               |
| yt-dlp / streaming media                            | Parabolic, MeTube, Varia, Persepolis, XDM, YoutubeDLSharp (lib)                          | Backlog                                                            |
| Native HLS/M3U8 engine path                         | AB DM (HLSPartDownloader), Brisk, XDM                                                    | Backlog (subset of streaming)                                      |
| Post-processing pipeline (unpack/scripts/mux)       | SABnzbd, NZBGet, pyLoad, Parabolic (ffmpeg), Persepolis (mux)                            | Partial (checksum only)                                            |
| Webhooks / notifications                            | Gopeed (task done/error POST), NZBGet/SABnzbd (scripts), Varia (toasts)                  | Absent                                                             |
| Auth: API keys / roles                              | qBittorrent (SID + Bearer key), SABnzbd (key), NZBGet (3 tiers), Gopeed (X-Api-Token)    | Absent                                                             |
| *arr download-client compatibility                  | qBittorrent, SABnzbd, NZBGet (native adapters); rdt-client (shim)                        | Absent (shim opportunity)                                          |
| Docker/self-host packaging conventions              | MeTube, linuxserver.io, Gopeed                                                           | Absent (Aspire is local-dev only)                                  |
| Mobile client                                       | Gopeed (Android/iOS), AB DM (Android), JD (MyJD apps)                                    | Absent                                                             |
| Auto-update (app or bundled tools)                  | IDM, FDM, JD, YoutubeDLSharp (binary bootstrap)                                          | Backlog                                                            |

## Top 10 ideas for Kurio

Ranked by value-to-effort, best first.

1. **Harden resume persistence with atomic writes, split state files, and HTTP validators** (AB Download Manager,
   curl, aria2). Adopt write-temp-then-atomic-rename for every state flush, split per-download metadata from the
   frequently-rewritten segment map, persist ETag/Last-Modified at start, and send If-Range on resume — falling
   back to restart (surfaced over SignalR, not silent) when the server returns 200. First step: audit
   Kurio.Core's persistence for atomicity and validator capture, and add an integration test that kills the
   process mid-download and resumes against a changed resource.

2. **Replace hand-rolled primitives with BCL: `RandomAccess` + `preallocationSize`, `TokenBucketRateLimiter`**
   (dotnet/runtime notes). One shared `SafeFileHandle` per download with positional `WriteAsync` per segment
   removes temp-file merging and locking; `File.OpenHandle(preallocationSize:)` gives safe zero-fill
   preallocation; `System.Threading.RateLimiting` replaces custom token-bucket math. Low-risk refactors with
   outsized correctness/perf payoff. First step: one issue per swap, starting with the rate limiter.

3. **Browser capture endpoint + minimal MV3 extension** (Gopeed, XDM, AB Download Manager, Brisk). Add
   `POST /api/capture` accepting url/headers/cookies/referrer/suggestedName with CORS scoped to the extension
   origin plus a dashboard-generated pairing token; then a context-menu-only WebExtension that posts to it. This
   clears the browser-integration backlog item with the transport every server-first peer converged on. First
   step: define and document the capture payload schema in Kurio.Contracts (it doubles as the clipboard-capture
   schema).

4. **Publish the API as a versioned contract with named lifecycle events** (aria2, Motrix MDXP, AriaNg). Emit
   `DownloadStarted/Paused/Completed/Failed` SignalR events (not just progress ticks), ship an OpenAPI spec at
   the repo root, and add a capabilities endpoint so clients can introspect server features. First step: commit
   the OpenAPI file and an event-name enum in Kurio.Contracts; treat any change as semver-relevant.

5. **Add a probe/resolve step before task creation** (Gopeed `/api/v1/resolve`, DownThemAll Preroller). A
   HEAD/ranged-GET probe resolves real filename (Content-Disposition), size, Accept-Ranges, and naming conflicts
   up front, and gates segmentation with a documented single-stream fallback when ranges are unsupported. It is
   also the natural future hook for mirror discovery (RFC 6249 `Link: rel=duplicate`/`Digest` sniffing) and
   plugins. First step: `POST /api/downloads/probe` returning a typed probe result the add-dialog consumes.

6. **Dynamic segmentation: largest-segment bisection, work stealing, and a stall watchdog** (IDM, Brisk). When a
   worker finishes early, split the largest remaining segment's tail instead of idling; refuse splits below a
   ~1-2 MB floor; cancel and re-issue segments with no byte progress for N seconds (stall, not just HTTP error).
   This is the single biggest engine-quality differentiator users notice. First step: prototype behind a feature
   flag with a `SegmentMapChanged` SignalR event so the dashboard can render an IDM-style segment map.

7. **First-class queues with schedules and post-completion actions** (Persepolis, XDM, AB Download Manager).
   Model `DownloadQueue` entities (id, name, ordered items, concurrency limit, optional
   start/end time-of-day + days-of-week, AfterQueueAction) evaluated by a persistent `IHostedService` using range
   checks (`now >= start && now < end`), never exact minute matching. Clears the scheduling backlog item. First
   step: a PRD for the Queue entity + schedule evaluation service.

8. **History surface + categories with save paths, aiming at a qBittorrent compat shim** (SABnzbd, qBittorrent,
   Sonarr/Radarr contract). Keep completed/failed jobs in a queryable history with a retention policy, add a
   category field carrying default save path/priority, and expose a stable final output path per job — then an
   optional `/api/v2` qBittorrent-shaped shim makes Kurio a drop-in *arr download client with zero upstream work.
   First step: history endpoints + retention config; shim as its own follow-up issue.

9. **Ship a proper Docker image following self-hosting conventions** (MeTube, linuxserver.io). PUID/PGID/UMASK
   env vars, a single /config volume for state, separate /downloads (+ incomplete dir) volumes, baked-in
   HEALTHCHECK against ASP.NET Core health checks, `UsePathBase` support for reverse proxies, and a static API
   key. This is how a web-dashboard-only download manager actually reaches its audience. First step: Dockerfile +
   compose example + documented env-var contract.

10. **yt-dlp adapter as a distinct job kind via YoutubeDLSharp** (Parabolic, MeTube). Wrap yt-dlp as a supervised
    subprocess job type with its own resume semantics (persist URL+options, re-invoke on resume — process replay,
    not byte offsets), typed stderr error mapping kept small, a probe-then-pick-format flow, and self-bootstrapped
    yt-dlp/ffmpeg binaries surfaced in a diagnostics endpoint. First step: define `IDownloadJob` so a
    non-range-based job type fits the queue/monitor/SignalR contract before writing the adapter.

## Pitfalls to avoid

- One monolithic session/state file: a crash while writing it corrupts every download's in-flight state, and
  periodic snapshots are not transactional logs (aria2 --save-session caveat).
- Resuming on file-size alone without ETag/Last-Modified validation silently produces corrupt files (wget2's own
  contributors flag this ambiguity; curl's docs call it out).
- Renaming to the final filename before checksum verification and flush completes hands users a corrupt file that
  looks finished (FDM's rename-on-verified-complete exists for a reason).
- Hard dependency on an external engine binary: Motrix v1, Persepolis, uGet, and Varia all paid for aria2
  coupling with crashes, packaging weight, or a full rewrite. Keep Kurio.Core pure managed code.
- Exact-time scheduler triggers: XDM's 60-second timer matching `hour:minute` equality silently skips actions
  when a tick is missed; evaluate schedules as ranges on every tick and persist them across restarts.
- Letting Polly retries create new HttpClients: it defeats connection pooling and re-triggers TLS handshakes;
  retries must reuse the pooled handler (curl's pool semantics).
- Assuming N segments = N independent connections: .NET uses one multiplexed HTTP/2 connection per origin by
  default; make the protocol policy (EnableMultipleHttp2Connections vs forced HTTP/1.1) an explicit, tested
  decision.
- Per-origin connection caps must span the whole queue, not one download — two 8-segment downloads from one host
  is 16 connections and a likely rate-limit ban (curl --parallel-max-host).
- Fast Windows preallocation via SetFileValidData leaks previously deleted disk data; use
  `File.OpenHandle(preallocationSize:)` which zero-fills (aria2 issue #583).
- fsync extremes: per-write fsync tanks NAS/HDD throughput, fsync-only-at-completion risks large loss windows;
  use a configurable time/byte-bounded cadence, decoupled from state-file flushes.
- Regex-dispatched site-plugin farms decay under hoster churn and dead solver services (pyLoad); keep site logic
  in independently versioned plugins with a stable contract.
- Client-held shared secrets (AriaNg localStorage) and `Access-Control-Allow-Origin: *` are not acceptable for a
  LAN/internet-reachable server; plan API-key/role auth (NZBGet's add-only tier is a good model) before the
  extension ships.
- Forced two-stage staging queues confuse users ("why isn't my link downloading?") — if a discovery/review step
  is added (JDownloader LinkGrabber), auto-commit the common single-direct-link case.
- Purging completed jobs immediately breaks *arr-style import polling and any automation that reads history;
  retain with a configurable window (Sonarr/Radarr Completed Download Handling).
- Unbounded history is the opposite failure (SABnzbd users hit huge-history problems); prune from day one.
- Persistence-format migrations without a tested backup/migration path (Motrix v2 beta's "don't use your only
  copy" warning).
- Dead alternative transports left in-tree: XDM carries three generations of browser IPC as commented-out code;
  pick one transport and delete the rest.
- Recursive chown of download volumes on container start is brutally slow on NAS libraries; only chown /config
  (linuxserver.io scoping rule).

## Reuse candidates

| Candidate                                              | Purpose for Kurio                                                                    | Maturity                                                           | Verdict                                                                                               |
| ------------------------------------------------------ | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------- |
| System.Threading.RateLimiting (TokenBucketRateLimiter) | Replace hand-rolled token-bucket speed limiter                                       | First-party BCL, powers ASP.NET Core middleware                    | Adopt now                                                                                             |
| RandomAccess / File.OpenHandle (+preallocationSize)    | Concurrent positional segment writes, safe preallocation                             | First-party BCL (.NET 6+)                                          | Adopt now                                                                                             |
| FluentFTP                                              | Entire FTP/FTPS backlog item (LIST parsing, TLS, proxies)                            | ~3.4k stars, very active, zero deps, real-server test suite        | Adopt when FTP work starts                                                                            |
| YoutubeDLSharp                                         | yt-dlp process wrapper (OptionSet, progress parsing, binary bootstrap)               | Maintained, BSD-3, de facto standard .NET binding                  | Adopt for the streaming backlog item                                                                  |
| yt-dlp + ffmpeg (binaries)                             | Streaming-media extraction/muxing                                                    | Extremely active upstream                                          | Adopt as supervised subprocesses with pinned, self-bootstrapped versions (Parabolic pattern)          |
| System.IO.Pipelines                                    | Socket-to-disk hot path if GC pressure shows up                                      | First-party BCL, powers Kestrel/SignalR                            | Watch — profile first; bigger refactor than the wins above                                            |
| bezzad/Downloader                                      | Reference C# segmented engine (DownloadPackage token, offset writes, range fallback) | ~1.7k stars, active, used by v2rayN                                | Watch/study — overlaps Kurio.Core's raison d'etre; mine patterns, don't replace the engine            |
| MonoTorrent                                            | Managed BitTorrent engine if torrents ever enter scope                               | ~1.2k stars, stable 3.0.x, single primary maintainer               | Watch — only mature managed option, but flag bus-factor risk                                          |
| SQLite (Microsoft.Data.Sqlite / EF Core) or LiteDB     | Queryable history/categories store beside engine state files                         | First-party / mature                                               | Watch — adopt for history/analytics if JSON files strain; keep per-download segment state file-based  |
| qBittorrent WebUI API v2 (as a spec)                   | Optional compat shim for Sonarr/Radarr integration                                   | De facto standard, widely cloned (rdt-client precedent)            | Adopt as an additive, optional route prefix                                                           |
| Metalink RFC 5854 / RFC 6249                           | Multi-mirror + per-piece hash downloads                                              | IETF standard; real-world adoption limited to aria2/wget2          | Watch — cheap RFC 6249 header sniffing in the probe step first; .meta4 import later                   |
| aria2 (as external engine)                             | Backlogged "aria2-integration layer" for exotic protocols                            | Mature but maintenance-mode; every wrapper regretted hard coupling | Avoid as a core dependency; at most an optional IDownloadBackend plugin                               |
| Polly (already in use)                                 | Retry/resilience                                                                     | Industry standard                                                  | Keep — but pin retries to the pooled handler and add stall-detection triggers, not just HTTP failures |
