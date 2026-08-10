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
        public int RuntimeRevision => _runtimeRevision;

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
        public void OnHitDealt(in OnHitEventData data) => _effectDispatch?.OnHitDealt(data);

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

        public bool HasTag(
            EquipmentTagUid tag)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var def = _slots[i]?.Definition;
                if (def?.Tags == null) continue;
                for (int j = 0; j < def.Tags.Length; j++)
                {
                    EquipmentTagDefinition candidate =
                        def.Tags[j];
                    if (candidate != null &&
                        candidate.Uid == tag)
                        return true;
                }
            }
            return false;
        }

        public bool HasTag(
            EquipmentTagDefinition tag)
        {
            return tag != null &&
                HasTag(tag.Uid);
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
            TrackAppliedBuffs(instance);
            return true;
        }

        public bool Remove(int slot)
        {
            if ((uint)slot >= SlotCount) return false;
            var instance = _slots[slot];
            if (instance == null) return false;

            RemoveAppliedBuffs(instance);
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

        public EquipmentTransactionSlotState CaptureTransactionSlot(int slot)
        {
            if ((uint)slot >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot));
            EquipmentInstance instance = _slots[slot];
            if (instance == null)
                return EquipmentTransactionSlotState.Empty;
            return new EquipmentTransactionSlotState
            {
                Occupied = true,
                EquipmentId = instance.Definition?.Id
                    ?? throw new DeterministicSimulationException(
                        $"Equipment slot {slot} has no definition."),
                StackCount = instance.StackCount,
                ChargeCount = instance.ChargeCount,
                ReadyTick = instance.ReadyTick,
                EffectStates = CaptureEffectStates(instance.EffectRuntimes),
            };
        }

        public static EquipmentTransactionSlotState CreateInitialTransactionSlot(
            EquipmentDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            int effectCount = definition.Effects?.Length ?? 0;
            var effectStates =
                new List<EquipmentEffectRuntimeSnapshot>(effectCount);
            for (int i = 0; i < effectCount; i++)
            {
                int moduleCount =
                    definition.Effects[i]?.Modules?.Length ?? 0;
                effectStates.Add(
                    new EquipmentEffectRuntimeSnapshot
                    {
                        ModuleStates =
                            new List<EquipmentEffectModuleRuntimeState>(
                                new EquipmentEffectModuleRuntimeState[
                                    moduleCount]),
                    });
            }
            return new EquipmentTransactionSlotState
            {
                Occupied = true,
                EquipmentId = definition.Id,
                StackCount = 1,
                ChargeCount = ResolveMaxCharge(definition),
                ReadyTick = 0,
                EffectStates = effectStates,
            };
        }

        public bool MatchesTransactionSlot(
            int slot,
            in EquipmentTransactionSlotState expected)
        {
            return CaptureTransactionSlot(slot).Equals(expected);
        }

        public void RestoreTransactionSlot(
            int slot,
            in EquipmentTransactionSlotState state)
        {
            if ((uint)slot >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (_slots[slot] != null && !Remove(slot))
                throw new DeterministicSimulationException(
                    $"Failed to clear Equipment slot {slot} for transaction restore.");
            if (!state.Occupied)
                return;
            if (DefinitionDatabase == null ||
                !DefinitionDatabase.TryGetDefinition(
                    state.EquipmentId,
                    out EquipmentDefinition definition))
                throw new DeterministicSimulationException(
                    $"Transaction restore references missing Equipment {state.EquipmentId}.");
            if (!Add(definition, slot))
                throw new DeterministicSimulationException(
                    $"Failed to restore Equipment {state.EquipmentId} to slot {slot}.");
            EquipmentInstance instance = _slots[slot];
            instance.StackCount = state.StackCount;
            instance.ChargeCount = state.ChargeCount;
            instance.ReadyTick = state.ReadyTick;
            instance.EffectRuntimes =
                RestoreEffectStates(definition, state.EffectStates);
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

        private void TrackAppliedBuffs(EquipmentInstance instance)
        {
            // BuffEquipmentModules are dispatched via OnEquipped timing.
            // After dispatch, find which buffs were applied and track them.
            if (instance.Definition?.Effects == null || _owner.BuffHandler == null)
                return;
            for (int fxIdx = 0; fxIdx < instance.Definition.Effects.Length; fxIdx++)
            {
                var fxDef = instance.Definition.Effects[fxIdx];
                if (fxDef?.Modules == null) continue;
                for (int modIdx = 0; modIdx < fxDef.Modules.Length; modIdx++)
                {
                    if (fxDef.Modules[modIdx] is BuffEquipmentModule buffMod &&
                        buffMod.BuffConfigId.IsValid)
                    {
                        if (instance._appliedBuffConfigIds == null)
                            instance._appliedBuffConfigIds = new System.Collections.Generic.List<int>();
                        instance._appliedBuffConfigIds.Add(buffMod.BuffConfigId.Value);
                    }
                }
            }
        }

        private void RemoveAppliedBuffs(EquipmentInstance instance)
        {
            if (instance._appliedBuffConfigIds == null || _owner.BuffHandler == null)
                return;
            for (int i = 0; i < instance._appliedBuffConfigIds.Count; i++)
            {
                _owner.BuffHandler.Remove(new BuffConfigId(instance._appliedBuffConfigIds[i]));
            }
            instance._appliedBuffConfigIds = null;
        }

        public override void ClearForDeath()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst == null) continue;
                RemoveAppliedBuffs(inst);
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
                RemoveAppliedBuffs(inst);
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

        public EquipmentUseCheckResult CheckUse(
            int slot,
            AimSnapshot target)
        {
            if ((uint)slot >= SlotCount)
                return EquipmentUseCheckResult.Rejected;
            EquipmentInstance instance = _slots[slot];
            if (instance == null ||
                !TryGetActiveEffect(
                    instance,
                    out EquipmentEffectDef effect))
                return EquipmentUseCheckResult.Rejected;
            if (_owner.LifeState != LifeState.Alive ||
                !_owner.CapabilityState.CanCast ||
                !_owner.CanRunActiveGameplayThisTick)
                return EquipmentUseCheckResult.Rejected;

            int currentTick =
                SimulationTickContext.Current.Tick;
            EquipmentActiveSettings settings =
                effect.ActiveSettings;
            if (settings.CooldownTicks < 0 ||
                settings.ChargeCost < 0)
                throw new DeterministicSimulationException(
                    $"Equipment {instance.Definition.Id} has invalid active-use settings.");
            if (currentTick < instance.ReadyTick)
                return EquipmentUseCheckResult.Rejected;
            if (settings.SharedCooldownGroup.Value != 0 &&
                _sharedCooldowns.TryGetValue(
                    settings.SharedCooldownGroup,
                    out int sharedReadyTick) &&
                currentTick < sharedReadyTick)
                return EquipmentUseCheckResult.Rejected;
            if (settings.ChargeCost > 0 &&
                instance.ChargeCount <
                    settings.ChargeCost)
                return EquipmentUseCheckResult.Rejected;
            EquipmentEffectModule[] modules =
                effect.Modules ?? Array.Empty<EquipmentEffectModule>();
            if (modules.Length == 0)
                return EquipmentUseCheckResult.Rejected;
            for (int i = 0; i < modules.Length; i++)
            {
                EquipmentEffectModule module = modules[i];
                if (module == null ||
                    !HasTiming(
                        module,
                        EquipmentEffectInvokeTiming.ActiveUse) ||
                    !module.CanExecute())
                    return EquipmentUseCheckResult.Rejected;
            }
            return EquipmentUseCheckResult.Ready;
        }

        public bool Use(int slot, AimSnapshot target)
        {
            if (CheckUse(slot, target) !=
                EquipmentUseCheckResult.Ready)
                return false;
            EquipmentInstance instance = _slots[slot];
            if (!TryGetActiveEffect(
                    instance,
                    out EquipmentEffectDef effect))
                throw new DeterministicSimulationException(
                    "Active equipment disappeared between CheckUse and Use.");
            EquipmentEffectModule[] modules =
                effect.Modules;
            for (int i = 0; i < modules.Length; i++)
                modules[i].Execute(_owner, instance);

            EquipmentActiveSettings settings =
                effect.ActiveSettings;
            int readyTick = checked(
                SimulationTickContext.Current.Tick +
                settings.CooldownTicks);
            instance.ReadyTick = readyTick;
            if (settings.SharedCooldownGroup.Value != 0)
                _sharedCooldowns[
                    settings.SharedCooldownGroup] =
                    readyTick;
            if (settings.ChargeCost > 0)
                instance.ChargeCount -=
                    settings.ChargeCost;
            _runtimeRevision++;
            if (settings.ChargeCost > 0 &&
                instance.ChargeCount == 0 &&
                instance.Definition.Tier ==
                    EquipmentTier.Consumable)
                Remove(slot);
            return true;
        }

        private static bool TryGetActiveEffect(
            EquipmentInstance instance,
            out EquipmentEffectDef activeEffect)
        {
            activeEffect = null;
            EquipmentEffectDef[] effects =
                instance?.Definition?.Effects;
            if (effects == null) return false;
            for (int i = 0; i < effects.Length; i++)
            {
                EquipmentEffectDef effect = effects[i];
                if (effect == null || !effect.IsActive)
                    continue;
                if (activeEffect != null)
                    throw new DeterministicSimulationException(
                        $"Equipment {instance.Definition.Id} has multiple active effects.");
                activeEffect = effect;
            }
            return activeEffect != null;
        }

        private static bool HasTiming(
            EquipmentEffectModule module,
            EquipmentEffectInvokeTiming timing)
        {
            EquipmentEffectInvokeTiming[] timings =
                module.InvokeTimings;
            if (timings == null) return false;
            for (int i = 0; i < timings.Length; i++)
                if (timings[i] == timing) return true;
            return false;
        }

        /// <summary>
        /// Expose the effect dispatch for modules that need context data.
        /// Internal: used by EquipmentEffectModule subclasses.
        /// </summary>
        internal EquipmentEffectDispatch GetEffectDispatch() => _effectDispatch;

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
                state.Slots = new System.Collections.Generic.List<EquipmentSlotSnapshot>(SlotCount);
            else if (state.Slots.Count != SlotCount)
                state.Slots = new System.Collections.Generic.List<EquipmentSlotSnapshot>(SlotCount);

            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                state.Slots.Add(inst != null
                    ? new EquipmentSlotSnapshot
                    {
                        Occupied = true,
                        EquipmentId = inst.Definition?.Id
                            ?? throw new DeterministicSimulationException(
                                $"Equipment slot {i} has no definition during capture."),
                        StackCount = inst.StackCount,
                        ChargeCount = inst.ChargeCount,
                        ReadyTick = inst.ReadyTick,
                        FixedStatHandles = CloneHandles(new System.Collections.Generic.List<StatModifierHandle>(inst._fixedStatHandles)),
                        EffectStates = CaptureEffectStates(inst.EffectRuntimes),
                    }
                    : EquipmentSlotSnapshot.Empty);
            }

            if (state.SharedCooldowns == null)
                state.SharedCooldowns = new System.Collections.Generic.List<EquipmentSharedCooldownSnapshot>();
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

            var slots = state.Slots ?? new System.Collections.Generic.List<EquipmentSlotSnapshot>();
            if (slots.Count != SlotCount)
            {
                throw new DeterministicSimulationException(
                    $"Equipment snapshot has {slots.Count} slots; expected {SlotCount}.");
            }

            for (int i = 0; i < slots.Count; i++)
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
                    _fixedStatHandles = CloneHandles(ss.FixedStatHandles).ToArray(),
                    EffectRuntimes = RestoreEffectStates(definition, ss.EffectStates),
                };
            }

            var cooldowns = state.SharedCooldowns ?? new System.Collections.Generic.List<EquipmentSharedCooldownSnapshot>();
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

        private static System.Collections.Generic.List<StatModifierHandle> CloneHandles(System.Collections.Generic.List<StatModifierHandle> handles)
        {
            if (handles == null || handles.Count == 0) return new System.Collections.Generic.List<StatModifierHandle>();
            var clone = new System.Collections.Generic.List<StatModifierHandle>(handles.Count);
            clone.AddRange(handles);
            return clone;
        }

        private static System.Collections.Generic.List<EquipmentEffectRuntimeSnapshot> CaptureEffectStates(
            EquipmentEffectRuntime[] runtimes)
        {
            if (runtimes == null || runtimes.Length == 0)
                return new System.Collections.Generic.List<EquipmentEffectRuntimeSnapshot>();

            var result = new System.Collections.Generic.List<EquipmentEffectRuntimeSnapshot>(runtimes.Length);
            for (int i = 0; i < runtimes.Length; i++)
            {
                var modules = runtimes[i]?.ModuleStates;
                var moduleClone = modules == null
                    ? new System.Collections.Generic.List<EquipmentEffectModuleRuntimeState>()
                    : new System.Collections.Generic.List<EquipmentEffectModuleRuntimeState>(modules);
                result.Add(new EquipmentEffectRuntimeSnapshot { ModuleStates = moduleClone });
            }
            return result;
        }

        private static EquipmentEffectRuntime[] RestoreEffectStates(
            EquipmentDefinition definition,
            System.Collections.Generic.List<EquipmentEffectRuntimeSnapshot> states)
        {
            int expectedCount = definition.Effects?.Length ?? 0;
            int stateCount = states?.Count ?? 0;
            if (stateCount != expectedCount)
            {
                throw new DeterministicSimulationException(
                    $"Equipment {definition.Id} effect snapshot count {stateCount} does not match definition count {expectedCount}.");
            }

            var result = new EquipmentEffectRuntime[expectedCount];
            for (int i = 0; i < expectedCount; i++)
            {
                var runtime = new EquipmentEffectRuntime(definition.Effects[i]);
                var source = states[i].ModuleStates
                    ?? new System.Collections.Generic.List<EquipmentEffectModuleRuntimeState>();
                if (source.Count != runtime.ModuleStates.Length)
                {
                    throw new DeterministicSimulationException(
                        $"Equipment {definition.Id} effect {i} module-state count mismatch.");
                }
                for (int k = 0; k < source.Count; k++) runtime.ModuleStates[k] = source[k];
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
        internal System.Collections.Generic.List<int> _appliedBuffConfigIds;
    }

    public enum EquipmentUseCheckResult : byte
    {
        Ready = 0,
        NeedApproach = 1,
        Rejected = 2,
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
        public System.Collections.Generic.List<StatModifierHandle> FixedStatHandles;
        public System.Collections.Generic.List<EquipmentEffectRuntimeSnapshot> EffectStates;
        public static readonly EquipmentSlotSnapshot Empty = default;
    }

    public struct EquipmentTransactionSlotState :
        IEquatable<EquipmentTransactionSlotState>
    {
        public bool Occupied;
        public int EquipmentId;
        public int StackCount;
        public int ChargeCount;
        public int ReadyTick;
        public List<EquipmentEffectRuntimeSnapshot> EffectStates;

        public static readonly EquipmentTransactionSlotState Empty = default;

        public bool Equals(EquipmentTransactionSlotState other)
        {
            if (Occupied != other.Occupied)
                return false;
            if (!Occupied)
                return true;
            if (EquipmentId != other.EquipmentId ||
                StackCount != other.StackCount ||
                ChargeCount != other.ChargeCount ||
                ReadyTick != other.ReadyTick)
                return false;
            List<EquipmentEffectRuntimeSnapshot> left =
                EffectStates ?? new List<EquipmentEffectRuntimeSnapshot>();
            List<EquipmentEffectRuntimeSnapshot> right =
                other.EffectStates ?? new List<EquipmentEffectRuntimeSnapshot>();
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
            {
                List<EquipmentEffectModuleRuntimeState> leftModules =
                    left[i].ModuleStates ??
                    new List<EquipmentEffectModuleRuntimeState>();
                List<EquipmentEffectModuleRuntimeState> rightModules =
                    right[i].ModuleStates ??
                    new List<EquipmentEffectModuleRuntimeState>();
                if (leftModules.Count != rightModules.Count)
                    return false;
                for (int j = 0; j < leftModules.Count; j++)
                {
                    EquipmentEffectModuleRuntimeState a = leftModules[j];
                    EquipmentEffectModuleRuntimeState b = rightModules[j];
                    if (a.NextExecuteTick != b.NextExecuteTick ||
                        a.InternalCooldownReadyTick !=
                        b.InternalCooldownReadyTick ||
                        a.StackCount != b.StackCount ||
                        a.TimerTicks != b.TimerTicks)
                        return false;
                }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is EquipmentTransactionSlotState other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Occupied ? 1 : 0;
                hash = (hash * 397) ^ EquipmentId;
                hash = (hash * 397) ^ StackCount;
                hash = (hash * 397) ^ ChargeCount;
                hash = (hash * 397) ^ ReadyTick;
                return hash;
            }
        }
    }

    public struct EquipmentEffectRuntimeSnapshot
    {
        public System.Collections.Generic.List<EquipmentEffectModuleRuntimeState> ModuleStates;
    }

    public struct EquipmentSharedCooldownSnapshot
    {
        public EquipmentCooldownGroupId GroupId;
        public int ReadyTick;
    }

    public struct EquipmentHandlerSnapshot
    {
        public System.Collections.Generic.List<EquipmentSlotSnapshot> Slots;
        public System.Collections.Generic.List<EquipmentSharedCooldownSnapshot> SharedCooldowns;
        public int RuntimeRevision;
        public static readonly EquipmentHandlerSnapshot Empty = new EquipmentHandlerSnapshot
        {
            Slots = new System.Collections.Generic.List<EquipmentSlotSnapshot>(EquipmentHandler.SlotCount),
            SharedCooldowns = new System.Collections.Generic.List<EquipmentSharedCooldownSnapshot>(),
        };
    }
}
