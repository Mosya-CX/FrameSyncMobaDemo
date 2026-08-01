using System;
using System.Collections.Generic;
using System.Reflection;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class NonHeroTopologyTests
    {
        private readonly List<GameObject> objects =
            new List<GameObject>();
        private SimulationTickContextController ticks;

        [TearDown]
        public void TearDown()
        {
            ticks?.EndTick();
            ticks = null;
            for (int i = objects.Count - 1;
                 i >= 0;
                 i--)
                if (objects[i] != null)
                    UnityEngine.Object.DestroyImmediate(
                        objects[i]);
            objects.Clear();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void MinionWave_ExpandsCanonicalTeamLaneMemberOrder()
        {
            var schedule = new BakedMinionWaveConfig(
                30,
                0,
                new[]
                {
                    new MinionWavePhase
                    {
                        StartWaveIndex = 0,
                        CompositionCycle = new[]
                        {
                            new MinionWaveComposition
                            {
                                Members = new[]
                                {
                                    new MinionWaveMember
                                    {
                                        UnitPrototypeId = 20,
                                        Count = 2,
                                        FirstSpawnOffsetTicks = 5,
                                        SpawnStepTicks = 1,
                                    },
                                },
                            },
                        },
                    },
                });
            var lane = new LaneRuntimeData(
                3,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        new fp2(1, 2),
                        new fp2(1, 0)),
                    new LaneTeamSpawnData(
                        new TeamId(2),
                        new fp2(9, 2),
                        new fp2(-1, 0)),
                },
                new[] { fp2.zero, new fp2(10, 0) },
                (fp)2m);
            var system = new MinionSystem(
                new UnitWorld(),
                schedule,
                new[] { lane });
            BeginTick(0);

            system.TickLogic();

            Assert.That(system.WaveIndex, Is.EqualTo(1));
            Assert.That(system.PendingTickets.Count, Is.EqualTo(4));
            Assert.That(system.PendingTickets[0].TeamId,
                Is.EqualTo(new TeamId(1)));
            Assert.That(system.PendingTickets[0].SpawnLogicTick,
                Is.EqualTo(5));
            Assert.That(system.PendingTickets[1].SpawnLogicTick,
                Is.EqualTo(5));
            Assert.That(system.PendingTickets[1].TeamId,
                Is.EqualTo(new TeamId(2)));
            Assert.That(system.PendingTickets[2].SpawnLogicTick,
                Is.EqualTo(6));
            Assert.That(system.PendingTickets[2].TeamId,
                Is.EqualTo(new TeamId(1)));
            Assert.That(system.PendingTickets[0].LaneId,
                Is.EqualTo(3));
        }

        [Test]
        public void MinionUnregister_RemovesUidWithoutLeavingTombstone()
        {
            var system = new MinionSystem(
                new UnitWorld(),
                new BakedMinionWaveConfig(
                    30,
                    100,
                    Array.Empty<MinionWavePhase>()),
                Array.Empty<LaneRuntimeData>());
            UnitUid first = new UnitUid(10, 20, 0);
            UnitUid second = new UnitUid(10, 20, 1);
            var snapshot = new MinionSystemSnapshot
            {
                WaveIndex = 0,
                NextWaveLogicTick = 100,
                PendingTickets = Array.Empty<MinionTicket>(),
                NextTicketCursor = 0,
                ManagedMinionUids = new[] { first, second },
            };
            system.Restore(snapshot);

            Assert.IsTrue(system.UnregisterManagedUnit(first));

            Assert.That(system.ManagedMinionUids.Count, Is.EqualTo(1));
            Assert.That(system.ManagedMinionUids[0], Is.EqualTo(second));
        }

        [Test]
        public void LaneNearestPoint_ProjectsOntoCenterlineSegment()
        {
            var lane = new LaneRuntimeData(
                1,
                Array.Empty<LaneTeamSpawnData>(),
                new[]
                {
                    fp2.zero,
                    new fp2(0, 40),
                    new fp2(40, 40),
                },
                (fp)2);

            fp2 nearest = lane.GetNearestCenterlinePoint(
                new fp2(1, 20),
                out fp distanceSq);

            Assert.That(nearest.x, Is.EqualTo(fp.zero));
            Assert.That(nearest.y, Is.EqualTo((fp)20));
            Assert.That(distanceSq, Is.EqualTo(fp.one));
        }

        [Test]
        public void MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance()
        {
            UnitWorld world = CreateSpawnWorld();
            var lane = new LaneRuntimeData(
                1,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        fp2.zero,
                        new fp2(fp.zero, fp.one)),
                },
                new[]
                {
                    fp2.zero,
                    new fp2(0, 40),
                },
                (fp)2);
            world.MinionSystem = new MinionSystem(
                world,
                new BakedMinionWaveConfig(
                    30,
                    100,
                    Array.Empty<MinionWavePhase>()),
                new[] { lane });
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 31, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                new fp2(1, 20));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);

            controller.AIThink();

            Assert.That(controller.AIState,
                Is.EqualTo(MinionAIState.AdvanceLane));
            Assert.That(owner.Intent.Kind,
                Is.EqualTo(IntentKind.LaneAdvance));
        }

        [Test]
        public void MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField()
        {
            UnitWorld world = CreateSpawnWorld();
            var lane = new LaneRuntimeData(
                1,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        fp2.zero,
                        new fp2(fp.zero, fp.one)),
                },
                new[]
                {
                    fp2.zero,
                    new fp2(0, 40),
                },
                (fp)2);
            world.MinionSystem = new MinionSystem(
                world,
                new BakedMinionWaveConfig(
                    30,
                    100,
                    Array.Empty<MinionWavePhase>()),
                new[] { lane });
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 32, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                new fp2(20, 20));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);

            controller.AIThink();

            Assert.That(controller.AIState,
                Is.EqualTo(MinionAIState.ReturnToLane));
            Assert.That(owner.Intent.Kind,
                Is.EqualTo(IntentKind.LaneAdvance));
        }

        [Test]
        public void AIController_DoesNotTickOnSpawnTick()
        {
            Unit unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 1, 1),
                UnitKind.Minion,
                0,
                new TeamId(1),
                1);
            var world = new UnitWorld();
            world.RegisterUnit(unit);
            var controller = new CountingController(unit);
            Assert.That(world.RegisterAIController(
                unit.UnitUid,
                controller), Is.True);
            BeginTick(10);

            world.TickAIControllers();
            Assert.That(controller.TickCount, Is.Zero);

            ticks.EndTick();
            ticks.BeginTick(11, ExecutionMode.ServerAuthority);
            world.TickAIControllers();
            Assert.That(controller.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void MinionAI_UsesLaneAdvanceOrderThroughPlanner()
        {
            UnitWorld world = CreateSpawnWorld();
            var lane = new LaneRuntimeData(
                3,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        fp2.zero,
                        new fp2(fp.one, fp.zero)),
                },
                new[]
                {
                    fp2.zero,
                    new fp2(20, 0),
                },
                (fp)2m);
            world.MinionSystem = new MinionSystem(
                world,
                new BakedMinionWaveConfig(
                    30,
                    100,
                    Array.Empty<MinionWavePhase>()),
                new[] { lane });
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = 31,
                RuntimeEntityPrefabId = 31,
                UnitKind = UnitKind.Minion,
                BaseStats = new StatPreset(),
                PhysicsProfile =
                    PhysicsProfile2D.DefaultCircle,
                LocomotionProfile =
                    LocomotionProfile.DefaultMinion,
            };
            BeginTick(10);
            Unit minion = world.SpawnUnit(
                prototype,
                new TeamId(1),
                10,
                fp.zero,
                fp.zero);
            var controller =
                new MinionAIController(
                    minion,
                    3);
            world.RegisterAIController(controller);

            ticks.EndTick();
            ticks.BeginTick(
                11,
                ExecutionMode.ServerAuthority);
            world.TickAIControllers();

            Assert.That(
                minion.Intent.Kind,
                Is.EqualTo(
                    IntentKind.LaneAdvance));
            Assert.That(
                minion.Intent.TargetPosition.x,
                Is.EqualTo((fp)20));
            Assert.That(
                minion.Intent.TargetPosition.y,
                Is.EqualTo(fp.zero));
        }

        [Test]
        public void TowerAI_TargetIsOwnedByUnitIntentNotAISnapshot()
        {
            Unit tower = UnitTestFactory.CreateUnit(
                new UnitUid(10, 21, 0),
                UnitKind.Structure,
                0,
                new TeamId(1),
                21);
            UnitUid target = new UnitUid(10, 22, 0);
            var controller = new TowerAIController(tower);

            controller.AcquireTarget(target);
            UnitAIControllerSnapshot snapshot = default;
            controller.Capture(ref snapshot);
            controller.LoseTarget();
            controller.Restore(snapshot);

            Assert.That(
                controller.AIState,
                Is.EqualTo(TowerAIState.AttackingTarget));
            Assert.That(
                controller.CurrentTargetUid.IsValid(),
                Is.False,
                "AI restore must not recreate target state outside Unit.Intent.");
        }

        [Test]
        public void TowerAI_UsesStableMinionPriorityAndNeverChases()
        {
            var world = CreateSpawnWorld();
            Unit tower = CreateRegisteredUnit(
                world,
                new UnitUid(10, 21, 0),
                UnitKind.Structure,
                NonHeroUnitSubKindId.Unspecified,
                new TeamId(1),
                fp2.zero);
            Unit ranged = CreateRegisteredUnit(
                world,
                new UnitUid(10, 22, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.RangedMinion,
                new TeamId(2),
                new fp2(1, 0));
            Unit melee = CreateRegisteredUnit(
                world,
                new UnitUid(10, 23, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(2),
                new fp2(2, 0));
            CreateRegisteredUnit(
                world,
                new UnitUid(10, 24, 0),
                UnitKind.Hero,
                0,
                new TeamId(2),
                new fp2(fp.one / (fp)2, 0));
            tower.StatHandler.SetStat(StatId.AttackRange, (fp)800);
            var controller = new TowerAIController(tower);
            BeginTick(11);

            controller.AIThink();

            Assert.That(tower.Intent.TargetUnit, Is.EqualTo(melee.UnitUid));
            Assert.That(tower.Intent.AllowChase, Is.False);
            Assert.That(tower.Intent.TargetUnit, Is.Not.EqualTo(ranged.UnitUid));
        }

        [Test]
        public void MinionAI_SelectsEnemyMinionBeforeOrdinaryHero()
        {
            var world = CreateSpawnWorld();
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 31, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                fp2.zero);
            owner.StatHandler.SetStat(
                StatId.AttackRange,
                (fp)200);
            CreateRegisteredUnit(
                world,
                new UnitUid(10, 32, 0),
                UnitKind.Hero,
                0,
                new TeamId(2),
                new fp2(fp.one / (fp)2, 0));
            Unit enemyMinion = CreateRegisteredUnit(
                world,
                new UnitUid(10, 33, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.RangedMinion,
                new TeamId(2),
                new fp2(2, 0));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);

            controller.AIThink();

            Assert.That(
                owner.Intent.TargetUnit,
                Is.EqualTo(enemyMinion.UnitUid));
            Assert.That(owner.Intent.AllowChase, Is.True);
        }

        [Test]
        public void MinionAI_FiltersNonEnemiesAndReacquiresAfterTargetInvalidates()
        {
            var world = CreateSpawnWorld();
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 41, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                fp2.zero);
            owner.StatHandler.SetStat(
                StatId.AttackRange,
                (fp)200);
            CreateRegisteredUnit(
                world,
                new UnitUid(10, 42, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                new fp2(fp.one / (fp)4, fp.zero));
            CreateRegisteredUnit(
                world,
                new UnitUid(10, 43, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                TeamId.Neutral,
                new fp2(fp.one / (fp)2, fp.zero));
            Unit firstEnemy = CreateRegisteredUnit(
                world,
                new UnitUid(10, 44, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(2),
                new fp2(fp.one, fp.zero));
            Unit secondEnemy = CreateRegisteredUnit(
                world,
                new UnitUid(10, 45, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.RangedMinion,
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);

            controller.AIThink();

            Assert.That(owner.Intent.TargetUnit, Is.EqualTo(firstEnemy.UnitUid));

            world.RequestEnterDying(firstEnemy);
            ticks.EndTick();
            ticks.BeginTick(16, ExecutionMode.ServerAuthority);
            controller.AIThink();

            Assert.That(owner.Intent.TargetUnit, Is.EqualTo(secondEnemy.UnitUid));
            Assert.That(controller.AIState, Is.EqualTo(MinionAIState.EngageTarget));
        }

        [Test]
        public void MinionAI_LegalCurrentTargetIsNeverReplaced()
        {
            var world = CreateSpawnWorld();
            world.MinionSystem = new MinionSystem(
                world,
                new BakedMinionWaveConfig(
                    30,
                    100,
                    Array.Empty<MinionWavePhase>()),
                new[]
                {
                    new LaneRuntimeData(
                        1,
                        new[]
                        {
                            new LaneTeamSpawnData(
                                new TeamId(1),
                                fp2.zero,
                                new fp2(fp.one, fp.zero)),
                        },
                        new[]
                        {
                            new fp2(-10, 0),
                            new fp2(10, 0),
                        },
                        (fp)2),
                });
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 46, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                fp2.zero);
            Unit currentHero = CreateRegisteredUnit(
                world,
                new UnitUid(10, 47, 0),
                UnitKind.Hero,
                0,
                new TeamId(2),
                new fp2(fp.one, fp.zero));
            CreateRegisteredUnit(
                world,
                new UnitUid(10, 48, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(2),
                new fp2(fp.one / (fp)2, fp.zero));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);
            controller.AcquireTarget(currentHero.UnitUid);

            ticks.EndTick();
            ticks.BeginTick(46, ExecutionMode.ServerAuthority);
            controller.AIThink();

            Assert.That(
                owner.Intent.TargetUnit,
                Is.EqualTo(currentHero.UnitUid),
                "A legal current target must remain locked even after the legacy lock window expires.");
        }

        [Test]
        public void RangedMinionAI_AcquiresBeyondAttackRangeWithinPadding()
        {
            var world = CreateSpawnWorld();
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 49, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.RangedMinion,
                new TeamId(1),
                fp2.zero);
            owner.StatHandler.SetStat(StatId.AttackRange, (fp)500);
            Unit enemy = CreateRegisteredUnit(
                world,
                new UnitUid(10, 50, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(2),
                new fp2((fp)5.5m, fp.zero));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);

            controller.AIThink();

            Assert.That(owner.AttackHandler.CurrentAttackRange,
                Is.LessThan((fp)5.5m));
            Assert.That(owner.Intent.TargetUnit, Is.EqualTo(enemy.UnitUid));
        }

        [Test]
        public void MinionAI_EqualPriorityAndDistanceUsesLowestUnitUid()
        {
            var world = CreateSpawnWorld();
            Unit owner = CreateRegisteredUnit(
                world,
                new UnitUid(10, 51, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(1),
                fp2.zero);
            CreateRegisteredUnit(
                world,
                new UnitUid(10, 53, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(2),
                new fp2(fp.one, fp.zero));
            Unit lowerUid = CreateRegisteredUnit(
                world,
                new UnitUid(10, 52, 0),
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                new TeamId(2),
                new fp2(-fp.one, fp.zero));
            var controller = new MinionAIController(owner, 1);
            BeginTick(11);

            controller.AIThink();

            Assert.That(owner.Intent.TargetUnit, Is.EqualTo(lowerUid.UnitUid));
        }

        [Test]
        public void JungleCamp_MainDeath_UnregistersAndRespawnsNewGeneration()
        {
            UnitWorld world = CreateSpawnWorld();
            ConfigureMonsterPrototype(world, 40);
            JungleCamp camp = CreateCamp(7, 40, 0f);
            camp.InitializeForMatch(world);
            BeginTick(0);

            world.TickJungleCamps();
            UnitUid firstUid = camp.MemberUidsBySlot[0];
            Assert.That(firstUid.IsValid(), Is.True);
            Assert.That(world.TryGetAIController(firstUid, out _), Is.True);

            Assert.That(world.TryGetUnit(firstUid, out Unit monster), Is.True);
            world.RequestEnterDying(monster);
            world.ConfirmUnitDeath(monster);
            world.FinalizeNonHeroDeath(monster);

            Assert.That(camp.State,
                Is.EqualTo(JungleCampState.WaitingRespawn));
            Assert.That(world.TryGetAIController(firstUid, out _), Is.False);

            ticks.EndTick();
            ticks.BeginTick(1, ExecutionMode.ServerAuthority);
            world.TickJungleCamps();
            UnitUid secondUid = camp.MemberUidsBySlot[0];
            Assert.That(secondUid.IsValid(), Is.True);
            Assert.That(secondUid, Is.Not.EqualTo(firstUid));
        }

        [Test]
        public void JungleCamp_SnapshotRoundTrip_PreservesRuntimeState()
        {
            UnitWorld world = CreateSpawnWorld();
            ConfigureMonsterPrototype(world, 50);
            JungleCamp camp = CreateCamp(8, 50, 10f);
            camp.InitializeForMatch(world);
            BeginTick(0);
            world.TickJungleCamps();
            JungleCampSnapshot snapshot = default;
            camp.Capture(ref snapshot);

            camp.Restore(snapshot);
            JungleCampSnapshot roundTrip = default;
            camp.Capture(ref roundTrip);

            Assert.That(roundTrip.CampId, Is.EqualTo(snapshot.CampId));
            Assert.That(roundTrip.State, Is.EqualTo(snapshot.State));
            CollectionAssert.AreEqual(
                snapshot.MemberUidsBySlot,
                roundTrip.MemberUidsBySlot);
            CollectionAssert.AreEqual(
                snapshot.MemberAliveBySlot,
                roundTrip.MemberAliveBySlot);
        }

        private UnitWorld CreateSpawnWorld()
        {
            return new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
        }

        private static Unit CreateRegisteredUnit(
            UnitWorld world,
            UnitUid uid,
            UnitKind kind,
            ushort subKindId,
            TeamId teamId,
            fp2 position)
        {
            Unit unit = UnitTestFactory.CreateUnit(
                uid,
                kind,
                subKindId,
                teamId,
                uid.RuntimeEntityPrefabId);
            unit.PhysicsEntity.SetLogicPose(
                position,
                new fp2(fp.one, fp.zero));
            unit.World = world;
            world.RegisterUnit(unit);
            world.PhysicsWorld.RegisterUnit(unit.PhysicsEntity);
            return unit;
        }

        private static void ConfigureMonsterPrototype(
            UnitWorld world,
            int prototypeId)
        {
            UnitTestFactory.ConfigureWorldForPrototype(
                world,
                new UnitPrototype
                {
                    UnitPrototypeId = prototypeId,
                    RuntimeEntityPrefabId = prototypeId,
                    UnitKind = UnitKind.Monster,
                    BaseStats = new StatPreset(),
                    PhysicsProfile = PhysicsProfile2D.DefaultCircle,
                    LocomotionProfile =
                        LocomotionProfile.DefaultMonster,
                },
                fp.zero,
                fp.zero);
        }

        private JungleCamp CreateCamp(
            int campId,
            int prototypeId,
            float respawnDelay)
        {
            GameObject root = Track(new GameObject("Camp"));
            GameObject anchor = Track(new GameObject("Anchor"));
            GameObject spawn = Track(new GameObject("Spawn"));
            anchor.transform.position = Vector3.zero;
            spawn.transform.position = new Vector3(2, 0, 3);
            spawn.transform.forward = Vector3.forward;
            JungleCamp camp = root.AddComponent<JungleCamp>();
            SetField(camp, "campId", campId);
            SetField(camp, "campTeamId", 0);
            SetField(camp, "campAnchor", anchor.transform);
            SetField(camp, "mainMonsterSlotIndex", 0);
            SetField(camp, "respawnDelaySeconds", respawnDelay);
            SetField(camp, "spawnSlots", new[]
            {
                new JungleCampSpawnSlot
                {
                    SlotIndex = 0,
                    UnitPrototypeId = prototypeId,
                    SpawnPoint = spawn.transform,
                },
            });
            return camp;
        }

        private void BeginTick(int tick)
        {
            ticks = new SimulationTickContextController();
            ticks.BeginTick(tick, ExecutionMode.ServerAuthority);
        }

        private GameObject Track(GameObject value)
        {
            objects.Add(value);
            return value;
        }

        private static void SetField(
            object target,
            string name,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private sealed class CountingController :
            UnitAIController
        {
            public int TickCount { get; private set; }

            public CountingController(Unit owner)
                : base(owner)
            {
                ControllerKind =
                    UnitAIControllerKind.Minion;
            }

            public override void AIThink()
            {
                TickCount++;
            }
        }
    }
}
