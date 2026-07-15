using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public async Task<string> HealthAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync("healthz", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> WaitForSnapshotAsync(
        string contains,
        bool absent,
        int timeoutMs,
        int pollMs,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contains) || contains.Length > 256)
        {
            throw new ArgumentException("contains must be 1 to 256 characters.", nameof(contains));
        }

        if (timeoutMs is < 100 or > 30000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "timeoutMs must be 100 to 30000.");
        }

        if (pollMs is < 50 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(pollMs), "pollMs must be 50 to 1000.");
        }

        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            var snapshot = await SnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (SnapshotMatches(snapshot, contains, absent))
            {
                return snapshot;
            }

            if (elapsed.ElapsedMilliseconds >= timeoutMs)
            {
                throw new TimeoutException("Snapshot condition was not met within " + timeoutMs + " ms.");
            }

            var delay = Math.Min(pollMs, timeoutMs - (int)elapsed.ElapsedMilliseconds);
            await Task.Delay(Math.Max(1, delay), cancellationToken).ConfigureAwait(false);
        }
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
        using var json = JsonDocument.Parse(await HealthAsync(cancellationToken).ConfigureAwait(false));
        return json.RootElement.GetProperty("processId").GetInt32();
    }

    internal static bool SnapshotMatches(string snapshot, string contains, bool absent)
    {
        var found = snapshot.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
        return absent ? !found : found;
    }

    public static void SelfTest()
    {
        const string snapshot = "{\"scene\":\"Main\",\"text\":\"Start Game\"}";
        if (!SnapshotMatches(snapshot, "start game", false) ||
            !SnapshotMatches(snapshot, "loading", true))
        {
            throw new InvalidOperationException("Snapshot wait matching failed.");
        }
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
