using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class AbilitySession
    {
        public int SessionUid;
        public AbilityRuntime Runtime;
        public byte CurrentStageKey;
        public int StartLogicTick;
        public int StageElapsedTicks;
        public AimSnapshot Aim;
        public AbilityBlackboard Blackboard = new AbilityBlackboard();
        public bool Interrupted;
        public bool Cancelled;
        public bool CostPaid;

        public bool IsStageTimedOut(CastStage stage)
        {
            if (stage.DurationTicks == 0) return StageElapsedTicks >= 1;
            return StageElapsedTicks >= stage.DurationTicks;
        }
    }

    public sealed class AbilityRuntime : IRollback<AbilityRuntimeSnapshot>
    {
        public AbilityDef Definition;
        public int Level;
        public UnitWorld World { get; set; }
        public UnitUid CasterUnitUid;
        public int CooldownEndsAtTick;
        public AbilitySession ActiveSession;
        public AbilityPassiveEffectRuntime PassiveEffectRuntime;

        public bool IsLearned => Level > 0;
        public bool IsReady(int currentTick)
            => IsLearned && currentTick >= CooldownEndsAtTick && ActiveSession == null;

        /// <summary>
        /// Current UI icon for this ability instance (design v15.2): the
        /// current cast stage's IconOverride when in a session, otherwise the
        /// AbilityDef.Icon. Presentation-only; never affects Gameplay.
        /// </summary>
        public Sprite GetCurrentIcon()
        {
            Sprite icon = Definition?.Icon;
            if (ActiveSession != null &&
                Definition?.CastModel != null)
            {
                CastStage? stage =
                    Definition.CastModel.GetStage(
                        ActiveSession.CurrentStageKey);
                if (stage.HasValue &&
                    stage.Value.IconOverride != null)
                    icon = stage.Value.IconOverride;
            }
            return icon;
        }

        public void StartCooldown(int currentTick, int cooldownTicks)
            => CooldownEndsAtTick = currentTick + cooldownTicks;
        public void ResetCooldown(int currentTick) => CooldownEndsAtTick = currentTick;

        public AbilitySession BeginSession(int sessionUid, int currentTick, AimSnapshot aim)
        {
            ActiveSession = new AbilitySession
            {
                SessionUid = sessionUid, Runtime = this,
                StartLogicTick = currentTick, Aim = aim,
            };
            return ActiveSession;
        }

        public void EndSession(int currentTick, int cooldownTicks)
        {
            ActiveSession = null;
            if (cooldownTicks > 0) StartCooldown(currentTick, cooldownTicks);
        }

        public void CancelSession(int currentTick)
        {
            if (ActiveSession == null) return;
            ActiveSession.Cancelled = true;
            EndSession(currentTick, 0);
        }

        public void Capture(ref AbilityRuntimeSnapshot state)
        {
            state.AbilityId = Definition?.AbilityId ?? 0;
            state.Level = Level;
            state.CooldownEndsAtTick = CooldownEndsAtTick;
            state.CasterUnitUid = CasterUnitUid;
            state.HasActiveSession = ActiveSession != null;
            state.HasPassiveEffectRuntime = PassiveEffectRuntime != null;
            if (PassiveEffectRuntime != null)
                state.PassiveEffectRuntimeState = PassiveEffectRuntime.State;
            if (ActiveSession != null)
            {
                state.ActiveSession = new AbilitySessionSnapshot
                {
                    SessionUid = ActiveSession.SessionUid,
                    CurrentStageKey = ActiveSession.CurrentStageKey,
                    StartLogicTick = ActiveSession.StartLogicTick,
                    StageElapsedTicks = ActiveSession.StageElapsedTicks,
                    Aim = ActiveSession.Aim,
                    Blackboard = ActiveSession.Blackboard.Capture(),
                    Interrupted = ActiveSession.Interrupted,
                    Cancelled = ActiveSession.Cancelled,
                    CostPaid = ActiveSession.CostPaid,
                };
            }
        }
        public void Restore(in AbilityRuntimeSnapshot state)
        {
            if (Definition == null || Definition.AbilityId != state.AbilityId)
                throw new DeterministicSimulationException(
                    $"Ability runtime snapshot definition mismatch for AbilityId {state.AbilityId}.");
            Level = state.Level;
            CooldownEndsAtTick = state.CooldownEndsAtTick;
            CasterUnitUid = state.CasterUnitUid;
            if (state.HasPassiveEffectRuntime)
            {
                if (Definition.PassiveEffect == null)
                    throw new DeterministicSimulationException(
                        $"Ability {state.AbilityId} snapshot requires a missing passive definition.");
                PassiveEffectRuntime ??= new AbilityPassiveEffectRuntime(Definition.PassiveEffect);
                PassiveEffectRuntime.State = state.PassiveEffectRuntimeState;
            }
            else
            {
                PassiveEffectRuntime = null;
            }
            ActiveSession = null;
            if (state.HasActiveSession)
            {
                ActiveSession = new AbilitySession
                {
                    SessionUid = state.ActiveSession.SessionUid,
                    Runtime = this,
                    CurrentStageKey = state.ActiveSession.CurrentStageKey,
                    StartLogicTick = state.ActiveSession.StartLogicTick,
                    StageElapsedTicks = state.ActiveSession.StageElapsedTicks,
                    Aim = state.ActiveSession.Aim,
                    Interrupted = state.ActiveSession.Interrupted,
                    Cancelled = state.ActiveSession.Cancelled,
                    CostPaid = state.ActiveSession.CostPaid,
                };
                ActiveSession.Blackboard.Restore(state.ActiveSession.Blackboard);
            }
        }
        public void Resolve(in RollbackContext context, UnitWorld world)
        {
            ActiveSession?.Blackboard.ValidateUnitReferences(world);
            PassiveEffectRuntime?.Resolve(world);
            if (ActiveSession != null && ActiveSession.Aim.Kind == AimKind.Unit &&
                !world.TryGetUnit(ActiveSession.Aim.TargetUnitUid, out _))
                throw new DeterministicSimulationException(
                    $"Ability session references missing Aim UnitUid {ActiveSession.Aim.TargetUnitUid}.");
        }
        public void Resolve(in RollbackContext context) { }
        public void Rebuild(in RollbackContext context) { }
    }

    public struct AbilityRuntimeSnapshot
    {
        public int AbilityId;
        public int Level;
        public int CooldownEndsAtTick;
        public UnitUid CasterUnitUid;
        public bool HasActiveSession;
        public AbilitySessionSnapshot ActiveSession;
        public bool HasPassiveEffectRuntime;
        public AbilityPassiveRuntimeState PassiveEffectRuntimeState;
    }

    public struct AbilitySessionSnapshot
    {
        public int SessionUid;
        public byte CurrentStageKey;
        public int StartLogicTick;
        public int StageElapsedTicks;
        public AimSnapshot Aim;
        public AbilityBlackboardSnapshot Blackboard;
        public bool Interrupted;
        public bool Cancelled;
        public bool CostPaid;
    }
}
