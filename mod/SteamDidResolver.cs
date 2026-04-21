namespace AtprotoTracker;

internal static class SteamDidResolver
{
    // TODO: resolve SteamID64 → atproto DID via a mapping service and cache the result.
    public static string? LookupDid(ulong steamId64) => null;

    public static AllyEntry ResolveAlly(ulong steamId64)
    {
        var did = LookupDid(steamId64);
        return did is not null ? AllyEntry.FromDid(did) : AllyEntry.FromSteam(steamId64);
    }
}
