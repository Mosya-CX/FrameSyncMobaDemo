using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class EquipmentHandler : UnitHandler, IRollback<EquipmentHandlerSnapshot>
    {
        public const int SlotCount = 6;

        private readonly EquipmentInstance[] _slots = new EquipmentInstance[SlotCount];
        private readonly Dictionary<EquipmentCooldownGroupId, int> _sharedCooldowns =
            new Dictionary<EquipmentCooldownGroupId, int>();
        private EquipmentEffectDispatch _effectDispatch;
        private int _runtimeRevision;
        private Unit _owner => Owner;
        public EquipmentDatabase DefinitionDatabase { private get; set; }

        protected override void OnOwnerBound()
        {
            _effectDispatch = new EquipmentEffectDispatch(Owner);
        }

        public void OnDamageTaken(in DamageEventData data) => _effectDispatch?.OnDamageTaken(data);
        public void OnDamageDealt(in DamageEventData data) => _effectDispatch?.OnDamageDealt(data);
        public void OnHealTaken(in HealEventData data) => _effectDispatch?.OnHealTaken(data);
        public void OnHealDealt(in HealEventData data) => _effectDispatch?.OnHealDealt(data);
        public void OnUnitDying(Unit unit) => _effectDispatch?.OnUnitDying(unit);
        public void OnUnitDeath(Unit unit) => _effectDispatch?.OnUnitDeath(unit);
        public void OnUnitKill(Unit victim) => _effectDispatch?.OnUnitKill(victim);

        public override void InitializeForNewRuntime()
        {
            Array.Clear(_slots, 0, SlotCount);
            _sharedCooldowns.Clear();
            _runtimeRevision = 0;
        }

        public EquipmentInstance GetSlot(int slot)
        {
            if ((uint)slot >= SlotCount) return null;
            return _slots[slot];
        }

        public int FindSlot(EquipmentInstance instance)
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] == instance) return i;
            return -1;
        }

        public int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i] == null) return i;
            return -1;
        }

        public bool IsFull() => FirstEmptySlot() < 0;

        public EquipmentDefinition GetSlotDef(int slot)
        {
            if ((uint)slot >= SlotCount) return null;
            return _slots[slot]?.Definition;
        }

        public bool HasTag(string tag)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var def = _slots[i]?.Definition;
                if (def?.Tags == null) continue;
                for (int j = 0; j < def.Tags.Length; j++)
                    if (def.Tags[j] == tag) return true;
            }
            return false;
        }

        public bool HasDefinition(EquipmentDefinition definition)
        {
            if (definition == null) return false;
            for (int i = 0; i < SlotCount; i++)
                if (_slots[i]?.Definition == definition) return true;
            return false;
        }

        public int FindStackableSlot(EquipmentDefinition definition)
        {
            if (definition == null) return -1;
            var maxStack = definition.MaxStack;
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst?.Definition == definition && inst.StackCount < maxStack)
                    return i;
            }
            return -1;
        }

        public void MergeIntoStack(int slot, int additionalStacks)
        {
            if ((uint)slot >= SlotCount) return;
            var inst = _slots[slot];
            if (inst == null) return;
            inst.StackCount += additionalStacks;
            if (inst.StackCount > inst.Definition.MaxStack)
                inst.StackCount = inst.Definition.MaxStack;
            _runtimeRevision++;
        }

        public bool Add(EquipmentDefinition definition, int slot)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if ((uint)slot >= SlotCount) return false;
            if (_slots[slot] != null) return false;

            var instance = new EquipmentInstance
            {
                Definition = definition,
                StackCount = 1,
                ChargeCount = ResolveMaxCharge(definition),
            };

            if (!definition.IsBaked)
                throw new InvalidOperationException($"Equipment {definition.Id} must be baked before runtime use.");
            if (_owner.StatHandler != null && definition.BakedFixedStats != null)
            {
                var handles = new List<StatModifierHandle>();
                for (int i = 0; i < definition.BakedFixedStats.Length; i++)
                {
                    var fs = definition.BakedFixedStats[i];
                    var handle = _owner.StatHandler.AddModifier(fs.Stat, StatModifierOperation.FlatAdd, fs.Value);
                    handles.Add(handle);
                }
                instance._fixedStatHandles = handles.ToArray();
            }

            _slots[slot] = instance;
            _runtimeRevision++;

            // Create EquipmentEffectRuntime instances
            if (definition.Effects != null && definition.Effects.Length > 0)
            {
                instance.EffectRuntimes = new EquipmentEffectRuntime[definition.Effects.Length];
                for (int i = 0; i < definition.Effects.Length; i++)
                {
                    instance.EffectRuntimes[i] = new EquipmentEffectRuntime(definition.Effects[i]);
                }
            }

            // Fire OnEquipped effect modules
            DispatchOnEquipped(instance);
            return true;
        }

        public bool Remove(int slot)
        {
            if ((uint)slot >= SlotCount) return false;
            var instance = _slots[slot];
            if (instance == null) return false;

            ReleaseFixedStats(instance);
            ReleaseEffectRuntimes(instance);
            DispatchOnUnequipped(instance);
            _slots[slot] = null;
            _runtimeRevision++;
            return true;
        }

        public void SwapSlots(int slotA, int slotB)
        {
            if ((uint)slotA >= SlotCount || (uint)slotB >= SlotCount) return;
            var temp = _slots[slotA];
            _slots[slotA] = _slots[slotB];
            _slots[slotB] = temp;
            _runtimeRevision++;
        }

        private void ReleaseFixedStats(EquipmentInstance instance)
        {
            if (_owner.StatHandler == null || instance._fixedStatHandles == null) return;
            for (int i = 0; i < instance._fixedStatHandles.Length; i++)
            {
                var handle = instance._fixedStatHandles[i];
                if (handle.IsValid)
                    _owner.StatHandler.RemoveModifier(handle);
            }
            instance._fixedStatHandles = null;
        }

        private void ReleaseEffectRuntimes(EquipmentInstance instance)
        {
            if (instance.EffectRuntimes == null) return;
            for (int i = 0; i < instance.EffectRuntimes.Length; i++)
            {
                instance.EffectRuntimes[i] = null;
            }
            instance.EffectRuntimes = null;
        }

        public override void ClearForDeath()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst == null) continue;
                ReleaseFixedStats(inst);
                ReleaseEffectRuntimes(inst);
            }
            _sharedCooldowns.Clear();
        }

        public override void ClearForRespawn()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst == null || _owner.StatHandler == null) continue;
                var def = inst.Definition;
                if (def?.BakedFixedStats == null) continue;

                var handles = new List<StatModifierHandle>();
                for (int j = 0; j < def.BakedFixedStats.Length; j++)
                {
                    var fs = def.BakedFixedStats[j];
                    var handle = _owner.StatHandler.AddModifier(fs.Stat, StatModifierOperation.FlatAdd, fs.Value);
                    handles.Add(handle);
                }
                inst._fixedStatHandles = handles.ToArray();

                // Rebuild EffectRuntimes for current life stage
                if (def.Effects != null && def.Effects.Length > 0)
                {
                    inst.EffectRuntimes = new EquipmentEffectRuntime[def.Effects.Length];
                    for (int j = 0; j < def.Effects.Length; j++)
                    {
                        inst.EffectRuntimes[j] = new EquipmentEffectRuntime(def.Effects[j]);
                    }
                }
            }
        }

        public void ClearForDespawn()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst == null) continue;
                ReleaseFixedStats(inst);
                ReleaseEffectRuntimes(inst);
            }
            Array.Clear(_slots, 0, SlotCount);
            _sharedCooldowns.Clear();
        }

        public void AdvanceEffects()
        {
            _effectDispatch?.Advance();
        }

        private void DispatchOnEquipped(EquipmentInstance instance)
        {
            DispatchEffectByTiming(instance, EquipmentEffectInvokeTiming.OnEquipped);
        }

        private void DispatchOnUnequipped(EquipmentInstance instance)
        {
            DispatchEffectByTiming(instance, EquipmentEffectInvokeTiming.OnUnequipped);
        }

        private void DispatchEffectByTiming(EquipmentInstance instance, EquipmentEffectInvokeTiming timing)
        {
            if (instance?.Definition?.Effects == null) return;
            for (int fxIdx = 0; fxIdx < instance.Definition.Effects.Length; fxIdx++)
            {
                var fxDef = instance.Definition.Effects[fxIdx];
                if (fxDef?.Modules == null) continue;
                for (int modIdx = 0; modIdx < fxDef.Modules.Length; modIdx++)
                {
                    var mod = fxDef.Modules[modIdx];
                    if (mod?.InvokeTimings == null) continue;
                    for (int t = 0; t < mod.InvokeTimings.Length; t++)
                    {
                        if (mod.InvokeTimings[t] == timing && mod.CanExecute())
                            mod.Execute(_owner, instance);
                    }
                }
            }
        }

        // ---- IRollback<EquipmentHandlerSnapshot> ----

        public void Capture(ref EquipmentHandlerSnapshot state)
        {
            if (state.Slots == null)
                state.Slots = new EquipmentSlotSnapshot[SlotCount];
            else if (state.Slots.Length != SlotCount)
                state.Slots = new EquipmentSlotSnapshot[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                state.Slots[i] = inst != null
                    ? new EquipmentSlotSnapshot
                    {
                        Occupied = true,
                        EquipmentId = inst.Definition?.Id
                            ?? throw new DeterministicSimulationException(
                                $"Equipment slot {i} has no definition during capture."),
                        StackCount = inst.StackCount,
                        ChargeCount = inst.ChargeCount,
                        ReadyTick = inst.ReadyTick,
                        FixedStatHandles = CloneHandles(inst._fixedStatHandles),
                        EffectStates = CaptureEffectStates(inst.EffectRuntimes),
                    }
                    : EquipmentSlotSnapshot.Empty;
            }

            if (state.SharedCooldowns == null)
                state.SharedCooldowns = new List<EquipmentSharedCooldownSnapshot>();
            else
                state.SharedCooldowns.Clear();

            foreach (var kv in _sharedCooldowns)
            {
                state.SharedCooldowns.Add(new EquipmentSharedCooldownSnapshot
                {
                    GroupId = kv.Key,
                    ReadyTick = kv.Value,
                });
            }
            state.SharedCooldowns.Sort((a, b) => a.GroupId.Value.CompareTo(b.GroupId.Value));

            state.RuntimeRevision = _runtimeRevision;
        }

        public void Restore(in EquipmentHandlerSnapshot state)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst != null) ReleaseEffectRuntimes(inst);
            }
            Array.Clear(_slots, 0, SlotCount);
            _sharedCooldowns.Clear();
            _runtimeRevision = state.RuntimeRevision;

            EquipmentSlotSnapshot[] slots = state.Slots ?? Array.Empty<EquipmentSlotSnapshot>();
            if (slots.Length != SlotCount)
            {
                throw new DeterministicSimulationException(
                    $"Equipment snapshot has {slots.Length} slots; expected {SlotCount}.");
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var ss = slots[i];
                if (!ss.Occupied) continue;
                if (DefinitionDatabase == null ||
                    !DefinitionDatabase.TryGetDefinition(ss.EquipmentId, out EquipmentDefinition definition))
                {
                    throw new DeterministicSimulationException(
                        $"Equipment snapshot references missing definition {ss.EquipmentId}.");
                }
                _slots[i] = new EquipmentInstance
                {
                    Definition = definition,
                    StackCount = ss.StackCount,
                    ChargeCount = ss.ChargeCount,
                    ReadyTick = ss.ReadyTick,
                    _fixedStatHandles = CloneHandles(ss.FixedStatHandles),
                    EffectRuntimes = RestoreEffectStates(definition, ss.EffectStates),
                };
            }

            List<EquipmentSharedCooldownSnapshot> cooldowns =
                state.SharedCooldowns ?? new List<EquipmentSharedCooldownSnapshot>();
            for (int i = 0; i < cooldowns.Count; i++)
            {
                var sc = cooldowns[i];
                _sharedCooldowns[sc.GroupId] = sc.ReadyTick;
            }
        }

        public void Resolve(in RollbackContext context)
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                EquipmentInstance instance = _slots[slot];
                if (instance?._fixedStatHandles == null) continue;
                for (int i = 0; i < instance._fixedStatHandles.Length; i++)
                {
                    StatModifierHandle handle = instance._fixedStatHandles[i];
                    if (handle.OwnerUnitUid != Owner.UnitUid ||
                        !Owner.StatHandler.TryGetModifier(handle, out _))
                        throw new DeterministicSimulationException(
                            $"Equipment slot {slot} references missing Stat modifier {handle.StatSeq}.");
                }
            }
        }
        public void Rebuild(in RollbackContext context) { }

        public override void ResetForPool()
        {
            Array.Clear(_slots, 0, SlotCount);
            _sharedCooldowns.Clear();
            _runtimeRevision = 0;
            _effectDispatch = null;
        }

        private static int ResolveMaxCharge(EquipmentDefinition definition)
        {
            return definition?.Tier == EquipmentTier.Consumable ? definition.MaxStack : 0;
        }

        private static StatModifierHandle[] CloneHandles(StatModifierHandle[] handles)
        {
            if (handles == null || handles.Length == 0) return Array.Empty<StatModifierHandle>();
            var clone = new StatModifierHandle[handles.Length];
            Array.Copy(handles, clone, handles.Length);
            return clone;
        }

        private static EquipmentEffectRuntimeSnapshot[] CaptureEffectStates(
            EquipmentEffectRuntime[] runtimes)
        {
            if (runtimes == null || runtimes.Length == 0)
                return Array.Empty<EquipmentEffectRuntimeSnapshot>();

            var result = new EquipmentEffectRuntimeSnapshot[runtimes.Length];
            for (int i = 0; i < runtimes.Length; i++)
            {
                EquipmentEffectModuleRuntimeState[] modules = runtimes[i]?.ModuleStates;
                var moduleClone = modules == null
                    ? Array.Empty<EquipmentEffectModuleRuntimeState>()
                    : (EquipmentEffectModuleRuntimeState[])modules.Clone();
                result[i] = new EquipmentEffectRuntimeSnapshot { ModuleStates = moduleClone };
            }
            return result;
        }

        private static EquipmentEffectRuntime[] RestoreEffectStates(
            EquipmentDefinition definition,
            EquipmentEffectRuntimeSnapshot[] states)
        {
            int expectedCount = definition.Effects?.Length ?? 0;
            int stateCount = states?.Length ?? 0;
            if (stateCount != expectedCount)
            {
                throw new DeterministicSimulationException(
                    $"Equipment {definition.Id} effect snapshot count {stateCount} does not match definition count {expectedCount}.");
            }

            var result = new EquipmentEffectRuntime[expectedCount];
            for (int i = 0; i < expectedCount; i++)
            {
                var runtime = new EquipmentEffectRuntime(definition.Effects[i]);
                EquipmentEffectModuleRuntimeState[] source = states[i].ModuleStates
                    ?? Array.Empty<EquipmentEffectModuleRuntimeState>();
                if (source.Length != runtime.ModuleStates.Length)
                {
                    throw new DeterministicSimulationException(
                        $"Equipment {definition.Id} effect {i} module-state count mismatch.");
                }
                Array.Copy(source, runtime.ModuleStates, source.Length);
                result[i] = runtime;
            }
            return result;
        }
    }

    public sealed class EquipmentInstance
    {
        public EquipmentDefinition Definition;
        public int StackCount;
        public int ChargeCount;
        public int ReadyTick;
        public EquipmentEffectRuntime[] EffectRuntimes;
        internal StatModifierHandle[] _fixedStatHandles;
    }

    public readonly struct EquipmentFixedStat
    {
        public readonly StatId Stat;
        public readonly fp Value;
        public EquipmentFixedStat(StatId stat, fp value) { Stat = stat; Value = value; }
    }

    public enum EquipmentTier : byte
    {
        Consumable = 0, Basic = 1, Advanced = 2, Finished = 3,
    }

    public readonly struct EquipmentCooldownGroupId : IEquatable<EquipmentCooldownGroupId>
    {
        public readonly int Value;
        public EquipmentCooldownGroupId(int value) => Value = value;
        public bool Equals(EquipmentCooldownGroupId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is EquipmentCooldownGroupId other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public struct EquipmentSlotSnapshot
    {
        public bool Occupied;
        public int EquipmentId;
        public int StackCount;
        public int ChargeCount;
        public int ReadyTick;
        public StatModifierHandle[] FixedStatHandles;
        public EquipmentEffectRuntimeSnapshot[] EffectStates;
        public static readonly EquipmentSlotSnapshot Empty = default;
    }

    public struct EquipmentEffectRuntimeSnapshot
    {
        public EquipmentEffectModuleRuntimeState[] ModuleStates;
    }

    public struct EquipmentSharedCooldownSnapshot
    {
        public EquipmentCooldownGroupId GroupId;
        public int ReadyTick;
    }

    public struct EquipmentHandlerSnapshot
    {
        public EquipmentSlotSnapshot[] Slots;
        public List<EquipmentSharedCooldownSnapshot> SharedCooldowns;
        public int RuntimeRevision;
        public static readonly EquipmentHandlerSnapshot Empty = new EquipmentHandlerSnapshot
        {
            Slots = new EquipmentSlotSnapshot[EquipmentHandler.SlotCount],
            SharedCooldowns = new List<EquipmentSharedCooldownSnapshot>(),
        };
    }
}
