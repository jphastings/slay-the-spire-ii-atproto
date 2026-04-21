namespace AtprotoTracker;

internal static class SteamDidResolver
{
    // TODO: resolve SteamID64 → atproto DID via a mapping service and cache the result.
    public static string? LookupDid(ulong steamId64) => null;

    public static string ResolveUri(ulong steamId64) =>
        LookupDid(steamId64) ?? $"https://steamcommunity.com/profiles/{steamId64}/";
}
