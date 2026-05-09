using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AtprotoTracker;

/// <summary>
/// On-disk per-DID queue of run-record PUTs that couldn't be delivered.
/// Records are stored already-signed and ready to PUT byte-for-byte; flushing
/// is just an XRPC call. Buckets are keyed by the DID the record was signed
/// against, so they only flush when that same DID is the currently-logged-in
/// account — even across game restarts and account switches.
/// </summary>
internal static class Outbox
{
    private const string RunCollection = "me.byjp.pesos.sts2.run";

    private static readonly object _flushLock = new();
    private static bool _flushing;

    private static string Root
    {
        get
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            return Path.Combine(dir, "outbox");
        }
    }

    private static string DidDir(string did) => Path.Combine(Root, EncodeDid(did));
    private static string RunsDir(string did) => Path.Combine(DidDir(did), "runs");

    /// <summary>Atomically write the prepared record JSON for (did, rkey).</summary>
    public static void EnqueueRun(string did, string rkey, JsonNode payload)
    {
        var dir = RunsDir(did);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, rkey + ".json");
        var tmp  = path + ".tmp";
        File.WriteAllText(tmp, payload.ToJsonString(), new UTF8Encoding(false));
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
        Log.Info($"queued run {rkey} for did={did} (offline/transient failure)");
    }

    /// <summary>
    /// Best-effort removal — called after a successful online publish so a
    /// stale queued snapshot of the same run doesn't get re-PUT later.
    /// </summary>
    public static void RemoveRun(string did, string rkey)
    {
        try
        {
            var path = Path.Combine(RunsDir(did), rkey + ".json");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) { Log.Warn($"outbox: couldn't remove {rkey}: {ex.Message}"); }
    }

    /// <summary>Drains the queue for the currently-authenticated DID.</summary>
    public static async Task FlushAsync()
    {
        var did = AuthState.Did;
        if (did is null || AuthState.Status != AuthStatus.Ok) return;

        // Single-flight: if a flush is already running we return; the in-flight
        // one will see anything we'd have processed.
        lock (_flushLock)
        {
            if (_flushing) return;
            _flushing = true;
        }

        try
        {
            var dir = RunsDir(did);
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.json");
            if (files.Length == 0) return;
            Log.Info($"outbox: flushing {files.Length} queued run(s) for {did}");

            var statsDeltaMinutes = 0;
            string? latestEndedAt = null;
            var activeRkey = RunTracker.Rkey;

            foreach (var path in files)
            {
                var rkey = Path.GetFileNameWithoutExtension(path);
                // The currently-active run is the one we'd race against — leave
                // its queue file for the next active-update cycle.
                if (rkey == activeRkey) continue;

                JsonNode? payload;
                try
                {
                    payload = JsonNode.Parse(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    Log.Warn($"outbox: corrupt file {path} ({ex.Message}) — discarding");
                    TryDelete(path);
                    continue;
                }
                if (payload is null) { TryDelete(path); continue; }

                try
                {
                    await Plugin.AtProto.PutRecordAsync(RunCollection, rkey, payload);
                }
                catch (Exception ex) when (RunPublisher.IsPermanentRejection(ex))
                {
                    Log.Error($"outbox: PDS rejected queued {rkey} — discarding", ex);
                    TryDelete(path);
                    continue;
                }
                catch (Exception ex)
                {
                    // Transient — leave on disk for next attempt.
                    Log.Warn($"outbox: transient failure flushing {rkey} ({ex.Message}); will retry");
                    return;
                }

                // PUT succeeded. Tally stats from finalized runs before deleting.
                var endedAt = payload["endedAt"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(endedAt))
                {
                    var dur = payload["durationSeconds"]?.GetValue<int>() ?? 0;
                    statsDeltaMinutes += Math.Max(1, dur / 60);
                    if (latestEndedAt is null || string.CompareOrdinal(endedAt, latestEndedAt) > 0)
                        latestEndedAt = endedAt;
                }
                TryDelete(path);
            }

            if (statsDeltaMinutes > 0 && latestEndedAt is not null)
            {
                try { await RunPublisher.MergeStatsDeltaAsync(statsDeltaMinutes, latestEndedAt); }
                catch (Exception ex) { Log.Warn($"outbox: stats merge failed ({ex.Message}); will retry on next flush"); }
            }
        }
        finally
        {
            lock (_flushLock) { _flushing = false; }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) { Log.Warn($"outbox: couldn't delete {path}: {ex.Message}"); }
    }

    // DIDs contain ':' which Windows forbids in filenames. Percent-encode the
    // few problematic characters; the result round-trips and stays readable.
    private static string EncodeDid(string did)
    {
        var sb = new StringBuilder(did.Length + 8);
        foreach (var c in did)
        {
            if (c is ':' or '/' or '\\' or '?' or '*' or '"' or '<' or '>' or '|')
                sb.Append('%').Append(((int)c).ToString("X2"));
            else sb.Append(c);
        }
        return sb.ToString();
    }
}
