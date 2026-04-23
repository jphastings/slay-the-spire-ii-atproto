using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AtprotoTracker;

internal static class RunStateExtractor
{
    /// <summary>Extract from live RunState (run start + mid-run updates).</summary>
    public static RunRecord ExtractLive(RunManager manager, RunState state)
    {
        // RunState implements IPlayerCollection; this picks the local player even in multiplayer.
        var me = LocalContext.GetMe(state);
        var mySteamId = GetULong(me, "NetId");

        return new RunRecord
        {
            Outcome         = "in_progress",
            Character       = GetString(me, "Character", "Id") != "" ? GetString(me, "Character", "Id") : GetString(me, "CharacterId"),
            Ascension       = (int)GetLong(state, "AscensionLevel"),
            Seed            = state.Rng?.StringSeed ?? "",
            SteamID64       = mySteamId > 0 ? mySteamId.ToString() : null,
            Floor      = (int)GetLong(state, "TotalFloor"),
            Act        = (int)GetLong(state, "CurrentActIndex") + 1,
            Deck            = CollectDeckIds(me, "Deck", "Cards"),
            Relics          = CollectIds(me, "Relics"),
            Potions         = CollectIds(me, "Potions"),
            Allies          = CollectAllies(state, mySteamId),
            UpdatedAt       = Iso.Now(),
        };
    }

    /// <summary>
    /// Build a terminal record from live state when RunState.IsGameOver flips
    /// true before RunManager.OnEnded fires. Outcome is inferred from
    /// IsAbandoned + local player HP (alive on a game-over = victory).
    /// </summary>
    public static RunRecord ExtractFromLiveOnGameOver(RunManager manager, RunState state)
    {
        var run = ExtractLive(manager, state);

        var me = LocalContext.GetMe(state);
        var alive = GetLong(me, "CurrentHp") > 0;
        var isAbandoned = GetBool(manager, "IsAbandoned");

        run.Outcome = isAbandoned ? "abandoned"
                    : alive       ? "victory"
                    :               "death";

        var endedAt = DateTime.UtcNow;
        var duration = GetLong(manager, "RunTime");
        run.EndedAt = Iso.At(endedAt);
        run.DurationSeconds = (int)duration;
        return run;
    }

    /// <summary>Extract final state from SerializableRun (run end).</summary>
    public static RunRecord Extract(RunManager manager, bool isVictory, SerializableRun serialized)
    {
        var state     = manager.DebugOnlyGetState();
        var me        = LocalContext.GetMe(serialized);
        var mySteamId = GetULong(me, "NetId");

        var outcome = isVictory            ? "victory"
                    : manager.IsAbandoned  ? "abandoned"
                    :                        "death";

        var endedAt   = DateTime.UtcNow;
        var duration  = isVictory ? GetLong(serialized, "WinTime") : GetLong(serialized, "RunTime");
        if (duration <= 0) duration = GetLong(serialized, "RunTime");
        var startedAt = duration > 0 ? endedAt.AddSeconds(-duration) : endedAt;

        return new RunRecord
        {
            Outcome         = outcome,
            Character       = GetString(me, "CharacterId"),
            Ascension       = (int)GetLong(serialized, "Ascension"),
            Seed            = state?.Rng?.StringSeed ?? GetString(serialized, "SerializableRng", "Seed"),
            SteamID64       = mySteamId > 0 ? mySteamId.ToString() : null,
            Floor      = (int)GetLong(state, "TotalFloor"),
            Act        = (int)GetLong(state, "CurrentActIndex") + 1,
            StartedAt       = Iso.At(startedAt),
            EndedAt         = Iso.At(endedAt),
            DurationSeconds = (int)duration,
            Deck            = CollectDeckIds(me, "Deck"),
            Relics          = CollectIds(me, "Relics"),
            Potions         = CollectIds(me, "Potions"),
            Allies          = CollectAllies(serialized, mySteamId),
            UpdatedAt       = Iso.At(endedAt),
        };
    }

    // --- Reflection helpers (public for use from RunLifecycleHooks) ---

