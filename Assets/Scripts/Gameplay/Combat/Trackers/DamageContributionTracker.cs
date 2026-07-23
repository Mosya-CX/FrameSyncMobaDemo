using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Tracks damage contributions against a single victim unit
    /// (Combat v13.2 section 4).
    ///
    /// Used to determine kill credit, assist gold/experience distribution,
    /// and death recap. Stored per-victim in CombatSystem.
    /// </summary>
    public sealed class DamageContributionTracker
    {
        /// <summary>Ticks before a contribution record expires (Combat v13.2 §7.1).</summary>
        public const int ContributionExpiryTicks = 150;

        private readonly UnitUid _victimUid;
        private readonly Dictionary<UnitUid, DamageContributionRecord> _records;
        private int _currentLogicTick;

        public DamageContributionTracker(UnitUid victimUid)
        {
            _victimUid = victimUid;
            _records = new Dictionary<UnitUid, DamageContributionRecord>();
        }

        public UnitUid VictimUid => _victimUid;

        public int RecordCount => _records.Count;

        /// <summary>
        /// Records damage dealt by a contributor to this victim.
        /// Called during damage settlement when a unit takes damage.
        /// </summary>
        public void AddContribution(UnitUid contributorUid, fp damageAmount, int logicTick)
        {
            if (!contributorUid.IsValid() || damageAmount <= fp.zero) return;

            if (_records.TryGetValue(contributorUid, out var record))
            {
                record.ContributionValue += damageAmount;
                record.LastContributionLogicTick = logicTick;
                record.ExpireLogicTick = logicTick + ContributionExpiryTicks;
                _records[contributorUid] = record;
            }
            else
            {
                _records[contributorUid] = new DamageContributionRecord
                {
                    ContributorHeroUid = contributorUid,
                    ContributionValue = damageAmount,
                    LastContributionLogicTick = logicTick,
                    ExpireLogicTick = logicTick + ContributionExpiryTicks,
                };
            }
        }

        /// <summary>
        /// Returns the contributor with the highest contribution value.
        /// Used for kill credit assignment.
        /// </summary>
        public DamageContributionRecord? GetTopContributor()
        {
            DamageContributionRecord? best = null;

            foreach (var kvp in _records)
            {
                DamageContributionRecord candidate = kvp.Value;
                if (!best.HasValue ||
                    candidate.ContributionValue > best.Value.ContributionValue ||
                    (candidate.ContributionValue == best.Value.ContributionValue &&
                     candidate.ContributorHeroUid.CompareTo(
                         best.Value.ContributorHeroUid) < 0))
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Returns UIDs of all contributors except the top one,
        /// sorted by contribution descending. Used for assist credit.
        /// </summary>
        public List<UnitUid> GetAssistants()
        {
            var contributors = GetContributors();
            var result = new List<UnitUid>();
            for (int i = 1; i < contributors.Count; i++)
            {
                if (contributors[i].ContributionValue > fp.zero)
                {
                    result.Add(contributors[i].ContributorHeroUid);
                }
            }
            result.Sort((left, right) => left.CompareTo(right));
            return result;
        }

        /// <summary>
        /// Returns all contributors with non-zero contribution,
        /// sorted by contribution descending.
        /// </summary>
        public List<DamageContributionRecord> GetContributors()
        {
            var list = new List<DamageContributionRecord>(_records.Values);
            list.Sort((a, b) =>
            {
                int comparison = b.ContributionValue.CompareTo(a.ContributionValue);
                return comparison != 0
                    ? comparison
                    : a.ContributorHeroUid.CompareTo(b.ContributorHeroUid);
            });
            return list;
        }

        public List<DamageContributionRecord> GetContributorsByUid()
        {
            var list = new List<DamageContributionRecord>(_records.Values);
            list.Sort((a, b) => a.ContributorHeroUid.CompareTo(b.ContributorHeroUid));
            return list;
        }

        internal void RestoreRecord(in DamageContributionRecordSnapshot snapshot)
        {
            if (!snapshot.ContributorHeroUid.IsValid() || snapshot.ContributionValue <= fp.zero)
                throw new FrameSyncMoba.Deterministic.DeterministicSimulationException(
                    "Invalid damage contribution snapshot record.");
            if (_records.ContainsKey(snapshot.ContributorHeroUid))
                throw new FrameSyncMoba.Deterministic.DeterministicSimulationException(
                    "Duplicate damage contributor in snapshot.");
            _records.Add(snapshot.ContributorHeroUid, new DamageContributionRecord
            {
                ContributorHeroUid = snapshot.ContributorHeroUid,
                ContributionValue = snapshot.ContributionValue,
                LastContributionLogicTick = snapshot.LastContributionLogicTick,
                ExpireLogicTick = snapshot.ExpireLogicTick,
            });
        }

        public void Clear()
        {
            _records.Clear();
        }

        /// <summary>
        /// Removes contribution records whose ExpireLogicTick has passed (Combat v13.2 §7.1).
        /// Called during CombatSystem.BeginTick.
        /// </summary>
        public void PruneExpired(int currentTick)
        {
            var toRemove = new List<UnitUid>();
            foreach (var kvp in _records)
            {
                if (kvp.Value.ExpireLogicTick > 0 &&
                    kvp.Value.ExpireLogicTick < currentTick)
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
                _records.Remove(toRemove[i]);
        }
    }

    /// <summary>
    /// A single contributor's damage record against a victim
    /// (Combat v13.2 section 12.1).
    /// </summary>
    public struct DamageContributionRecord
    {
        /// <summary>The hero unit that dealt damage.</summary>
        public UnitUid ContributorHeroUid;

        /// <summary>Total damage contributed (actual health + shield damage).</summary>
        public fp ContributionValue;

        /// <summary>The most recent Tick this contributor dealt damage.</summary>
        public int LastContributionLogicTick;

        /// <summary>Tick after which this record expires (Combat v13.2 §7.1).</summary>
        public int ExpireLogicTick;
    }
}
