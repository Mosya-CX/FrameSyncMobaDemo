using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    /// <summary>Kill streak broadcast tier.</summary>
    public enum KillStreakTier : byte
    {
        None = 0,
        KillingSpree = 3,
        Dominating = 5,
        Unstoppable = 7,
        Legendary = 10,
    }

    /// <summary>Multikill broadcast tier.</summary>
    public enum MultikillTier : byte
    {
        None = 0,
        DoubleKill = 2,
        TripleKill = 3,
        QuadraKill = 4,
        PentaKill = 5,
    }

    /// <summary>Presentation event for kill streak broadcasts.</summary>
    public struct KillStreakEvent
    {
        public int PlayerSlot;
        public KillStreakTier Tier;
        public int StreakCount;
    }

    /// <summary>Presentation event for multikill broadcasts.</summary>
    public struct MultikillEvent
    {
        public int PlayerSlot;
        public MultikillTier Tier;
    }

    /// <summary>Per-death recap entry for "what killed me" UI.</summary>
    public struct DeathRecapEntry
    {
        public UnitUid SourceUnitUid;
        public int DamageAmount;
        public int AbilityId;
        public int Tick;
        public bool IsAttack;
    }

    /// <summary>
    /// Tracks kill streaks, multikills, and death recap data.
    /// All data is deterministic (uses stable collections and tick-based ordering).
    /// Kill streak and multikill events are presentation-only.
    /// </summary>
    public sealed class MatchEventTracker
    {
        // Kill streak tracking: playerSlot -> current consecutive kills
        private readonly Dictionary<int, int> _killStreaks = new Dictionary<int, int>();

        // Multikill tracking: recent kills within the window
        private readonly int multikillWindowTicks;
        private readonly List<(int PlayerSlot, int KillTick)> _recentKills = new List<(int, int)>();

        // Death recap: per-unit damage log
        private const int MaxRecapEntries = 5;
        private readonly Dictionary<UnitUid, List<DeathRecapEntry>> _deathRecaps = new Dictionary<UnitUid, List<DeathRecapEntry>>();

        // Shutdown gold config
        public int BaseShutdownGold = 100;
        public int GoldPerStreakKill = 50;

        // Events to be consumed by presentation
        public readonly List<KillStreakEvent> PendingKillStreakEvents = new List<KillStreakEvent>();
        public readonly List<MultikillEvent> PendingMultikillEvents = new List<MultikillEvent>();

        public MatchEventTracker(int tickRate = 30)
        {
            multikillWindowTicks =
                DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(300, tickRate);
        }

        /// <summary>Get the current kill streak for a player slot.</summary>
        public int GetKillStreak(int playerSlot)
        {
            _killStreaks.TryGetValue(playerSlot, out int streak);
            return streak;
        }

        /// <summary>Record a kill. Returns the shutdown gold amount if the victim was on a streak.</summary>
        public int RecordKill(int killerPlayerSlot, int victimPlayerSlot, int killTick)
        {
            PendingKillStreakEvents.Clear();
            PendingMultikillEvents.Clear();

            // Reset victim streak
            _killStreaks.TryGetValue(victimPlayerSlot, out int victimStreak);
            _killStreaks[victimPlayerSlot] = 0;

            // Compute shutdown gold for killing a streaking player
            int shutdownGold = 0;
            if (victimStreak >= (int)KillStreakTier.KillingSpree)
            {
                shutdownGold = BaseShutdownGold + (victimStreak - (int)KillStreakTier.KillingSpree) * GoldPerStreakKill;
            }

            // Increment killer streak
            if (!_killStreaks.ContainsKey(killerPlayerSlot))
                _killStreaks[killerPlayerSlot] = 0;
            _killStreaks[killerPlayerSlot]++;
            int killerStreak = _killStreaks[killerPlayerSlot];

            // Check kill streak threshold
            KillStreakTier streakTier = GetKillStreakTier(killerStreak);
            if (streakTier != KillStreakTier.None)
            {
                // Only fire when exactly hitting the threshold
                bool isExactThreshold = killerStreak == (int)streakTier;
                if (isExactThreshold)
                {
                    PendingKillStreakEvents.Add(new KillStreakEvent
                    {
                        PlayerSlot = killerPlayerSlot,
                        Tier = streakTier,
                        StreakCount = killerStreak,
                    });
                }
            }

            // Update recent kills for multikill tracking
            _recentKills.Add((killerPlayerSlot, killTick));
            PruneRecentKills(killTick);

            // Check multikill
            int samePlayerKills = 0;
            for (int i = 0; i < _recentKills.Count; i++)
            {
                if (_recentKills[i].PlayerSlot == killerPlayerSlot)
                    samePlayerKills++;
            }

            MultikillTier multiTier = GetMultikillTier(samePlayerKills);
            if (multiTier != MultikillTier.None)
            {
                PendingMultikillEvents.Add(new MultikillEvent
                {
                    PlayerSlot = killerPlayerSlot,
                    Tier = multiTier,
                });
            }

            return shutdownGold;
        }

        /// <summary>Record damage dealt to a unit for death recap.</summary>
        public void RecordDamage(UnitUid targetUid, UnitUid sourceUid, int damageAmount, int abilityId, int tick, bool isAttack)
        {
            if (damageAmount <= 0) return;

            if (!_deathRecaps.TryGetValue(targetUid, out var list))
            {
                list = new List<DeathRecapEntry>(MaxRecapEntries);
                _deathRecaps[targetUid] = list;
            }

            list.Insert(0, new DeathRecapEntry
            {
                SourceUnitUid = sourceUid,
                DamageAmount = damageAmount,
                AbilityId = abilityId,
                Tick = tick,
                IsAttack = isAttack,
            });

            // Trim to max entries
            while (list.Count > MaxRecapEntries)
                list.RemoveAt(list.Count - 1);
        }

        /// <summary>Get and clear death recap data for a unit.</summary>
        public IReadOnlyList<DeathRecapEntry> ConsumeDeathRecap(UnitUid unitUid)
        {
            if (_deathRecaps.TryGetValue(unitUid, out var list))
            {
                var result = new List<DeathRecapEntry>(list);
                _deathRecaps.Remove(unitUid);
                return result;
            }
            return Array.Empty<DeathRecapEntry>();
        }

        /// <summary>Clear death recap on respawn.</summary>
        public void ClearDeathRecap(UnitUid unitUid)
        {
            _deathRecaps.Remove(unitUid);
        }

        private void PruneRecentKills(int currentTick)
        {
            int cutoff = currentTick - multikillWindowTicks;
            _recentKills.RemoveAll(k => k.KillTick < cutoff);
        }

        private static KillStreakTier GetKillStreakTier(int streak)
        {
            if (streak >= (int)KillStreakTier.Legendary) return KillStreakTier.Legendary;
            if (streak >= (int)KillStreakTier.Unstoppable) return KillStreakTier.Unstoppable;
            if (streak >= (int)KillStreakTier.Dominating) return KillStreakTier.Dominating;
            if (streak >= (int)KillStreakTier.KillingSpree) return KillStreakTier.KillingSpree;
            return KillStreakTier.None;
        }

        private static MultikillTier GetMultikillTier(int killCount)
        {
            if (killCount >= (int)MultikillTier.PentaKill) return MultikillTier.PentaKill;
            if (killCount >= (int)MultikillTier.QuadraKill) return MultikillTier.QuadraKill;
            if (killCount >= (int)MultikillTier.TripleKill) return MultikillTier.TripleKill;
            if (killCount >= (int)MultikillTier.DoubleKill) return MultikillTier.DoubleKill;
            return MultikillTier.None;
        }
    }
}
