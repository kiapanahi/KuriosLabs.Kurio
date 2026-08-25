# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Kurio is a cross-platform download manager built as a headless engine + API server + web UI. .NET 10, C# `latest`,
~21k LoC, solo project. GitHub remote: `kiapanahi/KuriosLabs.Kurio` (the remote spells "KuriosLabs", the local folder
"KuriousLabs"). Root namespace: `KuriousLabs.Kurio`.

The engine is HTTP(S)-only today. The README's feature list is mostly roadmap/vision (FTP, browser integration,
plugins, scheduling, clipboard capture, import/export, streaming media) — those are tracked as open GitHub issues,
not implemented. Desktop clients (Avalonia GUI, CLI/TUI) were built and deliberately removed in PR #83; the Blazor
web dashboard is the only UI.

## Commands

```bash
dotnet build                                    # builds KuriousLabs.Kurio.slnx
dotnet test                                     # both suites: Core 351 tests (~30s), Server 29 tests
dotnet test test/Kurio.Core.Tests --filter "FullyQualifiedName~SegmentManagerTests"  # one class
dotnet test test/Kurio.Core.Tests --filter "DisplayName~Resume"                      # by name fragment

dotnet run --project src/Kurio.AppHost          # Aspire: server + web + dashboard (preferred for local dev)
dotnet run --project src/Kurio.Server           # REST API + SignalR + SSE only
dotnet run --project src/Kurio.Web              # Blazor dashboard only (expects the server running)
```

Known state (verified 2026-08, after the phase-0 stabilization branch): both suites compile and pass — Core 351
tests including the loopback-HTTP integration tests in `test/Kurio.Core.Tests/Integration/` (they exercise the real
composition root against an in-process server), Server 29 tests. CI builds and tests every push/PR via
`.github/workflows/ci.yml`. The build still emits ~400 analyzer warnings (`latest-Recommended` analysis level in
`Directory.Build.props`), mostly style rules in test files and code not yet swept.

## Architecture

Project references: `AppHost` → `Server` + `Web` (Aspire orchestration); `Server` → `Core` + `Contracts` +
`ServiceDefaults`; `Web` → `Contracts` + `ServiceDefaults` only. The Web UI never references `Core` — it talks to the
server exclusively over HTTP/SignalR using contract DTOs.

- **Kurio.Core** — the engine library. `DownloadEngine` (registered singleton) orchestrates: `ProtocolHandlerFactory`
  picks an `IProtocolHandler` by URI scheme (only `HttpProtocolHandler` exists) → `SegmentManager` splits into byte
  ranges and downloads segments concurrently → `StorageManager` writes per-segment temp files, then assembles →
  `JsonStatePersistence` saves resume state. Supporting subsystems: `Resilience/` (Polly-based
  `ResiliencePolicyFactory`, `ConnectionHealthMonitor`), `ErrorHandling/` (hand-rolled `RetryHandler`/`ErrorClassifier`
  predating the Polly migration — both stacks coexist; `RetryHandler` was slated for removal and never removed),
  `Statistics/` (`ProgressTracker`, `SpeedCalculator`, `EtaCalculator`, JSON history repo), `SpeedLimiter`
  (process-wide, shared across downloads). Composition root: `ServiceCollectionExtensions.AddKurioDownloadEngine()`.
- **Kurio.Contracts** — wire-level DTOs plus typed SignalR client/server interfaces (`Hubs/*Contracts.cs`). Some DTO
  fields are dead surface (proxy fields, `ScheduledAt` hardcoded to `null` in `QueueContractMapper`).
- **Kurio.Server** — attribute-routed controllers under `/api/*` (`Controllers/`), three SignalR hubs
  (`/hubs/downloads`, `/hubs/queue`, `/hubs/stats`) using the typed client interfaces from Contracts, an SSE stream at
  `GET /api/downloads/stream` (minimal-API endpoint in `Program.cs`), and hosted services:
  `DownloadEngineHostedService` (engine lifecycle), `ProgressBroadcaster` + `StatsBroadcaster` (poll the engine, push
  to hub groups). Controllers map Core models → contract DTOs via `Mappers/`; `Server/Models/` request/response types
  partially duplicate Contracts.
- **Kurio.Web** — Blazor Server dashboard. `KurioApiClient` (REST), `HubClientFactory` (SignalR connections),
  `ConnectionStateService`; server address comes from `KurioServerOptions` (validated by
  `KurioServerOptionsValidator`).

### Configuration: three surfaces, mostly disconnected (trap)

1. `appsettings.json` — only the `Kurio:Server:*` CORS keys (`AllowedOrigins`, `CorsPolicy`, `BaseUrl`) are read (in
   `Program.cs`). The entire `Kurio:Storage`, `Kurio:Engine`, `Kurio:Resilience`, `Kurio:Verification`, and
   `Kurio:Server:Authentication` sections are **dead — nothing reads them**.
2. Hardcoded DI defaults in `AddKurioDownloadEngine()` — what the engine actually runs on: `~/.kurio/temp`,
   `~/.kurio/state`, 3 concurrent downloads, `PerSegmentFiles` storage mode.
3. `Kurio.Core.Configuration.ConfigurationService` — a separate custom JSON config system (`config.json` under the
   platform app-data dir via `PlatformPathProvider`, FluentValidation validators in `Configuration/Validators/`).
   Currently only drives the speed limiter (read at startup, updatable at runtime via `PUT /api/config/speed-limit`).

Related trap: authentication is **half-implemented on the wrong side** — `Kurio.Web` attaches `X-Api-Key` headers to
REST/SignalR calls and configures cookie auth, but `Kurio.Server` never validates any of it; all server endpoints are
anonymous. When touching configuration or auth, consolidate toward one surface instead of adding another. Engine state
and history persist under `~/.kurio/` — wipe that directory if manual test runs behave strangely.

## Repo rules (from .github/copilot-instructions.md — treat as law)

- **Minimal APIs only** for ASP.NET Core. `Kurio.Server/Controllers/` violates this; write new endpoints as minimal
  APIs, and prefer migrating controllers when touching them.
- Central package management: package versions only in `Directory.Packages.props`; shared MSBuild props in
  `Directory.Build.props`.
- `System.Threading.Lock` instead of `SemaphoreSlim(1,1)` for mutual exclusion.
- All logging through `LoggerMessageAttribute` source generators — see the `*LogMessages.cs` partial-class convention
  next to each service.
- `.ConfigureAwait(false)` on every await in non-UI code (Core, Server); never in Blazor components.
- **Version bump is mandatory before any PR**: update `Version` in `Directory.Build.props` (semver: breaking/feature/
  fix → major/minor/patch) as its own commit `chore: bump version to X.Y.Z`.
- Git: Conventional Commits, 50/72 rule, feature branches, always a PR (default branch `main`).
- Feature workflow: PRD in `docs/prd/` first → derive GitHub issues → implement with tests. Note: the `docs/` tree was
  deleted in a doc-cleanup wave, and the `.slnx` still references two deleted PRD files.

## Stale-pointer warnings

- README links `docs/`, `CONTRIBUTING.md`, `CHANGELOG.md` — none exist.
- `Directory.Packages.props` still pins Avalonia/Spectre.Console versions for the deleted desktop clients.
- `src/Kurio.Server/TESTING.md` (curl-based manual test guide) predates the queue/stats/config endpoints, and
  `test/TESTING_STATUS.md` documents test drift that has since been repaired — treat both as historical.
- Internal version is 1.18.1 but there are no git tags and no GitHub releases; a local `review` branch holds an
  unmerged `IDownloadTask`→`DownloadTask` refactor from 2025-12-04.
