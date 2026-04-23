using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Rooms;

namespace AtprotoTracker;

/// <summary>
/// Run-scoped accumulator for aggregated combat stats.
/// Subscribes to CombatManager events on Attach; Harmony patches on CombatHistory
/// funnel damage/card/potion events here via the On* methods.
/// </summary>
internal static class CombatStats
{
    private static readonly object _lock = new();
    private static bool _attached;

    public static bool IsActive { get { lock (_lock) return _attached; } }

    // Run totals
    private static int _combats;
    private static int _combatsWon;
    private static int _elitesWon;
    private static int _bossesWon;
    private static int _turns;
    private static int _longestCombat;
    private static int _damageDealt;
    private static int _damageTaken;
    private static int _biggestDamageDealt;
    private static int _biggestDamageTaken;
    private static int _biggestTurnDamageDealt;
    private static int _biggestTurnDamageTaken;
    private static int _cardsPlayed;
    private static int _cardsDrawn;
    private static int _cardsExhausted;
    private static int _potionsUsed;
    private static int _noDamageTurns;
    private static int _highestBlockInTurn;
    private static readonly Dictionary<int, int> _hitsDealtDistribution = new();
    private static readonly Dictionary<int, int> _hitsTakenDistribution = new();
    private static readonly Dictionary<string, int> _cardUseDistribution = new();

    // Per-combat / per-turn rolling state
    private static int _roundsThisCombat;
    private static int _damageDealtThisTurn;
    private static int _damageTakenThisTurn;
    private static int _blockThisTurn;
    private static CombatSide _currentSide = CombatSide.None;

    public static void Attach()
    {
        lock (_lock)
        {
            if (_attached) return;
            Zero();
            try
            {
                var cm = CombatManager.Instance;
                if (cm is null)
                {
                    Log.Warn("CombatStats: CombatManager.Instance null at attach — stats disabled for this run");
                    return;
                }
                cm.CombatSetUp += OnCombatSetUp;
                cm.CombatWon   += OnCombatWon;
                cm.TurnStarted += OnTurnStarted;
                cm.TurnEnded   += OnTurnEnded;
                _attached = true;
            }
            catch (Exception ex)
            {
                Log.Error("CombatStats.Attach failed", ex);
            }
        }
    }

    public static void Detach()
    {
        lock (_lock)
        {
            if (!_attached) return;
            try
            {
                var cm = CombatManager.Instance;
                if (cm is not null)
                {
                    cm.CombatSetUp -= OnCombatSetUp;
                    cm.CombatWon   -= OnCombatWon;
                    cm.TurnStarted -= OnTurnStarted;
                    cm.TurnEnded   -= OnTurnEnded;
                }
            }
            catch (Exception ex)
            {
                Log.Error("CombatStats.Detach failed", ex);
            }
            _attached = false;
        }
    }

    /// <summary>Populate a RunRecord's Stats if any combat occurred.</summary>
    public static void Populate(RunRecord record)
    {
        lock (_lock)
        {
            if (_combats == 0) return;
            record.Stats = new CombatStatsSnapshot
            {
                Combats                = _combats,
                CombatsWon             = _combatsWon,
                ElitesWon              = _elitesWon,
                BossesWon              = _bossesWon,
                Turns                  = _turns,
                LongestCombat          = _longestCombat,
                DamageDealt            = _damageDealt,
                DamageTaken            = _damageTaken,
                BiggestDamageDealt     = _biggestDamageDealt,
                BiggestDamageTaken     = _biggestDamageTaken,
                BiggestTurnDamageDealt = _biggestTurnDamageDealt,
                BiggestTurnDamageTaken = _biggestTurnDamageTaken,
                CardsPlayed            = _cardsPlayed,
                CardsDrawn             = _cardsDrawn,
                CardsExhausted         = _cardsExhausted,
                PotionsUsed            = _potionsUsed,
                NoDamageTurns          = _noDamageTurns,
                HighestBlockInTurn     = _highestBlockInTurn,
                HitsDealtDistribution = _hitsDealtDistribution.Count > 0
                    ? new Dictionary<int, int>(_hitsDealtDistribution)
                    : null,
                HitsTakenDistribution = _hitsTakenDistribution.Count > 0
                    ? new Dictionary<int, int>(_hitsTakenDistribution)
                    : null,
                CardUseDistribution = _cardUseDistribution.Count > 0
                    ? new Dictionary<string, int>(_cardUseDistribution)
                    : null,
            };
        }
    }

    // --- Entry points invoked from CombatHistoryHooks ---

