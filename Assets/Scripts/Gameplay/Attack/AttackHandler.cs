using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum AttackPlanStatus : byte
    {
        Unavailable = 0,
        TargetInvalid = 1,
        OutOfRange = 2,
        WaitingForReady = 3,
        Ready = 4,
    }

    public enum AttackTimerResetReason : byte
    {
        AbilityEffect = 0,
        ScriptedRule = 1,
        MoveCancelRecovery = 2,
    }

    public class AttackHandler : UnitHandler, IRollback<AttackSnapshot>
    {
        private const int InvalidLogicTick = -1;

        private AttackSnapshot _state;
        private fp runtimeWindupRatio;
        private int runtimeTickRate;
        private int runtimeSequenceResetIntervalTicks;

        [Header("Authoring")]
        [Tooltip("Fraction of the attack period before impact. Converted to fixed point once at runtime initialization.")]
        [SerializeField, Range(0f, 1f)] private float windupRatio = 0.2f;
        [SerializeField, Min(0)] private int projectileDefId;
        [SerializeField, Min(0)] private int commitSfxEventId;
        [SerializeField] private PresentationAnchor commitSfxAnchor =
            PresentationAnchor.UnitRoot;

        public fp WindupRatio
        {
            get => runtimeWindupRatio;
            set => runtimeWindupRatio = fpmath.clamp(value, fp.zero, fp.one);
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

        public PresentationAnchor CommitSfxAnchor
        {
            get => commitSfxAnchor;
            set => commitSfxAnchor = value;
        }

        public ref readonly AttackSnapshot Snapshot => ref _state;
        public bool ImpactCommitted => _state.ImpactCommitted;
        public UnitUid CurrentTargetUid => _state.CurrentTargetUid;
        public byte AttackSequenceIndex => _state.AttackSequenceIndex;
        public int LastSuccessfulAttackLogicTick =>
            _state.LastSuccessfulAttackLogicTick;

        public fp CurrentAttackRange => Owner?.StatHandler != null
            ? Owner.StatHandler.GetStat(StatId.AttackRange) *
                (Owner.World?.StatDistanceToLogicDistanceScale ??
                 (fp)0.01m)
            : fp.zero;

        public bool IsAttackCycleActive =>
            HasActiveAttackCycle(
                SimulationTickContext.Current.Tick);

        public bool IsAttackReady() =>
            SimulationTickContext.Current.Tick >=
            _state.NextAttackReadyLogicTick;

        public bool CanStartNewAttack => IsAttackReady();

        public void InitializeForNewRuntime(
            int tickRate,
            int sequenceResetIntervalTicks)
        {
            if (tickRate <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(tickRate));
            if (sequenceResetIntervalTicks < 1)
                throw new System.ArgumentOutOfRangeException(
                    nameof(sequenceResetIntervalTicks));

            _state = AttackSnapshot.Default;
            _state.NextAttackReadyLogicTick =
                Owner?.UnitUid.SpawnLogicTick ?? 0;
            runtimeWindupRatio = fpmath.clamp(
                (fp)windupRatio, fp.zero, fp.one);
            runtimeTickRate = tickRate;
            runtimeSequenceResetIntervalTicks =
                sequenceResetIntervalTicks;
            ProjectileWorld = null;
        }

        public override void InitializeForNewRuntime() =>
            throw new System.InvalidOperationException(
                "AttackHandler requires baked TickRate and sequence reset interval.");

        public virtual AttackPlanStatus GetAttackPlanStatus(
            UnitUid targetUid)
        {
            if (Owner == null ||
                !Owner.AbilityMask.HasAttack ||
                !Owner.CapabilityState.CanAttack ||
                GetAttackSpeed() <= fp.zero)
            {
                return AttackPlanStatus.Unavailable;
            }

            if (!TryResolveTarget(targetUid, out Unit target))
                return AttackPlanStatus.TargetInvalid;

            if (!IsInAttackRange(target))
                return AttackPlanStatus.OutOfRange;

            return IsAttackReady()
                ? AttackPlanStatus.Ready
                : AttackPlanStatus.WaitingForReady;
        }

        public void ApplyAttackInput(UnitUid targetUid)
        {
            if (GetAttackPlanStatus(targetUid) == AttackPlanStatus.Ready)
                BeginAttack(targetUid);
        }

        public virtual void BeginAttack(UnitUid targetUid)
        {
            if (GetAttackPlanStatus(targetUid) != AttackPlanStatus.Ready)
                return;

            int currentTick = SimulationTickContext.Current.Tick;
            if (_state.LastSuccessfulAttackLogicTick != InvalidLogicTick &&
                currentTick - _state.LastSuccessfulAttackLogicTick >=
                runtimeSequenceResetIntervalTicks)
            {
                _state.AttackSequenceIndex = 0;
            }

            fp attackSpeed = GetAttackSpeed();
            int durationTicks = CeilPositive(
                (fp)runtimeTickRate / attackSpeed);
            if (durationTicks < 1) durationTicks = 1;

            fp ratio = fpmath.clamp(
                ResolveWindupRatio(), fp.zero, fp.one);
            int windupTicks = RoundPositive(
                (fp)durationTicks * ratio);
            windupTicks = ClampInt(1, durationTicks, windupTicks);

            _state.CurrentTargetUid = targetUid;
            _state.AttackStartLogicTick = currentTick;
            _state.ImpactLogicTick = currentTick + windupTicks;
            _state.NextAttackReadyLogicTick =
                currentTick + durationTicks;
            _state.ResolvedAttackDurationTicks = durationTicks;
            _state.ResolvedWindupTicks = windupTicks;
            _state.ImpactCommitted = false;
            _state.IsEmpoweredAttack = ResolveIsEmpoweredAttack();

            // Ordinary attack and active route movement are mutually
            // exclusive. Forced movement and dashes remain owned by the
            // MovementHandler and are intentionally unaffected.
            Owner.Locomotion?.CancelRoute(
                MoveCancelReason.AttackStarted);
            TurnToTargetImmediately(targetUid);
        }

        public void TickUpdate()
        {
            if (!_state.CurrentTargetUid.IsValid() ||
                _state.ImpactCommitted)
            {
                return;
            }

            if (Owner.HitReaction.InterruptsAttack)
            {
                CancelBeforeCommit();
                return;
            }

            if (SimulationTickContext.Current.Tick >=
                _state.ImpactLogicTick)
            {
                CommitAttack();
            }
        }

        /// <summary>
        /// Deterministically drops a current attack target that is no longer
        /// alive/present (e.g. it died this Tick and was disposed). Called
        /// after combat death disposals so the Tick-end snapshot never
        /// carries a stale target reference, which would otherwise make a
        /// later rollback restore fail "Attack snapshot references missing
        /// target".
        /// </summary>
        public void ClearTargetIfMissing()
        {
            if (!_state.CurrentTargetUid.IsValid())
            {
                return;
            }
            if (_state.ImpactCommitted)
            {
                // A committed attack already handed the target to its
                // projectile; the handler field stays set until the cycle
                // completes (and Resolve tolerates a target that died
                // mid-flight). Clearing it here would violate the
                // committed-with-target snapshot invariant.
                return;
            }
            UnitWorld world = Owner?.World;
            bool targetAlive =
                world != null &&
                world.TryGetUnit(
                    _state.CurrentTargetUid,
                    out Unit target) &&
                target.LifeState == LifeState.Alive;
            if (targetAlive)
            {
                return;
            }
            CancelBeforeCommit();
        }

        public virtual bool CommitAttack()
        {
            int currentTick = SimulationTickContext.Current.Tick;
            if (_state.ImpactCommitted ||
                !_state.CurrentTargetUid.IsValid() ||
                currentTick < _state.ImpactLogicTick)
            {
                return false;
            }

            if (!TryResolveTarget(
                    _state.CurrentTargetUid, out Unit target) ||
                !IsInAttackRange(target))
            {
                CancelBeforeCommit();
                return false;
            }

            TurnToTargetImmediately(target.UnitUid);

            byte committedSequence = _state.AttackSequenceIndex;
            bool emitted = ResolveProjectileDefId() == 0
                ? EmitDirectAttack(target)
                : EmitProjectileAttack(target);

            if (!emitted)
            {
                CancelBeforeCommit();
                return false;
            }

            _state.ImpactCommitted = true;
            _state.LastSuccessfulAttackLogicTick = currentTick;
            _state.AttackSequenceIndex =
                committedSequence == byte.MaxValue
                    ? (byte)0
                    : (byte)(committedSequence + 1);

            SubmitCommitSfx(committedSequence);
            return true;
        }

        public virtual void CancelBeforeCommit()
        {
            if (_state.ImpactCommitted) return;

            _state.CurrentTargetUid = default;
            _state.AttackStartLogicTick = InvalidLogicTick;
            _state.ImpactLogicTick = InvalidLogicTick;
            _state.NextAttackReadyLogicTick =
                SimulationTickContext.Current.Tick;
            _state.ResolvedAttackDurationTicks = 0;
            _state.ResolvedWindupTicks = 0;
            _state.IsEmpoweredAttack = false;
        }

        public virtual void ResetAttackTimer(
            AttackTimerResetReason reason)
        {
            _state.NextAttackReadyLogicTick =
                SimulationTickContext.Current.Tick;
        }

        protected virtual fp ResolveWindupRatio() =>
            runtimeWindupRatio;

        protected virtual bool ValidateAdditionalTarget(Unit target) =>
            true;

        protected virtual bool ResolveIsEmpoweredAttack() =>
            false;

        protected virtual int ResolveProjectileDefId() =>
            projectileDefId;

        protected virtual int ResolveCommitSfxEventId() =>
            commitSfxEventId;

        protected virtual PresentationAnchor
            ResolveCommitSfxAnchor() =>
                commitSfxAnchor;

        protected virtual bool EmitDirectAttack(Unit target)
        {
            fp damage = GetAttackDamage();
            CombatSystem combat = Owner.World?.CombatSystem;
            if (damage <= fp.zero || combat == null)
                return false;

            var source = new SourceDescriptor
            {
                SourceType = CombatSourceType.Attack,
                SourceId = CombatBuiltinSourceId.BasicAttack,
                OwnerUnitUid = Owner.UnitUid,
                EmitterUnitUid = Owner.UnitUid,
            };
            var request = new DamageRequest
            {
                Header = new CombatRequestHeader
                {
                    SourceUnitUid = Owner.UnitUid,
                    TargetUnitUid = target.UnitUid,
                    SourceDescriptor = source,
                    RecipeId =
                        CombatBuiltinRecipeId.BasicAttackDamage,
                },
                DamageType = DamageType.Physical,
                BaseDamage = damage,
            };
            return combat.SubmitDamage(request);
        }

        protected virtual ProjectileSpawnRequest
            BuildProjectileSpawnRequest(Unit target)
        {
            fp2 sourcePosition =
                Owner.PhysicsEntity.Transform2D.Position;
            fp2 direction =
                target.PhysicsEntity.Transform2D.Position -
                sourcePosition;
            return new ProjectileSpawnRequest(
                ResolveProjectileDefId(),
                Owner.UnitUid,
                Owner.TeamId,
                new SourceDescriptor
                {
                    SourceType = CombatSourceType.Attack,
                    SourceId = CombatBuiltinSourceId.BasicAttack,
                    OwnerUnitUid = Owner.UnitUid,
                    EmitterUnitUid = Owner.UnitUid,
                },
                sourcePosition,
                direction,
                null,
                0,
                target.UnitUid);
        }

        private bool EmitProjectileAttack(Unit target)
        {
            ProjectileWorld world =
                Owner.World?.ProjectileWorld ?? ProjectileWorld;
            if (world == null) return false;

            ProjectileUid uid = world.RequestSpawn(
                BuildProjectileSpawnRequest(target));
            if (uid.IsValid)
            {
                OnProjectileCommitted(uid);
            }
            return uid.IsValid;
        }

        /// <summary>
        /// Hook after a projectile attack is spawned (used by tower ramp and
        /// in-flight locking). Base implementation does nothing.
        /// </summary>
        protected virtual void OnProjectileCommitted(
            ProjectileUid uid)
        {
        }

        private bool TryResolveTarget(
            UnitUid targetUid,
            out Unit target)
        {
            target = null;
            if (!targetUid.IsValid() ||
                Owner?.World == null ||
                !Owner.World.TryGetUnit(targetUid, out target) ||
                target == null ||
                target.UnitUid == Owner.UnitUid ||
                target.LifeState != LifeState.Alive ||
                !target.CapabilityState.IsTargetable ||
                target.TeamId == TeamId.Neutral ||
                Owner.TeamId == TeamId.Neutral ||
                target.TeamId == Owner.TeamId ||
                !ValidateAdditionalTarget(target))
            {
                target = null;
                return false;
            }

            return true;
        }

        private bool IsInAttackRange(Unit target)
        {
            return Owner.PhysicsEntity != null &&
                target?.PhysicsEntity != null &&
                RangeQueryService.IsInRange(
                    Owner.PhysicsEntity,
                    target.PhysicsEntity,
                    CurrentAttackRange);
        }

        private void TurnToTargetImmediately(UnitUid targetUid)
        {
            if (!Owner.CapabilityState.CanTurn ||
                !Owner.CapabilityState.CanMove ||
                Owner.World == null ||
                !Owner.World.TryGetUnit(targetUid, out Unit target))
            {
                return;
            }

            fp2 direction =
                target.PhysicsEntity.Transform2D.Position -
                Owner.PhysicsEntity.Transform2D.Position;
            Owner.PhysicsEntity.SetLogicForward(direction);
        }

        private void SubmitCommitSfx(byte committedSequence)
        {
            int eventId = ResolveCommitSfxEventId();
            if (eventId == 0) return;
            Debug.Log(
                $"[AttackSfx] submit id={eventId} " +
                $"seq={committedSequence}");

            int tick = SimulationTickContext.Current.Tick;
            var evt = new SfxEvent
            {
                Id = new PresentationEventId
                {
                    SourceLogicTick = tick,
                    SourceKind = PresentationSourceKind.Unit,
                    SourceRuntimeUid = Owner.UnitUid,
                    EventSequence = committedSequence,
                    EventKey = eventId,
                },
                SfxDefId = eventId,
                Anchor = SfxAnchor.UnitRoot,
                AttachToUnit = Owner.UnitUid,
                SocketKey =
                    (int)ResolveCommitSfxAnchor(),
                PitchScale = fp.one,
                VolumeScale = fp.one,
            };

            VisualEventOutput.SubmitSfx(evt);
        }

        private fp GetAttackSpeed()
        {
            if (Owner?.StatHandler == null) return fp.zero;
            return Owner.StatHandler.GetStat(StatId.AttackSpeed);
        }

        protected fp GetAttackDamage()
        {
            if (Owner?.StatHandler == null) return fp.zero;
            return Owner.StatHandler.GetStat(StatId.AttackDamage);
        }

        private static int CeilPositive(fp value)
        {
            int whole = (int)value;
            return (fp)whole < value ? checked(whole + 1) : whole;
        }

        private static int RoundPositive(fp value)
        {
            if (value <= fp.zero) return 0;
            return (int)(value + fp.one / (fp)2);
        }

        private static int ClampInt(
            int min,
            int max,
            int value)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        public virtual void Capture(ref AttackSnapshot state)
        {
            ValidateState(_state);
            state = _state;
        }

        public virtual void Restore(in AttackSnapshot state)
        {
            ValidateState(state);
            _state = state;
        }

        public void Resolve(in RollbackContext context)
        {
            UnitUid targetUid = _state.CurrentTargetUid;
            if (targetUid.IsValid() &&
                !_state.ImpactCommitted &&
                (Owner?.World == null ||
                 !Owner.World.TryGetUnit(targetUid, out _)))
            {
                throw new DeterministicSimulationException(
                    $"Attack snapshot references missing target {targetUid} " +
                    $"(owner={Owner?.UnitUid} tick={context.TargetTick}).");
            }
        }

        public void Rebuild(in RollbackContext context)
        {
        }

        public AttackAnimationSnapshot GetAnimationSnapshot()
        {
            int now = SimulationTickContext.Current.Tick;
            bool hasCycle = HasActiveAttackCycle(now);
            bool windup = hasCycle && !_state.ImpactCommitted;
            bool recovery = hasCycle && _state.ImpactCommitted;

            float windupProgress = 0f;
            if (windup && _state.ResolvedWindupTicks > 0)
            {
                windupProgress =
                    (float)(now - _state.AttackStartLogicTick) /
                    _state.ResolvedWindupTicks;
            }

            float recoveryProgress = 0f;
            int recoveryTicks =
                _state.ResolvedAttackDurationTicks -
                _state.ResolvedWindupTicks;
            if (recovery && recoveryTicks > 0)
            {
                recoveryProgress =
                    (float)(now - _state.ImpactLogicTick) /
                    recoveryTicks;
            }

            byte animationSequence = _state.ImpactCommitted
                ? (_state.AttackSequenceIndex == 0
                    ? byte.MaxValue
                    : (byte)(_state.AttackSequenceIndex - 1))
                : _state.AttackSequenceIndex;

            return new AttackAnimationSnapshot
            {
                IsAttacking = hasCycle,
                SequenceIndex = animationSequence,
                ImpactCommitted = _state.ImpactCommitted,
                WindupProgress = Mathf.Clamp01(windupProgress),
                RecoveryProgress = Mathf.Clamp01(recoveryProgress),
            };
        }

        private bool HasActiveAttackCycle(int logicTick)
        {
            return _state.CurrentTargetUid.IsValid() &&
                _state.AttackStartLogicTick != InvalidLogicTick &&
                logicTick >= _state.AttackStartLogicTick &&
                logicTick < _state.NextAttackReadyLogicTick;
        }

        public override void ClearForDeath()
        {
            byte sequence = _state.AttackSequenceIndex;
            int lastSuccessful =
                _state.LastSuccessfulAttackLogicTick;
            _state = AttackSnapshot.Default;
            _state.NextAttackReadyLogicTick =
                SimulationTickContext.Current.Tick;
            _state.AttackSequenceIndex = sequence;
            _state.LastSuccessfulAttackLogicTick =
                lastSuccessful;
        }

        public override void ResetForPool()
        {
            _state = AttackSnapshot.Default;
            runtimeWindupRatio = default;
            runtimeTickRate = 0;
            runtimeSequenceResetIntervalTicks = 0;
            ProjectileWorld = null;
        }

        private static void ValidateState(
            in AttackSnapshot state)
        {
            if (state.ResolvedAttackDurationTicks < 0 ||
                state.ResolvedWindupTicks < 0 ||
                state.ResolvedWindupTicks >
                state.ResolvedAttackDurationTicks)
            {
                throw new DeterministicSimulationException(
                    "Attack snapshot contains invalid resolved timing.");
            }

            if (state.CurrentTargetUid.IsValid() &&
                (state.AttackStartLogicTick < 0 ||
                 state.ImpactLogicTick <
                 state.AttackStartLogicTick ||
                 state.NextAttackReadyLogicTick <
                 state.ImpactLogicTick))
            {
                throw new DeterministicSimulationException(
                    "Attack snapshot contains an invalid active timeline.");
            }

            if (!state.CurrentTargetUid.IsValid() &&
                state.ImpactCommitted)
            {
                throw new DeterministicSimulationException(
                    "Attack snapshot cannot be committed without a target.");
            }
        }
    }
}
