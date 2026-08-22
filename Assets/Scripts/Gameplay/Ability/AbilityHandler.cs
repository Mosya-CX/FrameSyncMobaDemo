using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilityHandler : UnitHandler, IRollback<AbilityHandlerSnapshot>
    {
        private readonly AbilityBook _book = new AbilityBook();
        private int _nextSessionUid = 1;
        private readonly List<ActiveAbilityCastInfo> _activeCasts = new List<ActiveAbilityCastInfo>(8);
        [SerializeField] private AbilityLoadoutAsset abilityLoadout;
        public AbilityDefinitionRegistry DefinitionRegistry { private get; set; }
        public AbilityLoadoutAsset AbilityLoadout => abilityLoadout;

        /// <summary>Read-only view of currently active ability sessions for presentation/animation.</summary>
        public IReadOnlyList<ActiveAbilityCastInfo> ActiveCasts => _activeCasts;
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

        /// <summary>Current ability runtime on a slot (presentation read).</summary>
        public AbilityRuntime GetActiveRuntime(byte slot) =>
            _book.GetSlot(slot)?.GetActiveAbility();

        public void InitializeConfiguredLoadoutOrThrow()
        {
            if (abilityLoadout == null) return;
            abilityLoadout.ApplyOrThrow(
                this,
                DefinitionRegistry ??
                throw new DeterministicSimulationException(
                    "AbilityHandler has no definition registry."));
            // Design v15.2 1.12.1: a freshly initialized hero starts with
            // one pending skill point (all abilities at level 0).
            if (PendingSkillPoints == 0)
            {
                GrantSkillPoint();
            }
        }

        public void SetFixedPassive(PassiveAbilityDef definition)
        {
            FixedPassive?.EffectRuntime.Deactivate(Owner);
            FixedPassive = definition == null ? null : new PassiveAbilityRuntime(definition);
            FixedPassive?.EffectRuntime.Activate(Owner);
        }

        /// <summary>
        /// Read-only state query for presentation profiles that provide a
        /// passive-ready locomotion or dash variant.
        /// </summary>
        public bool IsFixedPassiveReady(
            int abilityId,
            int logicTick)
        {
            return abilityId > 0 &&
                FixedPassive != null &&
                FixedPassive.Definition.AbilityId == abilityId &&
                FixedPassive.IsReady(logicTick);
        }

        /// <summary>
        /// Gameplay query used by AttackHandler at BeginAttack. The returned
        /// value is captured into AttackSnapshot and is the sole authority for
        /// empowered-attack animation selection during that attack cycle.
        /// </summary>
        public bool IsEmpoweredBasicAttackReady(
            int logicTick,
            Unit target)
        {
            return FixedPassive != null &&
                FixedPassive.IsReady(logicTick) &&
                FixedPassive.Definition.PassiveEffect
                    .CanEmpowerBasicAttack(
                        Owner,
                        target,
                        FixedPassive.EffectRuntime.State);
        }

        public bool HandleSignal(AbilitySignal signal)
        {
            if (Owner.HitReaction.InterruptsAbility &&
                signal.Verb != AbilitySignalVerb.Cancel)
                return false;
            AbilitySlotRuntime slot = _book.GetSlot(signal.Slot);
            if (slot == null) return false;
            AbilityRuntime runtime = slot.GetActiveAbility();
            if (runtime != null)
            {
                runtime.World = Owner.World;
                runtime.CasterUnitUid = Owner.UnitUid;
            }
            if (runtime?.Definition?.CastModel == null) return false;
            // Unlearned abilities (level 0) cannot be cast until a skill
            // point is allocated to their slot.
            if (runtime.Level <= 0 &&
                signal.Verb != AbilitySignalVerb.Cancel)
                return false;
            CastModelDef model = runtime.Definition.CastModel;
            int currentTick = SimulationTickContext.Current.Tick;

            if (signal.Verb == AbilitySignalVerb.Cancel)
            {
                if (runtime.ActiveSession == null) return false;
                CastStage cancelStage =
                    GetCastStage(
                        model,
                        runtime.ActiveSession.CurrentStageKey);
                cancelStage.Def?.OnExit(runtime.ActiveSession, runtime);
                runtime.CancelSession(currentTick);
                return true;
            }

            if (runtime.ActiveSession == null)
            {
                if (signal.Verb != AbilitySignalVerb.Commit &&
                    signal.Verb != AbilitySignalVerb.Focus)
                    return false;
                if (!runtime.IsReady(currentTick)) return false;
                int? nextKey =
                    model.HandleSignal(signal, byte.MaxValue);
                if (nextKey == null) return false;
                CastStage stage =
                    GetCastStage(model, (byte)nextKey.Value);
                if (stage.Def == null ||
                    !ValidateCastRequest(runtime, signal) ||
                    !CanAfford(runtime))
                {
                    return false;
                }
                if (_nextSessionUid == int.MaxValue)
                    throw new DeterministicSimulationException(
                        "Ability session UID exhausted.");
                AbilitySession session = runtime.BeginSession(
                    _nextSessionUid++,
                    currentTick,
                    signal.Aim);
                session.CurrentStageKey = (byte)nextKey.Value;
                if (stage.Def.OnEnter(session, runtime) ==
                    StageResult.Failed)
                {
                    runtime.EndSession(currentTick, 0);
                    return false;
                }
                if (stage.LockMovement)
                {
                    BeginLockedCast(signal.Aim);
                }
                AbilityCostTiming timing =
                    runtime.Definition.CostPlan.Timing;
                if (timing == AbilityCostTiming.OnSessionStart ||
                    (timing == AbilityCostTiming.OnFirstCommit &&
                     signal.Verb == AbilitySignalVerb.Commit))
                {
                    PayCost(runtime, session);
                }
                NotifyAbilityCastOnStageEnter(
                    runtime,
                    stage,
                    signal.Slot,
                    currentTick);
                return true;
            }

            AbilitySession activeSession = runtime.ActiveSession;
            CastStage currentStage =
                GetCastStage(
                    model,
                    activeSession.CurrentStageKey);

            // ToggleCastModelDef: a second Commit on the active stage turns
            // the toggle off and ends the session without starting cooldown.
            if (model is ToggleCastModelDef toggleModel &&
                signal.Verb == AbilitySignalVerb.Commit &&
                activeSession.CurrentStageKey ==
                    toggleModel.Active.StageKey)
            {
                currentStage.Def?.OnExit(
                    activeSession,
                    runtime);
                runtime.EndSession(currentTick, 0);
                return true;
            }

            int? transitionKey =
                model.CanHandleSignal(
                    signal,
                    activeSession.CurrentStageKey,
                    activeSession.StageElapsedTicks)
                    ? model.HandleSignal(
                        signal,
                        activeSession.CurrentStageKey)
                    : null;

            if (transitionKey != null &&
                transitionKey.Value != activeSession.CurrentStageKey)
            {
                if (!ValidateCastRequest(runtime, signal))
                    return false;
                bool mustPay =
                    runtime.Definition.CostPlan.Timing ==
                        AbilityCostTiming.OnFirstCommit &&
                    signal.Verb == AbilitySignalVerb.Commit &&
                    !activeSession.CostPaid;
                if (mustPay && !CanAfford(runtime))
                    return false;

                CastStage newStage =
                    GetCastStage(model, (byte)transitionKey.Value);
                if (newStage.Def == null) return false;
                currentStage.Def?.OnExit(activeSession, runtime);
                activeSession.CurrentStageKey =
                    (byte)transitionKey.Value;
                activeSession.StageElapsedTicks = 0;
                activeSession.Aim = signal.Aim;
                if (newStage.Def.OnEnter(activeSession, runtime) ==
                    StageResult.Failed)
                {
                    runtime.EndSession(currentTick, 0);
                    return false;
                }
                if (newStage.LockMovement)
                {
                    BeginLockedCast(
                        activeSession.Aim);
                }
                if (mustPay) PayCost(runtime, activeSession);
                NotifyAbilityCastOnStageEnter(
                    runtime,
                    newStage,
                    signal.Slot,
                    currentTick);
                return true;
            }

            if (signal.Verb == AbilitySignalVerb.Commit &&
                currentStage.Def != null)
            {
                if (!ValidateCastRequest(runtime, signal))
                    return false;
                if (runtime.Definition.CostPlan.Timing ==
                        AbilityCostTiming.OnFirstCommit &&
                    !activeSession.CostPaid)
                {
                    if (!CanAfford(runtime)) return false;
                    PayCost(runtime, activeSession);
                }
                activeSession.Aim = signal.Aim;
                currentStage.Def.OnSignal(
                    activeSession,
                    runtime,
                    signal);
                return true;
            }
            return false;
        }

        /// <summary>
        /// A locked cast stage begins: cancel the current route and attack
        /// cycle, and face the cast aim direction. Movement/attack inputs are
        /// separately gated by IsCastMovementLocked while the stage is
        /// active (Unit Framework v27.3 movable-cast rule).
        /// </summary>
        private void BeginLockedCast(
            in AimSnapshot aim)
        {
            Owner.Locomotion?.CancelRoute(
                MoveCancelReason.AbilityCastStarted);
            Owner.AttackHandler?.CancelBeforeCommit();
            Owner.AttackHandler?.ResetAttackTimer(
                AttackTimerResetReason.AbilityEffect);

            if (!Owner.CapabilityState.CanTurn ||
                Owner.PhysicsEntity == null)
            {
                return;
            }
            fp2 direction = default;
            switch (aim.Kind)
            {
                case AimKind.Direction:
                    direction = aim.Direction;
                    break;
                case AimKind.Point:
                    direction =
                        aim.TargetPoint -
                        Owner.PhysicsEntity
                            .Transform2D.Position;
                    break;
                case AimKind.Unit:
                    if (Owner.World != null &&
                        Owner.World.TryGetUnit(
                            aim.TargetUnitUid,
                            out Unit target))
                    {
                        direction =
                            target.PhysicsEntity
                                .Transform2D.Position -
                            Owner.PhysicsEntity
                                .Transform2D.Position;
                    }
                    break;
            }
            if (Physics.PhysicsGeometry2D
                    .TryCreateFacing(
                        direction,
                        out fp2 facing,
                        out _))
            {
                Owner.PhysicsEntity.SetLogicForward(
                    facing);
            }
        }

        private bool ValidateCastRequest(
            AbilityRuntime runtime,
            in AbilitySignal signal)
        {
            if (signal.Verb == AbilitySignalVerb.Commit &&
                !ValidateAim(runtime.Definition, signal.Aim))
            {
                return false;
            }

            AbilityCastConditionDef[] conditions =
                runtime.Definition.CastConditions;
            var context =
                new AbilityCastContext(Owner, runtime, signal);
            for (int i = 0; i < conditions.Length; i++)
            {
                if (!conditions[i].CanCast(context))
                    return false;
            }
            return true;
        }

        private bool ValidateAim(
            AbilityDef definition,
            in AimSnapshot aim)
        {
            if (aim.Kind != definition.AimKind)
                return false;
            if (aim.Kind == AimKind.Unit)
            {
                if (Owner.World == null ||
                    !Owner.World.TryGetUnit(
                        aim.TargetUnitUid,
                        out Unit target) ||
                    !target.CapabilityState.IsTargetable)
                {
                    return false;
                }
                return IsWithinCastRange(
                    definition.CastRange,
                    target.PhysicsEntity.Transform2D.Position);
            }
            if (aim.Kind == AimKind.Point)
            {
                return IsWithinCastRange(
                    definition.CastRange,
                    aim.TargetPoint);
            }
            return true;
        }

        private bool IsWithinCastRange(
            fp castRange,
            in fp2 targetPosition)
        {
            if (castRange <= fp.zero) return true;
            fp2 delta =
                targetPosition -
                Owner.PhysicsEntity.Transform2D.Position;
            return delta.x * delta.x + delta.y * delta.y <=
                castRange * castRange;
        }

        private bool CanAfford(AbilityRuntime runtime)
        {
            AbilityCostPlan plan = runtime.Definition.CostPlan;
            if (!plan.HasCost) return true;
            if (Owner.StatHandler == null) return false;
            plan.Resolve(
                runtime.Level,
                out fp resourceCost,
                out fp healthCost);
            return Owner.StatHandler.CurrentCastResource >=
                       resourceCost &&
                   Owner.StatHandler.CurrentHealth >
                       healthCost;
        }

        private void PayCost(
            AbilityRuntime runtime,
            AbilitySession session)
        {
            if (session.CostPaid) return;
            AbilityCostPlan plan = runtime.Definition.CostPlan;
            if (plan.HasCost)
            {
                plan.Resolve(
                    runtime.Level,
                    out fp resourceCost,
                    out fp healthCost);
                Owner.StatHandler.SetCurrentCastResource(
                    Owner.StatHandler.CurrentCastResource -
                    resourceCost);
                Owner.StatHandler.SetCurrentHealth(
                    Owner.StatHandler.CurrentHealth -
                    healthCost);
            }
            session.CostPaid = true;
        }

        private void RefundCostPercent(
            AbilityRuntime runtime,
            AbilitySession session,
            fp percent)
        {
            if (percent <= fp.zero ||
                !session.CostPaid ||
                Owner.StatHandler == null)
                return;
            AbilityCostPlan plan =
                runtime.Definition.CostPlan;
            if (!plan.HasCost) return;
            plan.Resolve(
                runtime.Level,
                out fp resourceCost,
                out fp healthCost);
            if (resourceCost > fp.zero)
            {
                Owner.StatHandler.SetCurrentCastResource(
                    Owner.StatHandler.CurrentCastResource +
                    resourceCost * percent);
            }
            if (healthCost > fp.zero)
            {
                Owner.StatHandler.SetCurrentHealth(
                    Owner.StatHandler.CurrentHealth +
                    healthCost * percent);
            }
        }

        public void TickUpdate()
        {
            TickPassiveRuntimes();
            // Capture the pre-advance cast state first so instant stages
            // (DurationTicks == 0) are still observable by the presentation
            // layer on the Tick they are cast; otherwise the session would
            // end before ActiveCasts is populated and no cast animation
            // would ever play for them.
            CaptureActiveCasts();

            foreach (var slot in _book.Slots)
            {
                var runtime = slot.GetActiveAbility();
                if (runtime != null) { runtime.World = Owner.World; runtime.CasterUnitUid = Owner.UnitUid; }
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

                // CC-interrupt check (Ability Design v15.2 section 5.3):
                // any cast session (channel, hold/charge, toggle) is
                // interrupted when the owner is blocked from casting.
                if (ChannelStageHelper.ShouldInterrupt(Owner))
                {
                    session.Interrupted = true;
                    runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                    continue;
                }
                if (model.Kind == CastModelKind.Channel)
                {
                    var (isActive, progress) = ChannelStageHelper.EvaluateChannel(
                        Owner, SimulationTickContext.Current.Tick, session.StartLogicTick, stage.DurationTicks);
                    if (!isActive) { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }
                }

                // Toggle resource-drain check (Ability Design v15.2 section 5.4)
                if (model is ToggleCastModelDef toggleModel)
                {
                    var resource = Owner.StatHandler?.CurrentCastResource ?? Unity.Mathematics.FixedPoint.fp.zero;
                    var (canContinue, _) = ToggleStageHelper.EvaluateToggle(
                        Owner, isToggledOn: true, toggleModel.ResourcePerTick, ref resource);
                    if (Owner.StatHandler != null) Owner.StatHandler.SetCurrentCastResource(resource);
                    if (!canContinue) { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }
                }

                StageResult tickResult = StageResult.Running;
                if (stage.Def != null) tickResult = stage.Def.OnTick(session, runtime);
                if (tickResult == StageResult.Failed)
                { runtime.EndSession(SimulationTickContext.Current.Tick, 0); continue; }

                bool timedOut = session.IsStageTimedOut(stage);
                HoldReleaseCastModelDef holdTimeoutModel =
                    model as HoldReleaseCastModelDef;
                bool holdTimeoutCancel =
                    timedOut &&
                    holdTimeoutModel != null &&
                    holdTimeoutModel.HoldTimeoutPolicy ==
                        HoldTimeoutPolicy.Cancel &&
                    session.CurrentStageKey ==
                        holdTimeoutModel.Hold.StageKey;
                if (tickResult == StageResult.Completed ||
                    timedOut)
                {
                    stage.Def?.OnExit(session, runtime);
                    if (holdTimeoutCancel)
                    {
                        RefundCostPercent(
                            runtime,
                            session,
                            holdTimeoutModel
                                .RefundCostPercentOnTimeout);
                        // Timeout cancel refunds half the cooldown: the
                        // ability goes on a 50% cooldown instead of none.
                        int fullCooldown =
                            runtime.Definition?
                                .GetCooldownTicks(
                                    runtime.Level) ??
                            0;
                        runtime.EndSession(
                            SimulationTickContext.Current.Tick,
                            fullCooldown / 2);
                        continue;
                    }
                    int? nextKey = model.ResolveStageEnd(
                        session.CurrentStageKey,
                        timedOut);
                    if (nextKey == null || nextKey.Value == session.CurrentStageKey)
                    {
                        runtime.EndSession(
                            SimulationTickContext.Current.Tick,
                            runtime.Definition?.GetCooldownTicks(
                                runtime.Level) ?? 0);
                    }
                    else
                    {
                        session.CurrentStageKey = (byte)nextKey.Value;
                        session.StageElapsedTicks = 0;
                        var nextStage = GetCastStage(model, session.CurrentStageKey);
                        if (nextStage.Def != null && nextStage.Def.OnEnter(session, runtime) == StageResult.Failed)
                            runtime.EndSession(SimulationTickContext.Current.Tick, 0);
                        else
                        {
                            if (nextStage.LockMovement)
                                BeginLockedCast(session.Aim);
                            NotifyAbilityCastOnStageEnter(
                                runtime,
                                nextStage,
                                slot.SlotIndex,
                                SimulationTickContext.Current.Tick);
                        }
                    }
                }
            }

            // Refresh entries whose sessions are still alive after the
            // advance; sessions that ended this Tick keep their captured
            // entry so the presentation layer sees the cast once.
            RefreshActiveCastsAfterAdvance();
        }

        /// <summary>
        /// True while any active cast stage locks voluntary Move / Attack
        /// (cast windup). Movable-cast stages (e.g. charge Hold) return false.
        /// </summary>
        public bool IsCastMovementLocked()
        {
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityRuntime runtime =
                    slots[i].GetActiveAbility();
                AbilitySession session =
                    runtime?.ActiveSession;
                if (session == null ||
                    runtime.Definition?.CastModel == null)
                {
                    continue;
                }
                CastStage stage =
                    GetCastStage(
                        runtime.Definition.CastModel,
                        session.CurrentStageKey);
                if (stage.Def != null &&
                    stage.LockMovement)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Resolves the stable world direction owned by the first active
        /// movement-locking cast stage. Special movement such as a dash may
        /// translate the unit while preserving this cast-facing lock.
        /// </summary>
        internal bool TryGetLockedCastDirection(out fp2 direction)
        {
            direction = default;
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityRuntime runtime =
                    slots[i].GetActiveAbility();
                AbilitySession session = runtime?.ActiveSession;
                if (session == null ||
                    runtime.Definition?.CastModel == null)
                {
                    continue;
                }

                CastStage stage = GetCastStage(
                    runtime.Definition.CastModel,
                    session.CurrentStageKey);
                if (stage.Def == null || !stage.LockMovement)
                    continue;

                switch (session.Aim.Kind)
                {
                    case AimKind.Direction:
                        direction = session.Aim.Direction;
                        break;
                    case AimKind.Point:
                        direction = session.Aim.TargetPoint -
                            Owner.PhysicsEntity.Transform2D.Position;
                        break;
                    case AimKind.Unit:
                        if (Owner.World != null &&
                            Owner.World.TryGetUnit(
                                session.Aim.TargetUnitUid,
                                out Unit target))
                        {
                            direction = target.PhysicsEntity
                                .Transform2D.Position -
                                Owner.PhysicsEntity
                                    .Transform2D.Position;
                        }
                        break;
                }

                if (Physics.PhysicsGeometry2D.TryCreateFacing(
                        direction,
                        out fp2 normalized,
                        out _))
                {
                    direction = normalized;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// True while any ability slot has an active cast/charge session
        /// (windup, hold, channel, ...). During such a session the unit must
        /// not start a normal attack (Unit Framework v27.3 cast rule).
        /// </summary>
        public bool HasActiveCastSession()
        {
            IReadOnlyList<AbilitySlotRuntime> slots =
                _book.Slots;
            for (int i = 0;
                 i < slots.Count;
                 i++)
            {
                AbilityRuntime runtime =
                    slots[i].GetActiveAbility();
                if (runtime?.ActiveSession != null)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasActiveActionStage()
        {
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (IsActionStageActive(slots[i].SlotIndex))
                    return true;
            }
            return false;
        }

        private void CaptureActiveCasts()
        {
            _activeCasts.Clear();
            for (int si = 0;
                 si < _book.Slots.Count;
                 si++)
            {
                AbilityRuntime rt =
                    _book.Slots[si].GetActiveAbility();
                if (rt?.ActiveSession == null)
                {
                    continue;
                }
                _activeCasts.Add(
                    BuildActiveCastInfo(
                        (byte)si,
                        rt));
            }
        }

        private void RefreshActiveCastsAfterAdvance()
        {
            for (int i = 0;
                 i < _activeCasts.Count;
                 i++)
            {
                byte slot = _activeCasts[i].Slot;
                AbilityRuntime rt =
                    _book.GetSlot(slot)?.GetActiveAbility();
                if (rt?.ActiveSession == null)
                {
                    continue;
                }
                _activeCasts[i] =
                    BuildActiveCastInfo(
                        slot,
                        rt);
            }
        }

        private static ActiveAbilityCastInfo
            BuildActiveCastInfo(
                byte slot,
                AbilityRuntime rt)
        {
            AbilitySession session = rt.ActiveSession;
            return new ActiveAbilityCastInfo
            {
                Slot = slot,
                AbilityId =
                    rt.Definition?.AbilityId ?? 0,
                Kind =
                    rt.Definition?.CastModel?.Kind ??
                    CastModelKind.Commit,
                StageKey =
                    session.CurrentStageKey,
                StageElapsedTicks =
                    session.StageElapsedTicks,
                CastRange =
                    rt.Definition?.CastRange ??
                    Unity.Mathematics.FixedPoint.fp.zero,
                AimKind = session.Aim.Kind,
            };
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

        public bool HasActiveSession(byte slot)
        {
            AbilityRuntime runtime =
                _book.GetSlot(slot)?.GetActiveAbility();
            return runtime?.ActiveSession != null;
        }

        /// <summary>
        /// Resolves the authored stage that a signal would enter without
        /// mutating the ability session. The Arbiter consumes only this
        /// structural description; ability legality remains Handler-owned.
        /// </summary>
        public bool TryDescribeRequestedStage(
            byte slot,
            AbilitySignalVerb verb,
            in AimSnapshot aim,
            out CastStage stage,
            out bool isDash)
        {
            return TryDescribeRequestedStageForArbitration(
                slot,
                verb,
                in aim,
                out stage,
                out isDash,
                out _);
        }

        internal bool TryDescribeRequestedStageForArbitration(
            byte slot,
            AbilitySignalVerb verb,
            in AimSnapshot aim,
            out CastStage stage,
            out bool isDash,
            out bool ownsActionRuntime)
        {
            stage = default;
            isDash = false;
            ownsActionRuntime = false;
            AbilityRuntime runtime =
                _book.GetSlot(slot)?.GetActiveAbility();
            CastModelDef model = runtime?.Definition?.CastModel;
            if (runtime == null || model == null)
                return false;

            byte currentStageKey = runtime.ActiveSession == null
                ? byte.MaxValue
                : runtime.ActiveSession.CurrentStageKey;
            var signal = new AbilitySignal
            {
                Slot = slot,
                Verb = verb,
                Aim = aim,
            };
            if (runtime.ActiveSession != null &&
                !model.CanHandleSignal(
                    signal,
                    currentStageKey,
                    runtime.ActiveSession.StageElapsedTicks))
                return false;

            int? nextKey = model.HandleSignal(signal, currentStageKey);
            if (!nextKey.HasValue &&
                runtime.ActiveSession != null &&
                model is ToggleCastModelDef toggle &&
                verb == AbilitySignalVerb.Commit &&
                currentStageKey == toggle.Active.StageKey)
                nextKey = currentStageKey;
            if (!nextKey.HasValue)
                return false;
            stage = GetCastStage(model, (byte)nextKey.Value);
            isDash = stage.Def is DashStageDef;
            // A pure Toggle changes persistent ability state but is not an
            // active cast (D-029). Turning it on or off must not reserve or
            // preempt a Main/Base ActionRuntime.
            ownsActionRuntime = !(model is ToggleCastModelDef);
            return stage.Def != null;
        }

        /// <summary>
        /// Recast waiting windows and pure Toggles retain their AbilitySession
        /// without owning Main/Base ActionRuntime resources.
        /// </summary>
        public bool IsActionStageActive(byte slot)
        {
            AbilityRuntime runtime =
                _book.GetSlot(slot)?.GetActiveAbility();
            AbilitySession session = runtime?.ActiveSession;
            if (session == null || runtime.Definition?.CastModel == null)
                return false;
            if (runtime.Definition.CastModel is ToggleCastModelDef)
                return false;
            if (runtime.Definition.CastModel is
                    SequentialRecastCastModelDef recast &&
                (session.CurrentStageKey == recast.FirstRecastWindow.StageKey ||
                 session.CurrentStageKey == recast.SecondRecastWindow.StageKey))
                return false;
            return GetCastStage(
                runtime.Definition.CastModel,
                session.CurrentStageKey).Def != null;
        }

        public bool TryDescribeActiveStage(
            byte slot,
            out CastStage stage,
            out bool isDash)
        {
            stage = default;
            isDash = false;
            AbilityRuntime runtime =
                _book.GetSlot(slot)?.GetActiveAbility();
            AbilitySession session = runtime?.ActiveSession;
            if (session == null ||
                runtime.Definition?.CastModel == null ||
                !IsActionStageActive(slot))
                return false;
            stage = GetCastStage(
                runtime.Definition.CastModel,
                session.CurrentStageKey);
            isDash = stage.Def is DashStageDef;
            return stage.Def != null;
        }

        public bool IsWaitingForCommit(byte slot)
        {
            AbilityRuntime runtime =
                _book.GetSlot(slot)?.GetActiveAbility();
            if (runtime?.ActiveSession == null ||
                !(runtime.Definition.CastModel is
                    HoldReleaseCastModelDef hold))
            {
                return false;
            }
            return runtime.ActiveSession.CurrentStageKey ==
                hold.Hold.StageKey;
        }

        /// <summary>
        /// Whether the slot may open a local aim indicator right now
        /// (Player Input v1.1 15.3: the indicator must not open while the
        /// ability is on cooldown, and must not open while the active
        /// session is in a stage that cannot accept a real next-stage
        /// Commit, e.g. the minimum recast-delay lockout of a sequential
        /// recast model). Read-only presentation query; never mutates.
        /// </summary>
        public bool CanOpenLocalAim(byte slot)
        {
            AbilityRuntime runtime =
                _book.GetSlot(slot)?.GetActiveAbility();
            if (runtime == null ||
                runtime.Level <= 0)
            {
                return false;
            }
            if (Owner?.CrowdControl != null &&
                Owner.CrowdControl.IsBlocked(
                    UnitActionBlockMask.AbilityCast))
            {
                return false;
            }
            if (runtime.ActiveSession == null)
            {
                return runtime.IsReady(
                    SimulationTickContext.Current.Tick);
            }
            CastModelDef model =
                runtime.Definition?.CastModel;
            if (model == null)
            {
                return false;
            }
            var commit = new AbilitySignal
            {
                Verb = AbilitySignalVerb.Commit,
            };
            int? nextKey = model.HandleSignal(
                commit,
                runtime.ActiveSession.CurrentStageKey);
            if (!nextKey.HasValue ||
                nextKey.Value ==
                    runtime.ActiveSession.CurrentStageKey)
            {
                return false;
            }
            return model.CanHandleSignal(
                commit,
                runtime.ActiveSession.CurrentStageKey,
                runtime.ActiveSession.StageElapsedTicks);
        }

        public int GetCooldownRemainingTicks(byte slot, int currentTick)
        {
            var slotRuntime = _book.GetSlot(slot);
            var ability = slotRuntime?.GetActiveAbility();
            if (ability == null) return 0;
            int remaining = ability.CooldownEndsAtTick - currentTick;
            return remaining > 0 ? remaining : 0;
        }

        public int GetCooldownTotalTicks(byte slot)
        {
            var slotRuntime = _book.GetSlot(slot);
            var ability = slotRuntime?.GetActiveAbility();
            return ability?.Definition?
                .GetCooldownTicks(ability.Level) ?? 0;
        }

        /// <summary>
        /// Presentation cooldown remaining. During a sequential recast
        /// window this exposes the short lockout before the next Commit is
        /// legal; otherwise it exposes the normal ability cooldown.
        /// </summary>
        public int GetDisplayCooldownRemainingTicks(
            byte slot,
            int currentTick)
        {
            AbilityRuntime ability =
                _book.GetSlot(slot)?.GetActiveAbility();
            if (TryGetSequentialRecastLockout(
                    ability,
                    out int remaining,
                    out _))
            {
                return remaining;
            }
            return GetCooldownRemainingTicks(slot, currentTick);
        }

        /// <summary>Total ticks paired with GetDisplayCooldownRemainingTicks.</summary>
        public int GetDisplayCooldownTotalTicks(byte slot)
        {
            AbilityRuntime ability =
                _book.GetSlot(slot)?.GetActiveAbility();
            if (TryGetSequentialRecastLockout(
                    ability,
                    out _,
                    out int total))
            {
                return total;
            }
            return GetCooldownTotalTicks(slot);
        }

        private static bool TryGetSequentialRecastLockout(
            AbilityRuntime ability,
            out int remaining,
            out int total)
        {
            remaining = 0;
            total = 0;
            AbilitySession session = ability?.ActiveSession;
            if (!(ability?.Definition?.CastModel is
                    SequentialRecastCastModelDef recast) ||
                session == null)
            {
                return false;
            }

            if (session.CurrentStageKey ==
                recast.FirstRecastWindow.StageKey)
            {
                total = recast.FirstMinimumRecastDelayTicks;
            }
            else if (session.CurrentStageKey ==
                recast.SecondRecastWindow.StageKey)
            {
                total = recast.SecondMinimumRecastDelayTicks;
            }
            else
            {
                return false;
            }

            remaining = total - session.StageElapsedTicks;
            if (remaining <= 0)
            {
                remaining = 0;
                return false;
            }
            return true;
        }

        public bool TryAllocateSkillPoint(byte slotIndex)
        {
            if (PendingSkillPoints == 0) return false;
            var slot = _book.GetSlot(slotIndex);
            if (slot == null) return false;
            AbilityRuntime active = slot.GetActiveAbility();
            if (active == null) return false;
            if (active.ActiveSession != null) return false;
            if (slot.AllocatedPoints >=
                slot.MaxAllocatedPoints)
                return false;
            int nextRankIndex =
                slot.AllocatedPoints;
            if (slot.RequiredUnitLevelByRank != null &&
                slot.RequiredUnitLevelByRank.Length >
                    nextRankIndex &&
                Owner.Level <
                    slot.RequiredUnitLevelByRank[
                        nextRankIndex])
                return false;
            slot.AllocatedPoints++;
            active.Level++;
            EnsureActivePassive(active);
            active.PassiveEffectRuntime?.SetAbilityLevel(
                active.Level);
            active.PassiveEffectRuntime?.RankChanged(Owner, active.Level);
            PendingSkillPoints--;
            return true;
        }

        /// <summary>
        /// Non-mutating check used by the HUD LevelUp button.
        /// </summary>
        public bool CanAllocateSkillPoint(
            byte slotIndex)
        {
            if (PendingSkillPoints == 0) return false;
            var slot = _book.GetSlot(slotIndex);
            if (slot == null) return false;
            AbilityRuntime active =
                slot.GetActiveAbility();
            if (active == null ||
                active.ActiveSession != null)
                return false;
            if (slot.AllocatedPoints >=
                slot.MaxAllocatedPoints)
                return false;
            int nextRankIndex =
                slot.AllocatedPoints;
            return slot.RequiredUnitLevelByRank == null ||
                slot.RequiredUnitLevelByRank.Length <=
                    nextRankIndex ||
                Owner.Level >=
                    slot.RequiredUnitLevelByRank[
                        nextRankIndex];
        }

        public int GetAbilityLevel(byte slotIndex)
        {
            return _book.GetSlot(slotIndex)
                    ?.GetActiveAbility()
                    ?.Level ?? 0;
        }

        public int GetAbilityLevelById(int abilityId)
        {
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityRuntime runtime = slots[i].GetActiveAbility();
                if (runtime?.Definition?.AbilityId == abilityId)
                    return runtime.Level;
            }
            return 0;
        }

        public void ReduceFixedPassiveCooldown(int reductionTicks)
        {
            if (FixedPassive == null || reductionTicks <= 0)
                return;
            int currentTick = SimulationTickContext.Current.Tick;
            AbilityPassiveRuntimeState state =
                FixedPassive.EffectRuntime.State;
            if (state.NextReadyLogicTick <= currentTick)
                return;
            state.NextReadyLogicTick -= reductionTicks;
            if (state.NextReadyLogicTick < currentTick)
                state.NextReadyLogicTick = currentTick;
            FixedPassive.EffectRuntime.State = state;
        }

        public bool IsUltimateSlot(byte slotIndex)
        {
            return _book.GetSlot(slotIndex)
                    ?.GetActiveAbility()
                    ?.Definition
                    ?.IsUltimate ?? false;
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
        public void OnUnitAssist(Unit victim) =>
            DispatchPassive(PassiveEventKind.UnitAssist, default, default, victim, 0, 0);
        public void OnLevelUp(int previousLevel, int newLevel) =>
            DispatchPassive(PassiveEventKind.LevelUp, default, default, null, previousLevel, newLevel);

        public void OnHitDealt(in OnHitEventData data)
        {
            bool wasReady = FixedPassive != null &&
                FixedPassive.IsReady(SimulationTickContext.Current.Tick);
            if (FixedPassive != null &&
                FixedPassive.EffectRuntime.OnHitDealt(Owner, data) &&
                wasReady)
            {
                FixedPassive.CommitTrigger(Owner);
            }

            IReadOnlyList<AbilitySlotRuntime> slots =
                _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityRuntime ability =
                    slots[i].GetActiveAbility();
                if (ability == null ||
                    ability.Level <= 0)
                    continue;
                EnsureActivePassive(ability);
                ability.PassiveEffectRuntime?.OnHitDealt(
                    Owner,
                    data);
            }
        }

        public void OnUnitDeath(Unit unit)
        {
            FixedPassive?.EffectRuntime.Death(Owner);
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
                slots[i].GetActiveAbility()?.PassiveEffectRuntime?.Death(Owner);
        }

        private static CastStage GetCastStage(CastModelDef model, byte stageKey)
        {
            return model?.GetStage(stageKey) ?? default;
        }

        private void NotifyAbilityCastOnStageEnter(
            AbilityRuntime runtime,
            in CastStage stage,
            byte slot,
            int currentTick)
        {
            if (!stage.NotifyAbilityCastOnEnter)
                return;
            Owner.BuffHandler?.OnAbilityCast(
                new AbilityCastEventData(
                    Owner.UnitUid,
                    runtime.Definition?.AbilityId ?? 0,
                    slot,
                    currentTick));
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

        /// <summary>
        /// Apply a percentage cooldown reduction to all abilities currently on cooldown.
        /// Internal: used by equipment passive modules (OnCastCooldownModule).
        /// </summary>
        internal void ApplyCooldownReductionPercent(fp percent, int currentTick)
        {
            if (percent <= fp.zero) return;
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var runtime = slots[i].GetActiveAbility();
                if (runtime == null || runtime.CooldownEndsAtTick <= currentTick) continue;
                int totalTicks = runtime.Definition?
                    .GetCooldownTicks(runtime.Level) ?? 0;
                if (totalTicks <= 0) continue;
                int reduction = (int)((fp)totalTicks * percent);
                if (reduction <= 0) continue;
                runtime.CooldownEndsAtTick -= reduction;
                if (runtime.CooldownEndsAtTick < currentTick)
                    runtime.CooldownEndsAtTick = currentTick;
            }
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
            ability.PassiveEffectRuntime.SetAbilityLevel(
                ability.Level);
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
            bool wasReady = FixedPassive != null &&
                FixedPassive.IsReady(SimulationTickContext.Current.Tick);
            if (FixedPassive != null &&
                InvokePassive(FixedPassive.EffectRuntime, kind, damage, heal,
                    relatedUnit, previousLevel, newLevel) &&
                wasReady)
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

        private void TickPassiveRuntimes()
        {
            FixedPassive?.EffectRuntime.Tick(Owner);
            IReadOnlyList<AbilitySlotRuntime> slots = _book.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                AbilityRuntime ability = slots[i].GetActiveAbility();
                if (ability == null || ability.Level <= 0)
                    continue;
                EnsureActivePassive(ability);
                ability.PassiveEffectRuntime?.Tick(Owner);
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
                case PassiveEventKind.UnitAssist: return runtime.UnitAssist(Owner, relatedUnit);
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
            UnitAssist,
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
            var snap = new AbilityBookSnapshot { SlotSnapshots = new System.Collections.Generic.List<AbilitySlotSnapshot>(_slots.Count) };
            for (int i = 0; i < _slots.Count; i++)
                snap.SlotSnapshots.Add(_slots[i].Capture());
            return snap;
        }
        public void Restore(AbilityBookSnapshot snapshot)
        {
            var states = snapshot.SlotSnapshots ?? new System.Collections.Generic.List<AbilitySlotSnapshot>();
            if (states.Count != _slots.Count)
                throw new DeterministicSimulationException(
                    $"Ability slot topology mismatch: runtime={_slots.Count}, snapshot={states.Count}.");
            for (int i = 0; i < states.Count; i++)
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
        public byte MaxAllocatedPoints;
        public int[] RequiredUnitLevelByRank =
            System.Array.Empty<int>();
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
            var runtimes = new System.Collections.Generic.List<AbilityRuntimeSnapshot>(_abilities.Count);
            for (int i = 0; i < _abilities.Count; i++)
            {
                var rt = new AbilityRuntimeSnapshot();
                _abilities[i].Capture(ref rt);
                runtimes.Add(rt);
            }
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
            var states = snap.AbilityRuntimes ?? new System.Collections.Generic.List<AbilityRuntimeSnapshot>();
            if (states.Count != _abilities.Count)
                throw new DeterministicSimulationException(
                    $"Ability runtime topology mismatch in slot {SlotIndex}.");
            AllocatedPoints = snap.AllocatedPoints;
            ActiveAbilityId = snap.ActiveAbilityId;
            for (int i = 0; i < states.Count; i++)
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
        public System.Collections.Generic.List<AbilityRuntimeSnapshot> AbilityRuntimes;
    }

    public struct AbilityBookSnapshot
    {
        public System.Collections.Generic.List<AbilitySlotSnapshot> SlotSnapshots;
    }

    /// <summary>
    /// Published each Tick by AbilityHandler to allow presentation
    /// systems (animator, UI cast bar) to observe active casts.
    /// </summary>
    public struct ActiveAbilityCastInfo
    {
        public byte Slot;
        public int AbilityId;
        public CastModelKind Kind;
        public byte StageKey;
        public int StageElapsedTicks;
        public Unity.Mathematics.FixedPoint.fp CastRange;
        public AimKind AimKind;
    }
}
