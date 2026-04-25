using System;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AtprotoTracker;

// --- Run start: create the initial "in_progress" record ---

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class RunStartHook
{
    private static readonly FieldInfo? StartTimeField =
        typeof(RunManager).GetField("_startTime", BindingFlags.NonPublic | BindingFlags.Instance);

    [HarmonyPostfix]
    public static void Postfix(RunManager __instance, RunState __result)
    {
        try
        {
            var startTime = StartTimeField is not null
                ? (long)StartTimeField.GetValue(__instance)!
                : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var seed = (uint)RunStateExtractor.GetLong(__result, "Rng", "Seed");

            RunTracker.Begin(startTime, seed);
            CombatStats.Attach();

            var run = RunStateExtractor.ExtractLive(__instance, __result);
            run.Outcome = "in_progress";
            run.StartedAt = Iso.At(DateTimeOffset.FromUnixTimeSeconds(startTime).UtcDateTime);
            RunTracker.PublishUpdate(run);

            __instance.RoomExited += OnRoomExited;
            SaveManager.Instance.Saved += OnSaved;
        }
        catch (Exception ex)
        {
            Log.Error("failed to capture run start", ex);
        }
    }

    internal static void OnRoomExited() => PublishLive("room exit");

    internal static void OnSaved() => PublishLive("save");

    private static void PublishLive(string trigger)
    {
        try
        {
            var manager = RunManager.Instance;
            if (manager is null || !RunTracker.IsTracking) return;
            var state = manager.DebugOnlyGetState();
            if (state is null) return;

            // If the game has flagged the run as over but RunManager.OnEnded
            // hasn't fired yet (e.g. player quits from the game over screen
            // without clicking "Return to Main Menu"), treat this autosave as
            // the terminal write so we don't leave the record "in_progress".
            if (RunStateExtractor.GetBool(state, "IsGameOver"))
            {
                FinalizeFromLive(manager, state);
                return;
            }

            var run = RunStateExtractor.ExtractLive(manager, state);
            run.Outcome = "in_progress";
            CombatStats.Populate(run);
            MapPointStats.Populate(run, state);
            RunTracker.PublishUpdate(run);
        }
        catch (Exception ex)
        {
            Log.Error($"failed to capture {trigger}", ex);
        }
    }

    private static void FinalizeFromLive(RunManager manager, RunState state)
    {
        manager.RoomExited -= OnRoomExited;
        SaveManager.Instance.Saved -= OnSaved;

        var run = RunStateExtractor.ExtractFromLiveOnGameOver(manager, state);
        CombatStats.Populate(run);
        MapPointStats.Populate(run, state);
        CombatStats.Detach();
        Log.Info($"finalizing from live state: outcome={run.Outcome}");
        RunTracker.PublishFinal(run);
    }
}

// --- Run end: final update with outcome + stats ---

[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
internal static class RunEndHook
{
    [HarmonyPostfix]
    public static void Postfix(RunManager __instance, bool isVictory, SerializableRun __result)
    {
        try
        {
            __instance.RoomExited -= RunStartHook.OnRoomExited;
            SaveManager.Instance.Saved -= RunStartHook.OnSaved;

            var run = RunStateExtractor.Extract(__instance, isVictory, __result);
            CombatStats.Populate(run);
            MapPointStats.Populate(run, __result);
            CombatStats.Detach();
            RunTracker.PublishFinal(run);
        }
        catch (Exception ex)
        {
            Log.Error("failed to capture run end", ex);
        }
    }
}
