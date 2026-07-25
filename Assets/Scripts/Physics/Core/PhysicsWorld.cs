using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Deterministic physics world that owns entity registration and spatial
    /// query infrastructure (Physics v13.1 sections 1.6, 3 and 7).
    /// </summary>
    public sealed class PhysicsWorld
    {
        private readonly List<PhysicsEntity2D> unitEntities = new List<PhysicsEntity2D>();
        private readonly List<PhysicsEntity2D> projectileEntities = new List<PhysicsEntity2D>();
        private readonly ReadOnlyCollection<PhysicsEntity2D> readOnlyUnitEntities;
        private readonly ReadOnlyCollection<PhysicsEntity2D> readOnlyProjectileEntities;

        public PhysicsWorld()
        {
            readOnlyUnitEntities = unitEntities.AsReadOnly();
            readOnlyProjectileEntities = projectileEntities.AsReadOnly();
            Settings = new PhysicsWorldSettings();
            UnitCollisionEvents = new UnitCollisionEventBuffer();
        }

        /// <summary>
        /// Spatial grid configuration (Physics v13.1 section 7.1).
        /// Set before calling <see cref="BuildUnitFinalGrid"/>.
        /// </summary>
        public PhysicsWorldSettings Settings { get; set; }

        /// <summary>
        /// Unit final grid built after movement and wall correction
        /// (Physics v13.1 section 7.2). Derived data — not in snapshot (section 7.6).
        /// </summary>
        public PhysicsSpatialGrid2D UnitFinalGrid { get; private set; }

        public UnitCollisionEventBuffer UnitCollisionEvents { get; }

        /// <summary>
        /// Registers a Unit-kind physics entity (Physics v13.1 section 3.1/3.3).
        /// The entity must have QueryInfo.Kind == Unit and a valid Owner.
        /// </summary>
        public void RegisterUnit(PhysicsEntity2D entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureNotRegistered(entity, unitEntities, "Unit");
            EnsureNotRegistered(entity, projectileEntities, "Unit");

            unitEntities.Add(entity);
        }

        /// <summary>
        /// Registers a Projectile-kind physics entity (Physics v13.1 section 3.1/3.4).
        /// The entity must have QueryInfo.Kind == Projectile and a valid Owner.
        /// </summary>
        public void RegisterProjectile(PhysicsEntity2D entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            EnsureNotRegistered(entity, unitEntities, "Projectile");
            EnsureNotRegistered(entity, projectileEntities, "Projectile");

            projectileEntities.Add(entity);
        }

        /// <summary>
        /// Unregisters an entity from whichever list it belongs to
        /// (Physics v13.1 section 3.1/3.5).
        /// </summary>
        public void Unregister(PhysicsEntity2D entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (unitEntities.Remove(entity))
            {
                return;
            }

            if (projectileEntities.Remove(entity))
            {
                return;
            }

            throw new InvalidOperationException(
                "The specified PhysicsEntity2D is not registered in this PhysicsWorld.");
        }

        /// <summary>
        /// Unregisters a Unit-kind entity (Physics v13.1 section 3.5).
        /// </summary>
        public void UnregisterUnit(PhysicsEntity2D entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!unitEntities.Remove(entity))
            {
                throw new InvalidOperationException(
                    "The specified PhysicsEntity2D is not registered as a Unit entity.");
            }
        }

        /// <summary>
        /// Unregisters a Projectile-kind entity (Physics v13.1 section 3.5).
        /// </summary>
        public void UnregisterProjectile(PhysicsEntity2D entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!projectileEntities.Remove(entity))
            {
                throw new InvalidOperationException(
                    "The specified PhysicsEntity2D is not registered as a Projectile entity.");
            }
        }

        /// <summary>
        /// All registered Unit entities in insertion order (Physics v13.1 section 3.1).
        /// Read-only; do not modify.
        /// </summary>
        public IReadOnlyList<PhysicsEntity2D> UnitEntities => readOnlyUnitEntities;

        /// <summary>
        /// All registered Projectile entities in insertion order (Physics v13.1 section 3.1).
        /// Read-only; do not modify.
        /// </summary>
        public IReadOnlyList<PhysicsEntity2D> ProjectileEntities => readOnlyProjectileEntities;

        /// <summary>
        /// Rebuilds the UnitFinalGrid from all registered Unit entities
        /// (Physics v13.1 section 7.3). Per design, this does NOT filter by
        /// LifeState, Capability.IsTargetable, UnitKind, or any business state —
        /// every registered unit with a valid Owner is inserted.
        /// </summary>
        public void BuildUnitFinalGrid()
        {
            if (UnitFinalGrid == null)
            {
                UnitFinalGrid = new PhysicsSpatialGrid2D(Settings.GridCellSize);
            }
            else
            {
                UnitFinalGrid.Clear();
            }

            for (int i = 0; i < unitEntities.Count; i++)
            {
                PhysicsEntity2D entity = unitEntities[i];

                if (entity.QueryInfo.Owner == null)
                {
                    continue;
                }

                UnitFinalGrid.Insert(entity, entity.Bounds);
            }
        }

        /// <summary>
        /// RVO pre-move spatial grid (Pathfinding Design v13.1 section 10.1-10.2).
        /// Built from unit positions BEFORE movement is applied.
        /// Independent from UnitFinalGrid — must not share the same instance.
        /// </summary>
        public PhysicsSpatialGrid2D RvoGrid { get; private set; }

        /// <summary>
        /// Build the RVO grid from current (pre-move) unit positions.
        /// Called after locomotion evaluation, before movement application.
        /// </summary>
        public void BuildRvoGrid()
        {
            if (RvoGrid == null)
            {
                RvoGrid = new PhysicsSpatialGrid2D(Settings.GridCellSize);
            }
            else
            {
                RvoGrid.Clear();
            }

            for (int i = 0; i < unitEntities.Count; i++)
            {
                PhysicsEntity2D entity = unitEntities[i];
                if (entity.QueryInfo.Owner == null) continue;
                RvoGrid.Insert(entity, entity.Bounds);
            }
        }

        public void DetectUnitCollisionEvents() =>
            UnitCollisionEvents.DetectAndPublish(readOnlyUnitEntities);

        public void Capture(ref PhysicsRuntimeSnapshot state) =>
            UnitCollisionEvents.Capture(ref state.CollisionBuffer);

        public void Restore(in PhysicsRuntimeSnapshot state) =>
            UnitCollisionEvents.Restore(state.CollisionBuffer);

        public void Resolve()
        {
            // PreviousPairs contains stable UID values only.
        }

        public void Rebuild()
        {
            BuildUnitFinalGrid();
            UnitCollisionEvents.ApplyPendingRestore();
        }

        private static void EnsureNotRegistered(
            PhysicsEntity2D entity,
            List<PhysicsEntity2D> list,
            string targetList)
        {
            if (list.Contains(entity))
            {
                throw new InvalidOperationException(
                    $"The specified PhysicsEntity2D is already registered in the {targetList} list.");
            }
        }
    }
}
