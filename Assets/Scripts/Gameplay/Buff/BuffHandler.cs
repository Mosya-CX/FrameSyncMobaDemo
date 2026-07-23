using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class BuffHandler : UnitHandler, IRollback<BuffHandlerSnapshot>
    {
        private readonly BuffStore _store = new BuffStore();
        private readonly List<BuffRuntime> _removalPending = new List<BuffRuntime>();
        private Unit _owner => Owner;
        public BuffDefinitionRegistry DefinitionRegistry { private get; set; }

        public override void InitializeForNewRuntime()
        {
            _store.Clear();
            _removalPending.Clear();
        }

        public int Count => _store.Count;

        // ---- Apply / Remove / ReduceStack ----

        public bool Apply(BuffConfigId configId, BuffDef definition, UnitUid sourceUnitUid)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!configId.IsValid) return false;

            if (_store.TryGet(configId, out var existing))
                return Reapply(existing, definition, sourceUnitUid);

            return ApplyNew(configId, definition, sourceUnitUid);
        }

        private bool ApplyNew(BuffConfigId configId, BuffDef definition, UnitUid sourceUnitUid)
        {
            var runtime = new BuffRuntime(configId, definition, sourceUnitUid);
            _store.Add(runtime);

            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].OnAdded(runtime, _owner);

            return true;
        }

        private bool Reapply(BuffRuntime runtime, BuffDef definition, UnitUid sourceUnitUid)
        {
            int oldStacks = runtime.CurrentStacks;
            runtime.SetSource(sourceUnitUid);

            if (definition.LifeRule == BuffLifeRule.Duration)
                runtime.SetRemainingTicks(definition.DurationTicks);

            if (definition.StackRule == BuffStackRule.Independent)
            {
                int newStacks = oldStacks + 1;
                if (newStacks > definition.MaxStacks) newStacks = definition.MaxStacks;
                runtime.SetStacks(newStacks);
            }

            int newStackCount = runtime.CurrentStacks;
            if (newStackCount != oldStacks)
            {
                var effects = runtime.GetEffects();
                for (int i = 0; i < effects.Length; i++)
                    effects[i].OnStackChanged(runtime, _owner, oldStacks, newStackCount);
            }

            return true;
        }

        public bool Remove(BuffConfigId configId)
        {
            if (!_store.TryGet(configId, out var runtime)) return false;
            ExecuteRemoval(runtime, RemovalReason.ManualRemove);
            return true;
        }

        public bool ReduceStack(BuffConfigId configId, int count)
        {
            if (!_store.TryGet(configId, out var runtime)) return false;

            int oldStacks = runtime.CurrentStacks;
            runtime.ReduceStacks(count);
            int newStacks = runtime.CurrentStacks;

            if (newStacks != oldStacks)
            {
                var effects = runtime.GetEffects();
                for (int i = 0; i < effects.Length; i++)
                    effects[i].OnStackChanged(runtime, _owner, oldStacks, newStacks);
            }

            if (runtime.IsStackExhausted())
                ExecuteRemoval(runtime, RemovalReason.StackExhausted);

            return true;
        }

        // ---- Advance (per-Tick update) ----

        public void Advance()
        {
            int deltaTicks = SimulationTickContext.Current.DeltaTick;
            var ordered = _store.GetAllOrdered();

            for (int i = 0; i < ordered.Count; i++)
            {
                var runtime = ordered[i];
                if (runtime.IsRemoving) continue;
                runtime.Tick(deltaTicks);
                if (runtime.IsExpired())
                    _removalPending.Add(runtime);
            }

            for (int i = 0; i < _removalPending.Count; i++)
                ExecuteRemoval(_removalPending[i], RemovalReason.DurationExpired);
            _removalPending.Clear();
        }

        // ---- Lifecycle hooks ----

        public override void ClearForDeath()
        {
            var ordered = _store.GetAllOrdered();
            var toRemove = new List<BuffRuntime>();

            for (int i = 0; i < ordered.Count; i++)
            {
                var runtime = ordered[i];
                if (runtime.IsPermanent)
                    ReleaseEffectHandlesForDeath(runtime);
                else
                    toRemove.Add(runtime);
            }

            for (int i = 0; i < toRemove.Count; i++)
                ExecuteRemoval(toRemove[i], RemovalReason.DeathCleanup);
        }

        public override void ClearForRespawn()
        {
            var ordered = _store.GetAllOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                var runtime = ordered[i];
                if (!runtime.IsPermanent) continue;
                RebuildEffectHandlesForRespawn(runtime);
            }
        }

        public void ClearForDespawn(RemovalReason reason)
        {
            var ordered = _store.GetAllOrdered();
            var all = new List<BuffRuntime>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                all.Add(ordered[i]);

            for (int i = 0; i < all.Count; i++)
            {
                var runtime = all[i];
                runtime.BeginRemoval(reason);
                ReleaseAllEffectHandles(runtime);
                _store.Remove(runtime.ConfigId);
            }
        }

        // ---- Typed event handlers (from CombatEvents dispatch) ----

        public void OnDamageTaken(DamageEventData data) =>
            DispatchReaction(BuffReactionKind.DamageTaken, data, default, default, null);
        public void OnDamageDealt(DamageEventData data) =>
            DispatchReaction(BuffReactionKind.DamageDealt, data, default, default, null);
        public void OnHealTaken(HealEventData data) =>
            DispatchReaction(BuffReactionKind.HealTaken, default, data, default, null);
        public void OnHealDealt(HealEventData data) =>
            DispatchReaction(BuffReactionKind.HealDealt, default, data, default, null);
        public void OnShieldApplied(ShieldEventData data) =>
            DispatchReaction(BuffReactionKind.ShieldApplied, default, default, data, null);
        public void OnUnitDying(Unit unit) =>
            DispatchReaction(BuffReactionKind.UnitDying, default, default, default, unit);
        public void OnUnitDeath(Unit unit) =>
            DispatchReaction(BuffReactionKind.UnitDeath, default, default, default, unit);
        public void OnUnitKill(Unit victim) =>
            DispatchReaction(BuffReactionKind.UnitKill, default, default, default, victim);
        public void OnUnitCollisionEnter(in UnitCollisionEnterEvent data) =>
            DispatchCollisionReaction(true, data, default);
        public void OnUnitCollisionExit(in UnitCollisionExitEvent data) =>
            DispatchCollisionReaction(false, default, data);

        // ---- Query methods ----

        public bool HasBuff(BuffConfigId configId) => _store.TryGet(configId, out _);
        public IReadOnlyList<BuffRuntime> GetAllBuffs() => _store.GetAllOrdered();

        // ---- Internal helpers ----

        private void ExecuteRemoval(BuffRuntime runtime, RemovalReason reason)
        {
            if (runtime.IsRemoving) return;
            runtime.BeginRemoval(reason);
            ReleaseAllEffectHandles(runtime);
            runtime.Blackboard.InvalidateAll();
            _store.Remove(runtime.ConfigId);
        }

        private void ReleaseAllEffectHandles(BuffRuntime runtime)
        {
            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].OnRemoved(runtime, _owner);
        }

        private void ReleaseEffectHandlesForDeath(BuffRuntime runtime)
        {
            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].ClearForDeath(runtime, _owner);
        }

        private void RebuildEffectHandlesForRespawn(BuffRuntime runtime)
        {
            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].ClearForRespawn(runtime, _owner);
        }

        // ---- IRollback<BuffHandlerSnapshot> ----

        public void Capture(ref BuffHandlerSnapshot state)
        {
            if (state.Buffs == null)
                state.Buffs = new List<BuffRuntimeSnapshot>();
            else
                state.Buffs.Clear();

            var ordered = _store.GetAllOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                var runtime = ordered[i];
                state.Buffs.Add(new BuffRuntimeSnapshot
                {
                    ConfigId = runtime.ConfigId,
                    SourceUnitUid = runtime.SourceUnitUid,
                    RemainingTicks = runtime.RemainingTicks,
                    CurrentStacks = runtime.CurrentStacks,
                    ElapsedTicks = runtime.ElapsedTicks,
                    IsPermanent = runtime.IsPermanent,
                    PeriodicTimer = runtime.PeriodicTimer,
                    RemovalReason = runtime.RemovalReason,
                    IsRemoving = runtime.IsRemoving,
                    Blackboard = runtime.Blackboard.Capture(),
                });
            }
        }

        public void Restore(in BuffHandlerSnapshot state)
        {
            _store.Clear();
            List<BuffRuntimeSnapshot> states = state.Buffs ?? new List<BuffRuntimeSnapshot>();
            BuffConfigId previousId = default;
            for (int i = 0; i < states.Count; i++)
            {
                BuffRuntimeSnapshot runtimeState = states[i];
                if (!runtimeState.ConfigId.IsValid ||
                    (i > 0 && previousId.CompareTo(runtimeState.ConfigId) >= 0))
                    throw new DeterministicSimulationException(
                        "Buff snapshots must be in unique ConfigId order.");
                if (DefinitionRegistry == null ||
                    !DefinitionRegistry.TryGet(runtimeState.ConfigId, out BuffDef definition))
                    throw new DeterministicSimulationException(
                        $"Buff snapshot references missing definition {runtimeState.ConfigId}.");
                var runtime = new BuffRuntime(
                    runtimeState.ConfigId, definition, runtimeState.SourceUnitUid);
                runtime.Restore(runtimeState);
                _store.Add(runtime);
                previousId = runtimeState.ConfigId;
            }
        }

        public void Resolve(in RollbackContext context)
        {
            UnitWorld world = Owner.World;
            if (world == null) return;
            IReadOnlyList<BuffRuntime> runtimes = _store.GetAllOrdered();
            for (int i = 0; i < runtimes.Count; i++)
            {
                UnitUid source = runtimes[i].SourceUnitUid;
                if (source.IsValid() && !world.TryGetUnit(source, out _))
                    throw new DeterministicSimulationException(
                        $"Buff {runtimes[i].ConfigId} references missing source {source}.");
            }
        }
        public void Rebuild(in RollbackContext context) { }

        public override void ResetForPool()
        {
            _store.Clear();
            _removalPending.Clear();
        }

        private void DispatchReaction(
            BuffReactionKind kind,
            in DamageEventData damage,
            in HealEventData heal,
            in ShieldEventData shield,
            Unit relatedUnit)
        {
            IReadOnlyList<BuffRuntime> runtimes = _store.GetAllOrdered();
            for (int i = 0; i < runtimes.Count; i++)
            {
                BuffRuntime runtime = runtimes[i];
                BuffEffect[] effects = runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                {
                    BuffEffect effect = effects[j];
                    switch (kind)
                    {
                        case BuffReactionKind.DamageTaken: effect.OnDamageTaken(runtime, _owner, damage); break;
                        case BuffReactionKind.DamageDealt: effect.OnDamageDealt(runtime, _owner, damage); break;
                        case BuffReactionKind.HealTaken: effect.OnHealTaken(runtime, _owner, heal); break;
                        case BuffReactionKind.HealDealt: effect.OnHealDealt(runtime, _owner, heal); break;
                        case BuffReactionKind.ShieldApplied: effect.OnShieldApplied(runtime, _owner, shield); break;
                        case BuffReactionKind.UnitDying: effect.OnUnitDying(runtime, _owner); break;
                        case BuffReactionKind.UnitDeath: effect.OnUnitDeath(runtime, _owner); break;
                        case BuffReactionKind.UnitKill: effect.OnUnitKill(runtime, _owner, relatedUnit); break;
                    }
                }
            }
        }

        private enum BuffReactionKind : byte
        {
            DamageTaken,
            DamageDealt,
            HealTaken,
            HealDealt,
            ShieldApplied,
            UnitDying,
            UnitDeath,
            UnitKill,
        }

        private void DispatchCollisionReaction(
            bool isEnter,
            in UnitCollisionEnterEvent enter,
            in UnitCollisionExitEvent exit)
        {
            IReadOnlyList<BuffRuntime> runtimes = _store.GetAllOrdered();
            for (int runtimeIndex = 0; runtimeIndex < runtimes.Count; runtimeIndex++)
            {
                BuffRuntime runtime = runtimes[runtimeIndex];
                BuffEffect[] effects = runtime.GetEffects();
                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    BuffEffect effect = effects[effectIndex];
                    if (effect == null) continue;
                    if (isEnter)
                        effect.OnUnitCollisionEnter(runtime, _owner, enter);
                    else
                        effect.OnUnitCollisionExit(runtime, _owner, exit);
                }
            }
        }
    }
}
