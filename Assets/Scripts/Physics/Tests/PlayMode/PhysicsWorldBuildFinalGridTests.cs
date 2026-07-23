using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.PlayModeTests
{
    public sealed class PhysicsWorldBuildFinalGridTests
    {
        private GameObject ownerGo;
        private readonly List<GameObject> entityGos = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            ownerGo = new GameObject("BuildFinalGridOwner");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in entityGos)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            entityGos.Clear();

            if (ownerGo != null)
            {
                Object.DestroyImmediate(ownerGo);
            }
        }

        private PhysicsEntity2D CreateUnitEntity(int tick, int prefabId, byte seq, fp x, fp y)
        {
            var go = new GameObject($"Unit_{tick}_{prefabId}_{seq}");
            entityGos.Add(go);
            var entity = go.AddComponent<PhysicsEntity2D>();

            var transform = new PhysicsTransform2D(new fp2(x, y), new fp2(x, y), default, default);
            var shape = PhysicsShape2D.CreateCircle(default, (fp)1m);
            entity.RestoreLogicSpatialState(transform, shape);

            entity.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(tick, prefabId, seq),
                PhysicsEntityKind.Unit,
                0,
                ownerGo));

            return entity;
        }

        [Test]
        public void BuildUnitFinalGrid_AllRegisteredUnits_Inserted()
        {
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            var e2 = CreateUnitEntity(100, 2, 0, (fp)15m, (fp)15m);

            world.RegisterUnit(e1);
            world.RegisterUnit(e2);

            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void BuildUnitFinalGrid_NullOwner_Skipped()
        {
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var go = new GameObject("NoOwnerEntity");
            entityGos.Add(go);
            var entity = go.AddComponent<PhysicsEntity2D>();

            var transform = new PhysicsTransform2D(new fp2(5, 5), new fp2(5, 5), default, default);
            var shape = PhysicsShape2D.CreateCircle(default, (fp)1m);
            entity.RestoreLogicSpatialState(transform, shape);
            // QueryInfo not set — Owner is null

            world.RegisterUnit(entity);
            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void BuildUnitFinalGrid_ClearsPreviousGrid()
        {
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            world.RegisterUnit(e1);
            world.BuildUnitFinalGrid();

            world.UnregisterUnit(e1);
            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void BuildUnitFinalGrid_Deterministic_SameRegistration_SameGridState()
        {
            var world1 = new PhysicsWorld { Settings = { GridCellSize = (fp)10m } };
            var world2 = new PhysicsWorld { Settings = { GridCellSize = (fp)10m } };

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            var e2 = CreateUnitEntity(200, 2, 0, (fp)15m, (fp)15m);

            world1.RegisterUnit(e1);
            world1.RegisterUnit(e2);
            world1.BuildUnitFinalGrid();

            world2.RegisterUnit(e1);
            world2.RegisterUnit(e2);
            world2.BuildUnitFinalGrid();

            var results1 = new List<PhysicsEntity2D>();
            var results2 = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));

            world1.UnitFinalGrid.CollectCandidates(queryBounds, results1);
            world2.UnitFinalGrid.CollectCandidates(queryBounds, results2);

            Assert.AreEqual(results1.Count, results2.Count);
            for (int i = 0; i < results1.Count; i++)
            {
                Assert.AreEqual(
                    results1[i].QueryInfo.UidSnapshot,
                    results2[i].QueryInfo.UidSnapshot);
            }
        }

        [Test]
        public void BuildUnitFinalGrid_DoesNotFilterByBusinessState()
        {
            // Per section 7.3: BuildUnitFinalGrid inserts ALL registered units
            // with valid spatial state, regardless of business state.
            // Since Physics cannot reference Unit/LifeState, this test verifies
            // that all entities with non-null Owner are inserted.
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            var e2 = CreateUnitEntity(200, 2, 0, (fp)15m, (fp)15m);
            var e3 = CreateUnitEntity(300, 3, 0, (fp)25m, (fp)25m);

            world.RegisterUnit(e1);
            world.RegisterUnit(e2);
            world.RegisterUnit(e3);

            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(30, 30));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(3, results.Count);
        }
    }
}