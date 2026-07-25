using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class AttackHandler : UnitHandler, IRollback<AttackSnapshot>
    {
        private AttackSnapshot _state;
        private fp runtimeWindupRatio;
        private int runtimeTickRate;
        private int _lastAttackCompleteTick;
        public int IdleResetWindowTicks = 90;

        [Header("Authoring")]
        [Tooltip("Fraction of the attack period before impact. Converted to fixed point once at runtime initialization.")]
        [SerializeField, Range(0f, 1f)] private float windupRatio = 0.2f;
        [SerializeField, Min(0)] private int projectileDefId;
        [SerializeField, Min(0)] private int commitSfxEventId;

        public fp WindupRatio
        {
            get => runtimeWindupRatio;
            set => runtimeWindupRatio = value;
        }
        public int ProjectileDefId
        {
            get => projectileDefId;
            set => projectileDefId = value;
        }
        public ProjectileWorld ProjectileWorld { get; set; }
        public int CommitSfxEventId
        {
            get => commitSfxEventId;
            set => commitSfxEventId = value;
        }

        public ref readonly AttackSnapshot Snapshot => ref _state;
        public bool ImpactCommitted => _state.ImpactCommitted;
        public UnitUid CurrentTargetUid => _state.CurrentTargetUid;
        public byte AttackSequenceIndex => _state.AttackSequenceIndex;

        public bool CanStartNewAttack
        {
            get
            {
                int tick = SimulationTickContext.Current.Tick;
                return tick >= _state.NextAttackReadyLogicTick;
            }
        }

        public void InitializeForNewRuntime(int tickRate)
        {
            if (tickRate <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(tickRate));
            _state = AttackSnapshot.Default;
            runtimeWindupRatio = (fp)windupRatio;
            runtimeTickRate = tickRate;
            ProjectileWorld = null;
        }

        public override void InitializeForNewRuntime() =>
            throw new System.InvalidOperationException(
                "AttackHandler requires the baked global TickRate.");

        public void ApplyAttackInput(UnitUid targetUid)
        {
            if (Owner == null || Owner.HitReaction.InterruptsAttack) return;
            if (!CanStartNewAttack) return;
            if (!targetUid.IsValid()) return;

            int tick = SimulationTickContext.Current.Tick;
            fp attackSpeed = GetAttackSpeed();
            if (attackSpeed <= fp.zero) return;

            fp attacksPerTick = attackSpeed / (fp)runtimeTickRate;
            int totalAttackTicks = attacksPerTick > fp.zero
                ? CeilDiv(fp.one, attacksPerTick)
                : 1;

            int windupTicks = ClampInt(1, totalAttackTicks,
                (int)((fp)totalAttackTicks * WindupRatio));

            int startTick = tick;
            int impactTick = startTick + windupTicks;
            int readyTick = startTick + totalAttackTicks;

            _state.CurrentTargetUid = targetUid;
            _state.AttackStartLogicTick = startTick;
            _state.ImpactLogicTick = impactTick;
            _state.NextAttackReadyLogicTick = readyTick;
            _state.ImpactCommitted = false;
            _state.AttackSequenceIndex++;
        }

        public DamageRequest? TickUpdate()
        {
            int tick = SimulationTickContext.Current.Tick;

            if (Owner.HitReaction.InterruptsAttack && !_state.ImpactCommitted)
            {
                _state.ImpactCommitted = true;
                _state.NextAttackReadyLogicTick = tick;
                _lastAttackCompleteTick = tick;
                return null;
            }

            // Sequence idle reset: if no attack active and idle window expired, reset sequence to 0
            if (!_state.CurrentTargetUid.IsValid() || tick >= _state.NextAttackReadyLogicTick)
            {
                int idleTicks = tick - _lastAttackCompleteTick;
                if (IdleResetWindowTicks > 0 && idleTicks >= IdleResetWindowTicks)
                    _state.AttackSequenceIndex = 0;
            }

            if (_state.ImpactCommitted) return null;
            if (!_state.CurrentTargetUid.IsValid()) return null;
            if (_state.ImpactLogicTick <= 0) return null;
            if (tick < _state.ImpactLogicTick) return null;

            _state.ImpactCommitted = true;
            _lastAttackCompleteTick = tick;

            SubmitCommitSfx();

            if (ProjectileDefId == 0)
            {
                fp damage = GetAttackDamage();
                if (damage <= fp.zero) return null;

                return new DamageRequest
                {
                    SourceUnitUid = Owner.UnitUid,
                    TargetUnitUid = _state.CurrentTargetUid,
                    BaseDamage = damage,
                    AttackSequenceIndex = _state.AttackSequenceIndex,
                };
            }

            if (ProjectileDefId != 0 && ProjectileWorld != null)
            {
                var def = ProjectileWorld.DefRegistry?.FindById(ProjectileDefId);
                if (def != null)
                {
                    fp2 facing = Owner.MovementHandler != null
                        ? Owner.MovementHandler.Snapshot.Facing
                        : new fp2(fp.one, fp.zero);
                    fp2 spawnPos = Owner.MovementHandler != null
                        ? Owner.MovementHandler.Snapshot.Position
                        : fp2.zero;
                    ProjectileWorld.RequestSpawn(new ProjectileSpawnRequest(
                        def.DefId,
                        Owner.UnitUid,
                        Owner.TeamId,
                        spawnPos,
                        facing));
                }
            }
            return null;
        }

        private void SubmitCommitSfx()
        {
            if (CommitSfxEventId == 0) return;

            int tick = SimulationTickContext.Current.Tick;
            var evt = new SfxEvent
            {
                Id = new PresentationEventId
                {
                    SourceLogicTick = tick,
                    SourceKind = PresentationSourceKind.Unit,
                    SourceRuntimeUid = Owner.UnitUid,
                    EventSequence = _state.AttackSequenceIndex,
                    EventKey = CommitSfxEventId,
                },
                SfxDefId = CommitSfxEventId,
                Anchor = SfxAnchor.UnitRoot,
                AttachToUnit = Owner.UnitUid,
                PitchScale = fp.one,
                VolumeScale = fp.one,
            };

            VisualEventOutput.SubmitSfx(evt);
        }

        private fp GetAttackSpeed()
        {
            if (Owner.StatHandler == null) return fp.zero;
            return Owner.StatHandler.GetStat(StatId.AttackSpeed);
        }

        private fp GetAttackDamage()
        {
            if (Owner.StatHandler == null) return fp.zero;
            return Owner.StatHandler.GetStat(StatId.AttackDamage);
        }

        private static int CeilDiv(fp a, fp b)
        {
            if (b <= fp.zero) return 1;
            long num = a.RawValue;
            long den = b.RawValue;
            return (int)((num + den - 1) / den);
        }

        private static int ClampInt(int min, int max, int value)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public void Capture(ref AttackSnapshot state) { state = _state; }
        public void Restore(in AttackSnapshot state) { _state = state; }
        public void Resolve(in RollbackContext context)
        {
            UnitUid targetUid = _state.CurrentTargetUid;
            if (targetUid.IsValid() &&
                (Owner?.World == null || !Owner.World.TryGetUnit(targetUid, out _)))
                throw new DeterministicSimulationException(
                    $"Attack snapshot references missing target {targetUid}.");
        }

        public void Rebuild(in RollbackContext context)
        {
            // AttackSnapshot contains no derived index or Unity reference.
        }

        /// <summary>
        /// Builds an animation-facing snapshot of the current attack state.
        /// Safe to call from Presentation (LateUpdate).
        /// </summary>
        public AttackAnimationSnapshot GetAnimationSnapshot()
        {
            int now = SimulationTickContext.Current.Tick;
            bool isAttacking = _state.CurrentTargetUid.IsValid()
                           && now >= _state.AttackStartLogicTick
                           && now < _state.NextAttackReadyLogicTick
                           && !_state.ImpactCommitted;

            float windupProgress = 0f;
            float recoveryProgress = 0f;

            if (isAttacking)
            {
                int windupTicks = _state.ImpactLogicTick - _state.AttackStartLogicTick;
                int totalTicks = _state.NextAttackReadyLogicTick - _state.AttackStartLogicTick;
                int elapsed = now - _state.AttackStartLogicTick;

                if (windupTicks > 0 && !_state.ImpactCommitted)
                    windupProgress = (float)elapsed / (float)windupTicks;

                if (_state.ImpactCommitted && totalTicks > windupTicks)
                {
                    int recoveryElapsed = now - _state.ImpactLogicTick;
                    int recoveryTicks = totalTicks - windupTicks;
                    if (recoveryTicks > 0)
                        recoveryProgress = (float)recoveryElapsed / (float)recoveryTicks;
                }
            }

            return new AttackAnimationSnapshot
            {
                IsAttacking = isAttacking,
                SequenceIndex = _state.AttackSequenceIndex,
                ImpactCommitted = _state.ImpactCommitted,
                WindupProgress = windupProgress,
                RecoveryProgress = recoveryProgress,
            };
        }

        public override void ClearForDeath()
        {
            _state = AttackSnapshot.Default;
        }

        public override void ResetForPool()
        {
            _state = AttackSnapshot.Default;
            runtimeWindupRatio = default;
            ProjectileWorld = null;
        }
    }
}
