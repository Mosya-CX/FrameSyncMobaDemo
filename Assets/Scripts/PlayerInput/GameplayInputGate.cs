using UnitType = FrameSyncMoba.Unit.Unit;
using LifeState = FrameSyncMoba.Unit.LifeState;

namespace FrameSyncMoba.PlayerInput
{
    public interface IGameplayInputGate
    {
        bool IsMoveAllowed(UnitType unit);
        bool IsAttackAllowed(UnitType unit);
        bool IsAbilityAllowed(UnitType unit, byte abilitySlot);
    }

    public sealed class GameplayInputGate : IGameplayInputGate
    {
        public bool IsMoveAllowed(UnitType unit)
        {
            if (unit == null) return false;
            if (unit.LifeState != LifeState.Alive) return false;
            ref readonly var cap = ref unit.CapabilityState;
            if (!cap.CanMove) return false;
            if (unit.CrowdControl != null && unit.CrowdControl.IsMovementRestricted) return false;
            return true;
        }

        public bool IsAttackAllowed(UnitType unit)
        {
            if (unit == null) return false;
            if (unit.LifeState != LifeState.Alive) return false;
            ref readonly var cap = ref unit.CapabilityState;
            if (!cap.CanAttack) return false;
            if (unit.CrowdControl != null && unit.CrowdControl.IsActionRestricted) return false;
            return true;
        }

        public bool IsAbilityAllowed(UnitType unit, byte abilitySlot)
        {
            if (unit == null) return false;
            if (unit.LifeState != LifeState.Alive) return false;
            ref readonly var cap = ref unit.CapabilityState;
            if (!cap.CanCast) return false;
            if (unit.CrowdControl != null && unit.CrowdControl.IsActionRestricted) return false;
            return true;
        }
    }
}
