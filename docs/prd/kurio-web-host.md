# PRD: Kurio.Web Blazor Server Host (Issue #71)

## Background
The web dashboard needs its own Blazor Server host that consumes Kurio.Server via contracts and SignalR/HTTP, without referencing engine projects. This host should provide a modular shell for future pages (overview, downloads, queue, stats, settings) and handle connectivity, configuration, and auth plumbing.

## Goals
- Stand up `Kurio.Web` as a Blazor Server app targeting net10.0.
- Consume Kurio.Server only through `Kurio.Contracts` and SignalR/HTTP endpoints.
- Provide DI setup for HTTP/SignalR clients, configuration, and connection monitoring.
- Ship a minimal shell (layout, nav placeholder, health status) ready for future pages.
- Remain cross-platform and follow repo rules (central packages, versioning, minimal APIs).

## Non-Goals (for this PR)
- Implement full dashboard pages/UX (covered in #72).
- Finalize auth model (tracked in #73); only stub hooks/config placeholders now.
- CI/test matrix updates (tracked in #74) beyond project-level build verification.

## Functional Requirements
- Project: `src/Kurio.Web/Kurio.Web.csproj` using `Microsoft.NET.Sdk.Web`, `net10.0`, nullable enabled, implicit usings on.
- References: only `Kurio.Contracts` (no `Kurio.Core` or engine references).
- Hosting: Minimal Program.cs (or builder in App) wiring Blazor Server, response compression, and CORS options for dashboard; respects config.
- Configuration: `appsettings.json` with endpoints for Kurio.Server base URL and hubs; support environment overrides.
- DI: Typed `HttpClient` for API commands; `HubConnectionBuilder` factory for SignalR hubs (downloads, queue, stats); retry/backoff defaults.
- Layout: Basic shell (e.g., `MainLayout`, `NavMenu` placeholder) with connection status indicator and loading/error states.
- Error handling: Centralized exception logging; friendly UI message when server unreachable.
- Logging: Use `LoggerMessage` pattern for host-level logs where applicable.
- Cross-platform: No Windows-only APIs; runnable on Linux/macOS/Windows.

## Architecture & Design
- Keep boundary strict: UI consumes only contracts DTOs and hub payloads.
- Encapsulate server connectivity in services (e.g., `KurioServerClientOptions`, `HubClientFactory`).
- Prefer async APIs with `.ConfigureAwait(false)` in non-UI services.
- Prepare for auth: configuration placeholders for bearer/cookie and headers; no hard-coded secrets.
- Use centralized package management via `Directory.Packages.props` for any new dependencies.

## Testing & Acceptance
- Project builds successfully (`dotnet build` on solution or project).
- Blazor app starts with minimal page showing connectivity placeholder (manual smoke acceptable for this PR).
- No references to engine projects; only contracts + framework packages.
- Configurable server URL via appsettings/environment.

## Open Questions
- Final auth mechanism (cookie vs bearer) to be decided in #73.
- Whether to host UI and API under same origin/path; impacts CORS defaults (tracked in #73).

## Deliverables
- New `Kurio.Web` project scaffold with DI, config, layouts, and connection services.
- Updated solution file and docs linking PRD to #71 and epic #75.
