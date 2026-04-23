using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace AtprotoTracker;

// Postfix patches on CombatHistory let us tally damage/card/potion events without
// having to subscribe per-creature. The game invokes these methods from its own
// combat machinery regardless of multiplayer side; CombatStats filters by local
// player identity where needed.

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.DamageReceived))]
internal static class DamageReceivedPatch
{
    [HarmonyPostfix]
    public static void Postfix(Creature receiver, Creature? dealer, DamageResult result)
    {
        try { CombatStats.OnDamageReceived(receiver, dealer, result); }
        catch (Exception ex) { Log.Error("DamageReceivedPatch failed", ex); }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.BlockGained))]
internal static class BlockGainedPatch
{
    [HarmonyPostfix]
    public static void Postfix(Creature receiver, int amount)
    {
        try { CombatStats.OnBlockGained(receiver, amount); }
        catch (Exception ex) { Log.Error("BlockGainedPatch failed", ex); }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardPlayFinished))]
internal static class CardPlayFinishedPatch
{
    [HarmonyPostfix]
    public static void Postfix(CardPlay cardPlay)
    {
        try { CombatStats.OnCardPlayFinished(cardPlay?.Card?.Id?.ToString()); }
        catch (Exception ex) { Log.Error("CardPlayFinishedPatch failed", ex); }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardDrawn))]
internal static class CardDrawnPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        try { CombatStats.OnCardDrawn(); }
        catch (Exception ex) { Log.Error("CardDrawnPatch failed", ex); }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.CardExhausted))]
internal static class CardExhaustedPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        try { CombatStats.OnCardExhausted(); }
        catch (Exception ex) { Log.Error("CardExhaustedPatch failed", ex); }
    }
}

[HarmonyPatch(typeof(CombatHistory), nameof(CombatHistory.PotionUsed))]
internal static class PotionUsedPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        try { CombatStats.OnPotionUsed(); }
        catch (Exception ex) { Log.Error("PotionUsedPatch failed", ex); }
    }
}
