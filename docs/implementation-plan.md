# Kurio Implementation Plan

**Created:** November 26, 2025  
**Status:** Ready for Implementation  

---

## Overview

This document outlines the complete implementation plan for transforming Kurio into a robust, client-server architecture download manager. The plan is divided into three phases, addressing critical bugs, modernizing APIs, and building a web service architecture.

---

## 📋 Phase 1: Foundation Improvements (Reliability)

**Goal:** Fix core issues and modernize resilience patterns  
**Timeline:** 4-7 weeks  
**Priority:** Critical - Must complete before moving to Phase 2/3

### Issue #43: Migrate to Polly for Resilience and Retry Policies
- **URL:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/43
- **Type:** Refactoring
- **Priority:** High
- **Effort:** Medium (1-2 weeks)
- **Description:** Replace custom `RetryHandler` with Polly, an industry-standard resilience library already referenced in ServiceDefaults
- **Benefits:**
  - Battle-tested resilience patterns
  - Better telemetry and metrics integration
  - Sophisticated policy composition
  - Active community support

### Issue #44: Fix Concurrent Write Issues in Multi-Segment Downloads
- **URL:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/44
- **Type:** Bug Fix
- **Priority:** Critical
- **Effort:** High (2-3 weeks)
- **Description:** Eliminate file corruption during pause/resume cycles by implementing proper write synchronization
- **Solutions:**
  - **Mode 1:** Enhanced single-file approach with proper locking
  - **Mode 2:** Per-segment files with merge at completion (configurable)
- **Impact:** Resolves data corruption issues reported after pause/resume

### Issue #45: Add Segment-Level Checksum Verification
- **URL:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/45
- **Type:** Feature
- **Priority:** High
- **Effort:** Medium (1-2 weeks)
- **Description:** Add SHA256 checksums for each segment to ensure data integrity
- **Features:**
  - Compute checksum during download
  - Verify after writing to disk
  - Verify during resume
  - Detect corrupted segments early

---

## 🚀 Phase 2: Modernization (API Evolution)

**Goal:** Modernize progress streaming for web service architecture  
**Timeline:** 1-2 weeks  
**Priority:** High - Required before Phase 3

### Issue #46: Migrate from IObservable to IAsyncEnumerable
- **URL:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/46
- **Type:** Refactoring (Breaking Change)
- **Priority:** High
- **Effort:** Medium (1-2 weeks)
- **Description:** Replace `IObservable<DownloadProgress>` (System.Reactive) with `IAsyncEnumerable<DownloadProgress>`
- **Benefits:**
  - Native C# async/await support
  - Built-in backpressure handling
  - Perfect for HTTP streaming (SSE/SignalR)
  - Remove System.Reactive dependency
  - More familiar to developers
- **Blocks:** Issue #47 (Kurio.Server)

---

## 🌐 Phase 3: Web Service Architecture (Client-Server Model)

**Goal:** Transform Kurio into a client-server architecture for remote management  
**Timeline:** 4-6 weeks  
**Priority:** High

**PRD:** [docs/prd/web-service-architecture.md](./prd/web-service-architecture.md)

### Issue #47: Create Kurio.Server ASP.NET Core Web Service
- **URL:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/47
- **Type:** Feature
- **Priority:** High
- **Effort:** Very High (3-4 weeks)
- **Description:** Create ASP.NET Core web service that hosts the download engine
- **Features:**
  - REST API for all download operations
  - SignalR hub for real-time updates
  - Server-sent events (SSE) for simple clients
  - Background service hosting download engine
  - OpenAPI/Swagger documentation
  - Health checks and monitoring
- **Depends on:** Issue #46

### Issue #48: Update CLI to Use API Client
- **URL:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/48
- **Type:** Refactoring
- **Priority:** High
- **Effort:** High (1-2 weeks)
- **Description:** Convert `Kurio.Cli` from hosting engine in-process to connecting to Kurio.Server
- **Features:**
  - HTTP-based API client
  - SignalR connection for real-time updates
  - Connection state management
  - Automatic reconnection
  - Support local and remote servers
- **Depends on:** Issue #47

---

## Architecture Evolution

### Current Architecture (In-Process)
```
┌──────────────┐
│  Kurio.Cli   │
│  ┌────────┐  │
│  │  TUI   │  │
│  └───┬────┘  │
│      │       │
│  ┌───▼─────┐ │
│  │ Engine  │ │
│  └─────────┘ │
└──────────────┘
```

### Future Architecture (Client-Server)
```
┌──────────────┐         ┌──────────────┐
│  Kurio.Cli   │         │Kurio.Server  │
│  ┌────────┐  │         │  ┌────────┐  │
│  │  TUI   │  │         │  │  API   │  │
│  └───┬────┘  │         │  └───┬────┘  │
│      │       │  HTTP   │      │       │
│  ┌───▼─────┐ │────────▶│  ┌───▼─────┐ │
│  │API Client│◀────────│  │ Engine  │ │
│  └─────────┘ │ SignalR │  └─────────┘ │
└──────────────┘         └──────────────┘
              ↑              ↑              ↑
              │              │              │
    Multiple clients    can connect   to same server
```

