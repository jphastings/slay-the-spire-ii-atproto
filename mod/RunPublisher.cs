using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal static class RunPublisher
{
    private const string StatsCollection = "games.gamesgamesgamesgames.actor.stats";
    private const string RunCollection   = "me.byjp.pesos.sts2.run";
    private const string GameRef         = "at://did:web:gamesgamesgamesgames.games/games.gamesgamesgamesgames.game/3mglj4k2edl2l";

    public static async Task PublishAsync(RunRecord run)
    {
        var proto = Plugin.AtProto;

        if (AuthState.Status != AuthStatus.Ok)
        {
            Log.Warn($"not authenticated (status={AuthState.Status}) — skipping upload. {AuthState.Error}");
            return;
        }

        // 1. Ensure a stats record exists and compute its at-uri (stable across runs).
        var statsAtUri = await EnsureStatsRecordAsync(run);
        run.StatsRef = statsAtUri;
        run.Game     = GameRef;

        // 2. Create the run record.
        var runUri = await proto.CreateRecordAsync(RunCollection, run);
        Log.Info($"posted run: {runUri}");

        // 3. Update stats with new playtime / lastPlayed (put to same rkey).
        await UpdateStatsAsync(run);
    }

    private static async Task<string> EnsureStatsRecordAsync(RunRecord run)
    {
        var cfg   = Plugin.Config;
        var proto = Plugin.AtProto;

        if (!string.IsNullOrEmpty(cfg.StatsRkey))
        {
            var existing = await proto.GetRecordAsync(StatsCollection, cfg.StatsRkey);
            if (existing is not null)
                return $"at://{proto.Did}/{StatsCollection}/{cfg.StatsRkey}";
            Log.Warn($"cached statsRkey {cfg.StatsRkey} missing on PDS — creating a new one");
        }

        var stats = new StatsRecord
        {
            Game            = BuildGameRef(),
            Source          = "steam",
            PlaytimeMinutes = Math.Max(1, run.DurationSeconds / 60),
            LastPlayed      = run.EndedAt,
            CreatedAt       = Iso.Now(),
        };
        var uri = await proto.CreateRecordAsync(StatsCollection, stats);
        cfg.StatsRkey = uri[(uri.LastIndexOf('/') + 1)..];
        cfg.Save();
        return uri;
    }

    private static async Task UpdateStatsAsync(RunRecord run)
    {
        var cfg   = Plugin.Config;
        var proto = Plugin.AtProto;
        var existing = await proto.GetRecordAsync(StatsCollection, cfg.StatsRkey);
        var prior = 0;
        string createdAt = Iso.Now();
        if (existing?["value"] is JsonNode value)
        {
            prior     = value["playtime"]?.GetValue<int>() ?? 0;
            createdAt = value["createdAt"]?.GetValue<string>() ?? createdAt;
        }
        var stats = new StatsRecord
        {
            Game            = BuildGameRef(),
            Source          = "steam",
            PlaytimeMinutes = prior + Math.Max(1, run.DurationSeconds / 60),
            LastPlayed      = run.EndedAt,
            CreatedAt       = createdAt,
        };
        await proto.PutRecordAsync(StatsCollection, cfg.StatsRkey, stats);
    }

    // games.gamesgamesgamesgames.actor.game#gameRef shape:
    // { "uri": "at://...", "cid": "..." } — we only have the at-uri, emit uri-only.
    private static JsonNode BuildGameRef() => new JsonObject { ["uri"] = GameRef };
}

