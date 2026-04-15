using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Sts2At;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.OnEnded))]
internal static class RunEndHook
{
    [HarmonyPostfix]
    public static void Postfix(RunManager __instance, bool isVictory, SerializableRun __result)
    {
        try
        {
            var run = RunStateExtractor.Extract(__instance, isVictory, __result);
            _ = Task.Run(async () =>
            {
                try { await RunPublisher.PublishAsync(run); }
                catch (Exception ex) { Log.Error("publish failed", ex); }
            });
        }
        catch (Exception ex)
        {
            Log.Error("failed to capture run-end state", ex);
        }
    }
}
