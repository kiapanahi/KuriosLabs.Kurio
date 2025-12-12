# Kurio Web Dashboard PRD

## Purpose
Deliver a modular, SignalR-first web dashboard that lets users monitor and control Kurio without coupling UI code to the download engine. The dashboard must be deployable alongside `Kurio.Server` but remain cleanly separated through shared contracts and well-defined hubs/HTTP endpoints.

## Goals
- Provide live visibility into downloads, queue, throughput, errors, and health.
- Allow core actions: add, pause/resume, retry, remove downloads; manage queue order; clear completed items; adjust basic settings (categories, proxy, concurrency).
- Favor realtime SignalR for streaming updates; use HTTP for idempotent commands and initial snapshots.
- Keep UI modular: UI only consumes contracts; no engine references; boundary enforced via `Kurio.Contracts`.
- Operate cross-platform (Windows/macOS/Linux) and support same-origin or reverse-proxy hosting.

## Non-Goals (for this iteration)
- Full authentication/authorization redesign (basic auth wiring only, detailed policies later).
- Advanced scheduling, rules, or plugin management.
- Browser extension integration (separate track).
- Offline-first caching of large data sets; focus on connected experience with graceful reconnect.

## Target Users & Use Cases
- Power users running Kurio headless on a NAS/server who need a browser dashboard.
- Desktop users who prefer web UI over TUI for managing multiple downloads.
- Operators monitoring throughput, errors, and queue health in real time.

## Success Metrics
- Downloads list renders within 1.5s on initial load (50 items baseline).
- Realtime updates visible within 500ms of server event under normal load.
- Reconnect after transient network loss within 5s with state resynced.
- No UI/engine coupling: UI builds with only contracts + HTTP/SignalR clients.

## Scope & Features
- **Overview:** Key stats (active/queued/completed counts, throughput now/avg, failure count, recent alerts), server health indicator.
- **Downloads list:** Filter/sort, per-item progress, speed, ETA, size, status, actions (pause/resume/retry/remove), bulk clear completed.
- **Queue:** Position, priority changes (move up/down/top/bottom), scheduled items if present.
- **Add download:** URL entry, optional category/destination, connection count, checksum (if provided), proxy toggle.
- **Stats:** Throughput history (short window), aggregate totals, per-protocol counts if available.
- **Logs/alerts:** Recent failures/retries with timestamps and messages; link to affected download.
- **Settings (MVP):** Category defaults, proxy on/off + endpoint, concurrency/segment defaults; display-only advanced settings is acceptable.
- **Cross-cutting UX:**
  - Connection state indicator (online/reconnecting/offline) with backoff.
  - Empty/loading/error states for each page.
  - Responsive layout (desktop first, usable on tablets/phones).
  - Basic a11y: keyboard nav, focus order, aria labels on key controls.

## Architecture & Communication
- **Topology:** `Kurio.Web` (Blazor Server) talks to `Kurio.Server` via SignalR + HTTP. Contracts live in `Kurio.Contracts` referenced by both. Reverse proxy can host under one domain/path; dev can run cross-origin with CORS enabled.
- **Realtime (SignalR hubs):**
  - `DownloadsHub`: 
    - Server->client: `DownloadSnapshot` (initial batch), `DownloadUpdated`, `DownloadRemoved`, `DownloadCompleted`, `DownloadFailed`, `BulkCleared`.
    - Client->server: `SubscribeDownloads(filters)`, `UnsubscribeDownloads`, optional `RequestSnapshot`.
  - `QueueHub`:
    - Server->client: `QueueSnapshot`, `QueuePositionChanged`, `QueueItemAdded/Removed`.
    - Client->server: `SubscribeQueue`, `UnsubscribeQueue`.
  - `StatsHub`:
    - Server->client: `StatsSnapshot`, `StatsUpdated` (throughput/aggregate), optional `AlertRaised`.
    - Client->server: `SubscribeStats`, `UnsubscribeStats`.
  - All hubs use contracts DTOs; payloads sized for UI (no engine objects). Support resumable subscriptions and replay of latest snapshot on reconnect.
