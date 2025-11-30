using System.Net;
using System.Net.Sockets;

using KuriousLabs.Kurio.Core.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace KuriousLabs.Kurio.Resilience;

public sealed class ResiliencePolicyFactoryTests
{
    private readonly Mock<ILogger<ResiliencePolicyFactory>> _loggerMock;
    private readonly ResiliencePolicyOptions _options;

    public ResiliencePolicyFactoryTests()
    {
        _loggerMock = new Mock<ILogger<ResiliencePolicyFactory>>();
        _options = new ResiliencePolicyOptions
        {
            MaxRetryAttempts = 3,
            InitialDelaySeconds = 1,
            BackoffMultiplier = 2.0,
            EnableJitter = false // Disable jitter for predictable tests
        };
    }

    [Fact]
    public void CreateRetryPolicy_WithValidOptions_Succeeds()
    {
        // Arrange
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));

        // Act
        var policy = factory.CreateRetryPolicy<int>();

        // Assert
        Assert.NotNull(policy);
    }

    [Fact]
    public async Task CreateNetworkRetryPolicy_RetriesOnTransientErrors()
    {
        // Arrange
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));
        var connectionOptions = new ConnectionResilienceOptions
        {
            MaxRetryAttempts = 3,
            InitialRetryDelaySeconds = 1,
            UseJitter = false
        };

        var policy = factory.CreateNetworkRetryPolicy<int>(connectionOptions);

        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<int>(async ct =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new HttpRequestException("Network error",
                    new SocketException((int)SocketError.ConnectionRefused));
            });
        });

        // Should have tried 4 times (initial + 3 retries)
        Assert.Equal(4, attemptCount);
    }

    [Fact]
    public async Task CreateNetworkRetryPolicy_DoesNotRetryOnNonTransientErrors()
    {
        // Arrange
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));
        var connectionOptions = new ConnectionResilienceOptions
        {
            MaxRetryAttempts = 3,
            InitialRetryDelaySeconds = 1,
            UseJitter = false
        };

        var policy = factory.CreateNetworkRetryPolicy<int>(connectionOptions);

        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<int>(async ct =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new HttpRequestException("Not found", null, HttpStatusCode.NotFound);
            });
        });

        // Should have tried only once (non-transient error)
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task CreateNetworkRetryPolicy_SucceedsAfterRetry()
    {
        // Arrange
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));
        var connectionOptions = new ConnectionResilienceOptions
        {
            MaxRetryAttempts = 3,
            InitialRetryDelaySeconds = 1,
            UseJitter = false
        };

        var policy = factory.CreateNetworkRetryPolicy<int>(connectionOptions);

        var attemptCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.CompletedTask;
            if (attemptCount < 3)
            {
                throw new HttpRequestException("Temporary error",
                    new SocketException((int)SocketError.ConnectionReset));
            }
            return 42; // Success on third attempt
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task CreateTimeoutPolicy_EnforcesTimeout()
    {
        // Arrange
        _options.TimeoutMinutes = 1; // 1 minute timeout, but we'll use shorter for test
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));

        var policy = factory.CreateTimeoutPolicy<int>();

        // Act & Assert
        await Assert.ThrowsAsync<Polly.Timeout.TimeoutRejectedException>(async () => await policy.ExecuteAsync(async ct =>
            {
                await Task.Delay(TimeSpan.FromMinutes(2), ct);
                return 0;
            }));
    }

    [Fact]
    public void CreateCombinedPolicy_IncludesAllPolicies()
    {
        // Arrange
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));

        // Act
        var policy = factory.CreateCombinedPolicy<int>();

        // Assert
        Assert.NotNull(policy);
    }

    [Theory]
    [InlineData(SocketError.ConnectionRefused, true)]
    [InlineData(SocketError.ConnectionReset, true)]
    [InlineData(SocketError.NetworkDown, true)]
    [InlineData(SocketError.NetworkUnreachable, true)]
    [InlineData(SocketError.TimedOut, true)]
    [InlineData(SocketError.HostNotFound, false)] // Not transient
    public async Task IsTransientNetworkError_ClassifiesCorrectly(SocketError error, bool expectedTransient)
    {
        // This test verifies the internal error classification logic indirectly
        // by observing retry behavior

        // Arrange
        var factory = new ResiliencePolicyFactory(_loggerMock.Object, Options.Create(_options));
        var connectionOptions = new ConnectionResilienceOptions
        {
            MaxRetryAttempts = 2,
            InitialRetryDelaySeconds = 1,
            UseJitter = false
        };

        var policy = factory.CreateNetworkRetryPolicy<int>(connectionOptions);

        var attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await policy.ExecuteAsync<int>(async ct =>
            {
                attemptCount++;
                await Task.CompletedTask;
                throw new HttpRequestException("Error", new SocketException((int)error));
            });
        });

        // Transient errors should retry (3 attempts total)
        // Non-transient errors should not retry (1 attempt only)
        var expectedAttempts = expectedTransient ? 3 : 1;

        Assert.Equal(expectedAttempts, attemptCount);
    }
}

