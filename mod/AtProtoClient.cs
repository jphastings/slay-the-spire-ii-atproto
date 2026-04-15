using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal sealed class AtProtoClient
{
    private readonly HttpClient _http = new();
    private string _pdsUrl = "";
    private string? _did;
    private string? _accessJwt;
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
        _did       = body["did"]!.GetValue<string>();
        _accessJwt = body["accessJwt"]!.GetValue<string>();
        _expiresAt = DateTime.UtcNow.AddMinutes(90);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
    }

    public bool IsAuthenticated => _accessJwt is not null && DateTime.UtcNow < _expiresAt;

    public async Task<string> CreateRecordAsync(string collection, object record)
    {
        RequireAuth();
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
        RequireAuth();
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
        RequireAuth();
        var url = $"{_pdsUrl}/xrpc/com.atproto.repo.getRecord"
                + $"?repo={Uri.EscapeDataString(_did!)}"
                + $"&collection={Uri.EscapeDataString(collection)}"
                + $"&rkey={Uri.EscapeDataString(rkey)}";
        var res = await _http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<JsonNode>();
    }

    private void RequireAuth()
    {
        if (!IsAuthenticated)
            throw new InvalidOperationException("atproto-tracker: not authenticated");
    }
}
