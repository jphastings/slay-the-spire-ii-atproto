using System.Collections;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using static AtprotoTracker.RunStateExtractor;

namespace AtprotoTracker;

/// <summary>
/// Aggregates per-player stats out of the game's <c>MapPointHistory</c> —
/// a list-of-lists of <c>MapPointHistoryEntry</c>, each carrying one
/// <c>PlayerMapPointHistoryEntry</c> per player. The game already counts gold
/// flow, healing, and current values as the run progresses; we just sum
/// across map points keyed to the local player.
/// </summary>
internal static class MapPointStats
{
    public static void Populate(RunRecord record, RunState? state)
    {
        if (state is null) return;
        var me = LocalContext.GetMe(state);
        Apply(record, state, (ulong)GetLong(me, "NetId"));
    }

    public static void Populate(RunRecord record, SerializableRun? serialized)
    {
        if (serialized is null) return;
        var me = LocalContext.GetMe(serialized);
        Apply(record, serialized, (ulong)GetLong(me, "NetId"));
    }

    private static void Apply(RunRecord record, object container, ulong myNetId)
    {
        if (record.Stats is null || myNetId == 0) return;
        if (GetMember(container, "MapPointHistory") is not IEnumerable acts) return;

        int goldEarned = 0, goldSpent = 0, goldCurrent = 0, healingReceived = 0;
        bool sawAny = false;

        foreach (var act in acts)
        {
            if (act is not IEnumerable mapPoints) continue;
            foreach (var entry in mapPoints)
            {
                if (GetMember(entry, "PlayerStats") is not IEnumerable playerStats) continue;
                foreach (var ps in playerStats)
                {
                    if ((ulong)GetLong(ps, "PlayerId") != myNetId) continue;
                    sawAny = true;
                    goldEarned      += (int)GetLong(ps, "GoldGained");
                    goldSpent       += (int)GetLong(ps, "GoldSpent");
                    healingReceived += (int)GetLong(ps, "HpHealed");
                    // Last seen wins — map-point order is chronological.
                    goldCurrent      = (int)GetLong(ps, "CurrentGold");
                }
            }
        }

        if (!sawAny) return;

        record.Stats.GoldEarned      = goldEarned;
        record.Stats.GoldSpent       = goldSpent;
        record.Stats.GoldCurrent     = goldCurrent;
        record.Stats.HealingReceived = healingReceived;
    }
}
