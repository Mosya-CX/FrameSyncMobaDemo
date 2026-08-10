using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

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

            ValidateRecord(record);
            if (idToIndex.ContainsKey(record.Id))
            {
                throw new DeterministicSimulationException(
                    $"Duplicate CombatModifierRecord.Id {record.Id} on Unit {owner.UnitUid}.");
            }

            CombatModifierRecord stored = CloneRecord(record);
            int insertIndex = records.BinarySearch(
                stored,
                RecordComparer.Instance);
            if (insertIndex < 0)
                insertIndex = ~insertIndex;
            records.Insert(insertIndex, stored);
            RebuildIndicesFrom(insertIndex);

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

            // Preserve stable list order and repair every shifted lookup index.
            records.RemoveAt(index);
            idToIndex.Remove(handle.ModifierId);
            RebuildIndicesFrom(index);

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

        }

        internal void AccumulateDamage(
            CombatModifierScope scope,
            in CombatRequestHeader header,
            DamageType damageType,
            CombatFormulaSlot slot,
            fp baseValue,
            fp slotInput,
            UnitKind targetKind,
            StatHandler sourceStats,
            StatHandler targetStats,
            ref CombatFormulaAccumulator accumulator,
            ref CombatPolicyResolution policies)
        {
            for (int recordIndex = 0;
                 recordIndex < records.Count;
                 recordIndex++)
            {
                CombatModifierRecord record =
                    records[recordIndex];
                if (record.Domain != CombatDomain.Damage ||
                    record.Scope != scope ||
                    !record.Match.Matches(
                        header,
                        damageType,
                        targetKind))
                    continue;
                CombatFormulaPatch[] valuePatches =
                    record.ValuePatches ??
                    Array.Empty<CombatFormulaPatch>();
                for (int patchIndex = 0;
                     patchIndex < valuePatches.Length;
                     patchIndex++)
                {
                    CombatFormulaPatch patch =
                        valuePatches[patchIndex];
                    if (patch.Slot != slot)
                        continue;
                    fp operand = patch.Operand.Evaluate(
                        baseValue,
                        slotInput,
                        sourceStats,
                        targetStats);
                    accumulator.Accumulate(
                        patch.Operation,
                        operand);
                }
                CombatPolicyPatch[] policyPatches =
                    record.PolicyPatches ??
                    Array.Empty<CombatPolicyPatch>();
                for (int patchIndex = 0;
                     patchIndex < policyPatches.Length;
                     patchIndex++)
                    policies.Accumulate(
                        policyPatches[patchIndex].Kind);
            }
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
        /// Current number of attached records.
        /// </summary>
        public int Count => records.Count;

        private static int CompareRecordsById(CombatModifierRecord a, CombatModifierRecord b)
        {
            return a.Id.CompareTo(b.Id);
        }

        private void RebuildIndicesFrom(int firstIndex)
        {
            for (int index = firstIndex;
                 index < records.Count;
                 index++)
                idToIndex[records[index].Id] = index;
        }

        private static void ValidateRecord(
            CombatModifierRecord record)
        {
            if (record.Id == 0 ||
                !Enum.IsDefined(
                    typeof(CombatDomain),
                    record.Domain) ||
                !Enum.IsDefined(
                    typeof(CombatModifierScope),
                    record.Scope))
                throw new DeterministicSimulationException(
                    "Combat modifier identity, domain or scope is invalid.");
            CombatFormulaPatch[] values =
                record.ValuePatches ??
                Array.Empty<CombatFormulaPatch>();
            for (int i = 0; i < values.Length; i++)
            {
                if (!Enum.IsDefined(
                        typeof(CombatFormulaSlot),
                        values[i].Slot) ||
                    !Enum.IsDefined(
                        typeof(CombatModifierOperation),
                        values[i].Operation))
                    throw new DeterministicSimulationException(
                        $"Combat modifier {record.Id} has an invalid formula patch.");
                CombatOperandTerm[] terms =
                    values[i].Operand.Terms ??
                    Array.Empty<CombatOperandTerm>();
                for (int termIndex = 0;
                     termIndex < terms.Length;
                     termIndex++)
                    if (!Enum.IsDefined(
                            typeof(CombatValueRefKind),
                            terms[termIndex].Value.Kind))
                        throw new DeterministicSimulationException(
                            $"Combat modifier {record.Id} has an invalid operand reference.");
            }
            CombatPolicyPatch[] policies =
                record.PolicyPatches ??
                Array.Empty<CombatPolicyPatch>();
            for (int i = 0; i < policies.Length; i++)
                if (!Enum.IsDefined(
                        typeof(CombatPolicyKind),
                        policies[i].Kind))
                    throw new DeterministicSimulationException(
                        $"Combat modifier {record.Id} has an invalid policy patch.");
        }

        private static CombatModifierRecord CloneRecord(
            CombatModifierRecord source)
        {
            CombatFormulaPatch[] sourceValues =
                source.ValuePatches ??
                Array.Empty<CombatFormulaPatch>();
            var values =
                new CombatFormulaPatch[sourceValues.Length];
            for (int i = 0; i < sourceValues.Length; i++)
            {
                CombatOperand operand =
                    sourceValues[i].Operand;
                values[i] = new CombatFormulaPatch(
                    sourceValues[i].Slot,
                    sourceValues[i].Operation,
                    new CombatOperand(
                        operand.Constant,
                        operand.Terms));
            }
            CombatPolicyPatch[] policies =
                source.PolicyPatches == null
                    ? Array.Empty<CombatPolicyPatch>()
                    : (CombatPolicyPatch[])
                        source.PolicyPatches.Clone();
            return new CombatModifierRecord
            {
                Id = source.Id,
                Domain = source.Domain,
                Scope = source.Scope,
                Match = source.Match,
                ValuePatches = values,
                PolicyPatches = policies,
            };
        }

        private sealed class RecordComparer :
            IComparer<CombatModifierRecord>
        {
            public static readonly RecordComparer Instance =
                new RecordComparer();

            public int Compare(
                CombatModifierRecord x,
                CombatModifierRecord y)
            {
                return CompareRecordsById(x, y);
            }
        }

        // ---- IRollback<CombatModifierSetSnapshot> (Unit v27.3 §5.9.4) ----

        /// <summary>
        /// Captures all current records (deep copy by instantiating new
        /// CombatModifierRecord objects for each entry) into the snapshot.
        /// Does not capture the idToIndex dictionary — it is rebuilt on restore.
        /// </summary>
        public void Capture(ref CombatModifierSetSnapshot state)
        {
            var sortedRecords = new List<CombatModifierRecord>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                sortedRecords.Add(records[i]);
            }
            sortedRecords.Sort(CompareRecordsById);

            var ids = new ulong[sortedRecords.Count];
            var capturedRecords = new CombatModifierRecord[sortedRecords.Count];
            for (int i = 0; i < sortedRecords.Count; i++)
            {
                CombatModifierRecord original = sortedRecords[i];
                ids[i] = original.Id;
                capturedRecords[i] = CloneRecord(original);
            }

            state.Ids = ids;
            state.Records = capturedRecords;
        }

        /// <summary>
        /// Directly replaces all internal state from a snapshot (§5.9.4).
        /// Does NOT call Attach, Detach, or Clear.
        /// Does NOT trigger side effects or event dispatch.
        /// Rebuilds the idToIndex lookup from the restored records.
        /// </summary>
        public void Restore(in CombatModifierSetSnapshot state)
        {
            ulong[] ids = state.Ids;
            CombatModifierRecord[] restoredRecords = state.Records;
            if (ids == null || restoredRecords == null || ids.Length != restoredRecords.Length)
            {
                throw new DeterministicSimulationException(
                    "CombatModifierSet snapshot must contain matching Id and Record arrays.");
            }

            records.Clear();
            idToIndex.Clear();

            ulong previousId = 0;
            for (int i = 0; i < restoredRecords.Length; i++)
            {
                CombatModifierRecord record = restoredRecords[i];
                if (record == null ||
                    record.Id != ids[i] ||
                    (i > 0 && record.Id <= previousId))
                {
                    throw new DeterministicSimulationException(
                        "CombatModifierSet snapshot records must be non-null, match Ids, and be strictly ordered by ModifierId.");
                }

                previousId = record.Id;
                ValidateRecord(record);
                idToIndex[record.Id] = records.Count;
                records.Add(CloneRecord(record));
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

    internal struct CombatFormulaAccumulator
    {
        private fp addTotal;
        private fp multiplierTotal;
        private fp lowerBound;
        private fp upperBound;
        private bool hasLowerBound;
        private bool hasUpperBound;

        public static CombatFormulaAccumulator Create()
        {
            return new CombatFormulaAccumulator
            {
                multiplierTotal = fp.one,
            };
        }

        public void Accumulate(
            CombatModifierOperation operation,
            fp operand)
        {
            switch (operation)
            {
                case CombatModifierOperation.Add:
                    addTotal += operand;
                    break;
                case CombatModifierOperation.Multiply:
                    multiplierTotal *= operand;
                    break;
                case CombatModifierOperation.ClampMin:
                    if (!hasLowerBound ||
                        operand > lowerBound)
                    {
                        lowerBound = operand;
                        hasLowerBound = true;
                    }
                    break;
                case CombatModifierOperation.ClampMax:
                    if (!hasUpperBound ||
                        operand < upperBound)
                    {
                        upperBound = operand;
                        hasUpperBound = true;
                    }
                    break;
                default:
                    throw new DeterministicSimulationException(
                        $"Unknown Combat modifier operation {operation}.");
            }
        }

        public fp Apply(fp input)
        {
            fp output =
                (input + addTotal) * multiplierTotal;
            if (hasLowerBound && output < lowerBound)
                output = lowerBound;
            if (hasUpperBound && output > upperBound)
                output = upperBound;
            return output;
        }
    }

    internal struct CombatPolicyResolution
    {
        public bool ForceCrit;
        public bool ForbidCrit;
        public bool IgnoreAllShield;
        public bool IgnorePhysicalShield;
        public bool IgnoreMagicShield;

        public void Accumulate(CombatPolicyKind kind)
        {
            switch (kind)
            {
                case CombatPolicyKind.ForceCrit:
                    ForceCrit = true;
                    break;
                case CombatPolicyKind.ForbidCrit:
                    ForbidCrit = true;
                    break;
                case CombatPolicyKind.IgnoreAllShield:
                    IgnoreAllShield = true;
                    break;
                case CombatPolicyKind.IgnorePhysicalShield:
                    IgnorePhysicalShield = true;
                    break;
                case CombatPolicyKind.IgnoreMagicShield:
                    IgnoreMagicShield = true;
                    break;
                default:
                    throw new DeterministicSimulationException(
                        $"Unknown Combat policy {kind}.");
            }
        }
    }
}
