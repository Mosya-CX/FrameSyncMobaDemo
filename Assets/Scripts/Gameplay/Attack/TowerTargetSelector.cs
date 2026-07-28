using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;
using UnitUid = FrameSyncMoba.Unit.UnitUid;
using UnitKind = FrameSyncMoba.Unit.UnitKind;
using LifeState = FrameSyncMoba.Unit.LifeState;
using TeamId = FrameSyncMoba.Unit.TeamId;
using AttackHandler = FrameSyncMoba.Unit.AttackHandler;

namespace FrameSyncMoba.Gameplay.Attack
{
    /// <summary>
    /// Design-aligned name for tower targeting logic per NonHero v5 §8.
    /// Inherits all functionality from TowerAttackHandler unchanged.
    /// This alias keeps the design document name available for code
    /// navigation and documentation without duplicating logic.
    /// </summary>
    public sealed class TowerTargetSelector : TowerAttackHandler
    {
        public TowerTargetSelector(
            UnitType owner,
            AttackHandler baseAttackHandler)
            : base(owner, baseAttackHandler) { }
    }
}
