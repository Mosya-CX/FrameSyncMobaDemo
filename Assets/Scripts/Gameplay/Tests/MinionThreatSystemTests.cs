using System.Collections.Generic;
using System.Reflection;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class MinionThreatSystemTests
    {
        private SimulationTickContextController ticks;
        private readonly List<MinionAIController> controllers =
            new List<MinionAIController>();

        [SetUp]
        public void SetUp()
        {
            // A previous interrupted test run can leave the static
            // SimulationTickContext active; clean it so each test starts
            // with a fresh Tick context.
            CompleteLeakedTick();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0;
                 i < controllers.Count;
                 i++)
            {
                controllers[i].ClearForDeath();
            }
            controllers.Clear();
            if (ticks != null)
            {
                try
                {
                    ticks.EndTick();
                }
                catch (System.InvalidOperationException)
                {
                    // Ignore leaked tick state from an interrupted phase.
                }
                ticks = null;
            }
            CompleteLeakedTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        private static void CompleteLeakedTick()
        {
            System.Type contextType =
                typeof(SimulationTickContext);
            System.Reflection.PropertyInfo active =
                contextType.GetProperty(
                    "IsTickActive",
                    BindingFlags.Static |
                    BindingFlags.NonPublic);
            if (active != null &&
                (bool)active.GetValue(null))
            {
                System.Reflection.MethodInfo complete =
                    contextType.GetMethod(
                        "CompleteCurrent",
                        BindingFlags.Static |
                        BindingFlags.NonPublic);
                complete?.Invoke(null, null);
            }
        }

        [Test]
        public void
            Acquisition_SetsInitialThreat_AndPicksClosestUnclaimed()
        {
            UnitWorld world = CreateWorld();
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            Unit enemyA = CreateMinion(
                world,
                new UnitUid(11, 1201, 0),
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            Unit enemyB = CreateMinion(
                world,
                new UnitUid(11, 1201, 1),
                new TeamId(2),
                new fp2((fp)4, fp.zero));
            SetAttackRange(owner, (fp)200); // 2.0 logic; acquire = 3.5
            var controller = CreateController(
                owner,
                1);

            BeginTick(11);
            controller.AIThink();

            Assert.That(
                controller.CurrentTargetUid,
                Is.EqualTo(enemyA.UnitUid));
            Assert.That(
                controller.GetThreatValue(
                    enemyA.UnitUid),
                Is.EqualTo(800));
            Assert.That(
                controller.GetThreatValue(
                    enemyB.UnitUid),
                Is.EqualTo(0));
        }

        [Test]
        public void
            DamageTaken_AddsThreat_InverselyProportionalToDistance()
        {
            UnitWorld world = CreateWorld();
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            Unit attacker = CreateMinion(
                world,
                new UnitUid(11, 1201, 0),
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            SetAttackRange(owner, (fp)200); // range 2.0 logic
            var controller = CreateController(
                owner,
                1);

            InvokeDamageTaken(
                controller,
                attacker.UnitUid,
                owner.UnitUid);

            // gain = 60 * 200 / 200 = 60
            Assert.That(
                controller.GetThreatValue(
                    attacker.UnitUid),
                Is.EqualTo(60));

            attacker.PhysicsEntity.SetLogicPose(
                new fp2(
                    (fp)0.5m,
                    fp.zero),
                new fp2(
                    fp.one,
                    fp.zero));
            InvokeDamageTaken(
                controller,
                attacker.UnitUid,
                owner.UnitUid);

            // gain = 60 * 200 / max(50, 50) = 240; 60 + 240 = 300.
            Assert.That(
                controller.GetThreatValue(
                    attacker.UnitUid),
                Is.EqualTo(300));
        }

        [Test]
        public void
            HigherThreatTarget_SwitchesOnlyWhenNotInWindup()
        {
            UnitWorld world = CreateWorld();
            ConfigureLane(world);
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            Unit targetA = CreateMinion(
                world,
                new UnitUid(11, 1201, 0),
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            Unit targetB = CreateMinion(
                world,
                new UnitUid(11, 1201, 1),
                new TeamId(2),
                new fp2((fp)1, (fp)1));
            SetAttackRange(owner, (fp)200);
            SetAttackSpeed(owner, fp.one);
            var controller = CreateController(
                owner,
                1);
            SetThreat(controller, targetA.UnitUid, 800);
            SetThreat(controller, targetB.UnitUid, 900);
            owner.Planner.ReplaceIntent(
                new UnitIntent
                {
                    Kind = IntentKind.AttackTarget,
                    TargetUnit = targetA.UnitUid,
                    AllowChase = true,
                });

            // Idle attack state (no windup): switch to the higher threat.
            BeginTick(11);
            controller.AIThink();
            Assert.That(
                controller.CurrentTargetUid,
                Is.EqualTo(targetB.UnitUid));

            // Reset: A is now the higher threat; put the unit mid-windup on
            // A so the switch is not allowed even though B has lower threat.
            ticks.EndTick();
            SetThreat(controller, targetA.UnitUid, 900);
            SetThreat(controller, targetB.UnitUid, 700);
            owner.Planner.ReplaceIntent(
                new UnitIntent
                {
                    Kind = IntentKind.AttackTarget,
                    TargetUnit = targetB.UnitUid,
                    AllowChase = true,
                });
            targetB.PhysicsEntity.SetLogicPose(
                new fp2(
                    (fp)1.5m,
                    fp.zero),
                new fp2(
                    fp.one,
                    fp.zero));
            BeginTick(16);
            owner.AttackHandler.BeginAttack(
                targetB.UnitUid);
            Assert.That(
                owner.AttackHandler
                    .IsAttackCycleActive,
                Is.True);
            Assert.That(
                owner.AttackHandler
                    .ImpactCommitted,
                Is.False);

            controller.AIThink();

            Assert.That(
                controller.CurrentTargetUid,
                Is.EqualTo(targetB.UnitUid),
                "Must not switch target mid-windup.");
        }

        [Test]
        public void
            Acquisition_PairsAlliesWithDistinctTargets()
        {
            UnitWorld world = CreateWorld();
            Unit allyA = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            Unit allyB = CreateMinion(
                world,
                new UnitUid(10, 1202, 1),
                new TeamId(1),
                new fp2(
                    fp.zero,
                    (fp)0.5m));
            Unit enemy1 = CreateMinion(
                world,
                new UnitUid(11, 1201, 0),
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            Unit enemy2 = CreateMinion(
                world,
                new UnitUid(11, 1201, 1),
                new TeamId(2),
                new fp2(
                    (fp)2,
                    (fp)0.5m));
            SetAttackRange(allyA, (fp)200);
            SetAttackRange(allyB, (fp)200);
            var controllerA = CreateController(
                allyA,
                1);
            var controllerB = CreateController(
                allyB,
                1);

            BeginTick(11);
            controllerA.AIThink();
            controllerB.AIThink();

            UnitUid targetOfA =
                controllerA.CurrentTargetUid;
            UnitUid targetOfB =
                controllerB.CurrentTargetUid;
            Assert.That(targetOfA.IsValid(), Is.True);
            Assert.That(targetOfB.IsValid(), Is.True);
            Assert.That(
                targetOfA,
                Is.Not.EqualTo(targetOfB),
                "Nearby allies must not both pick the same enemy.");
            Assert.That(
                targetOfA == enemy1.UnitUid ||
                targetOfA == enemy2.UnitUid,
                Is.True);
            Assert.That(
                targetOfB == enemy1.UnitUid ||
                targetOfB == enemy2.UnitUid,
                Is.True);
        }

        [Test]
        public void ThreatTable_SnapshotRoundTrip_PreservesEntries()
        {
            UnitWorld world = CreateWorld();
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            Unit enemy = CreateMinion(
                world,
                new UnitUid(11, 1201, 0),
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            var controller = CreateController(
                owner,
                1);
            SetThreat(controller, enemy.UnitUid, 777);

            var snapshot =
                new UnitAIControllerSnapshot();
            controller.Capture(ref snapshot);
            controller.Restore(snapshot);

            Assert.That(
                controller.GetThreatValue(
                    enemy.UnitUid),
                Is.EqualTo(777));
        }

        [Test]
        public void SnapshotRoundTrip_PreservesLastThreatRefreshTick()
        {
            UnitWorld world = CreateWorld();
            ConfigureLane(world);
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            var controller = CreateController(
                owner,
                1);

            BeginTick(11);
            controller.AIThink();
            EndCurrentTick();

            Assert.That(
                GetLastThreatRefreshTick(
                    controller),
                Is.EqualTo(11));

            var snapshot =
                new UnitAIControllerSnapshot();
            controller.Capture(ref snapshot);

            UnitAIController restored =
                world.ReconstructAIController(
                    snapshot);
            controllers.Add(
                (MinionAIController)restored);
            restored.Restore(snapshot);

            Assert.That(
                GetLastThreatRefreshTick(
                    (MinionAIController)restored),
                Is.EqualTo(11));
        }

        [Test]
        public void
            RestoreReplacement_UnsubscribesPreviousController()
        {
            UnitWorld world = CreateWorld();
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            int baseline =
                CountDamageTakenHandlers();

            var first = CreateController(
                owner,
                1);
            world.RegisterAIController(
                owner.UnitUid,
                first);
            Assert.That(
                CountDamageTakenHandlers(),
                Is.EqualTo(baseline + 1));

            world.ClearAIControllersForRestore();
            Assert.That(
                CountDamageTakenHandlers(),
                Is.EqualTo(baseline));

            var snapshot =
                new UnitAIControllerSnapshot();
            first.Capture(ref snapshot);
            UnitAIController replacement =
                world.ReconstructAIController(
                    snapshot);
            controllers.Add(
                (MinionAIController)replacement);
            replacement.Restore(snapshot);
            world.RegisterAIController(
                replacement);

            Assert.That(
                CountDamageTakenHandlers(),
                Is.EqualTo(baseline + 1));
        }

        [Test]
        public void
            RestoredController_DoesNotReapplyStaleAttackThreat()
        {
            UnitWorld world = CreateWorld();
            ConfigureLane(world);
            Unit owner = CreateMinion(
                world,
                new UnitUid(10, 1202, 0),
                new TeamId(1),
                fp2.zero);
            Unit enemy = CreateMinion(
                world,
                new UnitUid(11, 1201, 0),
                new TeamId(2),
                new fp2((fp)2, fp.zero));
            SetAttackRange(owner, (fp)200);
            var controller = CreateController(
                owner,
                1);

            // Tick 11: first refresh acquires the enemy (threat 800,
            // refresh tick recorded as 11).
            BeginTick(11);
            controller.AIThink();
            EndCurrentTick();

            // Simulate an attack that landed at tick 10, i.e. BEFORE the
            // last refresh. The authoritative controller already accounted
            // for it; a rollback-restored controller must NOT re-apply the
            // attack gain just because its refresh tick was reset.
            SetAttackHandlerHitState(
                owner,
                enemy.UnitUid,
                10);

            var snapshot =
                new UnitAIControllerSnapshot();
            controller.Capture(ref snapshot);

            UnitAIController restored =
                world.ReconstructAIController(
                    snapshot);
            controllers.Add(
                (MinionAIController)restored);
            restored.Restore(snapshot);

            // Tick 16: next refresh. Correct behavior is decay only
            // (800 -> 780) with no spurious attack-threat gain.
            BeginTick(16);
            restored.AIThink();
            EndCurrentTick();

            Assert.That(
                ((MinionAIController)restored)
                    .GetThreatValue(
                        enemy.UnitUid),
                Is.EqualTo(780));
        }

        private static void InvokeDamageTaken(
            MinionAIController controller,
            UnitUid source,
            UnitUid target)
        {
            MethodInfo method =
                typeof(MinionAIController)
                    .GetMethod(
                        "HandleDamageTaken",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            method.Invoke(
                controller,
                new object[]
                {
                    new DamageEventData
                    {
                        SourceUid = source,
                        TargetUid = target,
                    },
                });
        }

        private static int
            CountDamageTakenHandlers()
        {
            FieldInfo field =
                typeof(CombatEvents)
                    .GetField(
                        "_onDamageTaken",
                        BindingFlags.Static |
                        BindingFlags.NonPublic);
            var handler =
                field.GetValue(null) as
                    System.Delegate;
            return handler?
                       .GetInvocationList()
                       .Length ?? 0;
        }

        private static int
            GetLastThreatRefreshTick(
                MinionAIController controller)
        {
            FieldInfo field =
                typeof(MinionAIController)
                    .GetField(
                        "lastThreatRefreshTick",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            return (int)field.GetValue(
                controller);
        }

        private static void
            SetAttackHandlerHitState(
                Unit owner,
                UnitUid target,
                int lastHitTick)
        {
            FieldInfo stateField =
                typeof(AttackHandler)
                    .GetField(
                        "_state",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            object boxed =
                stateField.GetValue(
                    owner.AttackHandler);
            AttackSnapshot state =
                (AttackSnapshot)boxed;
            state.CurrentTargetUid = target;
            state.LastSuccessfulAttackLogicTick =
                lastHitTick;
            stateField.SetValue(
                owner.AttackHandler,
                state);
        }

        private static void SetThreat(
            MinionAIController controller,
            UnitUid uid,
            int value)
        {
            MethodInfo method =
                typeof(MinionAIController)
                    .GetMethod(
                        "SetThreat",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
            method.Invoke(
                controller,
                new object[] { uid, value });
        }

        private MinionAIController CreateController(
            Unit owner,
            int laneId)
        {
            var controller =
                new MinionAIController(
                    owner,
                    laneId);
            controllers.Add(controller);
            return controller;
        }

        private static Unit CreateMinion(
            UnitWorld world,
            UnitUid uid,
            TeamId team,
            fp2 position)
        {
            Unit unit = UnitTestFactory.CreateUnit(
                uid,
                UnitKind.Minion,
                NonHeroUnitSubKindId.MeleeMinion,
                team,
                uid.RuntimeEntityPrefabId);
            unit.PhysicsEntity.SetLogicPose(
                position,
                new fp2(
                    fp.one,
                    fp.zero));
            unit.World = world;
            world.RegisterUnit(unit);
            world.PhysicsWorld.RegisterUnit(
                unit.PhysicsEntity);
            return unit;
        }

        private static void SetAttackRange(
            Unit unit,
            fp rawRange)
        {
            unit.StatHandler.SetStat(
                StatId.AttackRange,
                rawRange);
        }

        private static void SetAttackSpeed(
            Unit unit,
            fp speed)
        {
            unit.StatHandler.SetStat(
                StatId.AttackSpeed,
                speed);
        }

        private static UnitWorld CreateWorld()
        {
            return new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                TickRate = 30,
            };
        }

        private static void ConfigureLane(
            UnitWorld world)
        {
            var lane = new LaneRuntimeData(
                1,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        fp2.zero,
                        new fp2(
                            fp.zero,
                            fp.one)),
                },
                new[]
                {
                    fp2.zero,
                    new fp2(
                        fp.zero,
                        (fp)40),
                },
                (fp)2);
            world.MinionSystem =
                new MinionSystem(
                    world,
                    new BakedMinionWaveConfig(
                        30,
                        100,
                        System.Array.Empty<
                            MinionWavePhase>()),
                    new[] { lane });
        }

        private void BeginTick(int tick)
        {
            ticks = new SimulationTickContextController();
            ticks.BeginTick(
                tick,
                ExecutionMode.ServerAuthority);
        }

        private void EndCurrentTick()
        {
            ticks.EndTick();
            ticks = null;
        }
    }
}
