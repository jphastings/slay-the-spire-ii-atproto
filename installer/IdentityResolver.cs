using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AtprotoTracker.Installer;

internal sealed record MiniDoc(string Did, string Handle, string Pds);

internal static class IdentityResolver
{
    private const string Endpoint = "https://slingshot.microcosm.blue/xrpc/blue.microcosm.identity.resolveMiniDoc";
    private static readonly HttpClient Http = new();

    public static async Task<MiniDoc> ResolveAsync(string identifier)
    {
        var url = $"{Endpoint}?identifier={Uri.EscapeDataString(identifier)}";
        var res = await Http.GetAsync(url);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Slingshot: HTTP {(int)res.StatusCode} for {identifier}");
        var body = await res.Content.ReadFromJsonAsync<JsonNode>()
                   ?? throw new InvalidOperationException("empty resolve response");
        return new MiniDoc(
            body["did"]?.GetValue<string>() ?? throw new InvalidOperationException("missing did"),
            body["handle"]?.GetValue<string>() ?? throw new InvalidOperationException("missing handle"),
            body["pds"]?.GetValue<string>() ?? throw new InvalidOperationException("missing pds"));
    }

    public static async Task ValidateCredentialsAsync(MiniDoc mini, string appPassword)
    {
        var res = await Http.PostAsJsonAsync(
            $"{mini.Pds}/xrpc/com.atproto.server.createSession",
            new { identifier = mini.Did, password = appPassword });
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Authentication failed (HTTP {(int)res.StatusCode}): {err}");
        }
    }
}
