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

        return new RunRecord
        {
            Outcome         = "in_progress",
            Character       = GetString(me, "Character", "Id") != "" ? GetString(me, "Character", "Id") : GetString(me, "CharacterId"),
            Ascension       = (int)GetLong(state, "AscensionLevel"),
            Seed            = state.Rng?.StringSeed ?? "",
            Floor      = (int)GetLong(state, "TotalFloor"),
            Act        = (int)GetLong(state, "CurrentActIndex") + 1,
            Deck            = CollectIds(me, "Deck", "Cards"),
            Relics          = CollectIds(me, "Relics"),
            Potions         = CollectIds(me, "Potions"),
            Allies          = CollectAllies(state, GetULong(me, "NetId")),
            UpdatedAt       = Iso.Now(),
        };
    }

    /// <summary>Extract final state from SerializableRun (run end).</summary>
    public static RunRecord Extract(RunManager manager, bool isVictory, SerializableRun serialized)
    {
        var state = manager.DebugOnlyGetState();
        var me    = LocalContext.GetMe(serialized);

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
            Floor      = (int)GetLong(state, "TotalFloor"),
            Act        = (int)GetLong(state, "CurrentActIndex") + 1,
            StartedAt       = Iso.At(startedAt),
            EndedAt         = Iso.At(endedAt),
            DurationSeconds = (int)duration,
            Deck            = CollectIds(me, "Deck"),
            Relics          = CollectIds(me, "Relics"),
            Potions         = CollectIds(me, "Potions"),
            Allies          = CollectAllies(serialized, GetULong(me, "NetId")),
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
            null    => 0,
            long l  => l,
            int i   => i,
            uint u  => u,
            float f => (long)f,
            double d => (long)d,
            _       => long.TryParse(v.ToString(), out var x) ? x : 0,
        };
    }

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

    private static List<string>? CollectAllies(object? container, ulong myNetId)
    {
        if (GetMember(container, "Players") is not IEnumerable players) return null;
        var allies = new List<string>();
        int total = 0;
        foreach (var p in players)
        {
            total++;
            var netId = GetULong(p, "NetId");
            if (netId == 0 || netId == myNetId) continue;
            allies.Add(SteamDidResolver.ResolveUri(netId));
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
}

internal static class Iso
{
    public static string Now() => At(DateTime.UtcNow);
    public static string At(DateTime dt) =>
        dt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ",
            System.Globalization.CultureInfo.InvariantCulture);
}