    internal static void OnDamageReceived(Creature receiver, Creature? dealer, DamageResult result)
    {
        if (receiver is null || result is null) return;
        lock (_lock)
        {
            if (!_attached) return;
            int hpDamage = result.UnblockedDamage;
            int attackDamage = result.TotalDamage;

            bool dealerIsMe = dealer is not null && LocalContext.IsMe(dealer);
            bool receiverIsMe = LocalContext.IsMe(receiver);

            if (dealerIsMe && receiver.IsEnemy)
            {
                if (attackDamage > 0)
                    _hitsDealtDistribution[attackDamage] =
                        _hitsDealtDistribution.TryGetValue(attackDamage, out var c) ? c + 1 : 1;

                if (hpDamage > 0)
                {
                    _damageDealt += hpDamage;
                    if (hpDamage > _biggestDamageDealt) _biggestDamageDealt = hpDamage;
                    if (_currentSide == CombatSide.Player) _damageDealtThisTurn += hpDamage;
                }
            }

            if (receiverIsMe)
            {
                if (attackDamage > 0)
                    _hitsTakenDistribution[attackDamage] =
                        _hitsTakenDistribution.TryGetValue(attackDamage, out var c) ? c + 1 : 1;

                if (hpDamage > 0)
                {
                    _damageTaken += hpDamage;
                    if (hpDamage > _biggestDamageTaken) _biggestDamageTaken = hpDamage;
                    if (_currentSide == CombatSide.Enemy) _damageTakenThisTurn += hpDamage;
                }
            }
        }
    }

    internal static void OnBlockGained(Creature receiver, int amount)
    {
        if (receiver is null || amount <= 0) return;
        lock (_lock)
        {
            if (!_attached) return;
            if (LocalContext.IsMe(receiver) && _currentSide == CombatSide.Player)
                _blockThisTurn += amount;
        }
    }

    internal static void OnCardPlayFinished(string? cardId)
    {
        lock (_lock)
        {
            if (!_attached) return;
            _cardsPlayed++;
            if (!string.IsNullOrEmpty(cardId))
                _cardUseDistribution[cardId] =
                    _cardUseDistribution.TryGetValue(cardId, out var c) ? c + 1 : 1;
        }
    }

    internal static void OnCardDrawn()
    {
        lock (_lock) { if (_attached) _cardsDrawn++; }
    }

    internal static void OnCardExhausted()
    {
        lock (_lock) { if (_attached) _cardsExhausted++; }
    }

    internal static void OnPotionUsed()
    {
        lock (_lock) { if (_attached) _potionsUsed++; }
    }

    // --- CombatManager event handlers ---

    private static void OnCombatSetUp(CombatState state)
    {
        lock (_lock)
        {
            _combats++;
            _roundsThisCombat     = 0;
            _damageDealtThisTurn  = 0;
            _damageTakenThisTurn  = 0;
            _blockThisTurn        = 0;
            _currentSide          = state?.CurrentSide ?? CombatSide.None;
        }
    }

    private static void OnCombatWon(CombatRoom room)
    {
        lock (_lock)
        {
            _combatsWon++;
            switch (room?.RoomType)
            {
                case RoomType.Elite: _elitesWon++; break;
                case RoomType.Boss:  _bossesWon++; break;
            }
        }
    }

    private static void OnTurnStarted(CombatState state)
    {
        lock (_lock)
        {
            _currentSide = state?.CurrentSide ?? CombatSide.None;
            if (_currentSide == CombatSide.Player)
            {
                _damageDealtThisTurn = 0;
                _blockThisTurn       = 0;
            }
            else if (_currentSide == CombatSide.Enemy)
            {
                _damageTakenThisTurn = 0;
            }
        }
    }

    private static void OnTurnEnded(CombatState state)
    {
        lock (_lock)
        {
            if (_currentSide == CombatSide.Player)
            {
                _turns++;
                _roundsThisCombat++;
                if (_roundsThisCombat    > _longestCombat)      _longestCombat      = _roundsThisCombat;
                if (_damageDealtThisTurn > _biggestTurnDamageDealt) _biggestTurnDamageDealt = _damageDealtThisTurn;
                if (_blockThisTurn       > _highestBlockInTurn) _highestBlockInTurn = _blockThisTurn;
            }
            else if (_currentSide == CombatSide.Enemy)
            {
                if (_damageTakenThisTurn > _biggestTurnDamageTaken) _biggestTurnDamageTaken = _damageTakenThisTurn;
                if (_damageTakenThisTurn == 0) _noDamageTurns++;
            }
        }
    }

    private static void Zero()
    {
        _combats = _combatsWon = _elitesWon = _bossesWon = 0;
        _turns = _longestCombat = 0;
        _damageDealt = _damageTaken = _biggestDamageDealt = _biggestDamageTaken = 0;
        _biggestTurnDamageDealt = _biggestTurnDamageTaken = 0;
        _cardsPlayed = _cardsDrawn = _cardsExhausted = _potionsUsed = 0;
        _noDamageTurns = _highestBlockInTurn = 0;
        _hitsDealtDistribution.Clear();
        _hitsTakenDistribution.Clear();
        _cardUseDistribution.Clear();
        _roundsThisCombat = _damageDealtThisTurn = _damageTakenThisTurn = _blockThisTurn = 0;
        _currentSide = CombatSide.None;
    }
}
