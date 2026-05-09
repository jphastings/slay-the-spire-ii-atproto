using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AtprotoTracker.Signing;

namespace AtprotoTracker;

internal static class RunPublisher
{
    private const string StatsCollection = "games.gamesgamesgamesgames.actor.stats";
    private const string RunCollection   = "me.byjp.pesos.sts2.run";
    private const string GameRef         = "at://did:web:gamesgamesgamesgames.games/games.gamesgamesgamesgames.game/3mglj4k2edl2l";

    /// <summary>Create or update the in-progress run record (start + mid-run).</summary>
    public static async Task PublishUpdateAsync(RunRecord run, string rkey)
    {
        run.Game = GameRef;
        var did = AuthState.Did;

        if (AuthState.Status != AuthStatus.Ok)
        {
            QueueIfPossible(run, rkey, did, reason: $"unauthenticated (status={AuthState.Status})");
            return;
        }

        var payload = PreparePayload(run, did!);
        try
        {
            await Plugin.AtProto.PutRecordAsync(RunCollection, rkey, payload);
            // Online publish supersedes any stale queued snapshot for this run.
            Outbox.RemoveRun(did!, rkey);
        }
        catch (Exception ex) when (IsPermanentRejection(ex))
        {
            Log.Error($"PDS rejected run update {rkey} — dropping", ex);
        }
        catch (Exception ex)
        {
            Log.Warn($"run update {rkey} failed ({ex.Message}) — queueing");
            Outbox.EnqueueRun(did!, rkey, payload);
        }
    }

    /// <summary>Final update with outcome, then update stats.</summary>
    public static async Task PublishFinalAsync(RunRecord run, string rkey)
    {
        run.Game = GameRef;
        var did = AuthState.Did;

        if (AuthState.Status != AuthStatus.Ok)
        {
            QueueIfPossible(run, rkey, did, reason: $"unauthenticated (status={AuthState.Status})");
            return;
        }

        // 1. Stats record (best-effort: a stats failure shouldn't lose the run record).
        try
        {
            var statsAtUri = await EnsureStatsRecordAsync(run);
            run.StatsRef = statsAtUri;
        }
        catch (Exception ex)
        {
            Log.Warn($"stats ensure failed ({ex.Message}) — continuing without statsRef");
        }

        // 2. Final put of the run record.
        var payload = PreparePayload(run, did!);
        try
        {
            var runUri = await Plugin.AtProto.PutRecordAsync(RunCollection, rkey, payload);
            Outbox.RemoveRun(did!, rkey);
            Log.Info($"posted run: {runUri}");
        }
        catch (Exception ex) when (IsPermanentRejection(ex))
        {
            Log.Error($"PDS rejected final run {rkey} — dropping", ex);
            return;
        }
        catch (Exception ex)
        {
            Log.Warn($"final run {rkey} failed ({ex.Message}) — queueing; stats will roll up at flush");
            Outbox.EnqueueRun(did!, rkey, payload);
            return;
        }

        // 3. Update rolling stats (only when run record landed online).
        try { await UpdateStatsAsync(run); }
        catch (Exception ex) { Log.Warn($"stats update failed: {ex.Message}"); }
    }

    /// <summary>
    /// Adds <paramref name="deltaMinutes"/> playtime to the on-PDS stats
    /// record and bumps lastPlayed if <paramref name="lastPlayed"/> is newer.
    /// Used by the outbox flush to roll up queued runs in a single PUT.
    /// </summary>
    public static async Task MergeStatsDeltaAsync(int deltaMinutes, string lastPlayed)
    {
        var cfg   = Plugin.Config;
        var proto = Plugin.AtProto;

        // Make sure the rolling record exists; EnsureStatsRecord seeds it from
        // a synthetic first run if missing.
        if (string.IsNullOrEmpty(cfg.StatsRkey))
        {
            await EnsureStatsRecordAsync(new RunRecord
            {
                EndedAt = lastPlayed,
                DurationSeconds = deltaMinutes * 60,
            });
            return; // Seeded with the full delta — no further increment needed.
        }

        var existing  = await proto.GetRecordAsync(StatsCollection, cfg.StatsRkey);
        var prior     = 0;
        var createdAt = lastPlayed;
        var priorLast = "";
        if (existing?["value"] is JsonNode value)
        {
            prior     = value["playtime"]?.GetValue<int>() ?? 0;
            createdAt = value["createdAt"]?.GetValue<string>() ?? createdAt;
            priorLast = value["lastPlayed"]?.GetValue<string>() ?? "";
        }
        var stats = new StatsRecord
        {
            Game            = BuildGameRef(),
            Source          = "steam",
            PlaytimeMinutes = prior + deltaMinutes,
            LastPlayed      = string.CompareOrdinal(lastPlayed, priorLast) > 0 ? lastPlayed : priorLast,
            CreatedAt       = createdAt,
        };
        await proto.PutRecordAsync(StatsCollection, cfg.StatsRkey, stats);
    }

    /// <summary>
    /// True for HTTP 4xx (record-shape rejections, auth errors). Such failures
    /// won't be fixed by retrying offline, so we drop rather than queue.
    /// </summary>
    public static bool IsPermanentRejection(Exception ex)
    {
        if (ex is HttpRequestException hre && hre.StatusCode is { } sc)
        {
            var code = (int)sc;
            return code >= 400 && code < 500;
        }
        return false;
    }

    private static void QueueIfPossible(RunRecord run, string rkey, string? did, string reason)
    {
        if (string.IsNullOrEmpty(did))
        {
            Log.Warn($"can't queue run {rkey}: {reason} and no DID resolved this session — dropping");
            return;
        }
        var payload = PreparePayload(run, did!);
        Outbox.EnqueueRun(did!, rkey, payload);
    }

    /// <summary>
    /// Serializes the record and (if a signing key is embedded) appends a
    /// CID-first inline attestation bound to <paramref name="repoDid"/>. The
    /// returned JsonNode is the exact bytes we'll PUT to the PDS.
    /// </summary>
    private static JsonNode PreparePayload(RunRecord run, string repoDid)
    {
        var json = JsonSerializer.SerializeToNode(run)
                   ?? throw new InvalidOperationException("run serialized to null");
        if (json is not JsonObject record) return json;

        var priv = ModSigningKey.PrivateKey;
        var pub  = ModSigningKey.PublicDidKey;
        if (priv is null || pub is null) return record;

        var metadata = new JsonObject { ["$type"] = ModSigningKey.AttestationType };
        var attestation = InlineAttestation.CreateInline(record, metadata, repoDid, priv, pub);
        InlineAttestation.Append(record, attestation);
        return record;
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