    public static object? GetMember(object? obj, string name)
    {
        if (obj is null) return null;
        var t = obj.GetType();
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p is not null) return p.GetValue(obj);
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        return f?.GetValue(obj);
    }

    public static object? GetMember(object? obj, params string[] path)
    {
        foreach (var seg in path)
        {
            obj = GetMember(obj, seg);
            if (obj is null) return null;
        }
        return obj;
    }

    public static long GetLong(object? obj, params string[] path)
    {
        var v = GetMember(obj, path);
        return v switch
        {
            null      => 0,
            long l    => l,
            int i     => i,
            uint u    => u,
            float f   => (long)f,
            double d  => (long)d,
            decimal m => (long)m,
            _         => long.TryParse(v.ToString(), out var x) ? x : 0,
        };
    }

    public static bool GetBool(object? obj, params string[] path)
        => GetMember(obj, path) is bool b && b;

    public static ulong GetULong(object? obj, params string[] path)
    {
        var v = GetMember(obj, path);
        return v switch
        {
            null      => 0UL,
            ulong ul  => ul,
            long l    => (ulong)l,
            uint u    => u,
            int i     => (ulong)i,
            _         => ulong.TryParse(v.ToString(), out var x) ? x : 0UL,
        };
    }

    private static string GetString(object? obj, params string[] path)
        => GetMember(obj, path)?.ToString() ?? "";

    private static List<AllyEntry>? CollectAllies(object? container, ulong myNetId)
    {
        if (GetMember(container, "Players") is not IEnumerable players) return null;
        var allies = new List<AllyEntry>();
        int total = 0;
        foreach (var p in players)
        {
            total++;
            var netId = GetULong(p, "NetId");
            if (netId == 0 || netId == myNetId) continue;
            allies.Add(SteamDidResolver.ResolveAlly(netId));
        }
        return total > 1 ? allies : null;
    }

    private static List<string> CollectIds(object? owner, params string[] path)
    {
        var list = new List<string>();
        object? target = owner;
        foreach (var seg in path)
        {
            target = GetMember(target, seg);
            if (target is null) return list;
        }
        if (target is not IEnumerable seq) return list;
        foreach (var item in seq)
        {
            var id = GetMember(item, "Id") ?? GetMember(item, "CardId") ?? GetMember(item, "Name") ?? item;
            if (id is not null) list.Add(id.ToString()!);
        }
        return list;
    }

    /// <summary>
    /// Like <see cref="CollectIds"/>, but annotates each id with upgrade
    /// and enchantment state so the web client can render the exact
    /// visual variant without any lexicon change:
    ///   "bash"              — plain
    ///   "bash+"             — upgraded
    ///   "bash/sharp"        — base card carrying the Sharp enchantment
    ///   "bash+/perfect_fit" — upgraded + Perfect Fit enchantment
    /// Enchantment ids are the game's own snake_case <c>Id.Entry</c>
    /// strings (e.g. "sharp", "slumbering_essence"). Combat use stats
    /// (cardUseDistribution) intentionally keep the plain id.
    /// </summary>
    private static List<string> CollectDeckIds(object? owner, params string[] path)
    {
        var list = new List<string>();
        object? target = owner;
        foreach (var seg in path)
        {
            target = GetMember(target, seg);
            if (target is null) return list;
        }
        if (target is not IEnumerable seq) return list;
        foreach (var item in seq)
        {
            var id = GetMember(item, "Id") ?? GetMember(item, "CardId") ?? GetMember(item, "Name") ?? item;
            if (id is null) continue;
            var s = id.ToString()!;
            if (GetMember(item, "IsUpgraded") is bool up && up) s += "+";
            if (GetMember(item, "Enchantment", "Id", "Entry") is string enchantId && enchantId.Length > 0)
            {
                s += "/" + enchantId;
            }
            list.Add(s);
        }
        return list;
    }
}

internal static class Iso
{
    public static string Now() => At(DateTime.UtcNow);
    public static string At(DateTime dt) =>
        dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ",
            System.Globalization.CultureInfo.InvariantCulture);
}
