using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Sts2At;

internal sealed class AtProtoClient
{
    private readonly Config _cfg;
    private readonly HttpClient _http = new();
    private string? _did;
    private string? _accessJwt;
    private DateTime _expiresAt;

    public AtProtoClient(Config cfg) => _cfg = cfg;

    private async Task EnsureSessionAsync()
    {
        if (_accessJwt is not null && DateTime.UtcNow < _expiresAt) return;

        var res = await _http.PostAsJsonAsync(
            $"{_cfg.PdsUrl}/xrpc/com.atproto.server.createSession",
            new { identifier = _cfg.Handle, password = _cfg.AppPassword });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty session response");
        _did = body["did"]!.GetValue<string>();
        _accessJwt = body["accessJwt"]!.GetValue<string>();
        _expiresAt = DateTime.UtcNow.AddMinutes(90);
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessJwt);
    }

    public string Did => _did ?? throw new InvalidOperationException("not authenticated");

    public async Task<string> CreateRecordAsync(string collection, object record)
    {
        await EnsureSessionAsync();
        var res = await _http.PostAsJsonAsync(
            $"{_cfg.PdsUrl}/xrpc/com.atproto.repo.createRecord",
            new { repo = _did, collection, record });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty createRecord response");
        return body["uri"]!.GetValue<string>();
    }

    public async Task<string> PutRecordAsync(string collection, string rkey, object record)
    {
        await EnsureSessionAsync();
        var res = await _http.PostAsJsonAsync(
            $"{_cfg.PdsUrl}/xrpc/com.atproto.repo.putRecord",
            new { repo = _did, collection, rkey, record });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty putRecord response");
        return body["uri"]!.GetValue<string>();
    }

    public async Task<JsonNode?> GetRecordAsync(string collection, string rkey)
    {
        await EnsureSessionAsync();
        var url = $"{_cfg.PdsUrl}/xrpc/com.atproto.repo.getRecord"
                + $"?repo={Uri.EscapeDataString(_did!)}"
                + $"&collection={Uri.EscapeDataString(collection)}"
                + $"&rkey={Uri.EscapeDataString(rkey)}";
        var res = await _http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadFromJsonAsync<JsonNode>();
    }
}
