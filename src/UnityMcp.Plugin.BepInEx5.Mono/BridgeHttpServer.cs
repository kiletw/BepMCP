using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using UnityMcp.Core;

namespace UnityMcp.Plugin.BepInEx5.Mono;

internal sealed class BridgeHttpServer : IDisposable
{
    private readonly int _port;
    private readonly MainThreadScheduler _scheduler;
    private readonly ManualLogSource _log;
    private readonly Func<int> _maxEntities;
    private readonly HttpListener _listener = new HttpListener();
    private CancellationTokenSource? _stop;

    public BridgeHttpServer(int port, MainThreadScheduler scheduler, ManualLogSource log, Func<int> maxEntities)
    {
        _port = port;
        _scheduler = scheduler;
        _log = log;
        _maxEntities = maxEntities;
    }

    public void Start()
    {
        _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
        _listener.Start();
        _stop = new CancellationTokenSource();
        Task.Run(() => ListenLoop(_stop.Token));
    }

    public void Dispose()
    {
        _stop?.Cancel();
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => Handle(context));
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    _log.LogWarning("Bridge HTTP listener failed: " + ex.Message);
                }
            }
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        var requestId = Guid.NewGuid().ToString("N");
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            if (context.Request.HttpMethod == "GET" && path == "/healthz")
            {
                await WriteJson(context, "{\"ok\":true}").ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/snapshot")
            {
                var json = await _scheduler.Enqueue(() =>
                    UnitySnapshot.Capture(requestId, _maxEntities())).ConfigureAwait(false);
                await WriteJson(context, json).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/act")
            {
                string body;
                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                var json = await _scheduler.Enqueue(() => UnityActions.Execute(body, requestId))
                    .ConfigureAwait(false);
                await WriteJson(context, json).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await WriteJson(context, BridgeJson.Error(requestId, "NOT_FOUND", "Unknown endpoint."))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogWarning("Bridge HTTP request failed: " + ex);
            context.Response.StatusCode = 500;
            await WriteJson(context, BridgeJson.Error(requestId, "INTERNAL_ERROR", ex.Message))
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteJson(HttpListenerContext context, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        context.Response.OutputStream.Close();
    }
}
