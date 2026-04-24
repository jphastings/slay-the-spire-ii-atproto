using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal sealed class AtProtoClient
{
    private readonly HttpClient _http = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private string _pdsUrl = "";
    private string? _did;
    private string? _accessJwt;
    private string? _refreshJwt;
    // Conservative TTL: PDS access JWTs are typically ~2h, so refreshing
    // every 80 min leaves comfortable headroom for clock skew and long
    // single calls without juggling token-near-expiry races.
    private static readonly TimeSpan AccessTokenTtl = TimeSpan.FromMinutes(80);
    private DateTime _expiresAt;

    public string Did => _did ?? throw new InvalidOperationException("not authenticated");

    // Log in against a specific PDS with app-password credentials. Call once after
    // resolving the identity via Slingshot.
    public async Task LoginAsync(string pdsUrl, string identifier, string appPassword)
    {
        _pdsUrl = pdsUrl.TrimEnd('/');
        var res = await _http.PostAsJsonAsync(
            $"{_pdsUrl}/xrpc/com.atproto.server.createSession",
            new { identifier, password = appPassword });
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"createSession HTTP {(int)res.StatusCode}: {err}");
        }
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty session response");
        _did = body["did"]!.GetValue<string>();
        ApplyTokens(body);
    }

    public bool IsAuthenticated => _accessJwt is not null;

    public async Task<string> CreateRecordAsync(string collection, object record)
    {
        await EnsureFreshAsync();
        var res = await _http.PostAsJsonAsync(
            $"{_pdsUrl}/xrpc/com.atproto.repo.createRecord",
            new { repo = _did, collection, record });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty createRecord response");
        return body["uri"]!.GetValue<string>();
    }

    public async Task<string> PutRecordAsync(string collection, string rkey, object record)
    {
        await EnsureFreshAsync();
        var res = await _http.PostAsJsonAsync(
            $"{_pdsUrl}/xrpc/com.atproto.repo.putRecord",
            new { repo = _did, collection, rkey, record });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty putRecord response");
        return body["uri"]!.GetValue<string>();
    }

    public async Task<JsonNode?> GetRecordAsync(string collection, string rkey)
    {
        await EnsureFreshAsync();
        var url = $"{_pdsUrl}/xrpc/com.atproto.repo.getRecord"
                + $"?repo={Uri.EscapeDataString(_did!)}"
                + $"&collection={Uri.EscapeDataString(collection)}"
                + $"&rkey={Uri.EscapeDataString(rkey)}";
        var res = await _http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<JsonNode>();
    }

    private async Task EnsureFreshAsync()
    {
        if (_accessJwt is null)
            throw new InvalidOperationException("atproto-tracker: not authenticated");
        if (DateTime.UtcNow < _expiresAt) return;

        await _refreshLock.WaitAsync();
        try
        {
            // Re-check inside the lock — a parallel caller may have already refreshed.
            if (DateTime.UtcNow < _expiresAt) return;
            await RefreshSessionAsync();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshSessionAsync()
    {
        if (string.IsNullOrEmpty(_refreshJwt))
            throw new InvalidOperationException("atproto-tracker: no refresh token available");

        // refreshSession authenticates with the refresh JWT, not the access JWT,
        // so swap the bearer for this single call.
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"{_pdsUrl}/xrpc/com.atproto.server.refreshSession");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _refreshJwt);
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            // Treat refresh failure as a hard logout so the publish path stops
            // hammering the PDS and the badge surfaces a useful state.
            _accessJwt = null;
            _refreshJwt = null;
            AuthState.Set(AuthStatus.Failed, error: $"refreshSession HTTP {(int)res.StatusCode}: {err}");
            throw new InvalidOperationException($"refreshSession HTTP {(int)res.StatusCode}: {err}");
        }
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty refreshSession response");
        ApplyTokens(body);
    }

    private void ApplyTokens(JsonNode body)
    {
        _accessJwt  = body["accessJwt"]!.GetValue<string>();
        _refreshJwt = body["refreshJwt"]?.GetValue<string>() ?? _refreshJwt;
        _expiresAt  = DateTime.UtcNow + AccessTokenTtl;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessJwt);
    }
}
