using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct JungleCampSpawnSlot
    {
        [Range(0, byte.MaxValue)] public int SlotIndex;
        [Min(1)] public int UnitPrototypeId;
        public Transform SpawnPoint;
    }

    [DisallowMultipleComponent]
    public sealed class JungleCamp : MonoBehaviour
    {
        [Header("Stable topology")]
        [Min(1)]
        [SerializeField] private int campId = 1;
        [Range(0, byte.MaxValue)]
        [SerializeField] private int campTeamId;
        [SerializeField] private Transform campAnchor;
        [Range(0, byte.MaxValue)]
        [SerializeField] private int mainMonsterSlotIndex;
        [SerializeField] private JungleCampSpawnSlot[] spawnSlots =
            Array.Empty<JungleCampSpawnSlot>();

        [Header("Inspector timing and leash")]
        [Min(0f)]
        [SerializeField] private float initialSpawnSeconds;
        [Min(0f)]
        [SerializeField] private float respawnDelaySeconds = 60f;
        [Min(0f)]
        [SerializeField] private float softLeashRadius = 6f;
        [Min(0f)]
        [SerializeField] private float hardLeashRadius = 10f;
        [Min(0f)]
        [SerializeField] private float disengageDelaySeconds = 3f;

        private UnitWorld unitWorld;
        private int initialSpawnLogicTick;
        private int respawnDelayTicks;
        private int disengageDelayTicks;
        private fp2 campAnchorPosition;
        private fp softLeashRadiusSq;
        private fp hardLeashRadiusSq;
        private fp2[] spawnPositionBySlot =
            Array.Empty<fp2>();
        private fp2[] spawnForwardBySlot =
            Array.Empty<fp2>();
        private int[] prototypeIdBySlot =
            Array.Empty<int>();

        public int CampId => campId;
        public JungleCampState State { get; private set; } =
            JungleCampState.Dormant;
        public UnitUid[] MemberUidsBySlot { get; private set; } =
            Array.Empty<UnitUid>();
        public bool[] MemberAliveBySlot { get; private set; } =
            Array.Empty<bool>();
        public bool MainMonsterDead { get; private set; }
        public UnitUid PrimaryTargetUid { get; private set; }
        public int LastHostileActionLogicTick { get; private set; }
        public int NextRespawnLogicTick { get; private set; } = -1;
        public int ResetBeginLogicTick { get; private set; } = -1;
        public fp2 CampAnchorPosition => campAnchorPosition;
        public fp HardLeashRadiusSq => hardLeashRadiusSq;

        public void InitializeForMatch(
            UnitWorld ownerWorld)
        {
            if (unitWorld != null &&
                unitWorld != ownerWorld)
                throw new InvalidOperationException(
                    $"JungleCamp {campId} is already bound.");
            unitWorld = ownerWorld ??
                throw new ArgumentNullException(
                    nameof(ownerWorld));
            if (unitWorld.TickRate <= 0)
                throw new InvalidOperationException(
                    "UnitWorld TickRate must be configured before camps.");
            ValidateAndBakeTopology(unitWorld.TickRate);
            unitWorld.RegisterJungleCamp(this);
        }

        public void TickLogic()
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            switch (State)
            {
                case JungleCampState.Dormant:
                    if (currentTick >=
                        initialSpawnLogicTick)
                        SpawnAllMembers();
                    break;
                case JungleCampState.InCombat:
                    if (PrimaryTargetUid.IsValid() &&
                        (!unitWorld.TryGetUnit(
                             PrimaryTargetUid,
                             out Unit target) ||
                         target.LifeState !=
                         LifeState.Alive))
                    {
                        PrimaryTargetUid = default;
                    }
                    if (!PrimaryTargetUid.IsValid() &&
                        currentTick -
                        LastHostileActionLogicTick >=
                        disengageDelayTicks)
                        EndCombat();
                    break;
                case JungleCampState.Returning:
                    if (AllLivingMembersAtSpawn())
                    {
                        PrimaryTargetUid = default;
                        State = JungleCampState.Idle;
                        TryStartRespawnCountdown();
                    }
                    break;
                case JungleCampState.WaitingRespawn:
                    if (currentTick >=
                        NextRespawnLogicTick)
                        SpawnAllMembers();
                    break;
            }
        }

        public bool TryBeginCombat(
            UnitUid targetUid)
        {
            if (!targetUid.IsValid() ||
                State == JungleCampState.Dormant ||
                State ==
                JungleCampState.WaitingRespawn ||
                !unitWorld.TryGetUnit(
                    targetUid,
                    out Unit target) ||
                target.LifeState != LifeState.Alive)
                return false;

            PrimaryTargetUid = targetUid;
            LastHostileActionLogicTick =
                SimulationTickContext.Current.Tick;
            State = JungleCampState.InCombat;
            WakeLivingControllers();
            return true;
        }

        public void RecordHostileAction(
            UnitUid targetUid)
        {
            if (!TryBeginCombat(targetUid))
                return;
            LastHostileActionLogicTick =
                SimulationTickContext.Current.Tick;
        }

        public bool OnMemberDeath(
            UnitUid memberUid)
        {
            int slot = FindMemberSlot(memberUid);
            if (slot < 0 ||
                !MemberAliveBySlot[slot])
                return false;

            MemberAliveBySlot[slot] = false;
            if (slot == mainMonsterSlotIndex)
                MainMonsterDead = true;
            if (!HasLivingMember())
            {
                PrimaryTargetUid = default;
                State = JungleCampState.Returning;
            }
            TryStartRespawnCountdown();
            return true;
        }

        public fp2 GetSpawnPosition(
            int slotIndex)
        {
            ValidateSlotIndex(slotIndex);
            return spawnPositionBySlot[slotIndex];
        }

        public bool TryGetMemberSpawnPosition(
            UnitUid memberUid,
            out fp2 position)
        {
            int slot = FindMemberSlot(memberUid);
            if (slot < 0)
            {
                position = default;
                return false;
            }
            position = spawnPositionBySlot[slot];
            return true;
        }

        public void Capture(
            ref JungleCampSnapshot state)
        {
            state.CampId = campId;
            state.State = State;
            state.MemberUidsBySlot =
                (UnitUid[])MemberUidsBySlot.Clone();
            state.MemberAliveBySlot =
                (bool[])MemberAliveBySlot.Clone();
            state.MainMonsterDead =
                MainMonsterDead;
            state.PrimaryTargetUid =
                PrimaryTargetUid;
            state.LastHostileActionLogicTick =
                LastHostileActionLogicTick;
            state.NextRespawnLogicTick =
                NextRespawnLogicTick;
            state.ResetBeginLogicTick =
                ResetBeginLogicTick;
        }

        public void Restore(
            in JungleCampSnapshot state)
        {
            if (state.CampId != campId)
                throw new DeterministicSimulationException(
                    $"Cannot restore camp {state.CampId} into {campId}.");
            UnitUid[] uids =
                state.MemberUidsBySlot ??
                Array.Empty<UnitUid>();
            bool[] alive =
                state.MemberAliveBySlot ??
                Array.Empty<bool>();
            if (uids.Length !=
                    prototypeIdBySlot.Length ||
                alive.Length !=
                    prototypeIdBySlot.Length)
                throw new DeterministicSimulationException(
                    $"JungleCamp {campId} snapshot topology differs.");
            for (int i = 0; i < uids.Length; i++)
            {
                if (alive[i] &&
                    !uids[i].IsValid())
                    throw new DeterministicSimulationException(
                        $"JungleCamp {campId} slot {i} alive/UID state disagrees.");
            }
            if (state.State <
                    JungleCampState.Dormant ||
                state.State >
                    JungleCampState.WaitingRespawn)
                throw new DeterministicSimulationException(
                    $"JungleCamp {campId} has invalid state.");

            State = state.State;
            MemberUidsBySlot =
                (UnitUid[])uids.Clone();
            MemberAliveBySlot =
                (bool[])alive.Clone();
            MainMonsterDead =
                state.MainMonsterDead;
            PrimaryTargetUid =
                state.PrimaryTargetUid;
            LastHostileActionLogicTick =
                state.LastHostileActionLogicTick;
            NextRespawnLogicTick =
                state.NextRespawnLogicTick;
            ResetBeginLogicTick =
                state.ResetBeginLogicTick;
        }

        public void Resolve(
            in RollbackContext context)
        {
            for (int i = 0;
                 i < MemberUidsBySlot.Length;
                 i++)
            {
                UnitUid uid = MemberUidsBySlot[i];
                if (MemberAliveBySlot[i] &&
                    uid.IsValid() &&
                    !unitWorld.TryGetUnit(uid, out _))
                    throw new DeterministicSimulationException(
                        $"JungleCamp {campId} live member {uid} is missing.");
            }
            if (PrimaryTargetUid.IsValid() &&
                !unitWorld.TryGetUnit(
                    PrimaryTargetUid,
                    out _))
                throw new DeterministicSimulationException(
                    $"JungleCamp {campId} target {PrimaryTargetUid} is missing.");
        }

        public void Rebuild(
            in RollbackContext context)
        {
        }

        private void SpawnAllMembers()
        {
            for (int slot = 0;
                 slot < prototypeIdBySlot.Length;
                 slot++)
            {
                var request = new UnitSpawnRequest(
                    prototypeIdBySlot[slot],
                    new TeamId((byte)campTeamId),
                    spawnPositionBySlot[slot],
                    spawnForwardBySlot[slot],
                    default);
                UnitUid uid =
                    unitWorld.SpawnUnit(request);
                if (!unitWorld.TryGetUnit(
                        uid,
                        out Unit monster) ||
                    monster.UnitKind !=
                    UnitKind.Monster)
                    throw new DeterministicSimulationException(
                        $"JungleCamp {campId} slot {slot} spawned invalid monster.");
                MemberUidsBySlot[slot] = uid;
                MemberAliveBySlot[slot] = true;
                var controller =
                    new MonsterAIController(
                        monster,
                        campId,
                        slot);
                if (!unitWorld.RegisterAIController(
                        uid,
                        controller))
                    throw new DeterministicSimulationException(
                        $"JungleCamp {campId} failed to register AI for {uid}.");
            }
            MainMonsterDead = false;
            PrimaryTargetUid = default;
            LastHostileActionLogicTick = -1;
            NextRespawnLogicTick = -1;
            ResetBeginLogicTick = -1;
            State = JungleCampState.Idle;
        }

        private void EndCombat()
        {
            PrimaryTargetUid = default;
            ResetBeginLogicTick =
                SimulationTickContext.Current.Tick;
            State = JungleCampState.Returning;
            WakeLivingControllers();
            TryStartRespawnCountdown();
        }

        private void TryStartRespawnCountdown()
        {
            if (!MainMonsterDead ||
                State == JungleCampState.InCombat ||
                State ==
                JungleCampState.WaitingRespawn)
                return;

            for (int slot = 0;
                 slot < MemberUidsBySlot.Length;
                 slot++)
            {
                UnitUid uid = MemberUidsBySlot[slot];
                if (!uid.IsValid() ||
                    !MemberAliveBySlot[slot])
                    continue;
                MemberUidsBySlot[slot] = default;
                MemberAliveBySlot[slot] = false;
                unitWorld.DespawnUnit(
                    new UnitDespawnRequest(
                        uid,
                        UnitDespawnReason.ScriptedCleanup,
                        UnitDespawnMode.Destroy));
            }
            Array.Clear(
                MemberUidsBySlot,
                0,
                MemberUidsBySlot.Length);
            Array.Clear(
                MemberAliveBySlot,
                0,
                MemberAliveBySlot.Length);
            PrimaryTargetUid = default;
            NextRespawnLogicTick = checked(
                SimulationTickContext.Current.Tick +
                respawnDelayTicks);
            State =
                JungleCampState.WaitingRespawn;
        }

        private bool AllLivingMembersAtSpawn()
        {
            for (int slot = 0;
                 slot < MemberUidsBySlot.Length;
                 slot++)
            {
                if (!MemberAliveBySlot[slot])
                    continue;
                if (!unitWorld.TryGetUnit(
                        MemberUidsBySlot[slot],
                        out Unit member))
                    return false;
                fp2 delta =
                    member.PhysicsEntity.Transform2D.Position -
                    spawnPositionBySlot[slot];
                if (Unity.Mathematics.FixedPoint.fpmath
                        .lengthsq(delta) >
                    softLeashRadiusSq)
                    return false;
            }
            return true;
        }

        private bool HasLivingMember()
        {
            for (int i = 0;
                 i < MemberAliveBySlot.Length;
                 i++)
                if (MemberAliveBySlot[i])
                    return true;
            return false;
        }

        private int FindMemberSlot(UnitUid uid)
        {
            for (int i = 0;
                 i < MemberUidsBySlot.Length;
                 i++)
                if (MemberUidsBySlot[i] == uid)
                    return i;
            return -1;
        }

        private void WakeLivingControllers()
        {
            for (int i = 0;
                 i < MemberUidsBySlot.Length;
                 i++)
            {
                UnitUid uid = MemberUidsBySlot[i];
                if (uid.IsValid() &&
                    unitWorld.TryGetAIController(
                        uid,
                        out UnitAIController controller) &&
                    controller is
                        MonsterAIController monster)
                    monster.WakeForCampStateChange();
            }
        }

        private void ValidateAndBakeTopology(
            int tickRate)
        {
            if (campId <= 0 ||
                campTeamId < 0 ||
                campTeamId > byte.MaxValue ||
                campAnchor == null ||
                spawnSlots == null ||
                spawnSlots.Length == 0 ||
                mainMonsterSlotIndex < 0 ||
                mainMonsterSlotIndex >=
                spawnSlots.Length)
                throw new InvalidOperationException(
                    $"{name} JungleCamp topology is invalid.");
            ValidateFiniteNonnegative(
                initialSpawnSeconds,
                nameof(initialSpawnSeconds));
            ValidateFiniteNonnegative(
                respawnDelaySeconds,
                nameof(respawnDelaySeconds));
            ValidateFiniteNonnegative(
                softLeashRadius,
                nameof(softLeashRadius));
            ValidateFiniteNonnegative(
                hardLeashRadius,
                nameof(hardLeashRadius));
            ValidateFiniteNonnegative(
                disengageDelaySeconds,
                nameof(disengageDelaySeconds));
            if (hardLeashRadius <
                softLeashRadius)
                throw new InvalidOperationException(
                    $"{name} hard leash must not be smaller than soft leash.");

            int count = spawnSlots.Length;
            prototypeIdBySlot = new int[count];
            spawnPositionBySlot = new fp2[count];
            spawnForwardBySlot = new fp2[count];
            for (int i = 0; i < count; i++)
            {
                JungleCampSpawnSlot slot =
                    spawnSlots[i];
                if (slot.SlotIndex != i ||
                    slot.UnitPrototypeId <= 0 ||
                    slot.SpawnPoint == null)
                    throw new InvalidOperationException(
                        $"{name} slots must be contiguous and authored in SlotIndex order.");
                Vector3 position =
                    slot.SpawnPoint.position;
                Vector3 forward =
                    slot.SpawnPoint.forward;
                if (!Physics.PhysicsGeometry2D
                        .TryCreateFacing(
                            new fp2(
                                (fp)forward.x,
                                (fp)forward.z),
                            out fp2 normalized,
                            out _))
                    throw new InvalidOperationException(
                        $"{name} slot {i} has zero planar forward.");
                prototypeIdBySlot[i] =
                    slot.UnitPrototypeId;
                spawnPositionBySlot[i] =
                    new fp2(
                        (fp)position.x,
                        (fp)position.z);
                spawnForwardBySlot[i] =
                    normalized;
            }
            Vector3 anchor =
                campAnchor.position;
            campAnchorPosition =
                new fp2(
                    (fp)anchor.x,
                    (fp)anchor.z);
            initialSpawnLogicTick =
                SecondsToTicks(
                    initialSpawnSeconds,
                    tickRate);
            respawnDelayTicks =
                SecondsToTicks(
                    respawnDelaySeconds,
                    tickRate);
            disengageDelayTicks =
                SecondsToTicks(
                    disengageDelaySeconds,
                    tickRate);
            fp soft = (fp)softLeashRadius;
            fp hard = (fp)hardLeashRadius;
            softLeashRadiusSq = soft * soft;
            hardLeashRadiusSq = hard * hard;
            MemberUidsBySlot =
                new UnitUid[count];
            MemberAliveBySlot =
                new bool[count];
            State = JungleCampState.Dormant;
        }

        private void ValidateSlotIndex(
            int slotIndex)
        {
            if (slotIndex < 0 ||
                slotIndex >=
                spawnPositionBySlot.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(slotIndex));
        }

        private static int SecondsToTicks(
            float seconds,
            int tickRate) =>
            checked((int)Math.Ceiling(
                seconds * tickRate));

        private static void ValidateFiniteNonnegative(
            float value,
            string label)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value) ||
                value < 0f)
                throw new InvalidOperationException(
                    $"{label} must be finite and nonnegative.");
        }
    }
}
