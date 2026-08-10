using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Pool;

namespace FrameSyncMoba.Unit
{
    internal sealed class PendingSpawnEntry
    {
        public ProjectileUid Uid;
        public ProjectileDef Def;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public SourceDescriptor Source;
        public fp2 Position;
        public fp2 Direction;
        public ProjectileOnHitDamage[] OnHitDamageOverride;
        public int MaxLifetimeTicksOverride;
        public UnitUid TargetUnitUid;
    }

    public sealed class ProjectileWorld :
        IRollback<ProjectileWorldSnapshot>
    {
        private readonly Dictionary<ProjectileUid, ProjectileRuntime> lookup =
            new Dictionary<ProjectileUid, ProjectileRuntime>();
        private readonly List<ProjectileRuntime> ordered =
            new List<ProjectileRuntime>();
        private readonly List<PendingSpawnEntry> pendingSpawns =
            new List<PendingSpawnEntry>();
        private readonly Dictionary<ProjectileUid, PendingSpawnEntry> pendingByUid =
            new Dictionary<ProjectileUid, PendingSpawnEntry>();
        private readonly Dictionary<int, ObjectPool<PhysicsEntity2D>> entityPools =
            new Dictionary<int, ObjectPool<PhysicsEntity2D>>();
        private byte nextSequenceInTick;
        private int currentSequenceTick = -1;
        private bool spawnSequenceExhausted;

        public ProjectileDefRegistry DefRegistry { get; set; }
        public UnitWorld UnitWorld { get; set; }
        public PhysicsWorld PhysicsWorld { get; set; }
        public GlobalPrefabTable PrefabTable { get; set; }
        /// <summary>
        /// Logic seconds advanced per Tick (1 / TickRate). Applied to
        /// projectile Speed so Speed is authored in logic units per second.
        /// Defaults to 1 for legacy callers that treat Speed as per-Tick.
        /// </summary>
        public fp LogicSecondsPerTick { get; set; } = fp.one;
        public int Count => ordered.Count;
        public int PendingCount => pendingSpawns.Count;

        public ProjectileUid RequestSpawn(
            in ProjectileSpawnRequest request)
        {
            ProjectileDef def =
                DefRegistry?.FindById(request.ProjectileDefId);
            if (def == null ||
                !request.OwnerUnitUid.IsValid() ||
                !request.Source.IsValid ||
                request.Source.OwnerUnitUid !=
                    request.OwnerUnitUid ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    request.Direction,
                    out fp2 direction,
                    out _))
            {
                return ProjectileUid.Invalid;
            }

            int currentTick =
                SimulationTickContext.Current.Tick;
            if (currentSequenceTick != currentTick)
            {
                currentSequenceTick = currentTick;
                nextSequenceInTick = 0;
                spawnSequenceExhausted = false;
            }

            if (spawnSequenceExhausted)
                throw new DeterministicSimulationException(
                    "Projectile spawn sequence exhausted.");

            byte sequence = nextSequenceInTick;
            if (nextSequenceInTick == byte.MaxValue)
                spawnSequenceExhausted = true;
            else
                nextSequenceInTick++;

            var uid = new ProjectileUid(
                currentTick,
                def.RuntimeEntityPrefabId,
                sequence);
                var pending = new PendingSpawnEntry
                {
                    Uid = uid,
                    Def = def,
                    OwnerUnitUid = request.OwnerUnitUid,
                    TeamSnapshot = request.TeamSnapshot,
                    Source = request.Source,
                    Position = request.StartPosition,
                    Direction = direction,
                    TargetUnitUid =
                        request.TargetUnitUid,
                    OnHitDamageOverride =
                        request.OnHitDamageOverride != null
                            ? (ProjectileOnHitDamage[])
                                request.OnHitDamageOverride.Clone()
                            : null,
                    MaxLifetimeTicksOverride =
                        request.MaxLifetimeTicksOverride,
                };
            pendingSpawns.Add(pending);
            pendingByUid.Add(uid, pending);
            return uid;
        }

