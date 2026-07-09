using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityMcp.Core;

namespace UnityMcp.Plugin.BepInEx6.Il2Cpp;

internal sealed class BridgeHttpServer : IDisposable
{
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 1024 * 1024;

    private readonly int _port;
    private readonly MainThreadScheduler _scheduler;
    private readonly ManualLogSource _log;
    private readonly Func<int> _maxEntities;
    private readonly TcpListener _listener;
    private CancellationTokenSource? _stop;

    public BridgeHttpServer(int port, MainThreadScheduler scheduler, ManualLogSource log, Func<int> maxEntities)
    {
        _port = port;
        _scheduler = scheduler;
        _log = log;
        _maxEntities = maxEntities;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        _listener.Start();
        _stop = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_stop.Token));
    }

    public void Dispose()
    {
        _stop?.Cancel();
        _listener.Stop();
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                _ = Task.Run(() => Handle(client));
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                _log.LogWarning("Bridge TCP listener failed: " + ex.Message);
            }
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            var requestId = Guid.NewGuid().ToString("N");
            var stream = client.GetStream();

            try
            {
                var request = await ReadRequest(stream).ConfigureAwait(false);
                if (request.Method == "GET" && request.Path == "/healthz")
                {
                    await WriteJson(stream, 200, "{\"ok\":true}").ConfigureAwait(false);
                    return;
                }

                if (request.Method == "GET" && request.Path == "/snapshot")
                {
                    var json = await _scheduler.Enqueue(() =>
                        UnitySnapshot.Capture(requestId, _maxEntities())).ConfigureAwait(false);
                    await WriteJson(stream, 200, json).ConfigureAwait(false);
                    return;
                }

                if (request.Method == "POST" && request.Path == "/act")
                {
                    var json = await _scheduler.Enqueue(() => UnityActions.Execute(request.Body, requestId))
                        .ConfigureAwait(false);
                    await WriteJson(stream, 200, json).ConfigureAwait(false);
                    return;
                }

                await WriteJson(stream, 404, BridgeJson.Error(requestId, "NOT_FOUND", "Unknown endpoint."))
                    .ConfigureAwait(false);
            }
            catch (InvalidDataException ex)
            {
                await WriteJson(stream, 400, BridgeJson.Error(requestId, "BAD_REQUEST", ex.Message))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Bridge HTTP request failed: " + ex);
                await WriteJson(stream, 500, BridgeJson.Error(requestId, "INTERNAL_ERROR", ex.Message))
                    .ConfigureAwait(false);
            }
        }
    }

    private static async Task<(string Method, string Path, string Body)> ReadRequest(NetworkStream stream)
    {
        var header = new List<byte>();
        var terminator = 0;

        while (terminator < 4)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                throw new InvalidDataException("Incomplete HTTP headers.");
            }

            header.Add((byte)value);
            terminator = value == "\r\n\r\n"[terminator] ? terminator + 1 : value == '\r' ? 1 : 0;
            if (header.Count > MaxHeaderBytes)
            {
                throw new InvalidDataException("HTTP headers are too large.");
            }
        }

        var lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length != 3)
        {
            throw new InvalidDataException("Invalid HTTP request line.");
        }

        var contentLength = 0;
        foreach (var line in lines)
        {
            if (!line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!int.TryParse(line.Substring(15).Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
                    out contentLength) ||
                contentLength < 0 ||
                contentLength > MaxBodyBytes)
            {
                throw new InvalidDataException("Invalid Content-Length.");
            }
        }

        var body = new byte[contentLength];
        var offset = 0;
        while (offset < body.Length)
        {
            var read = await stream.ReadAsync(body, offset, body.Length - offset).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("Incomplete HTTP body.");
            }

            offset += read;
        }

        var path = requestLine[1].Split('?')[0];
        return (requestLine[0].ToUpperInvariant(), path, Encoding.UTF8.GetString(body));
    }

    private static async Task WriteJson(NetworkStream stream, int statusCode, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var reason = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            _ => "Internal Server Error"
        };
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 " + statusCode + " " + reason + "\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            "Content-Length: " + body.Length + "\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
        await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
    }
}
