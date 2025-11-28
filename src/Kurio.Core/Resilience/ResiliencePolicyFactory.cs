using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Kurio.Core.Resilience;

/// <summary>
///     Factory for creating Polly resilience policies for download operations.
/// </summary>
public sealed class ResiliencePolicyFactory
{
    private readonly ILogger<ResiliencePolicyFactory> _logger;
    private readonly ResiliencePolicyOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ResiliencePolicyFactory" /> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The resilience policy options.</param>
    public ResiliencePolicyFactory(
        ILogger<ResiliencePolicyFactory> logger,
        IOptions<ResiliencePolicyOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    ///     Creates a retry policy with exponential backoff and jitter.
    /// </summary>
    /// <returns>An async retry policy.</returns>
    public ResiliencePipeline<TResult> CreateRetryPolicy<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(_options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = _options.EnableJitter,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s due to {Exception}",
                        args.AttemptNumber + 1,
                        _options.MaxRetryAttempts,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "unknown");

                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<IOException>()
            })
            .Build();
    }

    /// <summary>
    ///     Creates a network-specific retry policy with enhanced error detection and adaptive backoff.
    /// </summary>
    /// <param name="connectionResilienceOptions">Connection resilience options.</param>
    /// <returns>A resilience pipeline for network operations.</returns>
    public ResiliencePipeline<TResult> CreateNetworkRetryPolicy<TResult>(
        ConnectionResilienceOptions connectionResilienceOptions)
    {
        var maxDelay = TimeSpan.FromSeconds(connectionResilienceOptions.MaxRetryDelaySeconds);

        return new ResiliencePipelineBuilder<TResult>()
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = connectionResilienceOptions.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(connectionResilienceOptions.InitialRetryDelaySeconds),
                MaxDelay = maxDelay,
                BackoffType = connectionResilienceOptions.EnableAdaptiveBackoff
                    ? DelayBackoffType.Exponential
                    : DelayBackoffType.Linear,
                UseJitter = connectionResilienceOptions.UseJitter,
                OnRetry = args =>
                {
                    var exception = args.Outcome.Exception;
                    var errorType = ClassifyNetworkError(exception);

                    _logger.LogWarning(
                        "Network retry {RetryCount}/{MaxRetries} after {Delay}s - Error type: {ErrorType}, Exception: {Exception}",
                        args.AttemptNumber + 1,
                        connectionResilienceOptions.MaxRetryAttempts,
                        args.RetryDelay.TotalSeconds,
                        errorType,
                        exception?.GetType().Name ?? "unknown");

                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<HttpRequestException>(ex => IsTransientNetworkError(ex))
                    .Handle<TimeoutException>()
                    .Handle<IOException>(ex => IsTransientNetworkError(ex))
                    .Handle<SocketException>(ex => IsTransientNetworkError(ex))
                    .Handle<TaskCanceledException>(ex => ex.InnerException is TimeoutException)
            })
            .Build();
    }

    /// <summary>
    ///     Creates a circuit breaker policy.
    /// </summary>
    /// <returns>An async circuit breaker policy.</returns>
    public ResiliencePipeline<TResult> CreateCircuitBreakerPolicy<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResult>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = _options.CircuitBreakerThreshold,
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Circuit breaker opened for {Duration}s due to {Exception}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "failure threshold");

                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker is half-open, testing connection");
                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            .Build();
    }

    /// <summary>
    ///     Creates a timeout policy.
    /// </summary>
    /// <returns>An async timeout policy.</returns>
    public ResiliencePipeline<TResult> CreateTimeoutPolicy<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMinutes(_options.TimeoutMinutes),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);

                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    ///     Creates a combined resilience pipeline with retry, circuit breaker, and timeout policies.
    /// </summary>
    /// <returns>A combined async policy wrapping retry, circuit breaker, and timeout.</returns>
    public ResiliencePipeline<TResult> CreateCombinedPolicy<TResult>()
    {
        return new ResiliencePipelineBuilder<TResult>()
            // Timeout first - outermost boundary
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMinutes(_options.TimeoutMinutes),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Operation timed out after {Timeout}s",
                        args.Timeout.TotalSeconds);

                    return ValueTask.CompletedTask;
                }
            })
            // Circuit breaker second - protects against cascading failures
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResult>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = _options.CircuitBreakerThreshold,
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Circuit breaker opened for {Duration}s due to {Exception}",
                        args.BreakDuration.TotalSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "failure threshold");

                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker is half-open, testing connection");
                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            // Retry last - innermost, tries multiple times
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(_options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = _options.EnableJitter,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s due to {Exception}",
                        args.AttemptNumber + 1,
                        _options.MaxRetryAttempts,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Exception?.GetType().Name ?? "unknown");

                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder<TResult>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<IOException>()
            })
            .Build();
    }

    /// <summary>
    ///     Creates a non-generic resilience pipeline (for void operations).
    /// </summary>
    /// <returns>A non-generic resilience pipeline.</returns>
    public ResiliencePipeline CreateCombinedPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromMinutes(_options.TimeoutMinutes),
                OnTimeout = args =>
                {
                    _logger.LogWarning("Operation timed out after {Timeout}s", args.Timeout.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = _options.CircuitBreakerThreshold,
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning(
                        "Circuit breaker opened for {Duration}s",
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker reset");
                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                Delay = TimeSpan.FromSeconds(_options.InitialDelaySeconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = _options.EnableJitter,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s",
                        args.AttemptNumber + 1,
                        _options.MaxRetryAttempts,
                        args.RetryDelay.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<IOException>()
            })
            .Build();
    }

    /// <summary>
    ///     Classifies network errors into categories.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns>The error type classification.</returns>
    private static string ClassifyNetworkError(Exception? exception)
    {
        return exception switch
        {
            null => "Unknown",
            TimeoutException => "Timeout",
            HttpRequestException httpEx when httpEx.InnerException is SocketException socketEx =>
                socketEx.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => "ConnectionRefused",
                    SocketError.ConnectionReset => "ConnectionReset",
                    SocketError.ConnectionAborted => "ConnectionAborted",
                    SocketError.HostNotFound => "HostNotFound",
                    SocketError.HostUnreachable => "HostUnreachable",
                    SocketError.NetworkDown => "NetworkDown",
                    SocketError.NetworkUnreachable => "NetworkUnreachable",
                    SocketError.TimedOut => "SocketTimeout",
                    _ => $"SocketError_{socketEx.SocketErrorCode}"
                },
            HttpRequestException => "HttpRequest",
            IOException ioEx when ioEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) =>
                "IOConnection",
            IOException => "IO",
            SocketException => "Socket",
            _ => exception.GetType().Name
        };
    }

    /// <summary>
    ///     Determines if an exception represents a transient network error that should be retried.
    /// </summary>
    /// <param name="exception">The exception to check.</param>
    /// <returns>True if the error is transient and retryable, false otherwise.</returns>
    private static bool IsTransientNetworkError(Exception exception)
    {
        return exception switch
        {
            // Connection issues that can be retried
            HttpRequestException httpEx when httpEx.InnerException is SocketException socketEx =>
                socketEx.SocketErrorCode is
                    SocketError.ConnectionRefused or
                    SocketError.ConnectionReset or
                    SocketError.ConnectionAborted or
                    SocketError.HostUnreachable or
                    SocketError.NetworkDown or
                    SocketError.NetworkUnreachable or
                    SocketError.TimedOut or
                    SocketError.TryAgain,

            // General HTTP request exceptions (could be network-related)
            HttpRequestException httpEx when httpEx.StatusCode is null or >= System.Net.HttpStatusCode.InternalServerError =>
                true,

            // IO exceptions related to network
            IOException ioEx when
                ioEx.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("reset", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("broken pipe", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("EOF", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("transport stream", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("unable to read", StringComparison.OrdinalIgnoreCase) ||
                ioEx.Message.Contains("unable to write", StringComparison.OrdinalIgnoreCase) ||
                ioEx.InnerException is SocketException =>
                true,

            // Direct socket exceptions
            SocketException sockEx =>
                sockEx.SocketErrorCode is
                    SocketError.ConnectionRefused or
                    SocketError.ConnectionReset or
                    SocketError.ConnectionAborted or
                    SocketError.HostUnreachable or
                    SocketError.NetworkDown or
                    SocketError.NetworkUnreachable or
                    SocketError.TimedOut or
                    SocketError.TryAgain,

            _ => false
        };
    }
}
