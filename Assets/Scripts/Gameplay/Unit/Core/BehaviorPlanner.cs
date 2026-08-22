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

        /// <summary>
        /// Replace only the long-term goal. Runtime cancellation belongs to
        /// Unit/Arbiter composition and is intentionally absent here.
        /// </summary>
        public void ReplaceIntent(in UnitIntent intent)
        {
            _currentIntent = intent;
        }

        public void ClearIntent() { _currentIntent = UnitIntent.None; }

        public void Tick(out ActionRequest primaryRequest)
        {
            primaryRequest = null;
            if (!_owner.CanRunActiveGameplayThisTick) return;
            if (_owner.LifeState != LifeState.Alive && _owner.LifeState != LifeState.Dying) return;

            int currentTick = SimulationTickContext.Current.Tick;

            // Unit Framework v27.3 3.3: the control system's stable forced
            // behavior winner is the highest-priority planning input.
            if (_owner.CrowdControl != null &&
                _owner.CrowdControl.TryGetBehaviorOverride(
                    out CrowdControlBehaviorOverride behavior))
            {
                primaryRequest =
                    PlanForcedBehavior(behavior);
                SuppressSatisfiedAction(ref primaryRequest);
                return;
            }
            if (!_currentIntent.IsActive) return;

            switch (_currentIntent.Kind)
            {
                case IntentKind.AttackTarget: primaryRequest = PlanAttackIntent(currentTick); break;
                case IntentKind.MoveToPosition: primaryRequest = PlanMoveIntent(); break;
                case IntentKind.CastAbility: primaryRequest = PlanCastIntent(currentTick); break;
                case IntentKind.LaneAdvance: primaryRequest = PlanLaneAdvance(); break;
                case IntentKind.ReturnToCamp: primaryRequest = PlanReturnToCamp(); break;
            }
            SuppressSatisfiedAction(ref primaryRequest);
        }

        private void SuppressSatisfiedAction(ref ActionRequest request)
        {
            if (_owner.ActionRuntimes == null)
                return;
            if ((request is MoveActionRequest move &&
                 _owner.ActionRuntimes.IsEquivalentMoveActive(move)) ||
                (request is AttackActionRequest attack &&
                 _owner.ActionRuntimes.IsEquivalentAttackActive(attack)))
                request = null;
        }

        private ActionRequest PlanForcedBehavior(
            in CrowdControlBehaviorOverride behavior)
        {
            switch (behavior.Kind)
            {
                case CrowdControlBehaviorKind.AttackTarget:
                    return behavior.TargetUnitUid.IsValid()
                        ? new AttackActionRequest(
                            behavior.TargetUnitUid)
                        : null;

                case CrowdControlBehaviorKind.MoveToTarget:
                    return behavior.TargetUnitUid.IsValid()
                        ? new MoveActionRequest(
                            behavior.TargetUnitUid,
                            (fp)0.5m,
                            MovePurpose.ControlMove)
                        : null;

                case CrowdControlBehaviorKind.FleeDirection:
                    fp2 current =
                        _owner.PhysicsEntity
                            .Transform2D.Position;
                    return new MoveActionRequest(
                        current +
                        behavior.Direction * (fp)10,
                        (fp)0.3m,
                        MovePurpose.ControlMove);

                default:
                    return null;
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

            // A charging/casting unit must not start a normal attack
            // (Unit Framework v27.3 cast rule). The ability session owns the
            // unit's action window.
            if (_owner.AbilityHandler != null &&
                _owner.AbilityHandler.HasActiveActionStage())
            {
                return null;
            }

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

            // Single source of truth: AbilityDef.CastRange. The behavior
            // layer must not duplicate ability range (Player Input v1.1 4.1 /
            // Unit Framework v27.3 cast intent). Zero means "no range
            // requirement", matching AbilityHandler.IsWithinCastRange.
            AbilityDef def =
                _owner.AbilityHandler?.GetAbilityDef(
                    (byte)abilityId);
            fp castRange =
                def?.CastRange ?? fp.zero;
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

            if (castRange <= fp.zero)
            {
                AbilitySignalVerb verb = _currentIntent.AbilityVerb;
                _currentIntent.Clear();
                return new CastActionRequest(
                    abilityId,
                    verb,
                    aim);
            }

            fp distSq = fpmath.dot(sourcePos - destPos, sourcePos - destPos);
            if (distSq <= castRange * castRange)
            {
                AbilitySignalVerb verb = _currentIntent.AbilityVerb;
                _currentIntent.Clear();
                return new CastActionRequest(
                    abilityId,
                    verb,
                    aim);
            }

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
