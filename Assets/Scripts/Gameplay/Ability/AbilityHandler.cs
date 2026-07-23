using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilityHandler : UnitHandler, IRollback<AbilityHandlerSnapshot>
    {
        private readonly AbilityBook _book = new AbilityBook();
        private int _nextSessionUid = 1;
        public AbilityDefinitionRegistry DefinitionRegistry { private get; set; }
        public byte PendingSkillPoints { get; private set; }
        public PassiveAbilityRuntime FixedPassive { get; private set; }

        public override void InitializeForNewRuntime()
        {
            _book.Clear();
            _nextSessionUid = 1;
            PendingSkillPoints = 0;
            FixedPassive = null;
        }
        public void AddSlot(AbilitySlotRuntime slot) => _book.AddSlot(slot);

        public void SetFixedPassive(PassiveAbilityDef definition)
        {
            FixedPassive?.EffectRuntime.Deactivate(Owner);
            FixedPassive = definition == null ? null : new PassiveAbilityRuntime(definition);
            FixedPassive?.EffectRuntime.Activate(Owner);
        }

        public bool HandleSignal(AbilitySignal signal)
        {
            if (Owner.HitReaction.InterruptsAbility && signal.Verb != AbilitySignalVerb.Cancel)
                return false;
            var slot = _book.GetSlot(signal.Slot);
            if (slot == null) return false;
            var runtime = slot.GetActiveAbility();
            if (runtime?.Definition?.CastModel == null) return false;
            var model = runtime.Definition.CastModel;

            // Cancel: interrupt active session, no cooldown
            if (signal.Verb == AbilitySignalVerb.Cancel)
            {
                if (runtime.ActiveSession == null) return false;
                var cancelStage = GetCastStage(model, runtime.ActiveSession.CurrentStageKey);
                cancelStage.Def?.OnExit(runtime.ActiveSession, runtime);
                runtime.CancelSession(SimulationTickContext.Current.Tick);
                return true;
            }

            if (runtime.ActiveSession == null)
            {
                if (signal.Verb != AbilitySignalVerb.Commit && signal.Verb != AbilitySignalVerb.Focus)
                    return false;
                if (!runtime.IsReady(SimulationTickContext.Current.Tick)) return false;
                int? nextKey = model.HandleSignal(signal, byte.MaxValue);
                if (nextKey == null) return false;
                if (_nextSessionUid == int.MaxValue)
                    throw new DeterministicSimulationException("Ability session UID exhausted.");
                var session = runtime.BeginSession(
                    _nextSessionUid++, SimulationTickContext.Current.Tick, signal.Aim);
                session.CurrentStageKey = (byte)nextKey.Value;
                var stage = GetCastStage(model, session.CurrentStageKey);
                if (stage.Def == null)
                {
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    return false;
                }
                if (stage.Def.OnEnter(session, runtime) == StageResult.Failed)
                {
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    return false;
                }
                return true;
            }

            var currentStage = GetCastStage(model, runtime.ActiveSession.CurrentStageKey);
            int? transitionKey = model.HandleSignal(signal, runtime.ActiveSession.CurrentStageKey);

            if (transitionKey != null && transitionKey.Value != runtime.ActiveSession.CurrentStageKey)
            {
                currentStage.Def?.OnExit(runtime.ActiveSession, runtime);
                runtime.ActiveSession.CurrentStageKey = (byte)transitionKey.Value;
                runtime.ActiveSession.StageElapsedTicks = 0;
                var newStage = GetCastStage(model, runtime.ActiveSession.CurrentStageKey);
                if (newStage.Def != null && newStage.Def.OnEnter(runtime.ActiveSession, runtime) == StageResult.Failed)
                {
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    return false;
                }
                return true;
            }

            if (signal.Verb == AbilitySignalVerb.Commit && currentStage.Def != null)
            {
                currentStage.Def.OnSignal(runtime.ActiveSession, runtime, signal);
                return true;
            }
            return false;
        }

        public void TickUpdate()
        {
            foreach (var slot in _book.Slots)
            {
                var runtime = slot.GetActiveAbility();
                if (Owner.HitReaction.InterruptsAbility && runtime?.ActiveSession != null)
                {
                    runtime.ActiveSession.Interrupted = true;
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    continue;
                }
                if (runtime?.ActiveSession == null) continue;
                var session = runtime.ActiveSession;
                var model = runtime.Definition.CastModel;
                var stage = GetCastStage(model, session.CurrentStageKey);
                session.StageElapsedTicks++;

                StageResult tickResult = StageResult.Running;
                if (stage.Def != null) tickResult = stage.Def.OnTick(session, runtime);
                if (tickResult == StageResult.Failed)
                { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }

                if (tickResult == StageResult.Completed || session.IsStageTimedOut(stage))
                {
                    stage.Def?.OnExit(session, runtime);
                    int? nextKey = model.HandleSignal(
                        new AbilitySignal { Verb = AbilitySignalVerb.Commit }, session.CurrentStageKey);
                    if (nextKey == null || nextKey.Value == session.CurrentStageKey)
                    {
                        runtime.EndSession(SimulationTickContext.Current.Tick, runtime.Definition?.CooldownTicks ?? 0);
                    }
                    else
                    {
                        session.CurrentStageKey = (byte)nextKey.Value;
                        session.StageElapsedTicks = 0;
                        var nextStage = GetCastStage(model, session.CurrentStageKey);
                        if (nextStage.Def != null && nextStage.Def.OnEnter(session, runtime) == StageResult.Failed)
                            runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    }
                }
            }
        }

        public void ForceInterruptAll()
        {
            foreach (var slot in _book.Slots)
            {
                var runtime = slot.GetActiveAbility();
                if (runtime?.ActiveSession != null)
                {
                    runtime.ActiveSession.Interrupted = true;
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                }
            }
        }

        public void GrantSkillPoint()
        {
            if (PendingSkillPoints == byte.MaxValue)
                throw new DeterministicSimulationException("Pending Ability skill points exhausted.");
            PendingSkillPoints++;
        }

        /// <summary>
        /// Get the AbilityDef for a given slot index (0-3 for QWER).
        /// Returns null if the slot has no ability configured.
        /// </summary>
        public AbilityDef GetAbilityDef(byte slot)
        {
            var slotRuntime = _book.GetSlot(slot);
            var ability = slotRuntime?.GetActiveAbility();
            return ability?.Definition;
        }

        public bool TryAllocateSkillPoint(byte slotIndex)
        {
            if (PendingSkillPoints == 0) return false;
            var slot = _book.GetSlot(slotIndex);
            if (slot == null) return false;
            AbilityRuntime active = slot.GetActiveAbility();
            if (active == null) return false;
            slot.AllocatedPoints++;
            active.Level++;
            EnsureActivePassive(active);
            active.PassiveEffectRuntime?.RankChanged(Owner, active.Level);
            PendingSkillPoints--;
            return true;
        }

        public void OnDamageTaken(in DamageEventData data) =>
            DispatchPassive(PassiveEventKind.DamageTaken, data, default, null, 0, 0);
        public void OnDamageDealt(in DamageEventData data) =>
            DispatchPassive(PassiveEventKind.DamageDealt, data, default, null, 0, 0);
        public void OnHealTaken(in HealEventData data) =>
            DispatchPassive(PassiveEventKind.HealTaken, default, data, null, 0, 0);
        public void OnHealDealt(in HealEventData data) =>
            DispatchPassive(PassiveEventKind.HealDealt, default, data, null, 0, 0);
        public void OnUnitDying(Unit unit) =>
            DispatchPassive(PassiveEventKind.UnitDying, default, default, unit, 0, 0);
        public void OnUnitKill(Unit victim) =>
            DispatchPassive(PassiveEventKind.UnitKill, default, default, victim, 0, 0);
        public void OnLevelUp(int previousLevel, int newLevel) =>
            DispatchPassive(PassiveEventKind.LevelUp, default, default, null, previousLevel, newLevel);

        public void OnUnitDeath(Unit unit)
        {
            FixedPassive?.EffectRuntime.Death(Owner);
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
                slots[i].GetActiveAbility()?.PassiveEffectRuntime?.Death(Owner);
        }

        private static CastStage GetCastStage(CastModelDef model, byte stageKey)
        {
            if (model is CommitCastModelDef c && stageKey == c.Cast.StageKey) return c.Cast;
            if (model is HoldReleaseCastModelDef hr)
            {
                if (stageKey == hr.Hold.StageKey) return hr.Hold;
                if (stageKey == hr.Release.StageKey) return hr.Release;
            }
            if (model is ChannelCastModelDef ch && stageKey == ch.Channel.StageKey) return ch.Channel;
            if (model is ActiveSignalCastModelDef a && stageKey == a.Active.StageKey) return a.Active;
            return default;
        }

        public void Capture(ref AbilityHandlerSnapshot state)
        {
            state.PendingSkillPoints = PendingSkillPoints;
            state.NextSessionUid = _nextSessionUid;
            state.BookSnapshot = _book.Capture();
            state.HasFixedPassive = FixedPassive != null;
            if (FixedPassive != null)
            {
                state.FixedPassiveAbilityId = FixedPassive.Definition.AbilityId;
                state.FixedPassiveRuntimeState = FixedPassive.EffectRuntime.State;
            }
        }
        public void Restore(in AbilityHandlerSnapshot state)
        {
            PendingSkillPoints = state.PendingSkillPoints;
            _nextSessionUid = state.NextSessionUid;
            _book.Restore(state.BookSnapshot);
            if (state.HasFixedPassive)
            {
                if (FixedPassive == null ||
                    FixedPassive.Definition.AbilityId != state.FixedPassiveAbilityId)
                    throw new DeterministicSimulationException(
                        $"Fixed passive topology mismatch for AbilityId {state.FixedPassiveAbilityId}.");
                FixedPassive.EffectRuntime.State = state.FixedPassiveRuntimeState;
            }
            else if (FixedPassive != null)
            {
                throw new DeterministicSimulationException(
                    "Runtime has a fixed passive absent from the Ability snapshot.");
            }
        }
        public void Resolve(in RollbackContext context)
        {
            UnitWorld world = Owner.World;
            if (world == null) return;
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
                slots[i].Resolve(context, world);
            FixedPassive?.EffectRuntime.Resolve(world);
        }
        public void Rebuild(in RollbackContext context)
        {
            FixedPassive?.EffectRuntime.Rebuild(Owner);
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
                slots[i].GetActiveAbility()?.PassiveEffectRuntime?.Rebuild(Owner);
        }

        public override void ClearForDeath()
        {
            ForceInterruptAll();
        }

        public override void ClearForRespawn()
        {
            FixedPassive?.EffectRuntime.Respawn(Owner);
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
                slots[i].GetActiveAbility()?.PassiveEffectRuntime?.Respawn(Owner);
        }

        public override void ResetForPool()
        {
            _book.Clear();
            _nextSessionUid = 1;
            PendingSkillPoints = 0;
            FixedPassive = null;
        }

        private void EnsureActivePassive(AbilityRuntime ability)
        {
            if (ability?.Definition?.PassiveEffect == null || ability.Level <= 0 ||
                ability.PassiveEffectRuntime != null)
                return;
            ability.PassiveEffectRuntime = new AbilityPassiveEffectRuntime(
                ability.Definition.PassiveEffect);
            ability.PassiveEffectRuntime.Activate(Owner);
        }

        private void DispatchPassive(
            PassiveEventKind kind,
            in DamageEventData damage,
            in HealEventData heal,
            Unit relatedUnit,
            int previousLevel,
            int newLevel)
        {
            if (FixedPassive != null &&
                FixedPassive.IsReady(SimulationTickContext.Current.Tick) &&
                InvokePassive(FixedPassive.EffectRuntime, kind, damage, heal,
                    relatedUnit, previousLevel, newLevel))
                FixedPassive.CommitTrigger(Owner);

            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityRuntime ability = slots[i].GetActiveAbility();
                if (ability == null || ability.Level <= 0) continue;
                EnsureActivePassive(ability);
                if (ability.PassiveEffectRuntime != null)
                    InvokePassive(ability.PassiveEffectRuntime, kind, damage, heal,
                        relatedUnit, previousLevel, newLevel);
            }
        }

        private bool InvokePassive(
            AbilityPassiveEffectRuntime runtime,
            PassiveEventKind kind,
            in DamageEventData damage,
            in HealEventData heal,
            Unit relatedUnit,
            int previousLevel,
            int newLevel)
        {
            switch (kind)
            {
                case PassiveEventKind.DamageTaken: return runtime.DamageTaken(Owner, damage);
                case PassiveEventKind.DamageDealt: return runtime.DamageDealt(Owner, damage);
                case PassiveEventKind.HealTaken: return runtime.HealTaken(Owner, heal);
                case PassiveEventKind.HealDealt: return runtime.HealDealt(Owner, heal);
                case PassiveEventKind.UnitDying: return runtime.UnitDying(Owner);
                case PassiveEventKind.UnitKill: return runtime.UnitKill(Owner, relatedUnit);
                case PassiveEventKind.LevelUp: return runtime.LevelUp(Owner, previousLevel, newLevel);
                default: throw new DeterministicSimulationException($"Unsupported passive event {kind}.");
            }
        }

        private enum PassiveEventKind : byte
        {
            DamageTaken,
            DamageDealt,
            HealTaken,
            HealDealt,
            UnitDying,
            UnitKill,
            LevelUp,
        }
    }

    public struct AbilityHandlerSnapshot
    {
        public byte PendingSkillPoints;
        public int NextSessionUid;
        public AbilityBookSnapshot BookSnapshot;
        public bool HasFixedPassive;
        public int FixedPassiveAbilityId;
        public AbilityPassiveRuntimeState FixedPassiveRuntimeState;
    }

    public sealed class AbilityBook
    {
        private readonly List<AbilitySlotRuntime> _slots = new List<AbilitySlotRuntime>();
        public IReadOnlyList<AbilitySlotRuntime> Slots => _slots;
        public void AddSlot(AbilitySlotRuntime slot)
        {
            if (slot == null) throw new System.ArgumentNullException(nameof(slot));
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].SlotIndex == slot.SlotIndex)
                    throw new DeterministicSimulationException(
                        $"Duplicate Ability slot {slot.SlotIndex}.");
            _slots.Add(slot);
            _slots.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        }
        public void Clear() => _slots.Clear();
        public AbilitySlotRuntime GetSlot(byte index)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].SlotIndex == index) return _slots[i];
            return null;
        }

        public AbilityBookSnapshot Capture()
        {
            var snap = new AbilityBookSnapshot { SlotSnapshots = new AbilitySlotSnapshot[_slots.Count] };
            for (int i = 0; i < _slots.Count; i++)
                snap.SlotSnapshots[i] = _slots[i].Capture();
            return snap;
        }
        public void Restore(AbilityBookSnapshot snapshot)
        {
            AbilitySlotSnapshot[] states =
                snapshot.SlotSnapshots ?? System.Array.Empty<AbilitySlotSnapshot>();
            if (states.Length != _slots.Count)
                throw new DeterministicSimulationException(
                    $"Ability slot topology mismatch: runtime={_slots.Count}, snapshot={states.Length}.");
            for (int i = 0; i < states.Length; i++)
            {
                if (_slots[i].SlotIndex != states[i].SlotIndex)
                    throw new DeterministicSimulationException(
                        $"Ability slot identity mismatch at index {i}.");
                _slots[i].Restore(states[i]);
            }
        }
    }

    public sealed class AbilitySlotRuntime
    {
        public byte SlotIndex;
        public byte AllocatedPoints;
        public int ActiveAbilityId;
        private readonly List<AbilityRuntime> _abilities = new List<AbilityRuntime>();

        public void AddAbility(AbilityRuntime runtime)
        {
            if (runtime?.Definition == null)
                throw new System.ArgumentNullException(nameof(runtime));
            for (int i = 0; i < _abilities.Count; i++)
                if (_abilities[i].Definition.AbilityId == runtime.Definition.AbilityId)
                    throw new DeterministicSimulationException(
                        $"Duplicate AbilityId {runtime.Definition.AbilityId} in slot {SlotIndex}.");
            _abilities.Add(runtime);
            _abilities.Sort((a, b) => a.Definition.AbilityId.CompareTo(b.Definition.AbilityId));
        }
        public AbilityRuntime GetActiveAbility()
        {
            foreach (var a in _abilities)
                if (a.Definition?.AbilityId == ActiveAbilityId) return a;
            return _abilities.Count > 0 ? _abilities[0] : null;
        }
        public AbilitySlotSnapshot Capture()
        {
            var runtimes = new AbilityRuntimeSnapshot[_abilities.Count];
            for (int i = 0; i < _abilities.Count; i++)
                _abilities[i].Capture(ref runtimes[i]);
            return new AbilitySlotSnapshot
            {
                SlotIndex = SlotIndex,
                AllocatedPoints = AllocatedPoints,
                ActiveAbilityId = ActiveAbilityId,
                AbilityRuntimes = runtimes,
            };
        }
        public void Restore(AbilitySlotSnapshot snap)
        {
            AbilityRuntimeSnapshot[] states =
                snap.AbilityRuntimes ?? System.Array.Empty<AbilityRuntimeSnapshot>();
            if (states.Length != _abilities.Count)
                throw new DeterministicSimulationException(
                    $"Ability runtime topology mismatch in slot {SlotIndex}.");
            AllocatedPoints = snap.AllocatedPoints;
            ActiveAbilityId = snap.ActiveAbilityId;
            for (int i = 0; i < states.Length; i++)
                _abilities[i].Restore(states[i]);
        }

        public void Resolve(in RollbackContext context, UnitWorld world)
        {
            for (int i = 0; i < _abilities.Count; i++)
                _abilities[i].Resolve(context, world);
        }
    }

    public struct AbilitySlotSnapshot
    {
        public byte SlotIndex;
        public byte AllocatedPoints;
        public int ActiveAbilityId;
        public AbilityRuntimeSnapshot[] AbilityRuntimes;
    }

    public struct AbilityBookSnapshot
    {
        public AbilitySlotSnapshot[] SlotSnapshots;
    }
}
