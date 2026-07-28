using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class SnapshotChecksumCompletenessTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void AggregateSnapshot_RestoresIntentDashAndLocomotion()
        {
            UnitWorld world = CreateWorld(withPathGrid: true);
            UnitType source = Spawn(world, 100, 0);
            UnitType target = Spawn(world, 101, 0);
            source.Planner.SetIntent(new UnitIntent
            {
                Kind = IntentKind.AttackTarget,
                TargetUnit = target.UnitUid,
                AllowChase = true,
                AllowReplan = true,
            });

            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                source.MovementHandler.ApplyDash(
                    new fp2(fp.one, fp.zero), (fp)8, (fp)4);
                Assert.That(
                    source.Locomotion.AcceptRouteRequest(
                        RouteMoveRequest.ToPosition(new fp2(9, 3), (fp)0.5m)),
                    Is.EqualTo(MoveAcceptResult.Accepted));
            }
            finally
            {
                tick.EndTick();
            }

            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();

            source.Planner.ClearIntent();
            source.MovementHandler.Restore(MovementSnapshot.Default);
            source.Locomotion.CancelRoute(MoveCancelReason.UserCommand);

            pipeline.RestoreFromSnapshot(snapshot, 3);

            Assert.That(source.Intent.Kind, Is.EqualTo(IntentKind.AttackTarget));
            Assert.That(source.Intent.TargetUnit, Is.EqualTo(target.UnitUid));
            MovementSnapshot restoredMovement = source.MovementHandler.Snapshot;
            Assert.That(restoredMovement.IsDashing, Is.True);
            Assert.That(
                fpmath.sqrt(
                    fpmath.lengthsq(
                        restoredMovement.Dash.TargetPosition -
                        restoredMovement.Dash.StartPosition)),
                Is.EqualTo((fp)8));
            Assert.That(source.Locomotion.CurrentTask.State,
                Is.EqualTo(MovementTaskState.Active));
            Assert.That(source.Locomotion.CurrentTask.Target.Position,
                Is.EqualTo(new fp2(9, 3)));
        }

        [Test]
        public void SharedChecksum_ChangesForIntentDashAndLocomotionState()
        {
            UnitWorld world = CreateWorld(withPathGrid: true);
            UnitType unit = Spawn(world, 110, 0);
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);
            GameplaySnapshot baseline = pipeline.CaptureAggregateSnapshot();
            uint baselineChecksum = Compute(baseline);

            unit.Planner.SetIntent(new UnitIntent
            {
                Kind = IntentKind.MoveToPosition,
                TargetPosition = new fp2(6, 2),
                AllowReplan = true,
            });
            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                unit.MovementHandler.ApplyDash(
                    new fp2(fp.zero, fp.one), (fp)6, (fp)3);
                Assert.That(
                    unit.Locomotion.AcceptRouteRequest(
                        RouteMoveRequest.ToPosition(new fp2(6, 2), (fp)0.25m)),
                    Is.EqualTo(MoveAcceptResult.Accepted));
            }
            finally
            {
                tick.EndTick();
            }

            GameplaySnapshot changed = pipeline.CaptureAggregateSnapshot();
            Assert.That(Compute(changed), Is.Not.EqualTo(baselineChecksum));
        }

        [Test]
        public void CombatModifierCapture_IsCanonicalAndDetachRepairsShiftedIndices()
        {
            UnitWorld firstWorld = CreateWorld();
            UnitWorld secondWorld = CreateWorld();
            UnitType first = Spawn(firstWorld, 120, 0);
            UnitType second = Spawn(secondWorld, 120, 0);

            first.CombatModifiers.Attach(new CombatModifierRecord { Id = 30 });
            first.CombatModifiers.Attach(new CombatModifierRecord { Id = 10 });
            CombatModifierHandle middle =
                first.CombatModifiers.Attach(new CombatModifierRecord { Id = 20 });
            CombatModifierHandle last =
                first.CombatModifiers.Attach(new CombatModifierRecord { Id = 40 });
            Assert.That(first.CombatModifiers.Detach(middle), Is.True);
            Assert.That(first.CombatModifiers.Detach(last), Is.True);
            first.CombatModifiers.Attach(new CombatModifierRecord { Id = 20 });
            first.CombatModifiers.Attach(new CombatModifierRecord { Id = 40 });

            second.CombatModifiers.Attach(new CombatModifierRecord { Id = 10 });
            second.CombatModifiers.Attach(new CombatModifierRecord { Id = 20 });
            second.CombatModifiers.Attach(new CombatModifierRecord { Id = 30 });
            second.CombatModifiers.Attach(new CombatModifierRecord { Id = 40 });

            GameplaySnapshot firstSnapshot =
                new SimulationTickPipeline(firstWorld, firstWorld.PhysicsWorld)
                    .CaptureAggregateSnapshot();
            GameplaySnapshot secondSnapshot =
                new SimulationTickPipeline(secondWorld, secondWorld.PhysicsWorld)
                    .CaptureAggregateSnapshot();

            CollectionAssert.AreEqual(
                new ulong[] { 10, 20, 30, 40 },
                firstSnapshot.UnitWorldState.Units[0].CombatModifierState.Ids);
            Assert.That(Compute(firstSnapshot), Is.EqualTo(Compute(secondSnapshot)));
        }

        [Test]
        public void AggregateSnapshot_RejectsLiveUnsnapshottedActionRuntime()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 130, 0);
            unit.ActionRuntimes.Add(new TestActionRuntime());
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);

            Assert.Throws<DeterministicSimulationException>(
                () => pipeline.CaptureAggregateSnapshot());
        }

        private static uint Compute(in GameplaySnapshot snapshot)
        {
            return SharedGameplayChecksum.Compute(
                snapshot,
                default,
                new CanonicalByteWriter(new byte[65536]));
        }

        private static UnitWorld CreateWorld(bool withPathGrid = false)
        {
            var world = new UnitWorld
            {
                StatDefinitionTable = new StatDefinitionTable(),
                PhysicsWorld = new PhysicsWorld(),
            };
            if (withPathGrid)
            {
                world.PathGrid = new PathGridMap2D();
                world.PathGrid.Initialise(fp2.zero, new fp2(15, 15), fp.one);
            }
            return world;
        }

        private static UnitType Spawn(UnitWorld world, int prefabId, int tick)
        {
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = prefabId,
                RuntimeEntityPrefabId = prefabId,
                UnitKind = UnitKind.Hero,
                BaseStats = new StatPreset(),
            };
            return world.SpawnUnit(prototype, TeamId.Neutral, tick, fp.zero, fp.zero);
        }

        private sealed class TestActionRuntime : IActionRuntime
        {
            public ActionKind Kind => ActionKind.Move;
            public bool IsFinished => false;
            public void Tick() { }
            public void Cancel() { }
        }
    }
}
