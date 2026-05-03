using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal static class SteamDidResolver
{
    private const string Endpoint = "https://keytrace.dev/xrpc/dev.keytrace.reverseLookup";
    private static readonly HttpClient _http = new();
    // Only positive hits are cached. Misses (no match, transient errors) are
    // retried next extraction so a player who publishes their keytrace claim
    // mid-session gets picked up without a game restart.
    private static readonly ConcurrentDictionary<ulong, string> _cache = new();
    private static readonly ConcurrentDictionary<ulong, byte> _inFlight = new();

    public static string? LookupDid(ulong steamId64)
    {
        if (_cache.TryGetValue(steamId64, out var did)) return did;
        _ = StartLookupAsync(steamId64);
        return null;
    }

    public static AllyEntry ResolveAlly(ulong steamId64) => new()
    {
        Steam   = steamId64.ToString(),
        Atproto = LookupDid(steamId64),
    };

    /// <summary>
    /// Awaitable lookup used by the boot-time ally backfill. Returns the
    /// resolved DID or null on a miss / transient error. Hits the same cache
    /// as <see cref="LookupDid"/> so concurrent in-game and backfill paths
    /// share state.
    /// </summary>
    public static async Task<string?> LookupDidAsync(ulong steamId64)
    {
        if (_cache.TryGetValue(steamId64, out var cached)) return cached;
        try
        {
            var url = $"{Endpoint}?type=steam&subject={steamId64}";
            var res = await _http.GetFromJsonAsync<ReverseLookupResponse>(url);
            if (res?.Matches is { Count: > 0 } m && m[0].Did is { } did)
            {
                _cache[steamId64] = did;
                return did;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"keytrace lookup failed for {steamId64}: {ex.Message}");
        }
        return null;
    }

    private static async Task StartLookupAsync(ulong steamId64)
    {
        if (!_inFlight.TryAdd(steamId64, 0)) return;
        try
        {
            var url = $"{Endpoint}?type=steam&subject={steamId64}";
            var res = await _http.GetFromJsonAsync<ReverseLookupResponse>(url);
            if (res?.Matches is { Count: > 0 } m && m[0].Did is { } did)
                _cache[steamId64] = did;
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
