using FluentAssertions;
using KuriousLabs.Kurio.Core;
using KuriousLabs.Kurio.Core.Abstractions;
using KuriousLabs.Kurio.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace KuriousLabs.Kurio.Integration;

/// <summary>
///     End-to-end tests that wire the REAL composition root
///     (<see cref="ServiceCollectionExtensions.AddKurioDownloadEngine(IServiceCollection, string, string, int)" />)
///     against an in-process HTTP server — no mocked storage or segment manager, so the
///     engine → segments → storage → commit seams are actually exercised.
/// </summary>
public sealed class DownloadEngineIntegrationTests
{
    private static byte[] CreatePayload(int size, int seed)
    {
        var payload = new byte[size];
        new Random(seed).NextBytes(payload);
        return payload;
    }

    private static ServiceProvider BuildEngineProvider(string root, int maxConcurrent)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddKurioDownloadEngine(
            Path.Combine(root, "temp"),
            Path.Combine(root, "state"),
            maxConcurrent);
        return services.BuildServiceProvider();
    }

    private static async Task WaitForStateAsync(
        IDownloadEngine engine,
        Guid taskId,
        DownloadState expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var task = engine.GetDownload(taskId);
            if (task?.State == expected)
            {
                return;
            }

            if (task?.State == DownloadState.Failed && expected != DownloadState.Failed)
            {
                throw new InvalidOperationException(
                    $"Download failed instead of reaching {expected}: {task.LastError?.Message}");
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"Timed out waiting for state {expected}; last observed: {engine.GetDownload(taskId)?.State}");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not reached within timeout.");
            }

            await Task.Delay(25);
        }
    }

    [Fact]
    public async Task SingleSegmentDownload_WithoutRangeSupport_CommitsFile()
    {
        // A server without range support forces the single-segment path, which
        // previously downloaded everything and then failed at commit because the
        // merged temp file was never produced.
        var payload = CreatePayload(256 * 1024, seed: 1);
        await using LoopbackHttpServer server = new(payload, supportsRanges: false);
        using TempDirectory root = new();
        await using var provider = BuildEngineProvider(root.Path, maxConcurrent: 3);
        var engine = provider.GetRequiredService<IDownloadEngine>();

        var task = await engine.AddDownloadAsync(server.Url, new DownloadOptions
        {
            DestinationDirectory = root.DestinationPath,
            FileName = "single.bin"
        });

        await WaitForStateAsync(engine, task.Id, DownloadState.Completed, TimeSpan.FromSeconds(30));

        var written = await File.ReadAllBytesAsync(Path.Combine(root.DestinationPath, "single.bin"));
        written.Should().Equal(payload);
    }

    [Fact]
    public async Task SmallFileDownload_WithRangeSupport_CommitsFile()
    {
        // Files below MinSegmentSize are single-segment even when ranges are
        // supported — the other trigger of the missing-commit-file failure.
        var payload = CreatePayload(64 * 1024, seed: 2);
        await using LoopbackHttpServer server = new(payload, supportsRanges: true);
        using TempDirectory root = new();
        await using var provider = BuildEngineProvider(root.Path, maxConcurrent: 3);
        var engine = provider.GetRequiredService<IDownloadEngine>();

        var task = await engine.AddDownloadAsync(server.Url, new DownloadOptions
        {
            DestinationDirectory = root.DestinationPath,
            FileName = "small.bin"
        });

        await WaitForStateAsync(engine, task.Id, DownloadState.Completed, TimeSpan.FromSeconds(30));

        var written = await File.ReadAllBytesAsync(Path.Combine(root.DestinationPath, "small.bin"));
        written.Should().Equal(payload);
    }

    [Fact]
    public async Task MultiSegmentDownload_WithRangeSupport_CommitsByteIdenticalFile()
    {
        var payload = CreatePayload(4 * 1024 * 1024, seed: 3);
        await using LoopbackHttpServer server = new(payload, supportsRanges: true);
        using TempDirectory root = new();
        await using var provider = BuildEngineProvider(root.Path, maxConcurrent: 3);
        var engine = provider.GetRequiredService<IDownloadEngine>();

        var task = await engine.AddDownloadAsync(server.Url, new DownloadOptions
        {
            DestinationDirectory = root.DestinationPath,
            FileName = "multi.bin",
            MaxConnections = 4
        });

        await WaitForStateAsync(engine, task.Id, DownloadState.Completed, TimeSpan.FromSeconds(60));

        var written = await File.ReadAllBytesAsync(Path.Combine(root.DestinationPath, "multi.bin"));
        written.Should().Equal(payload);
        server.BodyRequestCount.Should().BeGreaterThanOrEqualTo(2, "the file should be fetched in multiple segments");
    }

    [Fact]
    public async Task Scheduler_EnforcesMaxConcurrentDownloads()
    {
        // Gate every body response so downloads stay in-flight until released,
        // making the scheduler's concurrency behavior directly observable.
        var payload = CreatePayload(64 * 1024, seed: 4);
        using SemaphoreSlim gate = new(0);
        await using LoopbackHttpServer server = new(payload, supportsRanges: true, gate);
        using TempDirectory root = new();
        await using var provider = BuildEngineProvider(root.Path, maxConcurrent: 2);
        var engine = provider.GetRequiredService<IDownloadEngine>();

        List<Guid> taskIds = [];
        for (var i = 0; i < 5; i++)
        {
            var task = await engine.AddDownloadAsync(new Uri($"{server.Url}?i={i}"), new DownloadOptions
            {
                DestinationDirectory = root.DestinationPath,
                FileName = $"file{i}.bin"
            });
            taskIds.Add(task.Id);
        }

        // Wait for the cap's worth of in-flight requests, then give the 500ms
        // scheduler several more ticks to (incorrectly) start additional ones.
        await WaitUntilAsync(() => server.BodyRequestCount >= 2, TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromMilliseconds(1600));

        server.BodyRequestCount.Should().Be(2, "only MaxConcurrentDownloads downloads may run at once");
        var (active, queued) = engine.GetQueueStatistics();
        active.Should().Be(2);
        queued.Should().Be(3);

        gate.Release(int.MaxValue / 2);

        foreach (var taskId in taskIds)
        {
            await WaitForStateAsync(engine, taskId, DownloadState.Completed, TimeSpan.FromSeconds(30));
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Directory.CreateDirectory(DestinationPath);
        }

        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"kurio-it-{Guid.NewGuid():N}");

        public string DestinationPath => System.IO.Path.Combine(Path, "dest");

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Background work may still hold a handle briefly; temp dirs get
                // cleaned by the OS eventually.
            }
        }
    }
}
