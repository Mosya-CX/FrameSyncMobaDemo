using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct ProjectileHitRecord
    {
        public UnitUid TargetUid;
        public int HitCount;
        public int LastHitLogicTick;
    }

    public sealed class ProjectileRuntime
    {
        private readonly List<ProjectileHitRecord> hitRecords =
            new List<ProjectileHitRecord>();

        public ProjectileUid Uid { get; }
        public ProjectileDef Def { get; }
        public UnitUid OwnerUnitUid { get; }
        public TeamId TeamSnapshot { get; }
        public SourceDescriptor Source { get; }
        public OriginActionId OriginActionId { get; }
        public PhysicsEntity2D PhysicsEntity { get; }
        public fp2 Position => PhysicsEntity.Transform2D.Position;
        public fp2 PrevPosition => PhysicsEntity.Transform2D.PrevPosition;
        public fp2 Velocity { get; private set; }
        /// <summary>
        /// Logic seconds advanced per Tick (1 / TickRate). Def.Speed is
        /// authored in logic units per second; the per-Tick displacement is
        /// Speed * LogicSecondsPerTick.
        /// </summary>
        public fp LogicSecondsPerTick { get; set; } = fp.one;
        /// <summary>Locked homing target (design v19 跟踪弹体).</summary>
        public UnitUid TargetUnitUid { get; private set; }
        /// <summary>World used to resolve the homing target's position.
        /// Assigned by ProjectileWorld.</summary>
        public UnitWorld UnitWorld { get; set; }
        public int RemainingLifetimeTicks { get; private set; }
        public bool IsActive { get; private set; }
        public bool EndRequested { get; private set; }
        public ProjectileEndReason EndReason { get; private set; }
        public int TotalHitCount { get; private set; }
        public int RemainingPierceCount { get; private set; }
        public int RemainingBounceCount { get; private set; }
        public int NextQueryLogicTick { get; private set; }
        public IReadOnlyList<ProjectileHitRecord> HitRecords => hitRecords;
        /// <summary>
        /// Per-instance on-hit damage override (null = use ProjectileDef).
        /// Snapshot member; never references Unity objects.
        /// </summary>
        public ProjectileOnHitDamage[] OnHitDamageOverride
        {
            get;
            private set;
        }

        public ProjectileRuntime(
            ProjectileUid uid,
            ProjectileDef def,
            UnitUid ownerUnitUid,
            TeamId teamSnapshot,
            SourceDescriptor source,
            OriginActionId originActionId,
            PhysicsEntity2D physicsEntity,
            fp2 startPosition,
            fp2 direction,
            ProjectileOnHitDamage[] onHitDamageOverride = null,
            int maxLifetimeTicksOverride = 0,
            UnitUid targetUnitUid = default)
        {
            if (def == null)
                throw new System.ArgumentNullException(nameof(def));
            if (physicsEntity == null)
                throw new System.ArgumentNullException(nameof(physicsEntity));
            if (!source.IsValid)
                throw new DeterministicSimulationException(
                    "Projectile source descriptor is invalid.");
            if (!originActionId.IsValid)
                throw new DeterministicSimulationException(
                    "Projectile OriginActionId is invalid.");

            Uid = uid;
            Def = def;
            OwnerUnitUid = ownerUnitUid;
            TeamSnapshot = teamSnapshot;
            Source = source;
            OriginActionId = originActionId;
            PhysicsEntity = physicsEntity;
            PhysicsEntity.SetLogicPose(startPosition, direction);
            Velocity = direction * def.Speed;
            RemainingLifetimeTicks =
                maxLifetimeTicksOverride > 0
                    ? maxLifetimeTicksOverride
                    : def.MaxLifetimeTicks;
            OnHitDamageOverride =
                onHitDamageOverride != null
                    ? (ProjectileOnHitDamage[])
                        onHitDamageOverride.Clone()
                    : null;
            TargetUnitUid = targetUnitUid;
            RemainingPierceCount = def.HitPolicy.InitialPierceCount;
            RemainingBounceCount = def.HitPolicy.InitialBounceCount;
            NextQueryLogicTick = uid.SpawnLogicTick;
            IsActive = true;
        }

        public void AdvanceMotion()
        {
            if (!IsActive || EndRequested) return;
            if (Def.Homing)
            {
                Unit target = null;
                bool targetExists =
                    TargetUnitUid.IsValid() &&
                    UnitWorld != null &&
                    UnitWorld.TryGetUnit(
                        TargetUnitUid,
                        out target) &&
                    target != null;
                if (!targetExists)
                {
                    // Homing projectile with no live target must terminate
                    // instead of flying in a straight line forever (design
                    // v19 homing projectile lifecycle).
                    RequestEnd(
                        ProjectileEndReason
                            .ExplicitRequest);
                    return;
                }

                // Structures (towers) have no MovementHandler; track their
                // deterministic transform position instead.
                fp2 targetPosition =
                    target.MovementHandler != null
                        ? target.MovementHandler.Position
                        : target.PhysicsEntity
                            ?.Transform2D.Position ??
                          Position;
                fp2 toTarget =
                    targetPosition - Position;
                fp distSq =
                    fpmath.dot(toTarget, toTarget);
                if (distSq > fp.zero)
                {
                    fp2 dir =
                        fpmath.normalize(toTarget);
                    PhysicsEntity.SetLogicForward(dir);
                    Velocity =
                        dir * fpmath.length(Velocity);
                }
            }
            PhysicsEntity.SetLogicPosition(
                Position +
                Velocity * LogicSecondsPerTick);
            if (Def.Acceleration == fp.zero) return;

            fp speed = fpmath.length(Velocity) +
                Def.Acceleration * LogicSecondsPerTick;
            if (speed <= fp.zero)
            {
                Velocity = fp2.zero;
                return;
            }

            Velocity = fpmath.normalize(Velocity) * speed;
            PhysicsEntity.SetLogicForward(Velocity);
        }

        public void UpdateLifecycle()
        {
            if (!IsActive || EndRequested) return;
            RemainingLifetimeTicks--;
            if (RemainingLifetimeTicks <= 0)
                RequestEnd(ProjectileEndReason.LifetimeExpired);
        }

        public bool ShouldQuery(int logicTick)
        {
            ProjectileHitPolicy policy = Def.HitPolicy;
            return IsActive &&
                policy.Enabled &&
                logicTick >= NextQueryLogicTick &&
                (!EndRequested ||
                 !policy.StopResolvingAfterEndRequested) &&
                (policy.MaxTotalHitCount == 0 ||
                 TotalHitCount < policy.MaxTotalHitCount);
        }

        public void MarkQueried(int logicTick)
        {
            NextQueryLogicTick = checked(
                logicTick + Def.HitPolicy.QueryIntervalTicks);
        }

        public bool CanHitTarget(UnitUid targetUid, int logicTick)
        {
            if (!targetUid.IsValid()) return false;
            HitSameTargetPolicy policy =
                Def.HitPolicy.SameTargetPolicy;
            if (policy == HitSameTargetPolicy.Unrestricted)
                return true;

            int index = FindHitRecord(targetUid);
            if (index < 0) return true;
            if (policy == HitSameTargetPolicy.Once)
                return false;
            return logicTick - hitRecords[index].LastHitLogicTick >=
                Def.HitPolicy.SameTargetCooldownTicks;
        }

        public bool RegisterHit(UnitUid targetUid, int logicTick)
        {
            if (!ShouldAcceptResolvedHit(targetUid, logicTick))
                return false;

            int index = FindHitRecord(targetUid);
            if (index < 0)
            {
                hitRecords.Add(new ProjectileHitRecord
                {
                    TargetUid = targetUid,
                    HitCount = 1,
                    LastHitLogicTick = logicTick,
                });
                hitRecords.Sort((a, b) =>
                    a.TargetUid.CompareTo(b.TargetUid));
            }
            else
            {
                ProjectileHitRecord record = hitRecords[index];
                record.HitCount++;
                record.LastHitLogicTick = logicTick;
                hitRecords[index] = record;
            }

            TotalHitCount++;
            if (RemainingPierceCount > 0)
                RemainingPierceCount--;

            ProjectileHitPolicy policy = Def.HitPolicy;
            if (policy.EndOnFirstValidHit ||
                (policy.InitialPierceCount > 0 &&
                 RemainingPierceCount == 0) ||
                (policy.MaxTotalHitCount > 0 &&
                 TotalHitCount >= policy.MaxTotalHitCount))
            {
                RequestEnd(ProjectileEndReason.HitPolicyExhausted);
            }

            return true;
        }

        public void RequestEnd(ProjectileEndReason reason)
        {
            if (!IsActive || EndRequested) return;
            EndRequested = true;
            EndReason = reason == ProjectileEndReason.None
                ? ProjectileEndReason.ExplicitRequest
                : reason;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Destroy()
        {
            RequestEnd(ProjectileEndReason.ExplicitRequest);
        }

        internal void RestoreFromSnapshot(
            in ProjectileRuntimeSnapshot snapshot)
        {
            PhysicsEntity.TeleportLogicPosition(
                snapshot.PreviousPosition);
            PhysicsEntity.SetLogicPosition(
                snapshot.Position);
            if (snapshot.Velocity.x != fp.zero ||
                snapshot.Velocity.y != fp.zero)
                PhysicsEntity.SetLogicForward(snapshot.Velocity);
            Velocity = snapshot.Velocity;
            RemainingLifetimeTicks =
                snapshot.RemainingLifetimeTicks;
            IsActive = snapshot.IsActive;
            EndRequested = snapshot.EndRequested;
            EndReason = snapshot.EndReason;
            TotalHitCount = snapshot.TotalHitCount;
            RemainingPierceCount =
                snapshot.RemainingPierceCount;
            RemainingBounceCount =
                snapshot.RemainingBounceCount;
            NextQueryLogicTick = snapshot.NextQueryLogicTick;
            OnHitDamageOverride =
                snapshot.OnHitDamageOverride != null
                    ? (ProjectileOnHitDamage[])
                        snapshot.OnHitDamageOverride.Clone()
                    : null;
            TargetUnitUid = snapshot.TargetUnitUid;
            hitRecords.Clear();
            if (snapshot.HitRecords != null)
                hitRecords.AddRange(snapshot.HitRecords);
            ValidateRestoredState();
        }

        private bool ShouldAcceptResolvedHit(
            UnitUid targetUid,
            int logicTick)
        {
            return IsActive &&
                (!EndRequested ||
                 !Def.HitPolicy.StopResolvingAfterEndRequested) &&
                CanHitTarget(targetUid, logicTick) &&
                (Def.HitPolicy.MaxTotalHitCount == 0 ||
                 TotalHitCount < Def.HitPolicy.MaxTotalHitCount);
        }

        private int FindHitRecord(UnitUid targetUid)
        {
            for (int i = 0; i < hitRecords.Count; i++)
                if (hitRecords[i].TargetUid == targetUid)
                    return i;
            return -1;
        }

        private void ValidateRestoredState()
        {
            if (RemainingLifetimeTicks < 0 ||
                TotalHitCount < 0 ||
                RemainingPierceCount < 0 ||
                RemainingBounceCount < 0)
                throw new DeterministicSimulationException(
                    "Projectile snapshot contains negative runtime state.");
            if (EndRequested &&
                EndReason == ProjectileEndReason.None)
                throw new DeterministicSimulationException(
                    "Ended projectile snapshot has no end reason.");
            for (int i = 0; i < hitRecords.Count; i++)
            {
                ProjectileHitRecord record = hitRecords[i];
                if (!record.TargetUid.IsValid() ||
                    record.HitCount < 1 ||
                    (i > 0 &&
                     hitRecords[i - 1].TargetUid.CompareTo(
                         record.TargetUid) >= 0))
                    throw new DeterministicSimulationException(
                        "Projectile hit memory is invalid or not UID-sorted.");
            }
        }
    }
}
