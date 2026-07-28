using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct MinionSystemSnapshot : IRollbackSnapshot
    {
        public int WaveIndex;
        public int NextWaveLogicTick;
        public MinionTicket[] PendingTickets;
        public int NextTicketCursor;
        public UnitUid[] ManagedMinionUids;
    }

    public struct MinionTicket
    {
        public int SpawnLogicTick;
        public TeamId TeamId;
        public ushort LaneId;
        public int UnitPrototypeId;
        public int StableEntryIndex;
        public fp2 SpawnPosition;
        public fp2 SpawnForward;

        public static readonly MinionTicket Empty = default;
    }

    public struct JungleCampSnapshot : IRollbackSnapshot
    {
        public int CampId;
        public JungleCampState State;
        public UnitUid[] MemberUidsBySlot;
        public bool[] MemberAliveBySlot;
        public bool MainMonsterDead;
        public UnitUid PrimaryTargetUid;
        public int LastHostileActionLogicTick;
        public int NextRespawnLogicTick;
        public int ResetBeginLogicTick;
    }

    public enum JungleCampState : byte
    {
        Dormant = 0,
        Idle = 1,
        InCombat = 2,
        Returning = 3,
        WaitingRespawn = 4,
    }

    public struct UnitAIControllerSnapshot : IRollbackSnapshot
    {
        public UnitAIControllerKind ControllerKind;
        public UnitUid OwnerUnitUid;
        public MinionAIState MinionState;
        public int LaneId;
        public int MinionNextDecisionLogicTick;
        public int MinionTargetLockUntilLogicTick;
        public fp2 MinionEngageOrigin;
        public UnitUid MinionPendingAssistTargetUid;
        public int MinionPendingAssistExpireLogicTick;
        public MonsterAIState MonsterState;
        public int CampId;
        public int MonsterCampSlotIndex;
        public int MonsterNextDecisionLogicTick;
        public TowerAIState TowerState;
    }

    public enum UnitAIControllerKind : byte
    {
        None = 0,
        Minion = 1,
        Monster = 2,
        Tower = 3,
    }

    public enum MinionAIState : byte
    {
        AdvanceLane = 0,
        EngageTarget = 1,
        ReturnToLane = 2,
    }

    public enum MonsterAIState : byte
    {
        CampIdle = 0,
        EngageTarget = 1,
        ReturnToCamp = 2,
    }

    public enum TowerAIState : byte
    {
        Idle = 0,
        AttackingTarget = 1,
    }

    public struct NonHeroWorldSnapshot
    {
        public MinionSystemSnapshot MinionSystemState;
        public JungleCampSnapshot[] JungleCampStates;
        public UnitAIControllerSnapshot[] AIControllerStates;

        public static NonHeroWorldSnapshot CreateEmpty()
        {
            return new NonHeroWorldSnapshot
            {
                MinionSystemState = default,
                JungleCampStates = Array.Empty<JungleCampSnapshot>(),
                AIControllerStates = Array.Empty<UnitAIControllerSnapshot>(),
            };
        }
    }

    public interface IRollbackSnapshot { }
}
