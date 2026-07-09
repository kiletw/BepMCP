using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BepMcp.Server;

public sealed class UnityBridgeClient : IDisposable
{
    private const string Schema = "unity-mcp.bridge/1";
    private readonly HttpClient _http;

    public UnityBridgeClient(Uri baseAddress)
    {
        _http = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };
    }

    public async Task<string> SnapshotAsync(CancellationToken cancellationToken)
    {
        return await _http.GetStringAsync("snapshot", cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ScreenshotAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("screenshot", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0 || bytes.Length > 32 * 1024 * 1024)
        {
            throw new InvalidDataException("Screenshot size is invalid.");
        }

        return bytes;
    }

    public async Task<int> ProcessIdAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("healthz", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        return json.RootElement.GetProperty("processId").GetInt32();
    }

    public async Task<string> ActAsync(
        IReadOnlyList<UnityBridgeAction> actions,
        bool dryRun,
        string? requestId,
        CancellationToken cancellationToken)
    {
        if (actions.Count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(actions), "Actions must contain 1 to 100 items.");
        }

        var id = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId;
        var body = JsonSerializer.Serialize(new
        {
            schema = Schema,
            requestId = id,
            type = "act",
            payload = new { dryRun, actions }
        });
        using var response = await _http.PostAsync(
            "act",
            new StringContent(body, Encoding.UTF8, "application/json"),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
