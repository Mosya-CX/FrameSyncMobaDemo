using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    internal sealed class PendingSpawnEntry
    {
        public ProjectileUid Uid;
        public ProjectileDef Def;
        public UnitUid OwnerUnitUid;
        public TeamId TeamSnapshot;
        public fp2 Position;
        public fp2 Direction;
    }

    public sealed class ProjectileWorld
    {
        private readonly Dictionary<ProjectileUid, ProjectileRuntime> _lookup =
            new Dictionary<ProjectileUid, ProjectileRuntime>();
        private readonly List<ProjectileRuntime> _ordered = new List<ProjectileRuntime>();
        private readonly List<PendingSpawnEntry> _pendingSpawns = new List<PendingSpawnEntry>();
        private readonly Dictionary<ProjectileUid, PendingSpawnEntry> _pendingByUid =
            new Dictionary<ProjectileUid, PendingSpawnEntry>();
        private byte _nextSequenceInTick;
        private int _currentSequenceTick = -1;
        private bool _spawnSequenceExhausted;

        public ProjectileDefRegistry DefRegistry { get; set; }


        public int Count => _ordered.Count;

        public ProjectileUid RequestSpawn(in ProjectileSpawnRequest request)
        {
            ProjectileDef def = DefRegistry?.FindById(request.ProjectileDefId);
            if (def == null || !def.IsValid || !request.OwnerUnitUid.IsValid() ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    request.Direction, out fp2 direction, out _))
            {
                return ProjectileUid.Invalid;
            }

            int currentTick = SimulationTickContext.Current.Tick;
            if (_currentSequenceTick != currentTick)
            {
                _currentSequenceTick = currentTick;
                _nextSequenceInTick = 0;
                _spawnSequenceExhausted = false;
            }

            if (_spawnSequenceExhausted)
                throw new DeterministicSimulationException("Projectile spawn sequence exhausted.");

            byte sequence = _nextSequenceInTick;
            if (_nextSequenceInTick == byte.MaxValue) _spawnSequenceExhausted = true;
            else _nextSequenceInTick++;
            var uid = new ProjectileUid(currentTick, def.RuntimeEntityPrefabId, sequence);
            var pending = new PendingSpawnEntry
            {
                Uid = uid,
                Def = def,
                OwnerUnitUid = request.OwnerUnitUid,
                TeamSnapshot = request.TeamSnapshot,
                Position = request.StartPosition,
                Direction = direction,
            };
            _pendingSpawns.Add(pending);
            _pendingByUid.Add(uid, pending);
            return uid;
        }

        public void CommitSpawns()
        {
            _pendingSpawns.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            for (int i = 0; i < _pendingSpawns.Count; i++)
            {
                PendingSpawnEntry pending = _pendingSpawns[i];
                if (pending.Def == null)
                    throw new DeterministicSimulationException(
                        $"Pending projectile {pending.Uid} has no definition.");
                if (_lookup.ContainsKey(pending.Uid))
                    throw new DeterministicSimulationException(
                        $"Duplicate active ProjectileUid {pending.Uid}.");
                var runtime = new ProjectileRuntime(
                    pending.Uid,
                    pending.Def,
                    pending.OwnerUnitUid,
                    pending.TeamSnapshot,
                    pending.Position,
                    pending.Direction);
                _lookup.Add(pending.Uid, runtime);
                _ordered.Add(runtime);
            }
            _ordered.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            _pendingSpawns.Clear();
            _pendingByUid.Clear();
        }

        public void TickAll()
        {
            var toRemove = new List<ProjectileRuntime>();

            for (int i = 0; i < _ordered.Count; i++)
            {
                var runtime = _ordered[i];
                runtime.TickUpdate();

                if (!runtime.IsActive)
                {
                    toRemove.Add(runtime);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                var runtime = toRemove[i];
                _lookup.Remove(runtime.Uid);
                _ordered.Remove(runtime);
            }
        }

        public bool TryGet(ProjectileUid uid, out ProjectileRuntime runtime)
        {
            return _lookup.TryGetValue(uid, out runtime);
        }

        public IReadOnlyList<ProjectileRuntime> GetAllOrdered()
        {
            return _ordered;
        }

        public void Clear()
        {
            _lookup.Clear();
            _ordered.Clear();
            _pendingSpawns.Clear();
            _pendingByUid.Clear();
        }

        /// <summary>
        /// Looks up a ProjectileDef by its stable DefId.
        /// Returns null until the ProjectileDef registry is implemented.
        /// </summary>
        private ProjectileDef GetProjectileDef(int defId)
        {
            return DefRegistry?.FindById(defId);
        }

        public void Capture(ref ProjectileWorldSnapshot state)
        {
            // Pending spawns (not yet activated this Tick)
            if (state.PendingSpawns == null)
                state.PendingSpawns = new List<PendingSpawnRecordSnapshot>();
            else
                state.PendingSpawns.Clear();

            for (int i = 0; i < _pendingSpawns.Count; i++)
            {
                var ps = _pendingSpawns[i];
                state.PendingSpawns.Add(new PendingSpawnRecordSnapshot
                {
                    Uid = ps.Uid,
                    DefId = ps.Def.DefId,
                    OwnerUnitUid = ps.OwnerUnitUid,
                    TeamSnapshot = ps.TeamSnapshot,
                    StartPosition = ps.Position,
                    Direction = ps.Direction,
                });
            }

            // Active projectiles
            if (state.ActiveProjectiles == null)
                state.ActiveProjectiles = new List<ProjectileRuntimeSnapshot>();
            else
                state.ActiveProjectiles.Clear();

            for (int i = 0; i < _ordered.Count; i++)
            {
                var rt = _ordered[i];
                state.ActiveProjectiles.Add(new ProjectileRuntimeSnapshot
                {
                    Uid = rt.Uid,
                    DefId = rt.Def.DefId,
                    OwnerUnitUid = rt.OwnerUnitUid,
                    TeamSnapshot = rt.TeamSnapshot,
                    PreviousPosition = rt.PrevPosition,
                    Position = rt.Position,
                    Velocity = rt.Velocity,
                    RemainingLifetimeTicks = rt.RemainingLifetimeTicks,
                    IsActive = rt.IsActive,
                    HitCount = rt.HitCount,
                    HitTargets = new List<UnitUid>(rt.HitTargets),
                });
            }
        }

        public void Restore(in ProjectileWorldSnapshot state)
        {
            Clear();

            // Restore pending spawns
            if (state.PendingSpawns != null)
            {
                for (int i = 0; i < state.PendingSpawns.Count; i++)
                {
                    var ps = state.PendingSpawns[i];
                    ProjectileDef def = GetProjectileDef(ps.DefId);
                    if (def == null)
                        throw new DeterministicSimulationException(
                            $"Pending projectile snapshot references missing definition {ps.DefId}.");
                    var pending = new PendingSpawnEntry
                    {
                        Uid = ps.Uid,
                        OwnerUnitUid = ps.OwnerUnitUid,
                        TeamSnapshot = ps.TeamSnapshot,
                        Position = ps.StartPosition,
                        Direction = ps.Direction,
                        Def = def,
                    };
                    if (_pendingByUid.ContainsKey(ps.Uid))
                        throw new DeterministicSimulationException(
                            $"Duplicate pending ProjectileUid {ps.Uid} in snapshot.");
                    _pendingSpawns.Add(pending);
                    _pendingByUid.Add(ps.Uid, pending);
                }
            }

            // Restore active projectiles
            if (state.ActiveProjectiles != null)
            {
                for (int i = 0; i < state.ActiveProjectiles.Count; i++)
                {
                    var ps = state.ActiveProjectiles[i];
                    var def = GetProjectileDef(ps.DefId);
                    if (def == null)
                        throw new DeterministicSimulationException(
                            $"Active projectile snapshot references missing definition {ps.DefId}.");

                    var runtime = new ProjectileRuntime(
                        ps.Uid, def, ps.OwnerUnitUid, ps.TeamSnapshot, ps.Position, fp2.zero);
                    // Directly overwrite snapshot fields onto the runtime
                    runtime.RestoreFromSnapshot(ps);
                    if (_lookup.ContainsKey(ps.Uid))
                        throw new DeterministicSimulationException(
                            $"Duplicate active ProjectileUid {ps.Uid} in snapshot.");
                    _lookup.Add(ps.Uid, runtime);
                    _ordered.Add(runtime);
                }
                _ordered.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            }
        }

        public void Resolve(UnitWorld unitWorld)
        {
            if (unitWorld == null) throw new ArgumentNullException(nameof(unitWorld));
            for (int i = 0; i < _pendingSpawns.Count; i++)
                ValidateUnitReferences(unitWorld, _pendingSpawns[i].OwnerUnitUid, null);
            for (int i = 0; i < _ordered.Count; i++)
                ValidateUnitReferences(unitWorld, _ordered[i].OwnerUnitUid, _ordered[i].HitTargets);
        }

        public void Rebuild(in RollbackContext context) { }

        private static void ValidateUnitReferences(
            UnitWorld unitWorld,
            UnitUid ownerUid,
            List<UnitUid> hitTargets)
        {
            if (!unitWorld.TryGetUnit(ownerUid, out _))
                throw new DeterministicSimulationException(
                    $"Projectile snapshot references missing owner {ownerUid}.");
            if (hitTargets == null) return;
            for (int i = 0; i < hitTargets.Count; i++)
                if (!unitWorld.TryGetUnit(hitTargets[i], out _))
                    throw new DeterministicSimulationException(
                        $"Projectile hit memory references missing UnitUid {hitTargets[i]}.");
        }
    }
}
