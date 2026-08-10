using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic per-runtime state shared between reactions and effects.
    /// Slots come from BuffDefinition.BlackboardLayout; no Dictionary storage.
    /// (design v14.2 section 5)
    /// </summary>
    public sealed class BuffBlackboard
    {
        private BuffStateSlotDefinition[] _layout =
            Array.Empty<BuffStateSlotDefinition>();
        private BuffValue[] _values =
            Array.Empty<BuffValue>();

        public int SlotCount => _values.Length;

        public void Initialize(
            BuffBlackboardLayout layout)
        {
            if (layout == null ||
                layout.Slots == null ||
                layout.Slots.Length == 0)
            {
                _layout = Array.Empty<BuffStateSlotDefinition>();
                _values = Array.Empty<BuffValue>();
                return;
            }
            _layout = (BuffStateSlotDefinition[])
                layout.Slots.Clone();
            Array.Sort(
                _layout,
                (a, b) => a.SlotId.CompareTo(b.SlotId));
            _values = new BuffValue[_layout.Length];
            for (int i = 0; i < _layout.Length; i++)
            {
                if (!_layout[i].SlotId.IsValid)
                    throw new Deterministic.DeterministicSimulationException(
                        "BuffBlackboard layout contains an invalid slot id.");
                if (i > 0 &&
                    _layout[i - 1].SlotId == _layout[i].SlotId)
                    throw new Deterministic.DeterministicSimulationException(
                        "BuffBlackboard layout contains duplicate slot ids.");
                _values[i] = _layout[i].DefaultValue;
            }
        }

        public BuffValue Read(BuffStateSlotId slot)
        {
            int index = IndexOf(slot);
            return index >= 0 ? _values[index] : default;
        }

        public void Write(
            BuffStateSlotId slot,
            in BuffValue value)
        {
            int index = IndexOf(slot);
            if (index < 0)
                throw new Deterministic.DeterministicSimulationException(
                    $"BuffBlackboard has no slot {slot.Value}.");
            _values[index] = value;
        }

        public void Reset()
        {
            for (int i = 0; i < _values.Length; i++)
                _values[i] = _layout[i].DefaultValue;
        }

        public void InvalidateAll()
        {
            for (int i = 0; i < _values.Length; i++)
            {
                BuffValueKind kind = _values[i].Kind;
                _values[i] = default;
                _values[i].Kind = kind;
            }
        }

        // ---- Typed helpers ----

        public void WriteInt(
            BuffStateSlotId slot,
            int value) =>
            Write(slot, BuffValue.FromInt(value));

        public int ReadIntOrDefault(
            BuffStateSlotId slot) =>
            Read(slot).Kind == BuffValueKind.Int
                ? Read(slot).IntValue
                : 0;

        public void WriteBool(
            BuffStateSlotId slot,
            bool value) =>
            Write(slot, BuffValue.FromBool(value));

        public bool ReadBoolOrDefault(
            BuffStateSlotId slot) =>
            Read(slot).Kind == BuffValueKind.Bool
                ? Read(slot).BoolValue
                : false;

        public void WriteFp(
            BuffStateSlotId slot,
            fp value) =>
            Write(slot, BuffValue.FromFp(value));

        public fp ReadFpOrDefault(
            BuffStateSlotId slot) =>
            Read(slot).Kind == BuffValueKind.Fp
                ? Read(slot).FpValue
                : fp.zero;

        public void WriteFp2(
            BuffStateSlotId slot,
            fp2 value) =>
            Write(slot, BuffValue.FromFp2(value));

        public fp2 ReadFp2OrDefault(
            BuffStateSlotId slot) =>
            Read(slot).Kind == BuffValueKind.Fp2
                ? Read(slot).Fp2Value
                : fp2.zero;

        public void WriteUnitUid(
            BuffStateSlotId slot,
            UnitUid value) =>
            Write(slot, BuffValue.FromUnitUid(value));

        public UnitUid ReadUnitUidOrDefault(
            BuffStateSlotId slot) =>
            Read(slot).Kind == BuffValueKind.UnitUid
                ? Read(slot).UnitUidValue
                : default;

        public void WriteConfigId(
            BuffStateSlotId slot,
            int value) =>
            Write(slot, BuffValue.FromConfigId(value));

        public int ReadConfigIdOrDefault(
            BuffStateSlotId slot) =>
            Read(slot).Kind == BuffValueKind.StableConfigId
                ? Read(slot).ConfigIdValue
                : 0;

        public void WriteStatHandle(
            BuffStateSlotId slot,
            StatModifierHandle value) =>
            Write(slot, BuffValue.FromStatHandle(value));

        public bool TryGetStatHandle(
            BuffStateSlotId slot,
            out StatModifierHandle handle)
        {
            BuffValue value = Read(slot);
            if (value.Kind == BuffValueKind.StatModifierHandle)
            {
                handle = value.StatHandle;
                return handle.IsValid;
            }
            handle = default;
            return false;
        }

        public void WriteCombatHandle(
            BuffStateSlotId slot,
            CombatModifierHandle value) =>
            Write(slot, BuffValue.FromCombatHandle(value));

        public bool TryGetCombatHandle(
            BuffStateSlotId slot,
            out CombatModifierHandle handle)
        {
            BuffValue value = Read(slot);
            if (value.Kind == BuffValueKind.CombatModifierHandle)
            {
                handle = value.CombatHandle;
                return handle.IsValid;
            }
            handle = default;
            return false;
        }

        // ---- Snapshot ----

        public BuffBlackboardSnapshot Capture()
        {
            var slots =
                new List<BuffValueSnapshot>(_values.Length);
            for (int i = 0; i < _values.Length; i++)
            {
                slots.Add(new BuffValueSnapshot
                {
                    SlotId = _layout[i].SlotId,
                    Value = _values[i],
                });
            }
            return new BuffBlackboardSnapshot
            {
                Slots = slots,
            };
        }

        public void Restore(
            in BuffBlackboardSnapshot snapshot)
        {
            Initialize(
                new BuffBlackboardLayout
                {
                    Slots = _layout,
                });
            List<BuffValueSnapshot> slots =
                snapshot.Slots ??
                new List<BuffValueSnapshot>();
            for (int i = 0; i < slots.Count; i++)
            {
                BuffValueSnapshot entry = slots[i];
                if (!entry.SlotId.IsValid ||
                    (i > 0 &&
                     slots[i - 1].SlotId.CompareTo(
                         entry.SlotId) >= 0))
                    throw new Deterministic.DeterministicSimulationException(
                        "Buff blackboard snapshot is not in canonical slot-id order.");
                int index = IndexOf(entry.SlotId);
                if (index < 0)
                    throw new Deterministic.DeterministicSimulationException(
                        $"Buff blackboard snapshot references undeclared slot {entry.SlotId.Value}.");
                _values[index] = entry.Value;
            }
        }

        private int IndexOf(BuffStateSlotId slot)
        {
            for (int i = 0; i < _layout.Length; i++)
            {
                if (_layout[i].SlotId == slot)
                    return i;
            }
            return -1;
        }
    }

    public struct BuffValueSnapshot
    {
        public BuffStateSlotId SlotId;
        public BuffValue Value;
    }

    public struct BuffBlackboardSnapshot
    {
        public List<BuffValueSnapshot> Slots;
    }
}