- **HTTP (minimal APIs):**
  - Commands: `POST /downloads` (add), `POST /downloads/{id}/pause`, `.../resume`, `.../retry`, `DELETE /downloads/{id}?removeFiles=bool`, `POST /downloads/{id}/priority`, `POST /downloads/pause-all`, `POST /downloads/clear-completed`.
  - Reads/snapshots: `GET /downloads?filter=...`, `GET /downloads/{id}`, `GET /queue`, `GET /stats`, `GET /settings`.
  - Settings: `POST /settings` for basic dashboard-exposed fields (categories, proxy, concurrency defaults).
  - Health: `GET /health` for UI connectivity checks.
- **State model:** Initial page load fetches HTTP snapshot(s), then subscribes to hubs for live deltas. On reconnect, request fresh snapshots before applying live updates to avoid drift.
- **Error handling:** Graceful degradation to HTTP polling is out-of-scope; UI should surface reconnect/backoff and allow manual retry.

## Data Contracts (initial set)
Defined in `Kurio.Contracts` and shared by server + UI:
- `DownloadSummary`: id, name, url host, category, size, downloaded bytes, percent, status, speed, eta, connections, createdAt, lastUpdated, checksum present?, errors count.
- `DownloadProgressUpdate`: id, downloaded bytes, speed, eta, percent, active connections, timestamp.
- `DownloadStatusChange`: id, oldStatus, newStatus, reason?, message?, finishedAt?
- `QueueItem`: downloadId, position, priority, scheduledAt?, addedAt.
- `StatsSnapshot`: active/queued/completed/failed counts, current throughput, avg throughput (short window), total bytes downloaded, uptime.
- `Alert`: severity, message, downloadId?, occurredAt.
- `SettingsSummary`: category defaults, proxy enabled + endpoint, max concurrent downloads, default connections per download, default segment size if exposed.
- `AddDownloadCommand`, `Pause/Resume/Retry/Remove/ChangePriority` commands; `SettingsUpdateCommand`.

## UX & Interaction Notes
- Show per-item actions inline; confirm destructive remove.
- Use optimistic UI for pause/resume/retry; reconcile with server responses.
- Virtualize lists for performance beyond ~200 items.
- Preserve filters/sorts across reconnect when possible.

## Auth, CORS, Deployment
- Prefer same-origin hosting (reverse proxy `Kurio.Web` under `Kurio.Server` domain/path). For dev cross-origin, enable CORS for HTTPS localhost origins; allow credentials.
- Auth model: cookie or bearer per server decision; Blazor Server auth plumbing should integrate later—MVP can assume trusted local dev if auth not yet in place.
- Document reverse-proxy examples (YARP/Nginx) and environment variables for base addresses.

## Telemetry & Logging
- Instrument SignalR connection lifecycle (connect/reconnect/disconnect, failures) and key commands success/failure (add/pause/resume/remove).
- Surface errors in UI with actionable messages; log with correlation ids where possible.
- Basic client-side perf timing for initial load and hub connect is desirable (manual note acceptable if not automated).

## Testing Expectations
- Unit: DTO validation/serialization, small mapping helpers.
- Integration: hubs (subscribe, receive updates), HTTP commands happy-path + basic errors.
- UI smoke (bUnit/Playwright): initial load shows snapshot; actions (add/pause/resume/remove) update UI; reconnect indicator toggles when server offline.

## Dependencies & Sequencing
1) #69 Contracts library (DTOs/hub signatures).
2) #70 Server hubs + HTTP surfaces using contracts.
3) #71 Blazor host shell that consumes those surfaces.
4) #72 Dashboard pages wired to SignalR/HTTP.
5) #73 Auth/CORS/deployment story finalized.
6) #74 Tests/CI wired for new projects.

## Risks & Open Questions
- Auth model not finalized; may impact hosting and hub negotiation.
- Large download lists: may need pagination or server-side filtering; current scope assumes modest counts with virtualization.
- Throughput history source: confirm if engine provides time-series or if server must aggregate.
- Logs/alerts feed source: clarify existing logging surface vs new stream.

## Acceptance Criteria (for issue #68)
- This PRD lives at `docs/prd/web-dashboard.md` with the sections above.
- Documents pages, communication model (SignalR + HTTP), data contracts needed, auth/deployment notes, testing expectations, dependencies, and open questions.
- Reviewed and approved for the dashboard epic.
