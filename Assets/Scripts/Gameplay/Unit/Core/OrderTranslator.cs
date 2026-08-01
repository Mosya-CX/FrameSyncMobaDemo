using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public static class OrderTranslator
    {
        public static Order TranslateMoveCommand(in MoveCommand command)
        {
            if (!command.UnitUid.IsValid()) return Order.None;
            if (!command.Intent.HasInput) return Order.None;
            return Order.CreateMove(command.Intent.Direction);
        }

        public static Order CreateAttackOrder(UnitUid targetUnit)
        {
            if (!targetUnit.IsValid()) return Order.None;
            return Order.CreateAttack(targetUnit);
        }

        public static Order CreateCastOrder(int abilityId, UnitUid targetUnit, fp2 targetPosition)
        {
            if (abilityId <= 0) return Order.None;
            return Order.CreateCast(abilityId, targetUnit, targetPosition);
        }

        public static Order CreateCastOrder(
            int abilityId,
            AbilitySignalVerb verb,
            in AimSnapshot aim)
        {
            if (abilityId <= 0) return Order.None;
            return Order.CreateCast(abilityId, verb, aim);
        }

        public static Order CreateLaneAdvanceOrder(int laneIndex)
        {
            return Order.CreateLaneAdvance(laneIndex);
        }

        public static Order CreateReturnToCampOrder(int campId)
        {
            return Order.CreateReturnToCamp(campId);
        }

        public static UnitIntent ToIntent(in Order order)
        {
            return order.Kind switch
            {
                OrderKind.Move => new UnitIntent
                {
                    Kind = IntentKind.MoveToPosition,
                    TargetPosition = order.Move_TargetPosition,
                    AllowChase = false,
                    AllowReplan = true,
                },
                OrderKind.Attack => new UnitIntent
                {
                    Kind = IntentKind.AttackTarget,
                    TargetUnit = order.Attack_TargetUnit,
                    AllowChase = order.Attack_AllowChase,
                    AllowReplan = false,
                },
                OrderKind.Cast => new UnitIntent
                {
                    Kind = IntentKind.CastAbility,
                    AbilityId = order.Cast_AbilityId,
                    AbilityVerb = order.Cast_Verb,
                    AbilityAim = order.Cast_Aim,
                    TargetUnit = order.Cast_TargetUnit,
                    TargetPosition = order.Cast_TargetPosition,
                    AllowChase = true,
                    AllowReplan = false,
                },
                OrderKind.LaneAdvance => new UnitIntent
                {
                    Kind = IntentKind.LaneAdvance,
                    AllowChase = true,
                    AllowReplan = true,
                },
                OrderKind.ReturnToCamp => new UnitIntent
                {
                    Kind = IntentKind.ReturnToCamp,
                    AllowChase = false,
                    AllowReplan = false,
                },
                _ => UnitIntent.None,
            };
        }
    }
}
