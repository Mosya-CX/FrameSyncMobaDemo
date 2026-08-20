using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Kind of a combat contribution event (Combat v13.2 §7.14).
    /// Only <see cref="Damage"/> events participate in killer/assistant
    /// resolution; Shield/Heal events are recorded for audit and future use.
    /// </summary>
    public enum CombatContributionKind : byte
    {
        Damage = 0,
        Shield = 1,
        Heal = 2,
    }

    /// <summary>
    /// One lightweight, deterministic combat interaction against a victim.
    /// Ordered by (LogicTick, SequenceInTick) inside the log.
    /// </summary>
    public struct CombatContributionEvent
    {
        public UnitUid VictimUnitUid;
        public UnitUid ContributorHeroUid;
        public CombatContributionKind Kind;
        public fp Amount;
        public int LogicTick;
        public ushort SequenceInTick;
    }

    public struct CombatContributionEventSnapshot
    {
        public UnitUid ContributorHeroUid;
        public CombatContributionKind Kind;
        public fp Amount;
        public int LogicTick;
        public ushort SequenceInTick;
    }

    public struct CombatContributionEventLogSnapshot
    {
        public UnitUid VictimUnitUid;
        public UnitUid LastHitContributorUid;
        public CombatContributionEventSnapshot[] Events;
    }

    /// <summary>
    /// Cross-Tick per-victim event log (Combat v13.2 §7.14). Replaces the old
    /// aggregated DamageContributionTracker: every effective damage/shield/heal
    /// interaction is stored as an event, so the killer is the last Damage
    /// event's contributor (last hit) and assistants are the remaining
    /// distinct Damage contributors inside the assist window.
    /// </summary>
    public sealed class CombatContributionEventLog
    {
        /// <summary>Ticks before an event expires from the log (~5s @ 30).</summary>
        public const int DefaultAssistContributionDurationTicks = 150;
        public const int AssistContributionDurationTicks =
            DefaultAssistContributionDurationTicks;
        /// <summary>Defensive per-victim capacity; oldest events are dropped
        /// beyond this (equivalent to expiry).</summary>
        public const int MaxContributionEventsPerVictim = 256;

        private readonly UnitUid _victimUid;
        private readonly int assistContributionDurationTicks;
        private readonly List<CombatContributionEvent> _events =
            new List<CombatContributionEvent>();

        public CombatContributionEventLog(
            UnitUid victimUid,
            int assistDurationTicks =
                DefaultAssistContributionDurationTicks)
        {
            if (assistDurationTicks <= 0)
                throw new System.ArgumentOutOfRangeException(
                    nameof(assistDurationTicks));
            _victimUid = victimUid;
            assistContributionDurationTicks = assistDurationTicks;
        }

        public UnitUid VictimUid => _victimUid;

        /// <summary>Contributor of the most recent Damage event (the killer
        /// when the victim dies). Snapshot member.</summary>
        public UnitUid LastHitContributorUid { get; private set; }

        public int EventCount => _events.Count;

        public IReadOnlyList<CombatContributionEvent> Events =>
            _events;

        public void AddEvent(in CombatContributionEvent evt)
        {
            if (evt.VictimUnitUid != _victimUid)
            {
                throw new DeterministicSimulationException(
                    "Combat contribution event victim mismatch.");
            }
            if (evt.Kind == CombatContributionKind.Damage)
            {
                // The killer is the contributor of the last *effective*
                // damage. When the latest damage comes from a non-hero
                // source (invalid hero contributor, e.g. a minion or tower
                // finishing the target), the hero killer credit is cleared
                // so a hero that merely poked the target does not receive a
                // kill/creep/passive trigger.
                LastHitContributorUid =
                    evt.ContributorHeroUid.IsValid()
                        ? evt.ContributorHeroUid
                        : default;
            }
            if (!evt.ContributorHeroUid.IsValid() ||
                evt.Amount <= fp.zero)
                return;
            if (evt.Kind == CombatContributionKind.Damage)
            {
                LastHitContributorUid =
                    evt.ContributorHeroUid;
            }
            _events.Add(evt);
            if (_events.Count >
                MaxContributionEventsPerVictim)
            {
                _events.RemoveAt(0);
            }
        }

        public void PruneExpired(int currentTick)
        {
            int expiredBeforeTick =
                currentTick - assistContributionDurationTicks;
            int remove = 0;
            while (remove < _events.Count &&
                   _events[remove].LogicTick <
                       expiredBeforeTick)
            {
                remove++;
            }
            if (remove > 0)
                _events.RemoveRange(0, remove);
            if (_events.Count == 0)
                LastHitContributorUid = default;
        }

        /// <summary>Killer = contributor of the last Damage event.</summary>
        public UnitUid ResolveKiller(int currentTick)
        {
            PruneExpired(currentTick);
            return LastHitContributorUid;
        }

        /// <summary>
        /// Assistants = distinct Damage contributors inside the window,
        /// excluding the killer, filtered to valid enemy heroes, sorted by
        /// UnitUid ascending.
        /// </summary>
        public UnitUid[] ResolveAssistants(
            int currentTick,
            UnitWorld world,
            Unit victim,
            UnitUid killer)
        {
            PruneExpired(currentTick);
            var assistants = new List<UnitUid>();
            for (int i = 0; i < _events.Count; i++)
            {
                CombatContributionEvent evt =
                    _events[i];
                if (evt.Kind !=
                    CombatContributionKind.Damage)
                    continue;
                if (evt.ContributorHeroUid == killer)
                    continue;
                if (!world.TryGetUnit(
                        evt.ContributorHeroUid,
                        out Unit hero))
                    continue;
                if (hero.UnitKind != UnitKind.Hero ||
                    hero.TeamId == victim.TeamId)
                    continue;
                if (!assistants.Contains(
                        evt.ContributorHeroUid))
                {
                    assistants.Add(
                        evt.ContributorHeroUid);
                }
            }
            assistants.Sort((left, right) =>
                left.CompareTo(right));
            return assistants.ToArray();
        }

        public CombatContributionEventLogSnapshot Capture()
        {
            var events =
                new CombatContributionEventSnapshot[
                    _events.Count];
            for (int i = 0; i < _events.Count; i++)
            {
                CombatContributionEvent evt =
                    _events[i];
                events[i] =
                    new CombatContributionEventSnapshot
                    {
                        ContributorHeroUid =
                            evt.ContributorHeroUid,
                        Kind = evt.Kind,
                        Amount = evt.Amount,
                        LogicTick = evt.LogicTick,
                        SequenceInTick =
                            evt.SequenceInTick,
                    };
            }
            return new CombatContributionEventLogSnapshot
            {
                VictimUnitUid = _victimUid,
                LastHitContributorUid =
                    LastHitContributorUid,
                Events = events,
            };
        }

        public void Restore(
            in CombatContributionEventLogSnapshot snapshot)
        {
            _events.Clear();
            if (snapshot.Events != null)
            {
                int previousTick = -1;
                ushort previousSequence = 0;
                for (int i = 0;
                     i < snapshot.Events.Length;
                     i++)
                {
                    CombatContributionEventSnapshot snap =
                        snapshot.Events[i];
                    if (!snap.ContributorHeroUid.IsValid() ||
                        snap.Amount <= fp.zero)
                    {
                        throw new DeterministicSimulationException(
                            "Invalid combat contribution event snapshot.");
                    }
                    if (i > 0 &&
                        (snap.LogicTick < previousTick ||
                         (snap.LogicTick == previousTick &&
                          snap.SequenceInTick <=
                              previousSequence)))
                    {
                        throw new DeterministicSimulationException(
                            "Combat contribution events are not in stable order.");
                    }
                    previousTick = snap.LogicTick;
                    previousSequence =
                        snap.SequenceInTick;
                    _events.Add(
                        new CombatContributionEvent
                        {
                            VictimUnitUid = _victimUid,
                            ContributorHeroUid =
                                snap.ContributorHeroUid,
                            Kind = snap.Kind,
                            Amount = snap.Amount,
                            LogicTick = snap.LogicTick,
                            SequenceInTick =
                                snap.SequenceInTick,
                        });
                }
            }
            LastHitContributorUid =
                snapshot.LastHitContributorUid;
        }
    }
}
