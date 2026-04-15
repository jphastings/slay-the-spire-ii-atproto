using System;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Sts2At;

internal static class RunPublisher
{
    private const string StatsCollection = "games.gamesgamesgamesgames.actor.stats";
    private const string RunCollection   = "me.byjp.pesos.sts2.run";

    public static async Task PublishAsync(RunRecord run)
    {
        var cfg   = Plugin.Config;
        var proto = Plugin.AtProto;

        if (string.IsNullOrEmpty(cfg.Handle) || string.IsNullOrEmpty(cfg.AppPassword))
        {
            Log.Warn("handle/appPassword not configured — skipping upload");
            return;
        }

        // 1. Ensure a stats record exists and compute its at-uri (stable across runs).
        string? statsAtUri = null;
        if (!string.IsNullOrEmpty(cfg.GameRef))
        {
            statsAtUri = await EnsureStatsRecordAsync(run);
            run.StatsRef = statsAtUri;
            run.Game = cfg.GameRef;
        }

        // 2. Create the run record.
        var runUri = await proto.CreateRecordAsync(RunCollection, run);
        Log.Info($"posted run: {runUri}");

        // 3. Update stats with new playtime / lastPlayed (put to same rkey).
        if (statsAtUri is not null)
            await UpdateStatsAsync(run);
    }

    private static async Task<string> EnsureStatsRecordAsync(RunRecord run)
    {
        var cfg   = Plugin.Config;
        var proto = Plugin.AtProto;
        var gameRef = BuildGameRef(cfg.GameRef);

        if (!string.IsNullOrEmpty(cfg.StatsRkey))
        {
            var existing = await proto.GetRecordAsync(StatsCollection, cfg.StatsRkey);
            if (existing is not null)
                return $"at://{proto.Did}/{StatsCollection}/{cfg.StatsRkey}";
            Log.Warn($"cached statsRkey {cfg.StatsRkey} missing on PDS — creating a new one");
        }

        var stats = new StatsRecord
        {
            Game            = gameRef,
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
            Game            = BuildGameRef(cfg.GameRef),
            Source          = "steam",
            PlaytimeMinutes = prior + Math.Max(1, run.DurationSeconds / 60),
            LastPlayed      = run.EndedAt,
            CreatedAt       = createdAt,
        };
        await proto.PutRecordAsync(StatsCollection, cfg.StatsRkey, stats);
    }

    private static JsonNode BuildGameRef(string atUri)
    {
        // games.gamesgamesgamesgames.actor.game#gameRef expected shape:
        // { "uri": "at://...", "cid": "..." } — we only have the at-uri for now, emit uri-only.
        var o = new JsonObject { ["uri"] = atUri };
        return o;
    }
}

