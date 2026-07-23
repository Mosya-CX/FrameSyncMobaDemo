using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class JungleCampSystem
    {
        private readonly UnitWorld _unitWorld;
        private readonly List<JungleCamp> _camps = new List<JungleCamp>();
        private readonly JungleCampTiming timing;

        public IReadOnlyList<JungleCamp> Camps => _camps;

        public JungleCampSystem(UnitWorld unitWorld, in JungleCampTiming timing)
        {
            _unitWorld = unitWorld ?? throw new ArgumentNullException(nameof(unitWorld));
            timing.ValidateOrThrow();
            this.timing = timing;
        }

        public JungleCamp CreateCamp(int campId, int totalMembers)
        {
            if (GetCamp(campId) != null)
                throw new InvalidOperationException($"Duplicate JungleCamp id {campId}.");
            var camp = new JungleCamp(campId, totalMembers, timing.RespawnDelayTicks);
            int index = _camps.Count;
            while (index > 0 && _camps[index - 1].CampId > campId) index--;
            _camps.Insert(index, camp);
            return camp;
        }

        public void RegisterMember(int campId, int slot, UnitUid unitUid)
        {
            var camp = GetCamp(campId);
            if (camp == null) return;
            camp.SetMemberUid(slot, unitUid);
            camp.SetMemberAlive(slot, true);
        }

        public void OnMonsterDamaged(int campId, UnitUid attackerUid, int currentTick)
        {
            var camp = GetCamp(campId);
            if (camp == null) return;

            if (camp.State == JungleCampState.Idle || camp.State == JungleCampState.Reset)
            {
                camp.TransitionToCombat(attackerUid, currentTick);
            }

            camp.LastHostileActionLogicTick = currentTick;
        }

        public void OnMonsterDeath(int campId, int monsterSlot, int currentTick)
        {
            var camp = GetCamp(campId);
            if (camp == null) return;

            camp.SetMemberAlive(monsterSlot, false);

            if (monsterSlot == 0)
            {
                camp.MainMonsterDead = true;
                camp.TransitionToDead(currentTick);
            }
        }

        public void Tick(int currentTick)
        {
            for (int i = 0; i < _camps.Count; i++)
            {
                TickCamp(_camps[i], currentTick);
            }
        }

        private void TickCamp(JungleCamp camp, int currentTick)
        {
            switch (camp.State)
            {
                case JungleCampState.Combat:
                    int elapsed = currentTick - camp.LastHostileActionLogicTick;
                    if (elapsed >= timing.ResetTimeoutTicks)
                    {
                        camp.TransitionToReset(currentTick);
                    }
                    break;

                case JungleCampState.Reset:
                    int resetElapsed = currentTick - camp.ResetBeginLogicTick;
                    if (resetElapsed >= timing.ResetDurationTicks)
                    {
                        camp.TransitionToIdle();
                    }
                    break;

                case JungleCampState.Dead:
                    if (currentTick >= camp.NextRespawnLogicTick)
                    {
                        camp.TransitionToIdle();
                        camp.SetAllMembersAlive();
                        camp.MainMonsterDead = false;
                    }
                    break;
            }
        }

        public JungleCamp GetCamp(int campId)
        {
            for (int i = 0; i < _camps.Count; i++)
            {
                if (_camps[i].CampId == campId) return _camps[i];
            }
            return null;
        }

        public void Capture(List<JungleCampSnapshot> snapshots)
        {
            snapshots.Clear();
            for (int i = 0; i < _camps.Count; i++)
            {
                JungleCampSnapshot s = default;
                _camps[i].Capture(ref s);
                snapshots.Add(s);
            }
        }

        public void Restore(List<JungleCampSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count != _camps.Count)
                throw new DeterministicSimulationException(
                    $"JungleCamp topology mismatch: runtime={_camps.Count}, snapshot={snapshots?.Count ?? 0}.");
            for (int i = 0; i < _camps.Count; i++)
            {
                if (_camps[i].CampId != snapshots[i].CampId)
                    throw new DeterministicSimulationException(
                        $"JungleCamp identity mismatch at index {i}.");
                _camps[i].Restore(snapshots[i]);
            }
        }

        public void Resolve(in RollbackContext context)
        {
            for (int campIndex = 0; campIndex < _camps.Count; campIndex++)
            {
                JungleCamp camp = _camps[campIndex];
                if (camp.MemberUidsBySlot.Count != camp.MemberAliveBySlot.Count)
                    throw new DeterministicSimulationException(
                        $"JungleCamp {camp.CampId} member snapshot lengths differ.");
                for (int slot = 0; slot < camp.MemberUidsBySlot.Count; slot++)
                {
                    UnitUid member = camp.MemberUidsBySlot[slot];
                    if (member.IsValid() && camp.MemberAliveBySlot[slot] &&
                        !_unitWorld.TryGetUnit(member, out _))
                        throw new DeterministicSimulationException(
                            $"JungleCamp {camp.CampId} references missing live member {member}.");
                }
                if (camp.PrimaryTargetUid.IsValid() &&
                    !_unitWorld.TryGetUnit(camp.PrimaryTargetUid, out _))
                    throw new DeterministicSimulationException(
                        $"JungleCamp {camp.CampId} references missing target {camp.PrimaryTargetUid}.");
            }
        }

        public void Rebuild(in RollbackContext context) { }
    }

    public readonly struct JungleCampTiming
    {
        public readonly int ResetTimeoutTicks;
        public readonly int ResetDurationTicks;
        public readonly int RespawnDelayTicks;

        public JungleCampTiming(
            int resetTimeoutTicks,
            int resetDurationTicks,
            int respawnDelayTicks)
        {
            ResetTimeoutTicks = resetTimeoutTicks;
            ResetDurationTicks = resetDurationTicks;
            RespawnDelayTicks = respawnDelayTicks;
        }

        public void ValidateOrThrow()
        {
            if (ResetTimeoutTicks < 0 || ResetDurationTicks < 0 ||
                RespawnDelayTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(JungleCampTiming),
                    "Jungle timing values must be nonnegative.");
        }
    }

    public sealed class JungleCamp
    {
        private readonly int respawnDelayTicks;

        public int CampId { get; }
        public JungleCampState State { get; private set; }
        public List<UnitUid> MemberUidsBySlot { get; }
        public List<bool> MemberAliveBySlot { get; }
        public bool MainMonsterDead { get; set; }
        public UnitUid PrimaryTargetUid { get; private set; }
        public int LastHostileActionLogicTick { get; set; }
        public int NextRespawnLogicTick { get; private set; }
        public int ResetBeginLogicTick { get; private set; }

        public JungleCamp(int campId, int totalMembers, int respawnDelayTicks)
        {
            if (respawnDelayTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(respawnDelayTicks));
            this.respawnDelayTicks = respawnDelayTicks;
            CampId = campId;
            State = JungleCampState.Idle;
            MemberUidsBySlot = new List<UnitUid>(totalMembers);
            MemberAliveBySlot = new List<bool>(totalMembers);
            for (int i = 0; i < totalMembers; i++)
            {
                MemberUidsBySlot.Add(default);
                MemberAliveBySlot.Add(true);
            }
        }

        public void SetMemberUid(int slot, UnitUid uid)
        {
            if (slot >= 0 && slot < MemberUidsBySlot.Count)
                MemberUidsBySlot[slot] = uid;
        }

        public void SetMemberAlive(int slot, bool alive)
        {
            if (slot >= 0 && slot < MemberAliveBySlot.Count)
                MemberAliveBySlot[slot] = alive;
        }

        public void SetAllMembersAlive()
        {
            for (int i = 0; i < MemberAliveBySlot.Count; i++)
                MemberAliveBySlot[i] = true;
        }

        public void TransitionToCombat(UnitUid attackerUid, int currentTick)
        {
            State = JungleCampState.Combat;
            PrimaryTargetUid = attackerUid;
            LastHostileActionLogicTick = currentTick;
        }

        public void TransitionToReset(int currentTick)
        {
            State = JungleCampState.Reset;
            ResetBeginLogicTick = currentTick;
            PrimaryTargetUid = default;
        }

        public void TransitionToDead(int currentTick)
        {
            State = JungleCampState.Dead;
            NextRespawnLogicTick = checked(currentTick + respawnDelayTicks);
            PrimaryTargetUid = default;
        }

        public void TransitionToIdle()
        {
            State = JungleCampState.Idle;
            PrimaryTargetUid = default;
            LastHostileActionLogicTick = 0;
            ResetBeginLogicTick = 0;
        }

        public void Capture(ref JungleCampSnapshot state)
        {
            state.CampId = CampId;
            state.State = State;
            state.MemberUidsBySlot = new List<UnitUid>(MemberUidsBySlot);
            state.MemberAliveBySlot = new List<bool>(MemberAliveBySlot);
            state.MainMonsterDead = MainMonsterDead;
            state.PrimaryTargetUid = PrimaryTargetUid;
            state.LastHostileActionLogicTick = LastHostileActionLogicTick;
            state.NextRespawnLogicTick = NextRespawnLogicTick;
            state.ResetBeginLogicTick = ResetBeginLogicTick;
        }

        public void Restore(in JungleCampSnapshot state)
        {
            if (state.CampId != CampId)
                throw new DeterministicSimulationException(
                    $"Cannot restore JungleCamp {state.CampId} into {CampId}.");
            State = state.State;
            MemberUidsBySlot.Clear();
            if (state.MemberUidsBySlot != null)
                MemberUidsBySlot.AddRange(state.MemberUidsBySlot);
            MemberAliveBySlot.Clear();
            if (state.MemberAliveBySlot != null)
                MemberAliveBySlot.AddRange(state.MemberAliveBySlot);
            MainMonsterDead = state.MainMonsterDead;
            PrimaryTargetUid = state.PrimaryTargetUid;
            LastHostileActionLogicTick = state.LastHostileActionLogicTick;
            NextRespawnLogicTick = state.NextRespawnLogicTick;
            ResetBeginLogicTick = state.ResetBeginLogicTick;
        }
    }
}
