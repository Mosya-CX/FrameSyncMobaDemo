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
                var spec = new ActionStartSpec(
                    ActionSlot.Base,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionInterruptLevel.Ordinary,
                    true,
                    false);
                source.ActionRuntimes.Start(ActionKind.Move, spec);
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
            source.ActionRuntimes.ClearWithoutCancel();

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
            Assert.That(source.ActionRuntimes.Base.IsOccupied, Is.True);
            Assert.That(source.ActionRuntimes.Base.Kind,
                Is.EqualTo(ActionKind.Move));
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
                var spec = new ActionStartSpec(
                    ActionSlot.Base,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    ActionInterruptLevel.Ordinary,
                    true,
                    false);
                unit.ActionRuntimes.Start(ActionKind.Move, spec);
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
        public void AggregateSnapshot_CapturesLiveActionRuntime()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 130, 0);
            var spec = new ActionStartSpec(
                ActionSlot.Base,
                ActionResource.BaseAction |
                    ActionResource.Movement |
                    ActionResource.Facing,
                ActionResource.BaseAction |
                    ActionResource.Movement |
                    ActionResource.Facing,
                ActionInterruptLevel.Ordinary,
                true,
                false);
            unit.ActionRuntimes.Start(ActionKind.Move, spec);
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);

            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();

            Assert.That(
                snapshot.UnitWorldState.Units[0]
                    .ActionRuntimeState.Base.IsOccupied,
                Is.True);
            Assert.That(
                snapshot.UnitWorldState.Units[0]
                    .ActionRuntimeState.Base.Kind,
                Is.EqualTo(ActionKind.Move));
        }

        [Test]
        public void SharedChecksum_SerializesEveryActionRuntimeSlotMember()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 131, 0);
            var pipeline = new SimulationTickPipeline(
                world,
                world.PhysicsWorld);
            GameplaySnapshot baseline =
                pipeline.CaptureAggregateSnapshot();
            uint checksum = Compute(baseline);

            AssertSerialized(new ActionRuntimeSlotSnapshot
                { IsOccupied = true });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { Slot = ActionSlot.Main });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { Kind = ActionKind.Move });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { Phase = ActionRuntimePhase.Moving });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { OccupiedResources = ActionResource.Movement });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { Interruptible = true });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { BlocksVoluntaryMove = true });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { IsControlAction = true });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { TargetUnitUid = unit.UnitUid });
            AssertSerialized(new ActionRuntimeSlotSnapshot
                { AbilitySlot = 3 });

            void AssertSerialized(ActionRuntimeSlotSnapshot state)
            {
                GameplaySnapshot changed = baseline;
                UnitSnapshot unitState =
                    changed.UnitWorldState.Units[0];
                unitState.ActionRuntimeState.Main = state;
                changed.UnitWorldState.Units =
                    (UnitSnapshot[])changed.UnitWorldState.Units.Clone();
                changed.UnitWorldState.Units[0] = unitState;
                Assert.That(Compute(changed), Is.Not.EqualTo(checksum));
            }
        }

        [Test]
        public void Restore_RejectsActionRuntimeWithoutOwningHandlerState()
        {
            UnitWorld world = CreateWorld();
            Spawn(world, 132, 0);
            var pipeline = new SimulationTickPipeline(
                world,
                world.PhysicsWorld);
            GameplaySnapshot snapshot =
                pipeline.CaptureAggregateSnapshot();
            UnitSnapshot unitState = snapshot.UnitWorldState.Units[0];
            unitState.ActionRuntimeState.Base =
                new ActionRuntimeSlotSnapshot
                {
                    IsOccupied = true,
                    Slot = ActionSlot.Base,
                    Kind = ActionKind.Move,
                    Phase = ActionRuntimePhase.Moving,
                    OccupiedResources =
                        ActionResource.BaseAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    Interruptible = true,
                };
            snapshot.UnitWorldState.Units[0] = unitState;

            Assert.Throws<DeterministicSimulationException>(
                () => pipeline.RestoreFromSnapshot(snapshot, 2));
        }

        [Test]
        public void Restore_RejectsActionRuntimeOutsideFrozenMatrix()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 134, 0);
            var malformed = new ActionRuntimeSetSnapshot
            {
                Main = new ActionRuntimeSlotSnapshot
                {
                    IsOccupied = true,
                    Slot = ActionSlot.Main,
                    Kind = ActionKind.Move,
                    Phase = ActionRuntimePhase.Moving,
                    OccupiedResources = ActionResource.MainAction |
                        ActionResource.Movement |
                        ActionResource.Facing,
                    Interruptible = true,
                },
                Base = ActionRuntimeSlotSnapshot.Empty,
            };

            Assert.Throws<DeterministicSimulationException>(
                () => unit.ActionRuntimes.Restore(in malformed));
        }

        [Test]
        public void AutoReleaseRuntime_CapturesAndRestoresReconciledResources()
        {
            UnitWorld world = CreateWorld(withPathGrid: true);
            UnitType unit = Spawn(world, 135, 0);
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10011,
                CastModel = new HoldReleaseCastModelDef
                {
                    Hold = new CastStage
                    {
                        StageKey = 1,
                        Def = new DelayStageDef(),
                        DurationTicks = 1,
                        Interruptible = true,
                    },
                    Release = new CastStage
                    {
                        StageKey = 2,
                        Def = new DelayStageDef(),
                        DurationTicks = 30,
                        Interruptible = false,
                        LockMovement = true,
                    },
                },
                AimKind = AimKind.Direction,
                CostPlan = default,
            });
            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                Assert.That(unit.Arbiter.Submit(
                    new CastActionRequest(
                        0,
                        AbilitySignalVerb.Focus,
                        AimSnapshot.ForDirection(
                            new fp2(fp.one, fp.zero)))).IsGranted,
                    Is.True);
                Assert.That(unit.Arbiter.Submit(
                    new MoveActionRequest(new fp2(5, 0), (fp).25m))
                    .IsGranted, Is.True);

                unit.AbilityHandler.TickUpdate();
                unit.Arbiter.RefreshRuntimeStateFromHandlers();
                Assert.That(unit.ActionRuntimes.Base.IsOccupied, Is.False);
                Assert.That(unit.ActionRuntimes.Main.OccupiedResources,
                    Is.EqualTo(ActionResource.MainAction |
                        ActionResource.Ability |
                        ActionResource.Facing));
                var pipeline = new SimulationTickPipeline(
                    world,
                    world.PhysicsWorld);
                GameplaySnapshot boundary =
                    pipeline.CaptureAggregateSnapshot();
                unit.ActionRuntimes.ClearWithoutCancel();

                Assert.DoesNotThrow(() =>
                    pipeline.RestoreFromSnapshot(boundary, 2));
                Assert.That(unit.ActionRuntimes.Main.OccupiedResources,
                    Is.EqualTo(ActionResource.MainAction |
                        ActionResource.Ability |
                        ActionResource.Facing));
            }
            finally
            {
                tick.EndTick();
            }
        }

        [Test]
        public void DualCastRuntime_RestoreReplayMatchesContinuousChecksum()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 133, 0);
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10021,
                CastModel = new CommitCastModelDef
                {
                    Cast = new CastStage
                    {
                        StageKey = 1,
                        Def = new DelayStageDef(),
                        DurationTicks = 30,
                        Interruptible = false,
                        LockMovement = true,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            InstallAbility(unit, 2, new AbilityDef
            {
                AbilityId = 10023,
                CastModel = new CommitCastModelDef
                {
                    Cast = new CastStage
                    {
                        StageKey = 1,
                        Def = new DashStageDef
                        {
                            SpeedPerTick = (fp).2m,
                            TotalDistance = (fp)3,
                        },
                        DurationTicks = 15,
                        Interruptible = false,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            var tick = new SimulationTickContextController();
            tick.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                Assert.That(unit.Arbiter.Submit(
                    new CastActionRequest(
                        0,
                        AbilitySignalVerb.Commit,
                        AimSnapshot.ForDirection(
                            new fp2(fp.one, fp.zero)))).IsGranted,
                    Is.True);
                Assert.That(unit.Arbiter.Submit(
                    new CastActionRequest(
                        2,
                        AbilitySignalVerb.Commit,
                        AimSnapshot.ForDirection(
                            new fp2(fp.zero, fp.one)))).IsGranted,
                    Is.True);
                var pipeline = new SimulationTickPipeline(
                    world,
                    world.PhysicsWorld);
                GameplaySnapshot boundary =
                    pipeline.CaptureAggregateSnapshot();

                AdvanceActionHandlers(unit);
                uint continuous = Compute(
                    pipeline.CaptureAggregateSnapshot());

                pipeline.RestoreFromSnapshot(boundary, 2);
                Assert.That(unit.ActionRuntimes.Main.AbilitySlot,
                    Is.EqualTo(0));
                Assert.That(unit.ActionRuntimes.Base.AbilitySlot,
                    Is.EqualTo(2));
                AdvanceActionHandlers(unit);
                uint replayed = Compute(
                    pipeline.CaptureAggregateSnapshot());

                Assert.That(replayed, Is.EqualTo(continuous));
            }
            finally
            {
                tick.EndTick();
            }
        }

        private static void AdvanceActionHandlers(UnitType unit)
        {
            unit.AbilityHandler.TickUpdate();
            unit.MovementHandler.TickUpdate();
            unit.AttackHandler.TickUpdate();
            unit.Arbiter.RefreshRuntimeStateFromHandlers();
        }

        private static void InstallAbility(
            UnitType unit,
            byte slot,
            AbilityDef definition)
        {
            var runtime = new AbilityRuntime
            {
                Definition = definition,
                Level = 1,
            };
            var slotRuntime = new AbilitySlotRuntime
            {
                SlotIndex = slot,
                ActiveAbilityId = definition.AbilityId,
                AllocatedPoints = 1,
                MaxAllocatedPoints = 5,
            };
            slotRuntime.AddAbility(runtime);
            unit.AbilityHandler.AddSlot(slotRuntime);
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

    }
}
