using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    public struct UnitSnapshot
    {
        public UnitUid UnitUid;
        public UnitUid OwnerUid;
        public UnitKind UnitKind;
        public ushort UnitSubKindId;
        public TeamId TeamId;
        public int UnitPrototypeId;
        public LifeState LifeState;
        public CapabilityState CapabilityState;
        public HitReactionState HitReactionState;
        public PhysicsTransform2D PhysicsTransform;
        public PhysicsShape2D PhysicsShape;
        public StatHandlerSnapshot StatState;
        public CombatModifierSetSnapshot CombatModifierState;
        public AttackSnapshot AttackState;
        public MovementSnapshot MovementState;
        public AbilityHandlerSnapshot AbilityState;
        public BuffHandlerSnapshot BuffState;
        public CrowdControlHandlerSnapshot CCState;
        public LocomotionAgentSnapshot LocomotionState;
        public EquipmentHandlerSnapshot EquipmentState;
    }

    /// <summary>
    /// Snapshot of the entire UnitWorld for rollback.
    /// Uses T[] arrays per Snapshot Appendix v7.2 section 5.
    /// </summary>
    public struct UnitWorldSnapshot
    {
        public UnitSnapshot[] Units;
        public MinionSystemSnapshot MinionSystemState;
        public RespawnTimerSnapshot PendingUnitLifecycleState;
        public JungleCampSnapshot[] JungleCampStates;
        public UnitAIControllerSnapshot[] AIControllerStates;
        public int RuntimeRevision;

        public static UnitWorldSnapshot CreateEmpty() => new UnitWorldSnapshot
        {
            Units = Array.Empty<UnitSnapshot>(),
            JungleCampStates = Array.Empty<JungleCampSnapshot>(),
            AIControllerStates = Array.Empty<UnitAIControllerSnapshot>(),
        };
    }

    public struct GameplaySnapshot
    {
        public int SchemaVersion;

        public DeterministicRandomSnapshot RandomState;
        public MatchRuleRuntimeSnapshot MatchRuleState;
        public UnitWorldSnapshot UnitWorldState;
        public CombatSnapshot CombatState;
        public ProjectileWorldSnapshot ProjectileState;
        public EquipmentShopRuntimeSnapshot EquipmentShopState;
        public PhysicsRuntimeSnapshot PhysicsState;

        // Deferred snapshot members (reserved per Snapshot Appendix v7.2):
        // - MatchRuleRuntimeSnapshot  (deferred: MatchRuleRuntime not yet implemented)

        public bool IsValid => SchemaVersion > 0;

        public static GameplaySnapshot CreateEmpty()
        {
            return new GameplaySnapshot
            {
                SchemaVersion = 4,
                MatchRuleState = MatchRuleRuntimeSnapshot.Empty,
                RandomState = default,
                UnitWorldState = UnitWorldSnapshot.CreateEmpty(),
                CombatState = CombatSnapshot.Default,
                ProjectileState = ProjectileWorldSnapshot.Empty,
                EquipmentShopState = EquipmentShopRuntimeSnapshot.Empty,
                PhysicsState = PhysicsRuntimeSnapshot.Empty,
            };
        }
    }

    public struct RollbackFrameSnapshot
    {
        public int SnapshotTick;
        public int SnapshotSchemaVersion;
        public GameplaySnapshot Gameplay;
    }
}
