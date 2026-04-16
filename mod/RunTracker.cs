using System;
using System.Threading.Tasks;

namespace AtprotoTracker;

/// <summary>
/// Manages the lifecycle of the current in-progress run record.
/// Stores the deterministic rkey and orchestrates create/update/finalize writes.
/// </summary>
internal static class RunTracker
{
    private static readonly object _lock = new();
    private static string? _rkey;
    private static long _startTime;
    private static uint _seed;

    public static string? Rkey { get { lock (_lock) return _rkey; } }
    public static bool IsTracking => Rkey is not null;

    public static void Begin(long startTimeUnixSeconds, uint gameSeed)
    {
        lock (_lock)
        {
            _startTime = startTimeUnixSeconds;
            _seed = gameSeed;
            _rkey = Tid.FromRun(startTimeUnixSeconds, gameSeed);
        }
        Log.Info($"tracking run rkey={_rkey} (start={_startTime}, seed={_seed})");
    }

    public static void PublishUpdate(RunRecord run)
    {
        var rkey = Rkey;
        if (rkey is null) return;
        run.UpdatedAt = Iso.Now();
        _ = Task.Run(async () =>
        {
            try { await RunPublisher.PublishUpdateAsync(run, rkey); }
            catch (Exception ex) { Log.Error("run update failed", ex); }
        });
    }

    public static void PublishFinal(RunRecord run)
    {
        var rkey = Rkey;
        if (rkey is null) return;
        run.UpdatedAt = Iso.Now();
        _ = Task.Run(async () =>
        {
            try { await RunPublisher.PublishFinalAsync(run, rkey); }
            catch (Exception ex) { Log.Error("run final publish failed", ex); }
        });
        lock (_lock) { _rkey = null; }
    }
}
