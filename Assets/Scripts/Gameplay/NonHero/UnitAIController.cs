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
        private static readonly fp EngageRange = (fp)5m;
        private static readonly fp ChaseMaxDistance = (fp)12m;
        private static readonly fp LaneReturnThreshold = (fp)15m;
        private static readonly int TargetLockTicks = 30;

        public MinionAIState AIState { get; private set; }
        public int LaneId { get; set; }
        public UnitUid CurrentTargetUid { get; private set; }
        public fp2 EngageOrigin { get; private set; }
        public int TargetLockedUntilTick { get; private set; }

        private static readonly List<Unit> _scanBuffer = new List<Unit>(16);

        public MinionAIController(Unit owner, int laneId) : base(owner)
        {
            ControllerKind = UnitAIControllerKind.Minion;
            LaneId = laneId;
            AIState = MinionAIState.AdvanceLane;
            EngageOrigin = fp2.zero;
        }

        public override void AIThink()
        {
            if (!CanRunAIThisTick) return;

            // Always try to find targets first
            TryAcquireTarget();

            switch (AIState)
            {
                case MinionAIState.AdvanceLane:
                    ThinkAdvanceLane();
                    break;
                case MinionAIState.EngageTarget:
                    ThinkEngageTarget();
                    break;
                case MinionAIState.ReturnToLane:
                    ThinkReturnToLane();
                    break;
            }
        }

        private void TryAcquireTarget()
        {
            int currentTick = SimulationTickContext.Current.Tick;

            // If locked on current target and it's still valid, keep it
            if (currentTick < TargetLockedUntilTick
                && CurrentTargetUid.IsValid()
                && IsValidTarget(CurrentTargetUid))
            {
                return;
            }

            // Scan for best target
            UnitUid bestTarget = FindBestTarget();
            if (bestTarget.IsValid())
            {
                CurrentTargetUid = bestTarget;
                TargetLockedUntilTick = currentTick + TargetLockTicks;

                if (AIState != MinionAIState.EngageTarget)
                {
                    EngageOrigin = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
                    AIState = MinionAIState.EngageTarget;
                }
            }
            else
            {
                CurrentTargetUid = default;
                if (AIState == MinionAIState.EngageTarget)
                {
                    AIState = MinionAIState.ReturnToLane;
                }
            }
        }

        private UnitUid FindBestTarget()
        {
            if (OwnerUnit.World == null) return default;

            var allUnits = OwnerUnit.World.GetAllUnits();
            fp2 myPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            TeamId myTeam = OwnerUnit.TeamId;

            UnitUid bestTarget = default;
            int bestPriority = int.MaxValue;
            fp bestDistance = new fp(int.MaxValue);

            for (int i = 0; i < allUnits.Count; i++)
            {
                Unit candidate = allUnits[i];
                if (candidate == OwnerUnit) continue;
                if (candidate.TeamId == myTeam) continue;
                if (candidate.LifeState != LifeState.Alive) continue;

                fp2 candidatePos = candidate.MovementHandler?.Snapshot.Position ?? fp2.zero;
                fp distSq = fpmath.dot(myPos - candidatePos, myPos - candidatePos);
                fp dist = fpmath.sqrt(distSq);

                if (dist > EngageRange) continue;

                // Priority bands: heroes first, then minions, then structures
                int priority = GetTargetPriority(candidate);

                if (priority < bestPriority
                    || (priority == bestPriority && dist < bestDistance))
                {
                    bestPriority = priority;
                    bestDistance = dist;
                    bestTarget = candidate.UnitUid;
                }
                else if (priority == bestPriority && dist == bestDistance
                    && bestTarget.CompareTo(candidate.UnitUid) > 0)
                {
                    // Stable tie-break by UnitUid
                    bestTarget = candidate.UnitUid;
                }
            }

            return bestTarget;
        }

        private static int GetTargetPriority(Unit candidate)
        {
            switch (candidate.UnitKind)
            {
                case UnitKind.Hero: return 0;
                case UnitKind.Minion: return 1;
                case UnitKind.Monster: return 2;
                case UnitKind.Structure: return 3;
                default: return 4;
            }
        }

        private bool IsValidTarget(UnitUid targetUid)
        {
            if (!targetUid.IsValid()) return false;
            if (OwnerUnit.World == null) return false;
            if (!OwnerUnit.World.TryGetUnit(targetUid, out Unit target)) return false;
            if (target.LifeState != LifeState.Alive) return false;
            if (target.TeamId == OwnerUnit.TeamId) return false;

            fp2 myPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp2 targetPos = target.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp distSq = fpmath.dot(myPos - targetPos, myPos - targetPos);

            return distSq <= ChaseMaxDistance * ChaseMaxDistance;
        }

        private void ThinkAdvanceLane()
        {
            if (OwnerUnit.Locomotion != null)
            {
                // Prefer FlowField lane navigation when registry is available.
                // Falls back to A* waypoint movement.
                // (Pathfinding Design v13.1 section 8, Non-Hero Design v5 section 5)
                var registry = OwnerUnit.World?.FlowFieldRegistry;
                if (registry != null)
                {
                    var key = new FlowFieldKey(OwnerUnit.TeamId.Value, RadiusClass.Small);
                    if (registry.TryGet(key, out _))
                    {
                        var flowRequest = new RouteMoveRequest
                        {
                            Target = MoveTarget.None,
                            Purpose = MovePurpose.MoveToLane,
                            Kind = RouteKind.FlowField,
                            AllowRVO = true,
                        };
                        OwnerUnit.Locomotion.AcceptRouteRequest(flowRequest);
                        return;
                    }
                }

                // Fallback: A* waypoint movement along lane direction
                fp2 currentPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
                fp2 laneDir = LaneId == 0
                    ? new fp2(fp.one, fp.zero)
                    : new fp2(-fp.one, fp.zero);
                fp2 targetPos = currentPos + laneDir * (fp)6m;

                var request = RouteMoveRequest.ToPosition(targetPos, (fp)0.3m);
                request.Purpose = MovePurpose.MoveToLane;
                request.AllowRVO = true;
                OwnerUnit.Locomotion.AcceptRouteRequest(request);
            }
        }

        private void ThinkEngageTarget()
        {
            fp2 currentPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp distFromOriginSq = fpmath.dot(currentPos - EngageOrigin, currentPos - EngageOrigin);

            // Check if we've chased too far from engage origin
            if (distFromOriginSq > ChaseMaxDistance * ChaseMaxDistance)
            {
                CurrentTargetUid = default;
                AIState = MinionAIState.ReturnToLane;
                return;
            }

            if (CurrentTargetUid.IsValid())
            {
                if (OwnerUnit.AttackHandler != null)
                {
                    OwnerUnit.AttackHandler.ApplyAttackInput(CurrentTargetUid);
                }

                // Move toward target if needed
                if (OwnerUnit.World != null
                    && OwnerUnit.World.TryGetUnit(CurrentTargetUid, out Unit target)
                    && OwnerUnit.Locomotion != null)
                {
                    fp2 targetPos = target.MovementHandler?.Snapshot.Position ?? fp2.zero;
                    var request = RouteMoveRequest.FollowUnit(CurrentTargetUid, (fp)1.5m);
                    OwnerUnit.Locomotion.AcceptRouteRequest(request);
                }
            }
        }

        private void ThinkReturnToLane()
        {
            fp2 currentPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp distFromOriginSq = fpmath.dot(currentPos - EngageOrigin, currentPos - EngageOrigin);

            // If close enough to origin, resume advancing
            if (distFromOriginSq <= (fp)2m * (fp)2m)
            {
                AIState = MinionAIState.AdvanceLane;
                CurrentTargetUid = default;
                return;
            }

            // Move back toward engage origin (lane centerline)
            if (OwnerUnit.Locomotion != null)
            {
                var request = RouteMoveRequest.ToPosition(EngageOrigin, (fp)0.3m);
                OwnerUnit.Locomotion.AcceptRouteRequest(request);
            }
        }

        public void AcquireTarget(UnitUid targetUid)
        {
            CurrentTargetUid = targetUid;
            AIState = MinionAIState.EngageTarget;
            EngageOrigin = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            TargetLockedUntilTick = SimulationTickContext.Current.Tick + TargetLockTicks;
        }

        public override void Capture(ref UnitAIControllerSnapshot state)
        {
            base.Capture(ref state);
            state.MinionState = AIState;
            state.LaneId = LaneId;
            state.MinionTargetUid = CurrentTargetUid;
        }

        public override void Restore(in UnitAIControllerSnapshot state)
        {
            base.Restore(in state);
            AIState = state.MinionState;
            LaneId = state.LaneId;
            CurrentTargetUid = state.MinionTargetUid;
        }
    }

    public sealed class MonsterAIController : UnitAIController
    {
        public MonsterAIState AIState { get; private set; }
        public UnitUid PrimaryTargetUid { get; set; }
        public int CampId { get; set; }

        private fp2 _campOrigin;
        private fp _leashRadius = (fp)8m;
        private fp _patrolRadius = (fp)2m;
        private fp _aggroRange = (fp)5m;
        private int _returnStartTick;

        public void InitCampPosition(fp2 campOrigin, fp leashRadius = default, fp patrolRadius = default, fp aggroRange = default)
        {
            _campOrigin = campOrigin;
            if (leashRadius > fp.zero) _leashRadius = leashRadius;
            if (patrolRadius > fp.zero) _patrolRadius = patrolRadius;
            if (aggroRange > fp.zero) _aggroRange = aggroRange;
        }

        public MonsterAIController(Unit owner, int campId) : base(owner)
        {
            ControllerKind = UnitAIControllerKind.Monster;
            CampId = campId;
            AIState = MonsterAIState.Idle;
        }

        public override void AIThink()
        {
            if (!CanRunAIThisTick) return;

            switch (AIState)
            {
                case MonsterAIState.Idle:
                    break;
                case MonsterAIState.Chasing:
                    ThinkChase();
                    break;
                case MonsterAIState.Returning:
                    ThinkReturn();
                    break;
                case MonsterAIState.Dead:
                    break;
            }
        }

        private void ThinkChase()
        {
            // Leash check: return to camp if pulled too far
            fp2 myPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp distFromCamp = fpmath.distance(myPos, _campOrigin);
            if (distFromCamp > _leashRadius)
            {
                PrimaryTargetUid = default;
                _returnStartTick = SimulationTickContext.Current.Tick;
                AIState = MonsterAIState.Returning;
                return;
            }
            // Check if current target still valid
            if (!PrimaryTargetUid.IsValid() || !IsTargetAlive(PrimaryTargetUid))
            {
                PrimaryTargetUid = default;
                AIState = MonsterAIState.Idle;
                return;
            }
            if (OwnerUnit.AttackHandler != null)
            {
                OwnerUnit.AttackHandler.ApplyAttackInput(PrimaryTargetUid);
            }
        }

        private void ThinkReturn()
        {
            // Regenerate health rapidly while returning to camp
            if (OwnerUnit.StatHandler != null)
            {
                fp maxHp = OwnerUnit.StatHandler.GetStat(StatId.MaxHealth);
                fp regen = maxHp / (fp)3m; // 33% max HP per tick while returning
                fp newHp = OwnerUnit.StatHandler.CurrentHealth + regen;
                if (newHp > maxHp) newHp = maxHp;
                OwnerUnit.StatHandler.SetCurrentHealth(newHp);
            }
            // Move toward camp origin
            fp2 myPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp distToCamp = fpmath.distance(myPos, _campOrigin);
            if (distToCamp <= (fp)0.5m)
            {
                // Arrived at camp: full restore and go idle
                if (OwnerUnit.StatHandler != null)
                {
                    OwnerUnit.StatHandler.SetCurrentHealth(OwnerUnit.StatHandler.GetStat(StatId.MaxHealth));
                }
                OwnerUnit.MovementHandler?.ApplyMoveInput(MoveIntent.None);
                AIState = MonsterAIState.Idle;
                return;
            }
            // Path toward camp origin
            fp2 toCamp = _campOrigin - myPos;
            fp2 dir = fpmath.normalize(toCamp);
            OwnerUnit.MovementHandler?.ApplyMoveInput(MoveIntent.FromDirection(dir));
            // Attack nearby enemies even while returning (optional: keep target priority)
            TryAcquireTarget();
            if (PrimaryTargetUid.IsValid())
            {
                AIState = MonsterAIState.Chasing;
            }
        }

        

        private void ThinkIdle()
        {
            // Try to acquire a target
            TryAcquireTarget();
            if (PrimaryTargetUid.IsValid())
            {
                AIState = MonsterAIState.Chasing;
                return;
            }
            // No target: stay near camp origin (patrol-like)
            fp2 myPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            fp distFromCamp = fpmath.distance(myPos, _campOrigin);
            if (distFromCamp > _patrolRadius)
            {
                // Wander back toward camp origin
                fp2 toCamp = _campOrigin - myPos;
                fp2 dir = fpmath.normalize(toCamp);
                OwnerUnit.MovementHandler?.ApplyMoveInput(MoveIntent.FromDirection(dir));
            }
            else
            {
                // At camp: idle
                OwnerUnit.MovementHandler?.ApplyMoveInput(MoveIntent.None);
            }
        }

        private void TryAcquireTarget()
        {
            if (OwnerUnit.World == null) return;
            var allUnits = OwnerUnit.World.GetAllUnits();
            fp2 myPos = OwnerUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
            TeamId myTeam = OwnerUnit.TeamId;

            UnitUid bestTarget = default;
            fp bestDist = _aggroRange;

            for (int i = 0; i < allUnits.Count; i++)
            {
                var unit = allUnits[i];
                if (unit == null || unit.UnitUid == OwnerUnit.UnitUid) continue;
                if (unit.LifeState != LifeState.Alive) continue;
                if (unit.TeamId == myTeam) continue; // Don't attack same team
                // Only target heroes and minions
                if (unit.UnitKind != UnitKind.Hero && unit.UnitKind != UnitKind.Minion) continue;

                fp dist = fpmath.distance(myPos, unit.MovementHandler?.Snapshot.Position ?? fp2.zero);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = unit.UnitUid;
                }
            }

            PrimaryTargetUid = bestTarget;
        }

        private bool IsTargetAlive(UnitUid uid)
        {
            if (!uid.IsValid() || OwnerUnit.World == null) return false;
            return OwnerUnit.World.TryGetUnit(uid, out var unit) && unit.LifeState == LifeState.Alive;
        }

        public void SetState(MonsterAIState state, UnitUid target = default)
        {
            AIState = state;
            if (target.IsValid()) PrimaryTargetUid = target;
        }

        public override void Capture(ref UnitAIControllerSnapshot state)
        {
            base.Capture(ref state);
            state.MonsterState = AIState;
            state.CampId = CampId;
            state.MonsterTargetUid = PrimaryTargetUid;
        }

        public override void Restore(in UnitAIControllerSnapshot state)
        {
            base.Restore(in state);
            AIState = state.MonsterState;
            CampId = state.CampId;
            PrimaryTargetUid = state.MonsterTargetUid;
        }

        public override void ClearForDeath()
        {
            AIState = MonsterAIState.Dead;
            PrimaryTargetUid = default;
        }
    }

    public sealed class TowerAIController : UnitAIController
    {
        public TowerAIState AIState { get; private set; }
        public UnitUid CurrentTargetUid { get; private set; }

        public TowerAIController(Unit owner) : base(owner)
        {
            ControllerKind = UnitAIControllerKind.Tower;
            AIState = TowerAIState.Idle;
        }

        public override void AIThink()
        {
            if (!CanRunAIThisTick) return;

            if (AIState == TowerAIState.AttackingTarget
                && CurrentTargetUid.IsValid()
                && OwnerUnit.AttackHandler != null)
            {
                OwnerUnit.AttackHandler.ApplyAttackInput(CurrentTargetUid);
            }
        }

        public void AcquireTarget(UnitUid targetUid)
        {
            CurrentTargetUid = targetUid;
            AIState = TowerAIState.AttackingTarget;
        }

        public void LoseTarget()
        {
            CurrentTargetUid = default;
            AIState = TowerAIState.Idle;
        }

        public override void Capture(ref UnitAIControllerSnapshot state)
        {
            base.Capture(ref state);
            state.TowerState = AIState;
            state.TowerTargetUid = CurrentTargetUid;
        }

        public override void Restore(in UnitAIControllerSnapshot state)
        {
            base.Restore(in state);
            AIState = state.TowerState;
            CurrentTargetUid = state.TowerTargetUid;
        }
    }
}