        public void CommitSpawns()
        {
            pendingSpawns.Sort((a, b) =>
                a.Uid.CompareTo(b.Uid));
            for (int i = 0; i < pendingSpawns.Count; i++)
            {
                PendingSpawnEntry pending =
                    pendingSpawns[i];
                if (pending.Def == null)
                    throw new DeterministicSimulationException(
                        $"Pending projectile {pending.Uid} has no definition.");
                if (lookup.ContainsKey(pending.Uid))
                    throw new DeterministicSimulationException(
                        $"Duplicate active ProjectileUid {pending.Uid}.");

                PhysicsEntity2D entity =
                    AcquireEntity(pending.Def);
                var runtime = new ProjectileRuntime(
                    pending.Uid,
                    pending.Def,
                    pending.OwnerUnitUid,
                    pending.TeamSnapshot,
                    pending.Source,
                    entity,
                    pending.Position,
                    pending.Direction,
                    pending.OnHitDamageOverride,
                    pending.MaxLifetimeTicksOverride,
                    pending.TargetUnitUid)
                {
                    LogicSecondsPerTick =
                        LogicSecondsPerTick,
                    UnitWorld = UnitWorld,
                };
                BindEntity(runtime);
                lookup.Add(pending.Uid, runtime);
                ordered.Add(runtime);
            }

            ordered.Sort((a, b) =>
                a.Uid.CompareTo(b.Uid));
            pendingSpawns.Clear();
            pendingByUid.Clear();
        }

