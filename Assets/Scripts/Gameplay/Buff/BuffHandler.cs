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

        private BuffConfigId[] initialBuffConfigIds =
            Array.Empty<BuffConfigId>();

        /// <summary>Maximum active buffs before lowest-priority buff is dispelled. Default 255.</summary>
        public byte MaxBuffs = 255;

        public override void InitializeForNewRuntime()
        {
            _store.Clear();
            _removalPending.Clear();
        }

        public int Count => _store.Count;

        /// <summary>
        /// Configure this unit's built-in initial buffs (authoring-driven,
        /// not hard-coded per unit type). Applied once after the definition
        /// registry is bound; infinite/permanent buffs then survive death via
        /// the permanent-buff respawn lifecycle.
        /// </summary>
        public void SetInitialBuffConfigs(
            BuffConfigId[] configIds)
        {
            initialBuffConfigIds = configIds ??
                Array.Empty<BuffConfigId>();
        }

        /// <summary>
        /// Apply all configured initial buffs from the bound definition
        /// registry (design: built-in buffs are data-driven per prototype).
        /// </summary>
        public void ApplyInitialBuffs()
        {
            if (DefinitionRegistry == null ||
                initialBuffConfigIds == null)
            {
                return;
            }
            for (int i = 0;
                 i < initialBuffConfigIds.Length;
                 i++)
            {
                BuffConfigId configId =
                    initialBuffConfigIds[i];
                if (!configId.IsValid ||
                    !DefinitionRegistry.TryGet(
                        configId,
                        out BuffDefinition definition))
                {
                    continue;
                }
                Apply(
                    configId,
                    definition,
                    BuffSource.Create(
                        Owner?.UnitUid ?? default,
                        BuffSourceType.Script,
                        0));
            }
        }

        /// <summary>
        /// Read-only, stable BuffConfigId-ordered view for presentation/AI
        /// queries (design v14.2 stable ordering). Never mutated.
        /// </summary>
        public System.Collections.Generic
            .IReadOnlyList<BuffRuntime> GetAllOrdered()
        {
            return _store.GetAllOrdered();
        }

        // ---- Apply / Remove / ReduceStack ----

        public bool Apply(
            BuffConfigId configId,
            BuffDefinition definition,
            UnitUid sourceUnitUid)
        {
            return Apply(
                configId,
                definition,
                BuffSource.Create(
                    sourceUnitUid,
                    BuffSourceType.Script,
                    0));
        }

        public bool Apply(
            BuffConfigId configId,
            BuffDefinition definition,
            in BuffSource source)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!configId.IsValid) return false;

            if (_store.TryGet(configId, out var existing))
                return Reapply(existing, definition, source);

            return ApplyNew(configId, definition, source);
        }

        private bool ApplyNew(
            BuffConfigId configId,
            BuffDefinition definition,
            in BuffSource source)
        {
            var runtime = new BuffRuntime(
                configId,
                definition,
                source);
            EnforceMaxBuffs(definition);
            _store.Add(runtime);

            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].OnAdded(runtime, _owner);

            RunLifecycleGroups(
                definition.LifecycleReactions?.Added,
                runtime);

            int initialStacks = runtime.CurrentStacks;
            for (int i = 0; i < effects.Length; i++)
                effects[i].OnStackChanged(
                    runtime,
                    _owner,
                    0,
                    initialStacks);
            RunStackChangedGroups(
                runtime,
                0,
                initialStacks);

            if (definition.ApplyVfxDefId > 0 &&
                _owner.World != null &&
                _owner.PhysicsEntity != null)
            {
                int tick =
                    SimulationTickContext.Current.Tick;
                float durationSeconds =
                    definition.ApplyVfxDurationSeconds > 0f
                        ? definition.ApplyVfxDurationSeconds
                        : 1f;
                VisualEventOutput.SubmitVfx(
                    new VfxEvent
                    {
                        Id = new PresentationEventId
                        {
                            SourceLogicTick = tick,
                            SourceKind =
                                PresentationSourceKind.Unit,
                            SourceRuntimeUid =
                                _owner.UnitUid,
                            EventSequence =
                                (ushort)(definition.ConfigId
                                    .Value & 0xFFFF),
                            EventKey =
                                PresentationEventKeys
                                    .BuffApplied,
                        },
                        VfxDefId =
                            definition.ApplyVfxDefId,
                        WorldPosition =
                            _owner.PhysicsEntity
                                .Transform2D.Position,
                        AttachToUnit =
                            _owner.UnitUid,
                        DurationScale =
                            (fp)durationSeconds,
                    });
            }

            return true;
        }

        private bool Reapply(
            BuffRuntime runtime,
            BuffDefinition definition,
            in BuffSource source)
        {
            int oldStacks = runtime.CurrentStacks;
            runtime.SetSource(source);

            BuffLifeRuleConfig life =
                definition.Life;
            if (!definition.IsInfinite &&
                life != null)
            {
                switch (life.RefreshMode)
                {
                    case BuffRefreshMode.RefreshToFull:
                        runtime.SetRemainingTicks(
                            definition.DurationTicks);
                        break;
                    case BuffRefreshMode.ExtendByAmount:
                        runtime.SetRemainingTicks(
                            runtime.RemainingTicks +
                            definition.ExtendTicks);
                        break;
                    default:
                        break;
                }
            }

            BuffStackRuleConfig stack =
                definition.Stack;
            if (stack == null ||
                stack.AddMode == BuffAddMode.Add)
            {
                int newStacks = oldStacks + 1;
                if (newStacks >
                    definition.MaxStacks)
                    newStacks =
                        definition.MaxStacks;
                runtime.SetStacks(newStacks);
            }

            int newStackCount = runtime.CurrentStacks;
            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].OnReapplied(
                    runtime,
                    _owner);
            RunLifecycleGroups(
                definition.LifecycleReactions
                    ?.Reapplied,
                runtime);
            if (newStackCount != oldStacks)
            {
                for (int i = 0; i < effects.Length; i++)
                    effects[i].OnStackChanged(runtime, _owner, oldStacks, newStackCount);
                RunStackChangedGroups(
                    runtime,
                    oldStacks,
                    newStackCount);
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
            BuffStackRuleConfig stack =
                runtime.Definition.Stack;
            if (stack != null &&
                stack.ReduceMode ==
                    BuffReduceMode.ClearAll)
            {
                runtime.SetStacks(0);
            }
            else
            {
                int reduceAmount =
                    stack != null &&
                    stack.ReduceAmount > 0
                        ? stack.ReduceAmount
                        : 1;
                runtime.ReduceStacks(
                    count > 0
                        ? count
                        : reduceAmount);
            }
            int newStacks = runtime.CurrentStacks;

            if (newStacks != oldStacks)
            {
                var effects = runtime.GetEffects();
                for (int i = 0; i < effects.Length; i++)
                    effects[i].OnStackChanged(runtime, _owner, oldStacks, newStacks);
                RunStackChangedGroups(
                    runtime,
                    oldStacks,
                    newStacks);
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
                var effects = runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                    effects[j].OnTick(runtime, _owner);
                RunPeriodicReactions(runtime);
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

        public override void ClearForDespawn(UnitDespawnReason reason)
        {
            var ordered = _store.GetAllOrdered();
            var all = new System.Collections.Generic.List<BuffRuntime>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                all.Add(ordered[i]);

            for (int i = 0; i < all.Count; i++)
            {
                var runtime = all[i];
                // Map UnitDespawnReason to RemovalReason for backward compat
                var removalReason = reason switch
                {
                    UnitDespawnReason.SummonExpired => RemovalReason.DurationExpired,
                    UnitDespawnReason.OwnerRemoved => RemovalReason.ManualRemove,
                    UnitDespawnReason.ScriptedCleanup => RemovalReason.ManualRemove,
                    UnitDespawnReason.MatchCleanup => RemovalReason.ManualRemove,
                    _ => RemovalReason.ManualRemove,
                };
                runtime.BeginRemoval(removalReason);
                ClearEffectHandlesForDespawn(runtime);
                runtime.Blackboard.InvalidateAll();
                _store.Remove(runtime.ConfigId);
            }
        }

        [System.Obsolete("Use ClearForDespawn(UnitDespawnReason) instead.")]
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
                ClearEffectHandlesForDespawn(runtime);
                runtime.Blackboard.InvalidateAll();
                _store.Remove(runtime.ConfigId);
            }
        }

        // ---- Typed event handlers (from CombatEvents dispatch) ----

        public void OnDamageTaken(DamageEventData data) =>
            DispatchReaction(BuffReactionKind.DamageTaken, data, default, default, null);
        
        public void OnHitDealt(OnHitEventData data) => DispatchOnHitReaction(data);
        public void OnDamageDealt(DamageEventData data) =>
            DispatchReaction(BuffReactionKind.DamageDealt, data, default, default, null);
        public void OnHealTaken(HealEventData data) =>
            DispatchReaction(BuffReactionKind.HealTaken, default, data, default, null);
        public void OnHealDealt(HealEventData data) =>
            DispatchReaction(BuffReactionKind.HealDealt, default, data, default, null);
        public void OnShieldApplied(ShieldEventData data) =>
            DispatchReaction(BuffReactionKind.ShieldApplied, default, default, data, null);
        public void OnAbilityCast(in AbilityCastEventData data)
        {
            IReadOnlyList<BuffRuntime> runtimes =
                _store.GetAllOrdered();
            for (int i = 0; i < runtimes.Count; i++)
            {
                BuffRuntime runtime = runtimes[i];
                BuffEffect[] effects =
                    runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                    effects[j].OnAbilityCast(
                        runtime,
                        _owner,
                        data);
                RunEventGroups(
                    runtime.Definition
                        .EventReactions?.AbilityCast,
                    runtime);
            }
        }

        public void OnLevelUp(
            int previousLevel,
            int newLevel)
        {
            IReadOnlyList<BuffRuntime> runtimes =
                _store.GetAllOrdered();
            for (int i = 0; i < runtimes.Count; i++)
            {
                BuffRuntime runtime = runtimes[i];
                BuffEffect[] effects =
                    runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                    effects[j].OnLevelUp(
                        runtime,
                        _owner,
                        previousLevel,
                        newLevel);
                RunEventGroups(
                    runtime.Definition
                        .EventReactions?.LevelUp,
                    runtime);
            }
        }
        public void OnUnitDying(Unit unit) =>
            DispatchReaction(BuffReactionKind.UnitDying, default, default, default, unit);
        public void OnUnitDeath(Unit unit) =>
            DispatchReaction(BuffReactionKind.UnitDeath, default, default, default, unit);
        public void OnUnitKill(Unit victim) =>
            DispatchReaction(BuffReactionKind.UnitKill, default, default, default, victim);
        public void OnUnitAssist(Unit victim) =>
            DispatchReaction(BuffReactionKind.UnitAssist, default, default, default, victim);
        public void OnUnitCollisionEnter(in UnitCollisionEnterEvent data) =>
            DispatchCollisionReaction(true, data, default);
        public void OnUnitCollisionExit(in UnitCollisionExitEvent data) =>
            DispatchCollisionReaction(false, default, data);

        // ---- Query methods ----

        public bool HasBuff(BuffConfigId configId) => _store.TryGet(configId, out _);

        /// <summary>Read-only runtime lookup for effects that need to reach a
        /// freshly applied Buff (e.g. successor-buff rules).</summary>
        public bool TryGetRuntime(
            BuffConfigId configId,
            out BuffRuntime runtime) =>
            _store.TryGet(configId, out runtime);

        public bool GetBuffInfo(
            BuffConfigId configId,
            out BuffInfo info)
        {
            if (_store.TryGet(configId, out var runtime))
            {
                info = CreateInfo(runtime);
                return true;
            }
            info = default;
            return false;
        }

        public void GetBuffInfosByTag(
            byte tag,
            List<BuffInfo> result)
        {
            if (result == null || tag == 0)
                return;
            IReadOnlyList<BuffRuntime> ordered =
                _store.GetAllOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                BuffRuntime runtime = ordered[i];
                if (!runtime.Definition.HasTag(tag))
                    continue;
                result.Add(CreateInfo(runtime));
            }
        }

        public bool HasTag(byte tag)
        {
            if (tag == 0) return false;
            IReadOnlyList<BuffRuntime> ordered =
                _store.GetAllOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Definition.HasTag(tag))
                    return true;
            }
            return false;
        }

        public List<BuffInfo> GetAllBuffInfos()
        {
            IReadOnlyList<BuffRuntime> ordered =
                _store.GetAllOrdered();
            var result =
                new List<BuffInfo>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                result.Add(CreateInfo(ordered[i]));
            return result;
        }

        private BuffInfo CreateInfo(BuffRuntime runtime)
        {
            BuffDefinition definition =
                runtime.Definition;
            BuffDisplayInfo display =
                definition.Display;
            BuffTagSet tags = definition.Tags;
            return new BuffInfo(
                runtime.ConfigId,
                display?.Name,
                display?.Description,
                display?.Icon,
                runtime.CurrentStacks,
                definition.MaxStacks,
                definition.IsInfinite,
                runtime.RemainingTicks,
                definition.DurationTicks,
                tags?.TagIds,
                runtime.Source);
        }


        // ---- Tag-based removal ----

        /// <summary>Mass-remove all buffs with a given Tag value. Tag 0 buffs are unaffected.</summary>
        public void RemoveBuffsByTag(byte tag)
        {
            if (tag == 0) return;
            var ordered = _store.GetAllOrdered();
            var toRemove = new System.Collections.Generic.List<BuffRuntime>();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Definition.HasTag(tag))
                    toRemove.Add(ordered[i]);
            }
            for (int i = 0; i < toRemove.Count; i++)
                ExecuteRemoval(toRemove[i], RemovalReason.ManualRemove);
        }

        // ---- Internal helpers ----

        private void EnforceMaxBuffs(
            BuffDefinition incomingDef)
        {
            if (_store.Count < MaxBuffs) return;
            // Find lowest-priority (highest Priority value) non-permanent buff
            var ordered = _store.GetAllOrdered();
            BuffRuntime lowest = null;
            byte lowestPriority = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                var rt = ordered[i];
                if (rt.IsPermanent) continue;
                if (rt.Definition.Priority >= lowestPriority)
                {
                    lowestPriority = rt.Definition.Priority;
                    lowest = rt;
                }
            }
            if (lowest != null && (incomingDef == null || incomingDef.Priority <= lowestPriority))
            {
                ExecuteRemoval(lowest, RemovalReason.ManualRemove);
            }
        }

        private void DispatchOnHitReaction(in OnHitEventData data)
        {
            IReadOnlyList<BuffRuntime> runtimes = _store.GetAllOrdered();
            for (int i = 0; i < runtimes.Count; i++)
            {
                BuffRuntime runtime = runtimes[i];
                BuffEffect[] effects = runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                    effects[j].OnHitDealt(runtime, _owner, data);
                RunEventGroups(
                    runtime.Definition
                        .EventReactions?.OnHitDealt,
                    runtime);
            }
        }

        private void ExecuteRemoval(BuffRuntime runtime, RemovalReason reason)
        {
            if (runtime.IsRemoving) return;
            runtime.BeginRemoval(reason);
            RunLifecycleGroups(
                runtime.Definition
                    .LifecycleReactions?.Removed,
                runtime);
            ReleaseAllEffectHandles(runtime);
            _store.Remove(runtime.ConfigId);
            var removedEffects = runtime.GetEffects();
            for (int i = 0;
                 i < removedEffects.Length;
                 i++)
            {
                removedEffects[i].OnRemovedComplete(
                    runtime,
                    _owner);
            }
            runtime.Blackboard.InvalidateAll();
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

        private void ClearEffectHandlesForDespawn(
            BuffRuntime runtime)
        {
            var effects = runtime.GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i].ClearForDespawn(
                    runtime,
                    _owner);
        }

        // ---- Reaction dispatch ----

        private void ExecuteReactionGroup(
            BuffReactionGroup group,
            BuffRuntime runtime)
        {
            if (group == null)
                return;
            if (group.Condition != null &&
                !group.Condition.Passes(
                    runtime,
                    _owner))
                return;
            BuffReactionActionConfig[] actions =
                group.Actions;
            if (actions == null)
                return;
            for (int i = 0;
                 i < actions.Length;
                 i++)
                actions[i]?.Execute(
                    runtime,
                    _owner);
        }

        private void RunLifecycleGroups(
            BuffReactionGroup[] groups,
            BuffRuntime runtime)
        {
            if (groups == null)
                return;
            for (int i = 0;
                 i < groups.Length;
                 i++)
                ExecuteReactionGroup(
                    groups[i],
                    runtime);
        }

        private void RunEventGroups(
            BuffReactionGroup[] groups,
            BuffRuntime runtime)
        {
            RunLifecycleGroups(groups, runtime);
        }

        private void RunStackChangedGroups(
            BuffRuntime runtime,
            int previousStacks,
            int currentStacks)
        {
            BuffStackChangedReactionGroup[] groups =
                runtime.Definition
                    .LifecycleReactions?.StackChanged;
            if (groups == null)
                return;
            for (int i = 0;
                 i < groups.Length;
                 i++)
            {
                BuffStackChangedReactionGroup group =
                    groups[i];
                if (group == null)
                    continue;
                if (currentStacks < group.MinStack ||
                    currentStacks > group.MaxStack)
                    continue;
                ExecuteReactionGroup(
                    group,
                    runtime);
            }
        }

        private void RunPeriodicReactions(
            BuffRuntime runtime)
        {
            BuffPeriodicReactionGroup[] groups =
                runtime.Definition
                    .LifecycleReactions?.Periodic;
            if (groups == null)
                return;
            int currentTick =
                SimulationTickContext.Current.Tick;
            for (int i = 0;
                 i < groups.Length;
                 i++)
            {
                BuffPeriodicReactionGroup group =
                    groups[i];
                if (group == null ||
                    !group.NextTriggerTickSlot.IsValid)
                    continue;
                int intervalTicks =
                    BuffTickConverter.SecondsToTicks(
                        group.IntervalSeconds);
                if (intervalTicks <= 0)
                    continue;
                int next = runtime.Blackboard
                    .ReadIntOrDefault(
                        group.NextTriggerTickSlot);
                if (next <= 0)
                {
                    next = group.TriggerImmediately
                        ? currentTick
                        : currentTick +
                            intervalTicks;
                    runtime.Blackboard.WriteInt(
                        group.NextTriggerTickSlot,
                        next);
                    if (group.TriggerImmediately)
                        ExecuteReactionGroup(
                            group,
                            runtime);
                    continue;
                }
                if (currentTick >= next)
                {
                    ExecuteReactionGroup(
                        group,
                        runtime);
                    runtime.Blackboard.WriteInt(
                        group.NextTriggerTickSlot,
                        currentTick +
                            intervalTicks);
                }
            }
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
            var buffList = new List<BuffRuntimeSnapshot>();

            var ordered = _store.GetAllOrdered();
            for (int i = 0; i < ordered.Count; i++)
            {
                var runtime = ordered[i];
                buffList.Add(new BuffRuntimeSnapshot
                {
                    ConfigId = runtime.ConfigId,
                    SourceUnitUid = runtime.SourceUnitUid,
                    SourceType = runtime.Source.SourceType,
                    SourceConfigId = runtime.Source.SourceConfigId,
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
            state.Buffs = buffList.ToArray();
        }

        public void Restore(in BuffHandlerSnapshot state)
        {
            _store.Clear();
            BuffRuntimeSnapshot[] states = state.Buffs ?? Array.Empty<BuffRuntimeSnapshot>();
            BuffConfigId previousId = default;
            for (int i = 0; i < states.Length; i++)
            {
                BuffRuntimeSnapshot runtimeState = states[i];
                if (!runtimeState.ConfigId.IsValid ||
                    (i > 0 && previousId.CompareTo(runtimeState.ConfigId) >= 0))
                    throw new DeterministicSimulationException(
                        "Buff snapshots must be in unique ConfigId order.");
                if (DefinitionRegistry == null ||
                    !DefinitionRegistry.TryGet(runtimeState.ConfigId, out BuffDefinition definition))
                    throw new DeterministicSimulationException(
                        $"Buff snapshot references missing definition {runtimeState.ConfigId}.");
                var runtime = new BuffRuntime(
                    runtimeState.ConfigId,
                    definition,
                    BuffSource.Create(
                        runtimeState.SourceUnitUid,
                        runtimeState.SourceType,
                        runtimeState.SourceConfigId));
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
                BuffReactionGroup[] groups =
                    GetEventGroups(runtime, kind);
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
                        case BuffReactionKind.UnitAssist: effect.OnUnitAssist(runtime, _owner, relatedUnit); break;
                    }
                }
                RunEventGroups(groups, runtime);
            }
        }

        private static BuffReactionGroup[] GetEventGroups(
            BuffRuntime runtime,
            BuffReactionKind kind)
        {
            BuffEventReactions reactions =
                runtime.Definition.EventReactions;
            if (reactions == null)
                return null;
            switch (kind)
            {
                case BuffReactionKind.DamageTaken:
                    return reactions.DamageTaken;
                case BuffReactionKind.DamageDealt:
                    return reactions.DamageDealt;
                case BuffReactionKind.HealTaken:
                    return reactions.HealTaken;
                case BuffReactionKind.HealDealt:
                    return reactions.HealDealt;
                case BuffReactionKind.ShieldApplied:
                    return reactions.ShieldApplied;
                case BuffReactionKind.UnitDying:
                    return reactions.UnitDying;
                case BuffReactionKind.UnitDeath:
                    return reactions.UnitDeath;
                case BuffReactionKind.UnitKill:
                    return reactions.UnitKill;
                default:
                    return null;
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
            UnitAssist,
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
                BuffReactionGroup[] groups =
                    isEnter
                        ? runtime.Definition
                            .EventReactions?.CollisionEnter
                        : runtime.Definition
                            .EventReactions?.CollisionExit;
                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    BuffEffect effect = effects[effectIndex];
                    if (effect == null) continue;
                    if (isEnter)
                        effect.OnUnitCollisionEnter(runtime, _owner, enter);
                    else
                        effect.OnUnitCollisionExit(runtime, _owner, exit);
                }
                RunEventGroups(groups, runtime);
            }
        }
    }
}
