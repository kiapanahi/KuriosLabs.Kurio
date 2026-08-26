using System.Net;
using System.Net.Sockets;

namespace KuriousLabs.Kurio.Integration;

/// <summary>
///     Minimal in-process HTTP file server for integration tests. Serves a fixed
///     payload, optionally honoring Range requests, and can gate body responses
///     behind a semaphore to make concurrency observable and deterministic.
/// </summary>
internal sealed class LoopbackHttpServer : IAsyncDisposable
{
    private readonly Task _acceptLoop;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim? _gate;
    private readonly HttpListener _listener;
    private readonly byte[] _payload;
    private readonly bool _supportsRanges;
    private int _bodyRequestCount;

    public LoopbackHttpServer(byte[] payload, bool supportsRanges, SemaphoreSlim? gate = null)
    {
        _payload = payload;
        _supportsRanges = supportsRanges;
        _gate = gate;

        var port = GetFreePort();
        Url = new Uri($"http://127.0.0.1:{port}/file.bin");
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Gets the URL the payload is served from (query strings are ignored).</summary>
    public Uri Url { get; }

    /// <summary>Gets the number of GET (body) requests received; HEAD requests are not counted.</summary>
    public int BodyRequestCount => Volatile.Read(ref _bodyRequestCount);

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            await _acceptLoop;
        }
        catch (Exception)
        {
            // The accept loop is expected to abort during shutdown.
        }

        _cts.Dispose();
    }

    private static int GetFreePort()
    {
        TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                break; // listener stopped
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var response = context.Response;
        try
        {
            if (_supportsRanges)
            {
                response.AddHeader("Accept-Ranges", "bytes");
            }

            if (string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 200;
                response.ContentLength64 = _payload.Length;
                response.Close();
                return;
            }

            Interlocked.Increment(ref _bodyRequestCount);

            if (_gate is not null)
            {
                await _gate.WaitAsync(_cts.Token);
            }

            var rangeHeader = context.Request.Headers["Range"];
            if (_supportsRanges && rangeHeader is not null &&
                rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = rangeHeader["bytes=".Length..].Split('-');
                var start = long.Parse(parts[0]);
                var end = parts.Length > 1 && parts[1].Length > 0 ? long.Parse(parts[1]) : _payload.Length - 1;
                end = Math.Min(end, _payload.Length - 1);
                var length = end - start + 1;

                response.StatusCode = 206;
                response.AddHeader("Content-Range", $"bytes {start}-{end}/{_payload.Length}");
                response.ContentLength64 = length;
                await response.OutputStream.WriteAsync(_payload.AsMemory((int)start, (int)length), _cts.Token);
            }
            else
            {
                response.StatusCode = 200;
                response.ContentLength64 = _payload.Length;
                await response.OutputStream.WriteAsync(_payload, _cts.Token);
            }

            response.Close();
        }
        catch (Exception)
        {
            // Client aborted or server shutting down; drop the connection.
            try
            {
                response.Abort();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
