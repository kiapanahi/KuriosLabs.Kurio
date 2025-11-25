namespace Kurio.Core.Tests.Models;

using Kurio.Core.Models;
using Xunit;

public class DownloadStateFilterTests
{
    [Fact]
    public void Active_ShouldIncludeActiveStates()
    {
        // Arrange
        var filter = DownloadStateFilter.Active;

        // Assert
        Assert.True(filter.HasFlag(DownloadStateFilter.Queued));
        Assert.True(filter.HasFlag(DownloadStateFilter.Analyzing));
        Assert.True(filter.HasFlag(DownloadStateFilter.Downloading));
        Assert.False(filter.HasFlag(DownloadStateFilter.Completed));
        Assert.False(filter.HasFlag(DownloadStateFilter.Failed));
    }

    [Fact]
    public void All_ShouldIncludeAllStates()
    {
        // Arrange
        var filter = DownloadStateFilter.All;

        // Assert
        Assert.True(filter.HasFlag(DownloadStateFilter.Created));
        Assert.True(filter.HasFlag(DownloadStateFilter.Queued));
        Assert.True(filter.HasFlag(DownloadStateFilter.Analyzing));
        Assert.True(filter.HasFlag(DownloadStateFilter.Downloading));
        Assert.True(filter.HasFlag(DownloadStateFilter.Paused));
        Assert.True(filter.HasFlag(DownloadStateFilter.Completed));
        Assert.True(filter.HasFlag(DownloadStateFilter.Failed));
        Assert.True(filter.HasFlag(DownloadStateFilter.Cancelled));
    }

    [Fact]
    public void CombinedFlags_ShouldWorkCorrectly()
    {
        // Arrange
        var filter = DownloadStateFilter.Completed | DownloadStateFilter.Failed;

        // Assert
        Assert.True(filter.HasFlag(DownloadStateFilter.Completed));
        Assert.True(filter.HasFlag(DownloadStateFilter.Failed));
        Assert.False(filter.HasFlag(DownloadStateFilter.Downloading));
    }
}
