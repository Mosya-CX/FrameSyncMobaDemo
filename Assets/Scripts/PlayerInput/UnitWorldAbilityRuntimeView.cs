using System;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Read-only local projection over authoritative AbilityHandler state.
    /// It never mutates Gameplay and is not captured in snapshots.
    /// </summary>
    public sealed class UnitWorldAbilityRuntimeView :
        ILocalAbilityRuntimeView
    {
        private readonly UnitWorld world;

        public UnitWorldAbilityRuntimeView(UnitWorld world)
        {
            this.world = world ??
                throw new ArgumentNullException(nameof(world));
        }

        public bool HasActiveSession(
            UnitUid ownerUid,
            byte slot)
        {
            return world.TryGetUnit(
                    ownerUid,
                    out FrameSyncMoba.Unit.Unit unit) &&
                unit.AbilityHandler.HasActiveSession(slot);
        }

        public bool IsWaitingForCommit(
            UnitUid ownerUid,
            byte slot)
        {
            return world.TryGetUnit(
                    ownerUid,
                    out FrameSyncMoba.Unit.Unit unit) &&
                unit.AbilityHandler.IsWaitingForCommit(slot);
        }
    }
}
