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
