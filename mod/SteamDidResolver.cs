using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal static class SteamDidResolver
{
    private const string Endpoint = "https://keytrace.dev/xrpc/dev.keytrace.reverseLookup";
    private static readonly HttpClient _http = new();
    // Both hits and misses are cached for the lifetime of the mod (process).
    // Transient HTTP errors skip the cache so we retry next extraction.
    private static readonly ConcurrentDictionary<ulong, string?> _cache = new();
    private static readonly ConcurrentDictionary<ulong, byte> _inFlight = new();

    // TODO: remove this 404-guard once the keytrace.dev reverseLookup endpoint is
    // publicly released. Until then, a single 404 disables further lookups for
    // the session so we don't hammer a not-yet-live endpoint.
    private static bool _endpointAvailable = true;

    public static string? LookupDid(ulong steamId64)
    {
        if (_cache.TryGetValue(steamId64, out var did)) return did;
        if (!_endpointAvailable) return null;
        _ = StartLookupAsync(steamId64);
        return null;
    }

    public static AllyEntry ResolveAlly(ulong steamId64) => new()
    {
        Steam   = steamId64.ToString(),
        Atproto = LookupDid(steamId64),
    };

    private static async Task StartLookupAsync(ulong steamId64)
    {
        if (!_inFlight.TryAdd(steamId64, 0)) return;
        try
        {
            var url = $"{Endpoint}?type=steam&subject={steamId64}";
            var res = await _http.GetFromJsonAsync<ReverseLookupResponse>(url);
            _cache[steamId64] = res?.Matches is { Count: > 0 } m ? m[0].Did : null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _endpointAvailable = false;
            Log.Warn($"keytrace endpoint returned 404; disabling DID lookups for this session ({Endpoint})");
        }
        catch (Exception ex)
        {
            Log.Warn($"keytrace lookup failed for {steamId64}: {ex.Message}");
        }
        finally
        {
            _inFlight.TryRemove(steamId64, out _);
        }
    }

    private sealed class ReverseLookupResponse
    {
        [JsonPropertyName("total")]   public int Total { get; set; }
        [JsonPropertyName("matches")] public List<Match>? Matches { get; set; }
    }

    private sealed class Match
    {
        [JsonPropertyName("did")] public string? Did { get; set; }
    }
}
