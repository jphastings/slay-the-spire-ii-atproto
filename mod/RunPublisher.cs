using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AtprotoTracker;

internal static class RunPublisher
{
    private const string StatsCollection = "games.gamesgamesgamesgames.actor.stats";
    private const string RunCollection   = "me.byjp.pesos.sts2.run";
    private const string GameRef         = "at://did:web:gamesgamesgamesgames.games/games.gamesgamesgamesgames.game/3mglj4k2edl2l";

    /// <summary>Create or update the in-progress run record (start + mid-run).</summary>
    public static async Task PublishUpdateAsync(RunRecord run, string rkey)
    {
        if (AuthState.Status != AuthStatus.Ok) return;
        run.Game = GameRef;
        await Plugin.AtProto.PutRecordAsync(RunCollection, rkey, run);
    }

    /// <summary>Final update with outcome, then update stats.</summary>
    public static async Task PublishFinalAsync(RunRecord run, string rkey)
    {
        var proto = Plugin.AtProto;
        if (AuthState.Status != AuthStatus.Ok)
        {
            Log.Warn($"not authenticated (status={AuthState.Status}) — skipping upload");
            return;
        }

        // 1. Ensure stats record exists.
        var statsAtUri = await EnsureStatsRecordAsync(run);
        run.StatsRef = statsAtUri;
        run.Game     = GameRef;

        // 2. Final put of the run record.
        var runUri = await proto.PutRecordAsync(RunCollection, rkey, run);
        Log.Info($"posted run: {runUri}");

        // 3. Update rolling stats.
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
            LastPlayed      = run.EndedAt ?? Iso.Now(),
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
            LastPlayed      = run.EndedAt ?? Iso.Now(),
            CreatedAt       = createdAt,
        };
        await proto.PutRecordAsync(StatsCollection, cfg.StatsRkey, stats);
    }

    private static JsonNode BuildGameRef() => new JsonObject { ["uri"] = GameRef };
}
