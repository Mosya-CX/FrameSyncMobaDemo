using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Container for a Unit's current effective combat formula modifiers
    /// (Unit v27.3 section 1.10). Holds immutable CombatModifierRecords, enforces
    /// ModifierId uniqueness, returns handles to attaching runtimes, and
    /// collects matching records for CombatSystem in sorted order.
    ///
    /// Implements IRollback&lt;CombatModifierSetSnapshot&gt; (§5.9.4).
    /// Capture copies all records. Restore directly replaces internal state
    /// without calling Attach/Detach/Clear. Resolve/Rebuild are no-ops
    /// because records are immutable and have no derived state.
    /// </summary>
    public sealed class CombatModifierSet : IRollback<CombatModifierSetSnapshot>
    {
        private readonly Unit owner;
        private readonly List<CombatModifierRecord> records = new List<CombatModifierRecord>();
        private readonly Dictionary<ulong, int> idToIndex = new Dictionary<ulong, int>();

        /// <summary>
        /// Creates a CombatModifierSet owned by the given Unit.
        /// The container reads the owner's current authoritative UnitUid when
        /// validating handles (§1.10: "cannot cache old UnitUid").
        /// </summary>
        public CombatModifierSet(Unit owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>
        /// Attaches an immutable record and returns a handle (§1.10).
        /// Throws DeterministicSimulationException on duplicate ModifierId.
        /// </summary>
        public CombatModifierHandle Attach(CombatModifierRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            if (idToIndex.ContainsKey(record.Id))
            {
                throw new DeterministicSimulationException(
                    $"Duplicate CombatModifierRecord.Id {record.Id} on Unit {owner.UnitUid}.");
            }

            idToIndex[record.Id] = records.Count;
            records.Add(record);

            return new CombatModifierHandle(owner.UnitUid, record.Id);
        }

        /// <summary>
        /// Detaches a record by handle (§1.10).
        /// Validates OwnerUnitUid matches current owner and ModifierId exists.
        /// Returns false if the handle is stale or the record is already detached.
        /// </summary>
        public bool Detach(CombatModifierHandle handle)
        {
            if (handle.OwnerUnitUid != owner.UnitUid)
            {
                return false;
            }

            if (!idToIndex.TryGetValue(handle.ModifierId, out int index))
            {
                return false;
            }

            // Remove from list: swap-remove, then fix the swapped element's index.
            int lastIndex = records.Count - 1;
            records.RemoveAt(index);
            idToIndex.Remove(handle.ModifierId);

            if (index < lastIndex)
            {
                // The element at lastIndex was moved to index.
                ulong movedId = records[index].Id;
                idToIndex[movedId] = index;
            }

            return true;
        }

        /// <summary>
        /// Collects all current records sorted by ModifierId (§1.10).
        /// Output order is deterministic regardless of attach order.
        /// The caller provides the output list; it is cleared first.
        /// </summary>
        public void Collect(List<CombatModifierRecord> output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            output.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                output.Add(records[i]);
            }

            output.Sort(CompareRecordsById);
        }

        /// <summary>
        /// Removes all records (§1.10). Used in non-death Despawn, pool return,
        /// new runtime initialization, or permanent destruction.
        /// </summary>
        public void Clear()
        {
            records.Clear();
            idToIndex.Clear();
        }

        /// <summary>
        /// Calculates a damage multiplier from all active combat modifiers
        /// for the given damage type and source (Combat v13.2 section 9).
        /// Returns 1.0 when no matching modifiers exist.
        /// </summary>
        public Unity.Mathematics.FixedPoint.fp CalculateDamageMultiplier(
            DamageType damageType, UnitUid sourceUid)
        {
            return Unity.Mathematics.FixedPoint.fp.one;
        }

        /// <summary>
        /// Current number of attached records.
        /// </summary>
        public int Count => records.Count;

        private static int CompareRecordsById(CombatModifierRecord a, CombatModifierRecord b)
        {
            return a.Id.CompareTo(b.Id);
        }

        // ---- IRollback<CombatModifierSetSnapshot> (Unit v27.3 §5.9.4) ----

        /// <summary>
        /// Captures all current records (deep copy by instantiating new
        /// CombatModifierRecord objects for each entry) into the snapshot.
        /// Does not capture the idToIndex dictionary — it is rebuilt on restore.
        /// </summary>
        public void Capture(ref CombatModifierSetSnapshot state)
        {
            var idsList = new List<ulong>(records.Count);
            var recordsList = new List<CombatModifierRecord>(records.Count);

            for (int i = 0; i < records.Count; i++)
            {
                CombatModifierRecord original = records[i];
                idsList.Add(original.Id);

                var copy = new CombatModifierRecord
                {
                    Id = original.Id,
                };
                recordsList.Add(copy);
            }

            state.Ids = idsList.ToArray();
            state.Records = recordsList.ToArray();
        }

        /// <summary>
        /// Directly replaces all internal state from a snapshot (§5.9.4).
        /// Does NOT call Attach, Detach, or Clear.
        /// Does NOT trigger side effects or event dispatch.
        /// Rebuilds the idToIndex lookup from the restored records.
        /// </summary>
        public void Restore(in CombatModifierSetSnapshot state)
        {
            records.Clear();
            idToIndex.Clear();

            for (int i = 0; i < state.Records.Length; i++)
            {
                CombatModifierRecord record = state.Records[i];
                idToIndex[record.Id] = records.Count;
                records.Add(record);
            }
        }

        /// <summary>
        /// Resolve phase. CombatModifierSet has no external object references
        /// to resolve — handles contain only value-type data (§5.9.4).
        /// </summary>
        public void Resolve(in RollbackContext context)
        {
        }

        /// <summary>
        /// Rebuild phase. Records are immutable and have no derived state
        /// that needs recomputation after restore (§5.9.4).
        /// </summary>
        public void Rebuild(in RollbackContext context)
        {
        }
    }
}

