using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using AtprotoTracker.Signing;

namespace AtprotoTracker;

/// <summary>
/// One-shot pass at boot that walks the user's run records and tries to
/// resolve missing <c>allies[].atproto</c> DIDs via SteamDidResolver. Old
/// multiplayer records often shipped with steam-only allies because the
/// teammate hadn't yet published a keytrace claim; this backfills them
/// retroactively whenever the claim later appears.
///
/// Cheap to repeat thanks to <see cref="Config.AllyBackfillCheckpoint"/>:
/// records with rkey ≤ checkpoint are skipped without an HTTP probe, so a
/// boot with no new records does only a single listRecords call. TIDs are
/// 13-char base32 strings that sort lexicographically by time, so a string
/// comparison is enough to compare ages.
/// </summary>
internal static class AllyBackfill
{
    private const string Collection = "me.byjp.pesos.sts2.run";
    private const int PageLimit = 100;

    public static async Task RunAsync()
    {
        var client = Plugin.AtProto;
        var cfg    = Plugin.Config;
        if (!client.IsAuthenticated) return;

        var checkpoint = cfg.AllyBackfillCheckpoint;
        string? cursor = null;
        string? newestSeen = null;
        int scanned = 0, updated = 0;

        try
        {
            while (true)
            {
                var page = await client.ListRecordsAsync(Collection, cursor, PageLimit);
                var records = page?["records"]?.AsArray();
                if (records is null || records.Count == 0) break;

                foreach (var entry in records)
                {
                    if (entry is null) continue;
                    var uri  = entry["uri"]?.GetValue<string>() ?? "";
                    var slash = uri.LastIndexOf('/');
                    if (slash < 0 || slash == uri.Length - 1) continue;
                    var rkey = uri[(slash + 1)..];

                    // Track the largest rkey across the whole walk; persisted
                    // at the end so re-runs skip everything we've examined.
                    if (newestSeen is null || string.CompareOrdinal(rkey, newestSeen) > 0)
                        newestSeen = rkey;

                    if (!string.IsNullOrEmpty(checkpoint) &&
                        string.CompareOrdinal(rkey, checkpoint) <= 0) continue;

                    scanned++;
                    if (entry["value"] is JsonObject value &&
                        await TryBackfillAsync(client, rkey, value))
                        updated++;
                }

                cursor = page?["cursor"]?.GetValue<string>();
                if (string.IsNullOrEmpty(cursor)) break;
            }

            if (!string.IsNullOrEmpty(newestSeen) && newestSeen != checkpoint)
            {
                cfg.AllyBackfillCheckpoint = newestSeen;
                cfg.Save();
            }
            Log.Info($"ally backfill: scanned {scanned} new record(s), updated {updated}, checkpoint @ {newestSeen ?? "(none)"}");
        }
        catch (Exception ex)
        {
            Log.Warn($"ally backfill aborted: {ex.Message}");
        }
    }

    private static async Task<bool> TryBackfillAsync(AtProtoClient client, string rkey, JsonObject record)
    {
        if (record["allies"] is not JsonArray allies || allies.Count == 0) return false;

        bool changed = false;
        foreach (var ally in allies)
        {
            if (ally is not JsonObject obj) continue;
            if (!string.IsNullOrEmpty(obj["atproto"]?.GetValue<string>())) continue;
            var steamStr = obj["steam"]?.GetValue<string>();
            if (string.IsNullOrEmpty(steamStr)) continue;
            if (!ulong.TryParse(steamStr, out var steamId)) continue;

            var did = await SteamDidResolver.LookupDidAsync(steamId);
            if (string.IsNullOrEmpty(did)) continue;

            obj["atproto"] = did;
            changed = true;
        }

        if (!changed) return false;

        // Mutating the record body invalidates any previous inline signature.
        // Strip the existing one and re-sign if this build has a key, so the
        // backfilled record stays attestable when the production mod runs it.
        record.Remove("signatures");
        var priv = ModSigningKey.PrivateKey;
        var pub  = ModSigningKey.PublicDidKey;
        if (priv is not null && pub is not null)
        {
            var metadata = new JsonObject { ["$type"] = ModSigningKey.AttestationType };
            var attestation = InlineAttestation.CreateInline(record, metadata, client.Did, priv, pub);
            InlineAttestation.Append(record, attestation);
        }

        try
        {
            await client.PutRecordAsync(Collection, rkey, record);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"ally backfill putRecord failed for {rkey}: {ex.Message}");
            return false;
        }
    }
}
