using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FPhysics = FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class RangeQueryServiceTests
    {
        private readonly List<GameObject> gameObjects = new List<GameObject>();
        private PhysicsWorld physicsWorld;
        private RangeQueryService service;
        private StatDefinitionTable definitionTable;

        [SetUp]
        public void SetUp()
        {
            physicsWorld = new PhysicsWorld();
            physicsWorld.Settings.GridCellSize = (fp)10m;
            service = new RangeQueryService(physicsWorld);

            definitionTable = new StatDefinitionTable();
            definitionTable.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = false,
            });
            definitionTable.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "HP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = false,
            });
        }

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

        /// <summary>
        /// Helper: creates a Unit with PhysicsEntity2D, registers, and returns both.
        /// </summary>
        private (Unit unit, PhysicsEntity2D entity) CreateUnitEntity(
            int spawnTick,
            int prefabId,
            byte seq,
            fp x,
            fp y,
            fp radius,
            UnitKind kind,
            TeamId teamId,
            LifeState lifeState = LifeState.Alive,
            bool isTargetable = true)
        {
            var unit = UnitTestFactory.CreateUnit(
                new UnitUid(spawnTick, prefabId, seq),
                kind,
                0,
                teamId);
            PhysicsEntity2D entity = unit.PhysicsEntity;
            entity.TeleportLogicPosition(new fp2(x, y));
            entity.SetLogicShape(FPhysics.PhysicsShape2D.CreateCircle(default, radius));

            // Set life state and capability (bypass UnitWorld for test setup)
            if (lifeState != LifeState.Alive)
            {
                unit.ApplyLifeStateFromUnitWorld(lifeState);
            }

            if (!isTargetable)
            {
                ref CapabilityState cap = ref unit.RefCapabilityState();
                cap.DisableAllActions();
            }

            physicsWorld.RegisterUnit(entity);
            return (unit, entity);
        }

        private void BuildGrid()
        {
            physicsWorld.BuildUnitFinalGrid();
        }

        [Test]
        public void Query_EmptyGrid_ReturnsEmpty()
        {
            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Query_SingleUnitInRange_ReturnsIt()
        {
            var (unit, _) = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            BuildGrid();

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(unit, result[0]);
        }

        [Test]
        public void Query_UnitOutOfRange_ReturnsEmpty()
        {
            CreateUnitEntity(100, 1, 0, (fp)100m, (fp)100m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            BuildGrid();

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)5m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Query_EnemyOnly_FiltersCorrectly()
        {
            var team1 = new TeamId(1);
            var team2 = new TeamId(2);

            var (ally, _) = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, team1);
            var (enemy, _) = CreateUnitEntity(100, 2, 0, (fp)8m, (fp)8m, (fp)1m,
                UnitKind.Hero, team2);
            BuildGrid();

            var filter = new UnitTargetFilter
            {
                TeamRule = TeamQueryRule.EnemyOnly,
                UnitKindMask = UnitKindMask.All,
                LifeStateMask = UnitLifeStateMask.All,
            };

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = filter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, ally.UnitUid, team1, result, scratch);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(enemy, result[0]);
        }

        [Test]
        public void Query_AllyOnly_FiltersCorrectly()
        {
            var team1 = new TeamId(1);
            var team2 = new TeamId(2);

            var (ally, _) = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, team1);
            var (otherAlly, _) = CreateUnitEntity(100, 3, 0, (fp)6m, (fp)6m, (fp)1m,
                UnitKind.Hero, team1);
            var (enemy, _) = CreateUnitEntity(100, 2, 0, (fp)8m, (fp)8m, (fp)1m,
                UnitKind.Hero, team2);
            BuildGrid();

            var filter = new UnitTargetFilter
            {
                TeamRule = TeamQueryRule.AllyOnly,
                UnitKindMask = UnitKindMask.All,
                LifeStateMask = UnitLifeStateMask.All,
            };

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = filter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, ally.UnitUid, team1, result, scratch);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(otherAlly, result[0]);
        }

        [Test]
        public void Query_SelfOnly_ReturnsOnlySelf()
        {
            var team1 = new TeamId(1);
            var (self, _) = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, team1);
            CreateUnitEntity(100, 2, 0, (fp)8m, (fp)8m, (fp)1m,
                UnitKind.Hero, team1); // same team
            BuildGrid();

            var filter = new UnitTargetFilter
            {
                TeamRule = TeamQueryRule.SelfOnly,
                UnitKindMask = UnitKindMask.All,
                LifeStateMask = UnitLifeStateMask.All,
            };

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = filter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, self.UnitUid, team1, result, scratch);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(self, result[0]);
        }

        [Test]
        public void Query_LifeStateFilter_DeadUnitExcluded()
        {
            var (alive, _) = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral, LifeState.Alive);
            CreateUnitEntity(100, 2, 0, (fp)8m, (fp)8m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral, LifeState.Dead);
            BuildGrid();

            var filter = new UnitTargetFilter
            {
                TeamRule = TeamQueryRule.Any,
                UnitKindMask = UnitKindMask.All,
                LifeStateMask = UnitLifeStateMask.AliveOnly,
            };

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = filter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, alive.UnitUid, TeamId.Neutral, result, scratch);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(alive, result[0]);
        }

        [Test]
        public void Query_RequireTargetable_UntargetableExcluded()
        {
            CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral, LifeState.Alive, isTargetable: false);
            BuildGrid();

            var filter = new UnitTargetFilter
            {
                TeamRule = TeamQueryRule.Any,
                UnitKindMask = UnitKindMask.All,
                LifeStateMask = UnitLifeStateMask.All,
                RequireTargetable = true,
            };

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = filter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Query_UnitKindMask_FiltersHeroOnly()
        {
            var (hero, _) = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            CreateUnitEntity(100, 2, 0, (fp)8m, (fp)8m, (fp)1m,
                UnitKind.Minion, TeamId.Neutral);
            BuildGrid();

            var filter = new UnitTargetFilter
            {
                TeamRule = TeamQueryRule.Any,
                UnitKindMask = UnitKindMask.Hero,
                LifeStateMask = UnitLifeStateMask.All,
            };

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = filter,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, hero.UnitUid, TeamId.Neutral, result, scratch);

            Assert.AreEqual(1, result.Count);
            Assert.AreSame(hero, result[0]);
        }

        [Test]
        public void Query_SortByDistance_ClosestFirst()
        {
            var (far, _) = CreateUnitEntity(100, 1, 0, (fp)30m, (fp)30m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            var (near, _) = CreateUnitEntity(100, 2, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            BuildGrid();

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.Distance,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(2, result.Count);
            Assert.AreSame(near, result[0]);
            Assert.AreSame(far, result[1]);
        }

        [Test]
        public void Query_MaxResultTruncation()
        {
            CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            CreateUnitEntity(100, 1, 1, (fp)8m, (fp)8m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            CreateUnitEntity(100, 1, 2, (fp)12m, (fp)12m, (fp)1m,
                UnitKind.Hero, TeamId.Neutral);
            BuildGrid();

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
                MaxResult = 2,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Query_NoGrid_ReturnsEmpty()
        {
            // Build no grid
            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Query_Deterministic_SameInputSameResult()
        {
            // World 1
            var w1 = new PhysicsWorld();
            w1.Settings.GridCellSize = (fp)10m;
            var s1 = new RangeQueryService(w1);

            var go1 = new GameObject("Det1");
            gameObjects.Add(go1);
            var e1 = go1.AddComponent<PhysicsEntity2D>();
            e1.RestoreLogicSpatialState(
                new PhysicsTransform2D(new fp2(5, 5), new fp2(5, 5), default, default),
                FPhysics.PhysicsShape2D.CreateCircle(default, (fp)1m));
            e1.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(100, 1, 0), PhysicsEntityKind.Unit, 0, go1));
            w1.RegisterUnit(e1);

            var go1b = new GameObject("Det1b");
            gameObjects.Add(go1b);
            var e1b = go1b.AddComponent<PhysicsEntity2D>();
            e1b.RestoreLogicSpatialState(
                new PhysicsTransform2D(new fp2(8, 8), new fp2(8, 8), default, default),
                FPhysics.PhysicsShape2D.CreateCircle(default, (fp)1m));
            e1b.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(100, 2, 0), PhysicsEntityKind.Unit, 0, go1b));
            w1.RegisterUnit(e1b);
            w1.BuildUnitFinalGrid();

            // World 2 — same config, different order
            var w2 = new PhysicsWorld();
            w2.Settings.GridCellSize = (fp)10m;
            var s2 = new RangeQueryService(w2);

            var go2 = new GameObject("Det2");
            gameObjects.Add(go2);
            var e2 = go2.AddComponent<PhysicsEntity2D>();
            e2.RestoreLogicSpatialState(
                new PhysicsTransform2D(new fp2(8, 8), new fp2(8, 8), default, default),
                FPhysics.PhysicsShape2D.CreateCircle(default, (fp)1m));
            e2.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(100, 2, 0), PhysicsEntityKind.Unit, 0, go2));
            w2.RegisterUnit(e2);

            var go2b = new GameObject("Det2b");
            gameObjects.Add(go2b);
            var e2b = go2b.AddComponent<PhysicsEntity2D>();
            e2b.RestoreLogicSpatialState(
                new PhysicsTransform2D(new fp2(5, 5), new fp2(5, 5), default, default),
                FPhysics.PhysicsShape2D.CreateCircle(default, (fp)1m));
            e2b.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(100, 1, 0), PhysicsEntityKind.Unit, 0, go2b));
            w2.RegisterUnit(e2b);
            w2.BuildUnitFinalGrid();

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var r1 = new List<Unit>();
            var r2 = new List<Unit>();
            var sc1 = new List<PhysicsEntity2D>();
            var sc2 = new List<PhysicsEntity2D>();
            s1.Query(desc, default, TeamId.Neutral, r1, sc1);
            s2.Query(desc, default, TeamId.Neutral, r2, sc2);

            Assert.AreEqual(r1.Count, r2.Count);
            for (int i = 0; i < r1.Count; i++)
            {
                Assert.AreEqual(r1[i].UnitUid, r2[i].UnitUid);
            }
        }

        [Test]
        public void Query_GridDedup_SameEntityInMultipleCells_ReturnsOnce()
        {
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(100, 1, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
            PhysicsEntity2D entity = unit.PhysicsEntity;
            // Large entity spanning multiple grid cells
            entity.TeleportLogicPosition(new fp2(5, 5));
            entity.SetLogicShape(FPhysics.PhysicsShape2D.CreateCircle(default, (fp)8m));
            physicsWorld.RegisterUnit(entity);
            BuildGrid();

            var desc = new RangeQueryDesc
            {
                Shape = FPhysics.PhysicsShape2D.CreateCircle(default, (fp)50m),
                Transform = new PhysicsTransform2D(default, default, default, default),
                TargetFilter = UnitTargetFilter.Default,
                SortMode = RangeQuerySortMode.DistanceThenUid,
            };

            var result = new List<Unit>();
            var scratch = new List<PhysicsEntity2D>();
            service.Query(desc, default, TeamId.Neutral, result, scratch);

            Assert.AreEqual(1, result.Count);
        }
    }
}

