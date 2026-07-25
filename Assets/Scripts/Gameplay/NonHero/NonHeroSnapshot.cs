using System;
using FrameSyncMoba.Deterministic;

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
        public UnitUid UnitUid;
        public int SpawnLogicTick;
        public int LaneId;
        public bool IsSpawned;

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
        Idle = 0,
        Combat = 1,
        Reset = 2,
        Dead = 3,
    }

    public struct UnitAIControllerSnapshot : IRollbackSnapshot
    {
        public UnitAIControllerKind ControllerKind;
        public UnitUid OwnerUnitUid;
        public MinionAIState MinionState;
        public int LaneId;
        public UnitUid MinionTargetUid;
        public MonsterAIState MonsterState;
        public int CampId;
        public UnitUid MonsterTargetUid;
        public TowerAIState TowerState;
        public UnitUid TowerTargetUid;
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
        Idle = 0,
        Chasing = 1,
        Returning = 2,
        Dead = 3,
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
