using FrameSyncMoba.Unit;
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
            if (unit.CrowdControl != null &&
                unit.CrowdControl.IsBlocked(
                    UnitActionBlockMask.VoluntaryMove))
                return false;
            if (unit.AbilityHandler != null &&
                unit.AbilityHandler.IsCastMovementLocked())
                return false;
            return true;
        }

        public bool IsAttackAllowed(UnitType unit)
        {
            if (unit == null) return false;
            if (unit.LifeState != LifeState.Alive) return false;
            ref readonly var cap = ref unit.CapabilityState;
            if (!cap.CanAttack) return false;
            if (unit.CrowdControl != null &&
                unit.CrowdControl.IsBlocked(
                    UnitActionBlockMask.VoluntaryAttack))
                return false;
            if (unit.AbilityHandler != null &&
                unit.AbilityHandler.IsCastMovementLocked())
                return false;
            // Only a real action-owning cast stage blocks a normal attack.
            // Pure Toggles deliberately retain an AbilitySession without
            // owning Main/Base ActionRuntime resources (D-047), so their
            // persistent session must remain attack-neutral.
            if (unit.AbilityHandler != null &&
                unit.AbilityHandler.HasActiveActionStage())
                return false;
            return true;
        }

        public bool IsAbilityAllowed(UnitType unit, byte abilitySlot)
        {
            if (unit == null) return false;
            if (unit.LifeState != LifeState.Alive) return false;
            ref readonly var cap = ref unit.CapabilityState;
            if (!cap.CanCast) return false;
            if (unit.CrowdControl != null &&
                unit.CrowdControl.IsBlocked(
                    UnitActionBlockMask.AbilityCast))
                return false;
            return true;
        }
    }
}