---

## Implementation Timeline

### Week 1-2: Polly Migration (#43)
- Add Polly packages
- Create resilience policy factory
- Update SegmentManager
- Remove RetryHandler
- Write tests

### Week 3-5: Concurrent Write Fixes (#44)
- Implement write locking
- Add per-segment file mode
- Implement merge logic
- Comprehensive testing
- Performance benchmarking

### Week 6-7: Checksum Verification (#45)
- Create SegmentVerifier
- Integrate with SegmentManager
- Add resume verification
- Testing and validation

### Week 8-9: IAsyncEnumerable Migration (#46)
- Update interface
- Implement Channel-based streaming
- Update all consumers
- Remove System.Reactive
- Testing

### Week 10-13: Kurio.Server (#47)
- Create ASP.NET Core project
- Implement REST API
- Implement SignalR hub
- Background services
- Documentation
- Testing

### Week 14-15: CLI Client (#48)
- Create API client
- Update UI views
- Connection management
- Testing with real server

---

## Success Criteria

### Phase 1 Success
- [ ] No file corruption in any scenario
- [ ] Polly policies working correctly
- [ ] Segment checksums detecting corruption
- [ ] All tests passing
- [ ] Performance acceptable

### Phase 2 Success
- [ ] IAsyncEnumerable streaming working
- [ ] System.Reactive removed
- [ ] No performance regression
- [ ] All clients updated

### Phase 3 Success
- [ ] Server running as standalone service
- [ ] REST API fully functional
- [ ] Real-time updates working (SignalR/SSE)
- [ ] TUI connecting to server successfully
- [ ] Multiple clients can connect
- [ ] < 100ms API response time (p95)
- [ ] Support 100+ concurrent downloads

---

## Testing Strategy

### Unit Tests
- All core business logic
- Policy configurations
- Checksum calculations
- Storage operations
- API client operations

### Integration Tests
- Full download workflows
- Pause/resume functionality
- API endpoints
- SignalR connections
- SSE streaming

### Performance Tests
- Load testing (100+ downloads)
- Stress testing (large files)
- Endurance testing (24+ hours)
- Memory leak detection

### End-to-End Tests
- TUI connecting to server
- Multiple clients simultaneously
- Network failure scenarios
- Server restart handling

---

## Risk Management

### High Risks

**File Corruption (Issue #44)**
- **Mitigation:** Comprehensive testing with simulated failures
- **Fallback:** Per-segment file mode as safe alternative

**Breaking Changes (Issue #46)**
- **Mitigation:** Clear migration guide, thorough testing
- **Impact:** Only internal APIs, no user-facing changes

**Web Service Complexity (Issues #47, #48)**
- **Mitigation:** Incremental implementation, extensive testing
- **Fallback:** Keep in-process mode as fallback option

---

## Documentation Requirements

- [ ] PRD for web service architecture ✅ (Created)
- [ ] API documentation (OpenAPI/Swagger)
- [ ] Architecture decision records (ADRs)
- [ ] Deployment guide
- [ ] Configuration guide
- [ ] Client SDK documentation
- [ ] Migration guide (IObservable → IAsyncEnumerable)
- [ ] Troubleshooting guide

---

## Dependencies

### External Dependencies
- Polly (v8.5.0+)
- ASP.NET Core 10.0
- SignalR Client
- System.Threading.Channels (built-in)

### Internal Dependencies
```
#43 ──┐
#44 ──┤
#45 ──┴──▶ #46 ──▶ #47 ──▶ #48
```

- Phase 1 issues (#43, #44, #45) can be worked on in parallel
- Phase 2 (#46) should complete before starting Phase 3
- Phase 3 server (#47) must complete before client (#48)

---

## Next Steps

1. **Review and approve** this implementation plan
2. **Prioritize** Phase 1 issues for immediate work
3. **Assign** issues to GitHub Copilot Coding Agent or team members
4. **Create branches** for each major issue
5. **Start implementation** following the recommended order

---

## Resources

- **Epic Issue:** https://github.com/kiapanahi/KuriosLabs.Kurio/issues/3
- **PRD:** [docs/prd/web-service-architecture.md](./prd/web-service-architecture.md)
- **Polly Documentation:** https://www.pollydocs.org/
- **ASP.NET Core SignalR:** https://docs.microsoft.com/en-us/aspnet/core/signalr/
- **IAsyncEnumerable:** https://docs.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-8#asynchronous-streams

---

_This document will be updated as implementation progresses._