        public void AdvanceMotion()
        {
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].AdvanceMotion();
        }

        public void UpdateLifecycle()
        {
            for (int i = 0; i < ordered.Count; i++)
                ordered[i].UpdateLifecycle();
        }

        public void FlushDestroy()
        {
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                ProjectileRuntime runtime = ordered[i];
                if (!runtime.EndRequested &&
                    runtime.IsActive)
                    continue;

                runtime.Deactivate();
                ReleaseEntity(runtime);
                lookup.Remove(runtime.Uid);
                ordered.RemoveAt(i);
            }
        }

        [Obsolete(
            "Use CommitSpawns/AdvanceMotion/UpdateLifecycle/ResolveHits/" +
            "EmitEffects/FlushDestroy phases.")]
        public void TickAll()
        {
            AdvanceMotion();
            UpdateLifecycle();
            FlushDestroy();
        }

        public bool TryGet(
            ProjectileUid uid,
            out ProjectileRuntime runtime) =>
            lookup.TryGetValue(uid, out runtime);

        public IReadOnlyList<ProjectileRuntime>
            GetAllOrdered() => ordered;

        public void Clear()
        {
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                ordered[i].Deactivate();
                ReleaseEntity(ordered[i]);
            }
            lookup.Clear();
            ordered.Clear();
            pendingSpawns.Clear();
            pendingByUid.Clear();
            currentSequenceTick = -1;
            nextSequenceInTick = 0;
            spawnSequenceExhausted = false;
        }

        public void Dispose()
        {
            Clear();
            foreach (ObjectPool<PhysicsEntity2D> pool
                     in entityPools.Values)
            {
                pool.Clear();
            }
            entityPools.Clear();
        }

        public void Capture(
            ref ProjectileWorldSnapshot state)
        {
            var active =
                new ProjectileRuntimeSnapshot[ordered.Count];
            for (int i = 0; i < ordered.Count; i++)
            {
                ProjectileRuntime runtime = ordered[i];
                // A hit-memory target may have been disposed (death despawn)
                // while the projectile is still alive. Such records are
                // pruned here so every restored snapshot reference resolves:
                // the unit no longer exists and can never be hit again, so
                // keeping the memory would make ValidateUnitReferences throw
                // on every rollback through this tick.
                int validRecordCount = 0;
                for (int j = 0;
                     j < runtime.HitRecords.Count;
                     j++)
                {
                    if (UnitWorld.TryGetUnit(
                            runtime.HitRecords[j].TargetUid,
                            out _))
                    {
                        validRecordCount++;
                    }
                }
                var records =
                    new ProjectileHitRecord[
                        validRecordCount];
                int recordIndex = 0;
                for (int j = 0;
                     j < runtime.HitRecords.Count;
                     j++)
                {
                    ProjectileHitRecord record =
                        runtime.HitRecords[j];
                    if (!UnitWorld.TryGetUnit(
                            record.TargetUid,
                            out _))
                    {
                        continue;
                    }
                    records[recordIndex++] = record;
                }

                active[i] = new ProjectileRuntimeSnapshot
                {
                    Uid = runtime.Uid,
                    DefId = runtime.Def.DefId,
                    OwnerUnitUid = runtime.OwnerUnitUid,
                    TeamSnapshot = runtime.TeamSnapshot,
                    Source = runtime.Source,
                    PreviousPosition = runtime.PrevPosition,
                    Position = runtime.Position,
                    Velocity = runtime.Velocity,
                    RemainingLifetimeTicks =
                        runtime.RemainingLifetimeTicks,
                    IsActive = runtime.IsActive,
                    EndRequested = runtime.EndRequested,
                    EndReason = runtime.EndReason,
                    TotalHitCount = runtime.TotalHitCount,
                    RemainingPierceCount =
                        runtime.RemainingPierceCount,
                    RemainingBounceCount =
                        runtime.RemainingBounceCount,
                    NextQueryLogicTick =
                        runtime.NextQueryLogicTick,
                    HitRecords = records,
                    OnHitDamageOverride =
                        runtime.OnHitDamageOverride != null
                            ? (ProjectileOnHitDamage[])
                                runtime.OnHitDamageOverride.Clone()
                            : null,
                    TargetUnitUid =
                        runtime.TargetUnitUid,
                };
            }
            state.ActiveProjectiles = active;

            var pending =
                new PendingSpawnRecordSnapshot[
                    pendingSpawns.Count];
            for (int i = 0; i < pendingSpawns.Count; i++)
            {
                PendingSpawnEntry entry =
                    pendingSpawns[i];
                pending[i] =
                    new PendingSpawnRecordSnapshot
                    {
                        Uid = entry.Uid,
                        DefId = entry.Def.DefId,
                        OwnerUnitUid =
                            entry.OwnerUnitUid,
                        TeamSnapshot =
                            entry.TeamSnapshot,
                        Source = entry.Source,
                        StartPosition = entry.Position,
                        Direction = entry.Direction,
                        OnHitDamageOverride =
                            entry.OnHitDamageOverride != null
                                ? (ProjectileOnHitDamage[])
                                    entry.OnHitDamageOverride.Clone()
                                : null,
                        MaxLifetimeTicksOverride =
                            entry.MaxLifetimeTicksOverride,
                        TargetUnitUid =
                            entry.TargetUnitUid,
                    };
            }
            state.PendingSpawns = pending;
        }

        public void Restore(
            in ProjectileWorldSnapshot state)
        {
            Clear();
            if (state.PendingSpawns != null)
            {
                for (int i = 0;
                     i < state.PendingSpawns.Length;
                     i++)
                {
                    PendingSpawnRecordSnapshot snapshot =
                        state.PendingSpawns[i];
                    ProjectileDef def =
                        GetRequiredDef(snapshot.DefId);
                    ValidateSnapshotSource(
                        snapshot.OwnerUnitUid,
                        snapshot.Source);
                    var pending = new PendingSpawnEntry
                    {
                        Uid = snapshot.Uid,
                        Def = def,
                        OwnerUnitUid =
                            snapshot.OwnerUnitUid,
                        TeamSnapshot =
                            snapshot.TeamSnapshot,
                        Source = snapshot.Source,
                        Position = snapshot.StartPosition,
                        Direction = snapshot.Direction,
                        OnHitDamageOverride =
                            snapshot.OnHitDamageOverride != null
                                ? (ProjectileOnHitDamage[])
                                    snapshot.OnHitDamageOverride.Clone()
                                : null,
                        MaxLifetimeTicksOverride =
                            snapshot.MaxLifetimeTicksOverride,
                    };
                    if (pendingByUid.ContainsKey(
                            snapshot.Uid))
                        throw new DeterministicSimulationException(
                            $"Duplicate pending ProjectileUid {snapshot.Uid} in snapshot.");
                    pendingSpawns.Add(pending);
                    pendingByUid.Add(
                        snapshot.Uid,
                        pending);
                }
            }

            if (state.ActiveProjectiles != null)
            {
                for (int i = 0;
                     i < state.ActiveProjectiles.Length;
                     i++)
                {
                    ProjectileRuntimeSnapshot snapshot =
                        state.ActiveProjectiles[i];
                    ProjectileDef def =
                        GetRequiredDef(snapshot.DefId);
                    ValidateSnapshotSource(
                        snapshot.OwnerUnitUid,
                        snapshot.Source);
                    PhysicsEntity2D entity =
                        AcquireEntity(def);
                    fp2 restoreFacing =
                        snapshot.Velocity.x != fp.zero ||
                        snapshot.Velocity.y != fp.zero
                            ? snapshot.Velocity
                            : new fp2(fp.one, fp.zero);
                    var runtime = new ProjectileRuntime(
                        snapshot.Uid,
                        def,
                        snapshot.OwnerUnitUid,
                        snapshot.TeamSnapshot,
                        snapshot.Source,
                        entity,
                        snapshot.Position,
                        restoreFacing,
                        snapshot.OnHitDamageOverride,
                        0,
                        snapshot.TargetUnitUid)
                    {
                        UnitWorld = UnitWorld,
                        // Restored projectiles must keep the world's logic
                        // seconds per tick; the runtime ctor defaults to 1,
                        // which would make a restored projectile fly at
                        // Speed units per Tick (TickRate x too fast) and hit
                        // at the wrong tick after every rollback.
                        LogicSecondsPerTick =
                            LogicSecondsPerTick,
                    };
                    runtime.RestoreFromSnapshot(snapshot);
                    BindEntity(runtime);
                    if (lookup.ContainsKey(snapshot.Uid))
                        throw new DeterministicSimulationException(
                            $"Duplicate active ProjectileUid {snapshot.Uid} in snapshot.");
                    lookup.Add(snapshot.Uid, runtime);
                    ordered.Add(runtime);
                }
            }

            ValidateStableOrder(
                pendingSpawns,
                entry => entry.Uid,
                "pending");
            ValidateStableOrder(
                ordered,
                runtime => runtime.Uid,
                "active");
        }

        public void Resolve(
            in RollbackContext context)
        {
            if (UnitWorld == null)
                throw new DeterministicSimulationException(
                    "ProjectileWorld has no UnitWorld during Resolve.");

            for (int i = 0; i < pendingSpawns.Count; i++)
                ValidateUnitReferences(
                    pendingSpawns[i].OwnerUnitUid,
                    null);
            for (int i = 0; i < ordered.Count; i++)
                ValidateUnitReferences(
                    ordered[i].OwnerUnitUid,
                    ordered[i].HitRecords);
        }

        public void Resolve(UnitWorld unitWorld)
        {
            UnitWorld = unitWorld ??
                throw new ArgumentNullException(
                    nameof(unitWorld));
            RollbackContext context = default;
            Resolve(in context);
        }

        public void Rebuild(
            in RollbackContext context)
        {
            pendingByUid.Clear();
            for (int i = 0; i < pendingSpawns.Count; i++)
                pendingByUid.Add(
                    pendingSpawns[i].Uid,
                    pendingSpawns[i]);

            lookup.Clear();
            for (int i = 0; i < ordered.Count; i++)
                lookup.Add(ordered[i].Uid, ordered[i]);

            currentSequenceTick = -1;
            nextSequenceInTick = 0;
            spawnSequenceExhausted = false;
        }

        private ProjectileDef GetRequiredDef(int defId)
        {
            ProjectileDef def =
                DefRegistry?.FindById(defId);
            if (def == null)
                throw new DeterministicSimulationException(
                    $"Projectile snapshot references missing definition {defId}.");
            return def;
        }

        private PhysicsEntity2D AcquireEntity(
            ProjectileDef def)
        {
            if (PhysicsWorld == null)
                throw new DeterministicSimulationException(
                    "ProjectileWorld has no PhysicsWorld.");
            if (PrefabTable == null)
                throw new DeterministicSimulationException(
                    "ProjectileWorld has no GlobalPrefabTable.");

            if (!entityPools.TryGetValue(
                    def.RuntimeEntityPrefabId,
                    out ObjectPool<PhysicsEntity2D> pool))
            {
                GameObject prefab =
                    PrefabTable.GetRequiredPrefab(
                        PrefabKind.Projectile,
                        def.RuntimeEntityPrefabId);
                PhysicsEntity2D template =
                    prefab.GetComponent<PhysicsEntity2D>();
                if (template == null)
                    throw new InvalidOperationException(
                        $"Projectile prefab {def.RuntimeEntityPrefabId} has no PhysicsEntity2D.");

                pool = new ObjectPool<PhysicsEntity2D>(
                    () =>
                    {
                        GameObject instance =
                            UnityEngine.Object.Instantiate(prefab);
                        PhysicsEntity2D entity =
                            instance.GetComponent<PhysicsEntity2D>();
                        if (entity == null)
                        {
                            UnityEngine.Object.Destroy(instance);
                            throw new InvalidOperationException(
                                $"Projectile prefab {def.RuntimeEntityPrefabId} instantiated without PhysicsEntity2D.");
                        }
                        return entity;
                    },
                    entity => entity.gameObject.SetActive(true),
                    entity =>
                    {
                        entity.SetQueryInfo(default);
                        entity.gameObject.SetActive(false);
                    },
                    entity =>
                    {
                        if (UnityEngine.Application.isPlaying)
                            UnityEngine.Object.Destroy(
                                entity.gameObject);
                        else
                            UnityEngine.Object.DestroyImmediate(
                                entity.gameObject);
                    },
                    true,
                    4,
                    256);
                entityPools.Add(
                    def.RuntimeEntityPrefabId,
                    pool);
            }

            return pool.Get();
        }

        private void BindEntity(
            ProjectileRuntime runtime)
        {
            runtime.PhysicsEntity.SetQueryInfo(
                new PhysicsEntityQueryInfo(
                    new RuntimeUidQueryValue(
                        runtime.Uid.SpawnLogicTick,
                        runtime.Uid.RuntimeEntityPrefabId,
                        runtime.Uid.SpawnSequenceInTick),
                    PhysicsEntityKind.Projectile,
                    runtime.TeamSnapshot.Value,
                    runtime));
            PhysicsWorld.RegisterProjectile(
                runtime.PhysicsEntity);
        }

        private void ReleaseEntity(
            ProjectileRuntime runtime)
        {
            PhysicsWorld.UnregisterProjectile(
                runtime.PhysicsEntity);
            if (!entityPools.TryGetValue(
                    runtime.Def.RuntimeEntityPrefabId,
                    out ObjectPool<PhysicsEntity2D> pool))
                throw new DeterministicSimulationException(
                    $"Projectile entity pool {runtime.Def.RuntimeEntityPrefabId} is missing.");
            pool.Release(runtime.PhysicsEntity);
        }

        private void ValidateUnitReferences(
            UnitUid ownerUid,
            IReadOnlyList<ProjectileHitRecord> records)
        {
            if (!UnitWorld.TryGetUnit(ownerUid, out _))
                throw new DeterministicSimulationException(
                    $"Projectile snapshot references missing owner {ownerUid}.");
            if (records == null) return;
            for (int i = 0; i < records.Count; i++)
                if (!UnitWorld.TryGetUnit(
                        records[i].TargetUid,
                        out _))
                    throw new DeterministicSimulationException(
                        $"Projectile hit memory references missing UnitUid {records[i].TargetUid}.");
        }

        private static void ValidateSnapshotSource(
            UnitUid ownerUid,
            in SourceDescriptor source)
        {
            if (!source.IsValid ||
                source.OwnerUnitUid != ownerUid)
                throw new DeterministicSimulationException(
                    "Projectile snapshot source descriptor is invalid.");
        }

        private static void ValidateStableOrder<T>(
            IReadOnlyList<T> values,
            Func<T, ProjectileUid> uid,
            string label)
        {
            for (int i = 1; i < values.Count; i++)
                if (uid(values[i - 1]).CompareTo(
                        uid(values[i])) >= 0)
                    throw new DeterministicSimulationException(
                        $"Projectile {label} snapshot is not strictly UID-sorted.");
        }
    }
}
