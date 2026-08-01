using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class BehaviorPlanner
    {
        private readonly Unit _owner;
        private UnitIntent _currentIntent;

        public BehaviorPlanner(Unit owner)
        {
            _owner = owner;
            _currentIntent = UnitIntent.None;
        }

        public ref readonly UnitIntent CurrentIntent => ref _currentIntent;

        public void SetIntent(in UnitIntent intent) { _currentIntent = intent; }

        public void ClearIntent() { _currentIntent = UnitIntent.None; }

        public void Tick(out ActionRequest primaryRequest)
        {
            primaryRequest = null;
            if (!_owner.CanRunActiveGameplayThisTick) return;
            if (_owner.LifeState != LifeState.Alive && _owner.LifeState != LifeState.Dying) return;

            int currentTick = SimulationTickContext.Current.Tick;

            if (TryGetOverrideRequest(currentTick, out primaryRequest)) return;
            if (!_currentIntent.IsActive) return;

            switch (_currentIntent.Kind)
            {
                case IntentKind.AttackTarget: primaryRequest = PlanAttackIntent(currentTick); break;
                case IntentKind.MoveToPosition: primaryRequest = PlanMoveIntent(); break;
                case IntentKind.CastAbility: primaryRequest = PlanCastIntent(currentTick); break;
                case IntentKind.LaneAdvance: primaryRequest = PlanLaneAdvance(); break;
                case IntentKind.ReturnToCamp: primaryRequest = PlanReturnToCamp(); break;
            }
        }

        private bool TryGetOverrideRequest(int currentTick, out ActionRequest request)
        {
            request = null;
            if (_owner.CrowdControl == null) return false;
            var cc = _owner.CrowdControl.ActiveConstraint;
            if (!cc.IsActive) return false;
            switch (cc.Type)
            {
                case CrowdControlType.Stun:
                case CrowdControlType.Suppression:
                    return true; // Fully disabled
                case CrowdControlType.Knockback:
                    return true; // Handled by ForcedMoveExecutor
                default:
                    return false;
            }
        }

        private ActionRequest PlanAttackIntent(int currentTick)
        {
            UnitUid targetUid = _currentIntent.TargetUnit;
            AttackHandler attack = _owner.AttackHandler;
            if (attack == null) return null;

            // Do not resume active pursuit during windup or recovery. The
            // next completed attack cycle gets a fresh attack/range decision.
            if (attack.IsAttackCycleActive)
                return null;

            AttackPlanStatus status =
                attack.GetAttackPlanStatus(targetUid);
            switch (status)
            {
                case AttackPlanStatus.TargetInvalid:
                    _currentIntent.Clear();
                    return null;
                case AttackPlanStatus.OutOfRange:
                    if (_currentIntent.AllowChase)
                        return new MoveActionRequest(
                            targetUid,
                            attack.CurrentAttackRange,
                            MovePurpose.ChaseForAttack);
                    _currentIntent.Clear();
                    return null;
                case AttackPlanStatus.WaitingForReady:
                case AttackPlanStatus.Ready:
                    return new AttackActionRequest(targetUid);
                default:
                    return null;
            }
        }

        private ActionRequest PlanMoveIntent()
        {
            fp2 targetPos = _currentIntent.TargetPosition;
            fp2 currentPos = _owner.PhysicsEntity.Transform2D.Position;
            fp stopThreshold = fp.one / (fp)2;
            fp distSq = fpmath.dot(currentPos - targetPos, currentPos - targetPos);
            if (distSq <= stopThreshold * stopThreshold) { _currentIntent.Clear(); return null; }
            return new MoveActionRequest(
                targetPos,
                stopThreshold,
                MovePurpose.PointMove);
        }

        private ActionRequest PlanCastIntent(int currentTick)
        {
            AimSnapshot aim = _currentIntent.AbilityAim;
            int abilityId = _currentIntent.AbilityId;
            if (_owner.AbilityHandler == null) { _currentIntent.Clear(); return null; }

            fp castRange = (fp)4;
            fp2 sourcePos = _owner.PhysicsEntity.Transform2D.Position;
            fp2 destPos = sourcePos;
            if (aim.Kind == AimKind.Unit)
            {
                if (!_owner.World.TryGetUnit(
                        aim.TargetUnitUid,
                        out Unit targetUnit))
                {
                    _currentIntent.Clear();
                    return null;
                }
                destPos = targetUnit.PhysicsEntity.Transform2D.Position;
            }
            else if (aim.Kind == AimKind.Point)
                destPos = aim.TargetPoint;

            fp distSq = fpmath.dot(sourcePos - destPos, sourcePos - destPos);
            if (distSq <= castRange * castRange)
                return new CastActionRequest(
                    abilityId,
                    _currentIntent.AbilityVerb,
                    aim);

            if (_currentIntent.AllowChase)
                return new MoveActionRequest(
                    destPos,
                    castRange,
                    MovePurpose.ChaseForCast);

            _currentIntent.Clear();
            return null;
        }

        private ActionRequest PlanLaneAdvance()
        {
            fp2 targetPos = _currentIntent.TargetPosition;
            fp2 currentPos =
                _owner.PhysicsEntity.Transform2D.Position;
            fp stopThreshold = fp.one / (fp)2;
            fp distSq = fpmath.lengthsq(
                currentPos - targetPos);
            if (distSq <=
                stopThreshold * stopThreshold)
            {
                _currentIntent.Clear();
                return null;
            }
            return new MoveActionRequest(
                targetPos,
                stopThreshold,
                MovePurpose.LaneAdvance);
        }

        private ActionRequest PlanReturnToCamp()
        {
            fp2 targetPos = _currentIntent.TargetPosition;
            fp2 currentPos =
                _owner.PhysicsEntity.Transform2D.Position;
            fp stopThreshold = fp.one / (fp)2;
            fp distSq = fpmath.lengthsq(
                currentPos - targetPos);
            if (distSq <=
                stopThreshold * stopThreshold)
            {
                _currentIntent.Clear();
                return null;
            }
            return new MoveActionRequest(
                targetPos,
                stopThreshold,
                MovePurpose.ReturnToCamp);
        }

        public void ClearForDeath() { _currentIntent = UnitIntent.None; }
        public void ClearForRespawn() { _currentIntent = UnitIntent.None; }
    }
}
