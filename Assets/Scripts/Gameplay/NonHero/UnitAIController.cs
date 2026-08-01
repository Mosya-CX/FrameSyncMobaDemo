using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public abstract class UnitAIController
    {
        public UnitAIControllerKind ControllerKind { get; protected set; }
        public UnitUid OwnerUnitUid { get; protected set; }
        public Unit OwnerUnit { get; private set; }

        protected UnitAIController(Unit owner)
        {
            OwnerUnit = owner ?? throw new ArgumentNullException(nameof(owner));
            OwnerUnitUid = owner.UnitUid;
        }

        public int FirstActiveLogicTick => OwnerUnitUid.SpawnLogicTick + 1;

        public bool CanRunAIThisTick
        {
            get
            {
                if (OwnerUnit == null) return false;
                if (OwnerUnit.LifeState != LifeState.Alive) return false;
                return SimulationTickContext.Current.Tick > OwnerUnitUid.SpawnLogicTick;
            }
        }

        protected void IssueMoveOrder(fp2 targetPosition)
        {
            IssueOrderIfChanged(
                Order.CreateMove(targetPosition));
        }

        protected void IssueAttackOrder(UnitUid targetUid)
        {
            IssueAttackOrder(targetUid, true);
        }

        protected void IssueAttackOrder(
            UnitUid targetUid,
            bool allowChase)
        {
            IssueOrderIfChanged(
                Order.CreateAttack(
                    targetUid,
                    allowChase));
        }

        protected void IssueLaneAdvanceOrder(
            int laneId)
        {
            IssueOrderIfChanged(
                Order.CreateLaneAdvance(laneId));
        }

        protected void IssueReturnToCampOrder(
            int campId)
        {
            IssueOrderIfChanged(
                Order.CreateReturnToCamp(campId));
        }

        protected void ClearOrder()
        {
            IssueOrderIfChanged(Order.None);
        }

        private void IssueOrderIfChanged(
            in Order order)
        {
            if (OwnerUnit == null)
                return;
            UnitIntent current = OwnerUnit.Intent;
            bool unchanged = order.Kind switch
            {
                OrderKind.None =>
                    current.Kind == IntentKind.None,
                OrderKind.Move =>
                    current.Kind ==
                        IntentKind.MoveToPosition &&
                    current.TargetPosition.x ==
                        order.Move_TargetPosition.x &&
                    current.TargetPosition.y ==
                        order.Move_TargetPosition.y,
                OrderKind.Attack =>
                    current.Kind ==
                        IntentKind.AttackTarget &&
                    current.TargetUnit ==
                        order.Attack_TargetUnit &&
                    current.AllowChase ==
                        order.Attack_AllowChase,
                OrderKind.LaneAdvance =>
                    current.Kind ==
                        IntentKind.LaneAdvance,
                OrderKind.ReturnToCamp =>
                    current.Kind ==
                        IntentKind.ReturnToCamp,
                _ => false,
            };
            if (!unchanged)
                OwnerUnit.ApplyOrder(order);
        }

        public abstract void AIThink();

        public virtual void Capture(ref UnitAIControllerSnapshot state)
        {
            state.ControllerKind = ControllerKind;
            state.OwnerUnitUid = OwnerUnitUid;
        }

        public virtual void Restore(in UnitAIControllerSnapshot state)
        {
            ControllerKind = state.ControllerKind;
            OwnerUnitUid = state.OwnerUnitUid;
        }

        public virtual void ClearForDeath()
        {
        }

        public virtual void ClearForRespawn()
        {
        }

        public virtual void Resolve(in RollbackContext context) { }

        public virtual void Rebuild(in RollbackContext context) { }
    }

    public sealed class MinionAIController : UnitAIController
    {
        private static readonly fp AcquireRangePadding = fp.one;
        private static readonly fp ChaseMaxDistance = (fp)12m;
        private static readonly fp LaneReturnThreshold = (fp)15m;
        private const int TargetLockTicks = 30;
        private const int DecisionIntervalTicks = 5;

        public MinionAIState AIState { get; private set; }
        public int LaneId { get; set; }
        public UnitUid CurrentTargetUid =>
            OwnerUnit != null &&
            OwnerUnit.Intent.Kind ==
                IntentKind.AttackTarget
                ? OwnerUnit.Intent.TargetUnit
                : default;
        public fp2 EngageOrigin { get; private set; }
        public int TargetLockedUntilTick { get; private set; }
        public int NextDecisionLogicTick { get; private set; }
        public UnitUid PendingAssistTargetUid { get; private set; }
        public int PendingAssistExpireLogicTick { get; private set; }

        public MinionAIController(Unit owner, int laneId) : base(owner)
        {
            ControllerKind = UnitAIControllerKind.Minion;
            LaneId = laneId;
            AIState = MinionAIState.AdvanceLane;
            EngageOrigin = fp2.zero;
            NextDecisionLogicTick =
                owner.UnitUid.SpawnLogicTick + 1;
        }

        public override void AIThink()
        {
            if (!CanRunAIThisTick)
                return;
            int currentTick = SimulationTickContext.Current.Tick;
            if (currentTick < NextDecisionLogicTick)
                return;

            UnitUid currentTarget = CurrentTargetUid;
            bool currentTargetIsLegal =
                currentTarget.IsValid() &&
                IsValidTarget(currentTarget) &&
                IsWithinChaseBoundary();
            if (currentTargetIsLegal)
            {
                AIState = MinionAIState.EngageTarget;
                ScheduleNextDecision(currentTick);
                return;
            }

            UnitUid bestTarget = FindBestTarget();
            if (currentTarget.IsValid() &&
                currentTarget != bestTarget)
                ClearOrder();

            if (bestTarget.IsValid())
            {
                AcquireTarget(bestTarget);
                ScheduleNextDecision(currentTick);
                return;
            }

            LaneRuntimeData lane =
                ResolveLaneOrThrow();
            fp2 currentPosition =
                OwnerUnit.PhysicsEntity
                    .Transform2D.Position;
            lane.GetNearestCenterlinePoint(
                currentPosition,
                out fp distanceFromLaneSq);
            if (distanceFromLaneSq >
                LaneReturnThreshold *
                LaneReturnThreshold)
            {
                AIState = MinionAIState.ReturnToLane;
            }
            else
            {
                AIState = MinionAIState.AdvanceLane;
            }
            // The lane flow field owns both forward progress and lateral
            // correction. ReturnToLane remains an AI state, but it must not
            // replace the field with a competing point-target A* route.
            IssueLaneAdvanceOrder(LaneId);
            ScheduleNextDecision(currentTick);
        }

        private UnitUid FindBestTarget()
        {
            if (OwnerUnit.World == null) return default;

            var allUnits = OwnerUnit.World.GetAllUnits();
            fp2 myPos = OwnerUnit.PhysicsEntity.Transform2D.Position;
            TeamId myTeam = OwnerUnit.TeamId;

            UnitUid bestTarget = default;
            int bestPriority = int.MaxValue;
            fp bestDistanceSq = new fp(int.MaxValue);
            fp acquireRange = GetAcquireRange();
            fp acquireRangeSq = acquireRange * acquireRange;

            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit candidate = allUnits[i];
                if (candidate == OwnerUnit) continue;
                if (candidate.TeamId == myTeam) continue;
                if (candidate.LifeState != LifeState.Alive) continue;

                if (!IsValidTarget(candidate.UnitUid)) continue;
                fp2 candidatePos = candidate.PhysicsEntity.Transform2D.Position;
                fp distSq = fpmath.lengthsq(myPos - candidatePos);

                if (distSq > acquireRangeSq) continue;

                // Formal bands: ordinary minions, heroes/summons, structures.
                int priority = GetTargetPriority(candidate);

                if (priority < bestPriority
                    || (priority == bestPriority && distSq < bestDistanceSq))
                {
                    bestPriority = priority;
                    bestDistanceSq = distSq;
                    bestTarget = candidate.UnitUid;
                }
                else if (priority == bestPriority && distSq == bestDistanceSq
                    && bestTarget.CompareTo(candidate.UnitUid) > 0)
                {
                    // Stable tie-break by UnitUid
                    bestTarget = candidate.UnitUid;
                }
            }

            return bestTarget;
        }

        private fp GetAcquireRange()
        {
            return OwnerUnit.AttackHandler != null
                ? OwnerUnit.AttackHandler.CurrentAttackRange +
                  AcquireRangePadding
                : fp.zero;
        }

        private static int GetTargetPriority(Unit candidate)
        {
            if (candidate.OwnerUid.IsValid())
                return 3;
            switch (candidate.UnitKind)
            {
                case UnitKind.Minion: return 2;
                case UnitKind.Hero: return 3;
                case UnitKind.Structure: return 4;
                default: return int.MaxValue;
            }
        }

        private bool IsValidTarget(UnitUid targetUid)
        {
            if (!targetUid.IsValid()) return false;
            if (OwnerUnit.World == null) return false;
            if (!OwnerUnit.World.TryGetUnit(targetUid, out Unit target)) return false;
            if (target.LifeState != LifeState.Alive ||
                !target.CapabilityState.IsTargetable ||
                target.PhysicsEntity == null) return false;
            if (target.TeamId == OwnerUnit.TeamId) return false;
            if (target.TeamId == TeamId.Neutral) return false;
            if (GetTargetPriority(target) == int.MaxValue)
                return false;

            fp2 myPos = OwnerUnit.PhysicsEntity.Transform2D.Position;
            fp2 targetPos = target.PhysicsEntity.Transform2D.Position;
            fp distSq = fpmath.lengthsq(myPos - targetPos);

            return distSq <= ChaseMaxDistance * ChaseMaxDistance;
        }

        private bool IsWithinChaseBoundary()
        {
            fp2 currentPos = OwnerUnit.MovementHandler?.Position ?? fp2.zero;
            fp distFromOriginSq = fpmath.dot(currentPos - EngageOrigin, currentPos - EngageOrigin);
            if (distFromOriginSq >
                ChaseMaxDistance *
                ChaseMaxDistance)
                return false;
            LaneRuntimeData lane =
                ResolveLaneOrThrow();
            lane.GetNearestCenterlinePoint(
                currentPos,
                out fp laneDistanceSq);
            return laneDistanceSq <=
                LaneReturnThreshold *
                LaneReturnThreshold;
        }

        public void AcquireTarget(UnitUid targetUid)
        {
            if (!targetUid.IsValid())
                throw new ArgumentException(
                    "Target UID must be valid.",
                    nameof(targetUid));
            IssueAttackOrder(targetUid);
            AIState = MinionAIState.EngageTarget;
            EngageOrigin = OwnerUnit.MovementHandler?.Position ?? fp2.zero;
            TargetLockedUntilTick = SimulationTickContext.Current.Tick + TargetLockTicks;
        }

        private LaneRuntimeData ResolveLaneOrThrow()
        {
            if (OwnerUnit.World?.MinionSystem ==
                    null ||
                !OwnerUnit.World.MinionSystem
                    .TryGetLane(
                        LaneId,
                        out LaneRuntimeData lane))
                throw new DeterministicSimulationException(
                    $"Minion {OwnerUnitUid} cannot resolve Lane {LaneId}.");
            return lane;
        }

        private void ScheduleNextDecision(
            int currentTick)
        {
            NextDecisionLogicTick = checked(
                currentTick +
                DecisionIntervalTicks);
        }

        public override void Capture(ref UnitAIControllerSnapshot state)
        {
            base.Capture(ref state);
            state.MinionState = AIState;
            state.LaneId = LaneId;
            state.MinionNextDecisionLogicTick =
                NextDecisionLogicTick;
            state.MinionTargetLockUntilLogicTick =
                TargetLockedUntilTick;
            state.MinionEngageOrigin =
                EngageOrigin;
            state.MinionPendingAssistTargetUid =
                PendingAssistTargetUid;
            state.MinionPendingAssistExpireLogicTick =
                PendingAssistExpireLogicTick;
        }

        public override void Restore(in UnitAIControllerSnapshot state)
        {
            base.Restore(in state);
            AIState = state.MinionState;
            LaneId = state.LaneId;
            NextDecisionLogicTick =
                state.MinionNextDecisionLogicTick;
            TargetLockedUntilTick =
                state.MinionTargetLockUntilLogicTick;
            EngageOrigin =
                state.MinionEngageOrigin;
            PendingAssistTargetUid =
                state.MinionPendingAssistTargetUid;
            PendingAssistExpireLogicTick =
                state.MinionPendingAssistExpireLogicTick;
        }
    }

    public sealed class MonsterAIController : UnitAIController
    {
        private const int DecisionIntervalTicks = 5;

        public MonsterAIState AIState { get; private set; }
        public int CampId { get; private set; }
        public int CampSlotIndex { get; private set; }
        public int NextDecisionLogicTick { get; private set; }
        private JungleCamp camp;

        public MonsterAIController(
            Unit owner,
            int campId,
            int campSlotIndex = 0)
            : base(owner)
        {
            ControllerKind = UnitAIControllerKind.Monster;
            CampId = campId;
            CampSlotIndex = campSlotIndex;
            AIState = MonsterAIState.CampIdle;
            NextDecisionLogicTick =
                owner.UnitUid.SpawnLogicTick + 1;
        }

        public override void AIThink()
        {
            if (!CanRunAIThisTick)
                return;
            ResolveCampOrThrow();
            int currentTick =
                SimulationTickContext.Current.Tick;
            if (currentTick <
                NextDecisionLogicTick)
                return;
            switch (camp.State)
            {
                case JungleCampState.Dormant:
                case JungleCampState.WaitingRespawn:
                    ClearOrder();
                    break;
                case JungleCampState.Idle:
                    ThinkCampIdle();
                    break;
                case JungleCampState.InCombat:
                    ThinkEngage();
                    break;
                case JungleCampState.Returning:
                    ThinkReturnToCamp();
                    break;
            }
            NextDecisionLogicTick = checked(
                currentTick +
                DecisionIntervalTicks);
        }

        public void WakeForCampStateChange()
        {
            NextDecisionLogicTick =
                SimulationTickContext.Current.Tick;
        }

        private void ThinkCampIdle()
        {
            AIState = MonsterAIState.CampIdle;
            ClearOrder();
            UnitUid targetUid = FindNearestTarget();
            if (targetUid.IsValid())
            {
                camp.TryBeginCombat(targetUid);
            }
        }

        private void ThinkEngage()
        {
            UnitUid targetUid =
                camp.PrimaryTargetUid;
            if (!targetUid.IsValid() ||
                !IsTargetAlive(targetUid))
            {
                ClearOrder();
                return;
            }
            AIState = MonsterAIState.EngageTarget;
            IssueAttackOrder(targetUid);
        }

        private void ThinkReturnToCamp()
        {
            AIState = MonsterAIState.ReturnToCamp;
            IssueReturnToCampOrder(CampId);
        }

        private UnitUid FindNearestTarget()
        {
            IReadOnlyList<Unit> allUnits =
                OwnerUnit.World.GetAllUnits();
            fp2 myPosition =
                OwnerUnit.PhysicsEntity
                    .Transform2D.Position;
            UnitUid bestTarget = default;
            fp bestDistanceSq =
                camp.HardLeashRadiusSq;
            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit candidate = allUnits[i];
                if (candidate == null ||
                    candidate.LifeState !=
                        LifeState.Alive ||
                    candidate.TeamId ==
                        OwnerUnit.TeamId ||
                    (candidate.UnitKind !=
                         UnitKind.Hero &&
                     candidate.UnitKind !=
                         UnitKind.Minion))
                    continue;
                fp2 delta =
                    candidate.PhysicsEntity
                        .Transform2D.Position -
                    myPosition;
                fp distanceSq =
                    fpmath.lengthsq(delta);
                if (distanceSq <
                        bestDistanceSq ||
                    (distanceSq ==
                         bestDistanceSq &&
                     (!bestTarget.IsValid() ||
                      candidate.UnitUid.CompareTo(
                          bestTarget) < 0)))
                {
                    bestDistanceSq = distanceSq;
                    bestTarget = candidate.UnitUid;
                }
            }
            return bestTarget;
        }

        private bool IsTargetAlive(UnitUid uid)
        {
            if (!uid.IsValid() || OwnerUnit.World == null) return false;
            return OwnerUnit.World.TryGetUnit(uid, out var unit) && unit.LifeState == LifeState.Alive;
        }

        public override void Capture(ref UnitAIControllerSnapshot state)
        {
            base.Capture(ref state);
            state.MonsterState = AIState;
            state.CampId = CampId;
            state.MonsterCampSlotIndex =
                CampSlotIndex;
            state.MonsterNextDecisionLogicTick =
                NextDecisionLogicTick;
        }

        public override void Restore(in UnitAIControllerSnapshot state)
        {
            base.Restore(in state);
            AIState = state.MonsterState;
            CampId = state.CampId;
            CampSlotIndex =
                state.MonsterCampSlotIndex;
            NextDecisionLogicTick =
                state.MonsterNextDecisionLogicTick;
            camp = null;
        }

        public override void Resolve(
            in RollbackContext context)
        {
            ResolveCampOrThrow();
        }

        public override void ClearForDeath()
        {
            ClearOrder();
        }

        private void ResolveCampOrThrow()
        {
            if (camp != null)
                return;
            if (!OwnerUnit.World.TryGetJungleCamp(
                    CampId,
                    out camp))
                throw new DeterministicSimulationException(
                    $"Monster {OwnerUnitUid} cannot resolve JungleCamp {CampId}.");
        }
    }

    public sealed class TowerAIController : UnitAIController
    {
        public TowerAIState AIState { get; private set; }
        public UnitUid CurrentTargetUid =>
            OwnerUnit != null &&
            OwnerUnit.Intent.Kind ==
                IntentKind.AttackTarget
                ? OwnerUnit.Intent.TargetUnit
                : default;

        public TowerAIController(Unit owner) : base(owner)
        {
            ControllerKind = UnitAIControllerKind.Tower;
            AIState = TowerAIState.Idle;
        }

        public override void AIThink()
        {
            if (!CanRunAIThisTick ||
                OwnerUnit.AttackHandler == null ||
                !OwnerUnit.CapabilityState.CanAttack)
            {
                LoseTarget();
                return;
            }

            UnitUid target = FindBestTarget();

            if (target.IsValid())
            {
                IssueAttackOrder(target, false);
                AIState =
                    TowerAIState.AttackingTarget;
            }
            else
            {
                LoseTarget();
            }
        }

        public void AcquireTarget(UnitUid targetUid)
        {
            if (!targetUid.IsValid())
                throw new ArgumentException(
                    "Tower target must be a valid UnitUid.",
                    nameof(targetUid));
            IssueAttackOrder(targetUid, false);
            AIState = TowerAIState.AttackingTarget;
        }

        public void LoseTarget()
        {
            ClearOrder();
            AIState = TowerAIState.Idle;
        }

        private UnitUid FindBestTarget()
        {
            IReadOnlyList<Unit> units =
                OwnerUnit.World.GetAllUnits();
            UnitUid best = default;
            int bestPriority = int.MaxValue;
            fp bestDistanceSq = default;
            fp range =
                OwnerUnit.AttackHandler
                    .CurrentAttackRange;
            fp rangeSq = range * range;
            fp2 origin = OwnerUnit.PhysicsEntity
                .Transform2D.Position;

            for (int i = 0; i < units.Count; i++)
            {
                Unit candidate = units[i];
                if (!IsValidTarget(candidate))
                    continue;

                fp distanceSq = fpmath.lengthsq(
                    candidate.PhysicsEntity
                        .Transform2D.Position -
                    origin);
                if (distanceSq > rangeSq)
                    continue;

                int priority = GetTargetPriority(candidate);
                if (priority == int.MaxValue)
                    continue;
                if (!best.IsValid() ||
                    priority < bestPriority ||
                    (priority == bestPriority &&
                     (distanceSq < bestDistanceSq ||
                      (distanceSq == bestDistanceSq &&
                       candidate.UnitUid
                           .CompareTo(best) < 0))))
                {
                    best = candidate.UnitUid;
                    bestPriority = priority;
                    bestDistanceSq = distanceSq;
                }
            }

            return best;
        }

        private bool IsValidTarget(UnitUid targetUid)
        {
            return targetUid.IsValid() &&
                OwnerUnit.World.TryGetUnit(
                    targetUid,
                    out Unit target) &&
                IsValidTarget(target);
        }

        private bool IsValidTarget(Unit target)
        {
            if (target == null)
                return false;
            if (target == OwnerUnit ||
                target.TeamId == OwnerUnit.TeamId ||
                target.TeamId == TeamId.Neutral ||
                target.LifeState != LifeState.Alive ||
                !target.CapabilityState.IsTargetable ||
                target.PhysicsEntity == null ||
                GetTargetPriority(target) == int.MaxValue)
                return false;
            fp range = OwnerUnit.AttackHandler.CurrentAttackRange;
            fp distanceSq = fpmath.lengthsq(
                target.PhysicsEntity.Transform2D.Position -
                OwnerUnit.PhysicsEntity.Transform2D.Position);
            return distanceSq <= range * range;
        }

        private int GetTargetPriority(Unit candidate)
        {
            if (candidate.UnitKind == UnitKind.Hero)
                return IsAttackingAlliedHero(candidate) ? 0 : 5;
            if (candidate.OwnerUid.IsValid())
                return 1;
            if (candidate.UnitKind != UnitKind.Minion)
                return int.MaxValue;
            return candidate.UnitSubKindId switch
            {
                NonHeroUnitSubKindId.SiegeMinion => 2,
                NonHeroUnitSubKindId.SuperMinion => 2,
                NonHeroUnitSubKindId.MeleeMinion => 3,
                NonHeroUnitSubKindId.RangedMinion => 4,
                _ => 4,
            };
        }

        private bool IsAttackingAlliedHero(Unit candidate)
        {
            if (candidate.AttackHandler == null ||
                !candidate.AttackHandler
                    .GetAnimationSnapshot()
                    .IsAttacking)
                return false;
            UnitUid focusTarget =
                candidate.AttackHandler.CurrentTargetUid;
            return focusTarget.IsValid() &&
                OwnerUnit.World.TryGetUnit(
                    focusTarget,
                    out Unit alliedTarget) &&
                alliedTarget.TeamId == OwnerUnit.TeamId &&
                alliedTarget.UnitKind == UnitKind.Hero &&
                alliedTarget.LifeState == LifeState.Alive;
        }

        public override void Capture(ref UnitAIControllerSnapshot state)
        {
            base.Capture(ref state);
            state.TowerState = AIState;
        }

        public override void Restore(in UnitAIControllerSnapshot state)
        {
            base.Restore(in state);
            AIState = state.TowerState;
        }
    }
}
