using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal sealed record MiniDoc(string Did, string Handle, string Pds);

internal static class IdentityResolver
{
    private const string Endpoint = "https://slingshot.microcosm.blue/xrpc/blue.microcosm.identity.resolveMiniDoc";
    private static readonly HttpClient _http = new();

    public static async Task<MiniDoc> ResolveAsync(string identifier)
    {
        var url = $"{Endpoint}?identifier={Uri.EscapeDataString(identifier)}";
        var res = await _http.GetAsync(url);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Slingshot resolve failed: HTTP {(int)res.StatusCode} for {identifier}");
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty resolve response");
        var did    = body["did"]?.GetValue<string>();
        var handle = body["handle"]?.GetValue<string>();
        var pds    = body["pds"]?.GetValue<string>();
        if (did is null || handle is null || pds is null)
            throw new InvalidOperationException($"Slingshot response missing fields: {body}");
        return new MiniDoc(did, handle, pds);
    }
}
