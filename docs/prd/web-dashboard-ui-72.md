# PRD: Web Dashboard UI (Issue #72)

## Background
The Kurio Web host (issue #71) exists with navigation and connection plumbing but no functional dashboard UI. Issue #72 delivers the first usable dashboard: live downloads list, queue management, stats, and basic settings using the existing HTTP endpoints and SignalR hubs exposed by Kurio.Server.

## Goals
- Ship interactive pages for Overview, Downloads, Queue, Stats, and Settings in `Kurio.Web`.
- Use SignalR hubs (`downloads`, `queue`, `stats`) for live data; use HTTP endpoints for commands and initial load where needed.
- Support core actions: add/pause/resume/start/cancel downloads, change priority, clear completed, move queue items, update speed limits.
- Show connection/reconnect state and friendly empty/loading/error states.
- Keep UI strictly on `Kurio.Contracts`; no references to engine projects.

## Non-Goals
- Authentication/authorization (tracked in #73).
- Advanced scheduling, rules, or plugin management.
- Historical charts beyond session-level stats.

## Functional Requirements
- **Overview:** Surface key stats (active/queued/completed/failed, throughput now/avg, total bytes), recent alerts placeholder, quick links.
- **Downloads:** Live list with status, speed, ETA, progress bar, priority, created time, actions (start/pause/resume/cancel, change priority, clear completed). Support filtering by state.
- **Queue:** Show queue positions with move up/down/top/bottom and priority changes; live snapshot from queue hub plus HTTP commands.
- **Stats:** Live stats snapshot and periodic updates from stats hub (counts, throughput, totals, uptime).
- **Settings:** Read/update speed limit via `api/config/speed-limit`; show current effective limit.
- **Connection UX:** Reconnect indicator, manual retry, informative errors when hubs/API unavailable.
- **Contracts only:** UI consumes `KuriousLabs.Kurio.Contracts` DTOs and server HTTP responses; no engine references.

## Architecture Notes
- Reuse `HubClientFactory` for all hubs; per-page components manage connection lifecycle and request snapshots on connect.
- `KurioApiClient` provides HTTP commands for downloads (`/api/downloads/*`), queue (`/api/queue/*`), stats (`/api/stats`), and config (`/api/config/speed-limit`).
- Components maintain local state dictionaries keyed by download id for fast updates; apply hub deltas and refresh snapshots on reconnect.
- Prefer async flows; show optimistic UI where reasonable and reconcile with hub updates.

## Acceptance Criteria
- All pages render meaningful data from live hubs/HTTP when Kurio.Server is running.
- Actions (add/pause/resume/start/cancel, change priority, move queue, clear completed, update speed limit) call the correct endpoints and reflect in UI via hub updates or snapshots.
- Connection indicator reflects Connected/Reconnecting/Disconnected states with retry control.
- No references to `Kurio.Core` or engine projects; only `Kurio.Contracts` + framework packages.
- Styles updated for tables, cards, badges, and responsive layout.

## Dependencies
- Kurio.Server hubs and controllers already published in the epic branch (#69/#70/#71).
- Auth/CORS decisions pending in #73 (UI prepared for future headers/token wiring).

## Risks
- Hub method name mismatches could silently fail; ensure handlers match server method names.
- Large download lists may need virtualization; current scope targets moderate counts.
- Settings surface limited to speed limit until broader config endpoints are available.
