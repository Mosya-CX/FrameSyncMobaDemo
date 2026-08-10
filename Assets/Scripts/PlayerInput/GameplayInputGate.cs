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
            unit.AbilityHandler?.IsCastMovementLocked() == true)
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
            unit.AbilityHandler?.IsCastMovementLocked() == true)
                return false;
            // Charging/casting units must not start a normal attack
            // (Unit Framework v27.3 cast rule). Move stays allowed during
            // movable cast stages (e.g. charge Hold); only the attack
            // request is rejected while any cast/charge session is active.
            if (unit.AbilityHandler != null &&
                unit.AbilityHandler.HasActiveCastSession())
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
