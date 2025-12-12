# Testing Dashboard (Issues #73 and #74)

This document tracks progress on testing coverage for the dashboard surface as part of issue #74.

## Completed

### Authentication and CORS Configuration (#73) ✅
- [x] Authentication infrastructure added to Kurio.Web and Kurio.Server
- [x] CORS configuration enhanced with multiple policies
- [x] Deployment documentation created (reverse-proxy, Docker, systemd)
- [x] API key authentication support implemented
- [x] Cookie-based authentication for dashboard UI

### Integration Tests ✅
- [x] Existing QueueAndStatsTests covers SignalR hub HTTP snapshots
- [x] Integration tests verify queue management and stats endpoints
- [x] ServerTestFactory provides test infrastructure for dashboard testing

## Remaining Work

### Unit Tests for SignalR Hubs
SignalR hub unit tests were started but require API refinement:

**Challenges:**
- DownloadHub.ReceiveSnapshotAsync expects `IReadOnlyList<DownloadSummary>`, not `List<DownloadInfo>`
- IDownloadQueueManager interface doesn't expose `GetActiveCount()` and `GetQueuedCount()` methods
- Core model differences between test mocks and actual implementations

**Next Steps:**
1. Review and align hub client interfaces (IDownloadsClient, IQueueClient, IStatsClient)
2. Verify DTO types match between contracts and hub implementations
3. Complete unit tests with proper mocking of dependencies

### Component Tests for Kurio.Web (bUnit)
**Not Started** - Requires:
1. Create Kurio.Web.Tests project
2. Add bUnit and related packages
3. Write component tests for:
   - Overview page (dashboard summary)
   - Downloads list (with filtering/sorting)
   - Queue view (with drag/drop priority)
   - Add download form (validation)
   - Stats view (charts and metrics)
   - Connection state indicators

**Sample Test Structure:**
```csharp
public class OverviewPageTests : TestContext
{
    [Fact]
    public void OverviewPage_DisplaysStats_WhenConnected()
    {
        // Arrange
        var mockApiClient = new Mock<KurioApiClient>();
        Services.AddSingleton(mockApiClient.Object);
        
        // Act
        var cut = RenderComponent<Overview>();
        
        // Assert
        cut.Find("h1").TextContent.Should().Be("Dashboard Overview");
    }
}
```

### CI Configuration
**Not Started** - Requires:
1. Add test execution to GitHub Actions workflow
2. Configure test result reporting
3. Add code coverage collection
4. Set quality gates for pull requests

## Test Coverage Summary

| Component | Unit Tests | Integration Tests | Component Tests |
|-----------|------------|-------------------|-----------------|
| DownloadHub | ⏸️ Pending | ✅ Existing | N/A |
| QueueHub | ⏸️ Pending | ✅ Existing | N/A |
| StatsHub | ⏸️ Pending | ✅ Existing | N/A |
| Queue Controller | N/A | ✅ Existing | N/A |
| Stats Controller | N/A | ✅ Existing | N/A |
| Overview Page | N/A | N/A | ⏸️ Not Started |
| Downloads Page | N/A | N/A | ⏸️ Not Started |
| Queue Page | N/A | N/A | ⏸️ Not Started |

## Running Existing Tests

```bash
# Run all tests
dotnet test

# Run server tests only
dotnet test test/Kurio.Server.Tests/Kurio.Server.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Test Infrastructure

### ServerTestFactory
The existing `ServerTestFactory` provides:
- Mock IDownloadEngine
- Mock IDownloadQueueManager
- Mock IStatisticsService
- WebApplicationFactory<Program> for integration testing

### FakeDownloadTask
Implements `IDownloadTask` for test scenarios with configurable:
- State, Priority, Progress
- URLs, FileNames, Options
- Metadata and error tracking

## References

- [bUnit Documentation](https://bunit.dev/)
- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
