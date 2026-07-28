using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §2.3 — discriminated union of order types.
    /// Orders represent the semantic intent of a player or AI command,
    /// before translation into UnitIntent and the behavior chain.
    /// 
    /// Orders do NOT carry: pathfinding strategy, RVO settings, FlowField IDs,
    /// stop-range specifics, or path-smoothing parameters.
    /// </summary>
    public enum OrderKind : byte
    {
        None = 0,
        Move = 1,
        Attack = 2,
        Cast = 3,
        LaneAdvance = 4,
        ReturnToCamp = 5,
    }

    public readonly struct Order
    {
        public readonly OrderKind Kind;

        // Move
        public readonly fp2 Move_TargetPosition;

        // Attack
        public readonly UnitUid Attack_TargetUnit;

        // Cast
        public readonly int Cast_AbilityId;
        public readonly UnitUid Cast_TargetUnit;
        public readonly fp2 Cast_TargetPosition;
        public readonly AbilitySignalVerb Cast_Verb;
        public readonly AimSnapshot Cast_Aim;

        // LaneAdvance
        public readonly int LaneAdvance_LaneIndex;

        // ReturnToCamp
        public readonly int ReturnToCamp_CampId;

        private Order(OrderKind kind,
            fp2 moveTargetPosition,
            UnitUid attackTargetUnit,
            int castAbilityId,
            UnitUid castTargetUnit,
            fp2 castTargetPosition,
            AbilitySignalVerb castVerb,
            AimSnapshot castAim,
            int laneIndex,
            int campId)
        {
            Kind = kind;
            Move_TargetPosition = moveTargetPosition;
            Attack_TargetUnit = attackTargetUnit;
            Cast_AbilityId = castAbilityId;
            Cast_TargetUnit = castTargetUnit;
            Cast_TargetPosition = castTargetPosition;
            Cast_Verb = castVerb;
            Cast_Aim = castAim;
            LaneAdvance_LaneIndex = laneIndex;
            ReturnToCamp_CampId = campId;
        }

        public static Order CreateMove(fp2 targetPosition)
        {
            return new Order(
                OrderKind.Move, targetPosition, default,
                0, default, default, default, default, 0, 0);
        }

        public static Order CreateAttack(UnitUid targetUnit)
        {
            return new Order(
                OrderKind.Attack, default, targetUnit,
                0, default, default, default, default, 0, 0);
        }

        public static Order CreateCast(int abilityId, UnitUid targetUnit, fp2 targetPosition)
        {
            AimSnapshot aim = targetUnit.IsValid()
                ? AimSnapshot.ForUnit(targetUnit)
                : AimSnapshot.ForPoint(targetPosition);
            return CreateCast(
                abilityId,
                AbilitySignalVerb.Commit,
                aim);
        }

        public static Order CreateCast(
            int abilityId,
            AbilitySignalVerb verb,
            in AimSnapshot aim)
        {
            return new Order(
                OrderKind.Cast,
                default,
                default,
                abilityId,
                aim.TargetUnitUid,
                aim.TargetPoint,
                verb,
                aim,
                0,
                0);
        }

        public static Order CreateLaneAdvance(int laneIndex)
        {
            return new Order(
                OrderKind.LaneAdvance, default, default,
                0, default, default, default, default, laneIndex, 0);
        }

        public static Order CreateReturnToCamp(int campId)
        {
            return new Order(
                OrderKind.ReturnToCamp, default, default,
                0, default, default, default, default, 0, campId);
        }

        public static readonly Order None = default;

        public bool IsActive => Kind != OrderKind.None;
    }
}
