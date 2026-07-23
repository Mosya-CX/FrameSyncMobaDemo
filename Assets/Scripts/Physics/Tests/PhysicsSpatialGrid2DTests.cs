using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.Tests
{
    [TestFixture]
    public class PhysicsSpatialGrid2DTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in gameObjects)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }
            gameObjects.Clear();
        }

        private PhysicsEntity2D CreateEntity(int tick, int prefabId, byte seq, fp x, fp y, fp radius)
        {
            var go = new GameObject($"Entity_{tick}_{prefabId}_{seq}");
            gameObjects.Add(go);
            var entity = go.AddComponent<PhysicsEntity2D>();

            var transform = new PhysicsTransform2D(new fp2(x, y), new fp2(x, y), default, default);
            var shape = PhysicsShape2D.CreateCircle(default, radius);
            entity.RestoreLogicSpatialState(transform, shape);

            entity.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(tick, prefabId, seq),
                PhysicsEntityKind.Unit,
                0,
                go));

            return entity;
        }

        [Test]
        public void Insert_SingleEntity_CollectReturnsIt()
        {
            var grid = new PhysicsSpatialGrid2D((fp)10m);
            var entity = CreateEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m);

            grid.Insert(entity, entity.Bounds);

            var results = new List<PhysicsEntity2D>();
            grid.CollectCandidates(entity.Bounds, results);

            Assert.AreEqual(1, results.Count);
            Assert.AreSame(entity, results[0]);
        }

        [Test]
        public void Insert_MultipleEntities_CollectReturnsAll()
        {
            var grid = new PhysicsSpatialGrid2D((fp)10m);
            var e1 = CreateEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m);
            var e2 = CreateEntity(100, 1, 1, (fp)6m, (fp)6m, (fp)1m);

            grid.Insert(e1, e1.Bounds);
            grid.Insert(e2, e2.Bounds);

            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(10, 10));
            var results = new List<PhysicsEntity2D>();
            grid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(2, results.Count);
        }

        [Test]
        public void Collect_CrossCellEntity_AppearsOnce()
        {
            var grid = new PhysicsSpatialGrid2D((fp)10m);
            // Entity at cell boundary (0,0)-(1,1) spanning 4 cells
            var entity = CreateEntity(100, 1, 0, (fp)10m, (fp)10m, (fp)5m);

            grid.Insert(entity, entity.Bounds);

            var queryBounds = new PhysicsBounds2D(new fp2(5, 5), new fp2(15, 15));
            var results = new List<PhysicsEntity2D>();
            grid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(1, results.Count);
        }

        [Test]
        public void Collect_OutputSortedByUidSnapshot()
        {
            var grid = new PhysicsSpatialGrid2D((fp)100m);
            // Insert in reverse UID order
            var e3 = CreateEntity(300, 1, 0, (fp)5m, (fp)5m, (fp)1m);
            var e1 = CreateEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m);
            var e2 = CreateEntity(200, 1, 0, (fp)5m, (fp)5m, (fp)1m);

            grid.Insert(e3, e3.Bounds);
            grid.Insert(e1, e1.Bounds);
            grid.Insert(e2, e2.Bounds);

            var results = new List<PhysicsEntity2D>();
            grid.CollectCandidates(new PhysicsBounds2D(new fp2(0, 0), new fp2(100, 100)), results);

            Assert.AreEqual(3, results.Count);
            Assert.AreSame(e1, results[0]);
            Assert.AreSame(e2, results[1]);
            Assert.AreSame(e3, results[2]);
        }

        [Test]
        public void Collect_Deterministic_DifferentInsertOrder_SameOutput()
        {
            var results1 = new List<PhysicsEntity2D>();
            var results2 = new List<PhysicsEntity2D>();

            {
                var grid = new PhysicsSpatialGrid2D((fp)100m);
                var a = CreateEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m);
                var b = CreateEntity(100, 2, 0, (fp)6m, (fp)6m, (fp)1m);
                var c = CreateEntity(100, 3, 0, (fp)7m, (fp)7m, (fp)1m);

                grid.Insert(c, c.Bounds);
                grid.Insert(a, a.Bounds);
                grid.Insert(b, b.Bounds);

                grid.CollectCandidates(new PhysicsBounds2D(new fp2(0, 0), new fp2(100, 100)), results1);
            }

            {
                var grid = new PhysicsSpatialGrid2D((fp)100m);
                var a = CreateEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m);
                var b = CreateEntity(100, 2, 0, (fp)6m, (fp)6m, (fp)1m);
                var c = CreateEntity(100, 3, 0, (fp)7m, (fp)7m, (fp)1m);

                grid.Insert(a, a.Bounds);
                grid.Insert(b, b.Bounds);
                grid.Insert(c, c.Bounds);

                grid.CollectCandidates(new PhysicsBounds2D(new fp2(0, 0), new fp2(100, 100)), results2);
            }

            Assert.AreEqual(results1.Count, results2.Count);
            for (int i = 0; i < results1.Count; i++)
            {
                Assert.AreEqual(
                    results1[i].QueryInfo.UidSnapshot,
                    results2[i].QueryInfo.UidSnapshot);
            }
        }

        [Test]
        public void Collect_NoOverlap_ReturnsEmpty()
        {
            var grid = new PhysicsSpatialGrid2D((fp)10m);
            var entity = CreateEntity(100, 1, 0, (fp)50m, (fp)50m, (fp)1m);

            grid.Insert(entity, entity.Bounds);

            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(10, 10));
            var results = new List<PhysicsEntity2D>();
            grid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Clear_RemovesAllEntities()
        {
            var grid = new PhysicsSpatialGrid2D((fp)10m);
            var entity = CreateEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m);

            grid.Insert(entity, entity.Bounds);
            grid.Clear();

            var results = new List<PhysicsEntity2D>();
            grid.CollectCandidates(entity.Bounds, results);

            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Constructor_NonPositiveCellSize_Throws()
        {
            Assert.Throws<System.ArgumentException>(() => new PhysicsSpatialGrid2D(fp.zero));
            Assert.Throws<System.ArgumentException>(() => new PhysicsSpatialGrid2D((fp)(-1m)));
        }
    }
}