using System.Net;
using System.Net.Sockets;

using Kurio.Core.ErrorHandling;
using Kurio.Core.Models;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace Kurio.Core.Tests.ErrorHandling;

public class ErrorClassifierTests
{
    private readonly ErrorClassifier _classifier;

    public ErrorClassifierTests()
    {
        _classifier = new ErrorClassifier(NullLogger<ErrorClassifier>.Instance);
    }

    [Fact]
    public void Classify_SocketException_ReturnsNetworkCategory()
    {
        // Arrange
        var exception = new SocketException();

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.Network, error.Category);
        Assert.True(error.IsRecoverable);
        Assert.Equal(ErrorRecoveryAction.Retry, error.RecoveryAction);
    }

    [Fact]
    public void Classify_TimeoutException_ReturnsNetworkCategory()
    {
        // Arrange
        var exception = new TimeoutException("Request timed out");

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.Network, error.Category);
        Assert.True(error.IsRecoverable);
    }

    [Fact]
    public void Classify_HttpRequestException404_ReturnsResourceNotFound()
    {
        // Arrange
        var exception = new HttpRequestException("Not found", null, HttpStatusCode.NotFound);

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.ResourceNotFound, error.Category);
        Assert.Equal(404, error.HttpStatusCode);
        Assert.Equal(ErrorRecoveryAction.Fail, error.RecoveryAction);
    }

    [Fact]
    public void Classify_HttpRequestException401_ReturnsAuthenticationCategory()
    {
        // Arrange
        var exception = new HttpRequestException("Unauthorized", null, HttpStatusCode.Unauthorized);

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.Authentication, error.Category);
        Assert.Equal(401, error.HttpStatusCode);
        Assert.Equal(ErrorRecoveryAction.Pause, error.RecoveryAction);
    }

    [Fact]
    public void Classify_HttpRequestException429_ReturnsRateLimiting()
    {
        // Arrange
        var exception = new HttpRequestException("Too many requests", null, HttpStatusCode.TooManyRequests);

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.RateLimiting, error.Category);
        Assert.Equal(429, error.HttpStatusCode);
        Assert.Equal(ErrorRecoveryAction.Retry, error.RecoveryAction);
        Assert.NotNull(error.RetryAfter);
    }

    [Fact]
    public void Classify_HttpRequestException500_ReturnsHttpCategory()
    {
        // Arrange
        var exception = new HttpRequestException("Internal server error", null, HttpStatusCode.InternalServerError);

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.Http, error.Category);
        Assert.Equal(500, error.HttpStatusCode);
        Assert.True(error.IsRecoverable);
        Assert.Equal(ErrorRecoveryAction.Retry, error.RecoveryAction);
    }

    [Fact]
    public void Classify_IOException_ReturnsDiskIoCategory()
    {
        // Arrange
        var exception = new IOException("Disk full");

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.DiskIo, error.Category);
        Assert.Equal(ErrorRecoveryAction.Pause, error.RecoveryAction);
    }

    [Fact]
    public void Classify_UnauthorizedAccessException_ReturnsAuthenticationCategory()
    {
        // Arrange
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.Authentication, error.Category);
    }

    [Fact]
    public void Classify_UnknownException_ReturnsUnknownCategory()
    {
        // Arrange
        var exception = new InvalidCastException("Invalid cast");

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal(DownloadErrorCategory.Unknown, error.Category);
        Assert.Equal(ErrorRecoveryAction.Fail, error.RecoveryAction);
    }

    [Fact]
    public void IsTransient_NetworkError_ReturnsTrue()
    {
        // Arrange
        var error = new DownloadError
        {
            Message = "Network error",
            Category = DownloadErrorCategory.Network
        };

        // Act
        var isTransient = _classifier.IsTransient(error);

        // Assert
        Assert.True(isTransient);
    }

    [Fact]
    public void IsTransient_RateLimitingError_ReturnsTrue()
    {
        // Arrange
        var error = new DownloadError
        {
            Message = "Rate limited",
            Category = DownloadErrorCategory.RateLimiting
        };

        // Act
        var isTransient = _classifier.IsTransient(error);

        // Assert
        Assert.True(isTransient);
    }

    [Fact]
    public void IsTransient_ServerError_ReturnsTrue()
    {
        // Arrange
        var error = new DownloadError
        {
            Message = "Server error",
            Category = DownloadErrorCategory.Http,
            HttpStatusCode = 503
        };

        // Act
        var isTransient = _classifier.IsTransient(error);

        // Assert
        Assert.True(isTransient);
    }

    [Fact]
    public void IsTransient_ResourceNotFound_ReturnsFalse()
    {
        // Arrange
        var error = new DownloadError
        {
            Message = "Not found",
            Category = DownloadErrorCategory.ResourceNotFound
        };

        // Act
        var isTransient = _classifier.IsTransient(error);

        // Assert
        Assert.False(isTransient);
    }

    [Fact]
    public void GetRecoveryAction_RangeNotSatisfiable_ReturnsFallback()
    {
        // Arrange
        var error = new DownloadError
        {
            Message = "Range not satisfiable",
            Category = DownloadErrorCategory.Http,
            HttpStatusCode = 416
        };

        // Act
        var action = _classifier.GetRecoveryAction(error);

        // Assert
        Assert.Equal(ErrorRecoveryAction.FallbackToSingleThread, action);
    }

    [Fact]
    public void Classify_IncludesUserFriendlyMessage()
    {
        // Arrange
        var exception = new SocketException();

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.NotNull(error.UserFriendlyMessage);
        Assert.Contains("Network", error.UserFriendlyMessage);
    }

    [Fact]
    public void Classify_IncludesExceptionDetails()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var error = _classifier.Classify(exception);

        // Assert
        Assert.Equal("Test exception", error.Message);
        Assert.NotNull(error.ExceptionType);
        Assert.Contains("InvalidOperationException", error.ExceptionType);
    }
}
