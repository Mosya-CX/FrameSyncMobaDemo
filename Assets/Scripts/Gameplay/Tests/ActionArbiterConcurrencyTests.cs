using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class ActionArbiterConcurrencyTests
    {
        private SimulationTickContextController _tick;
        private bool _tickBegan;

        [SetUp]
        public void SetUp()
        {
            _tick = new SimulationTickContextController();
        }

        [TearDown]
        public void TearDown()
        {
            if (_tickBegan)
                _tick.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void LockedMainCast_AllowsAuthoredDashInBaseSlot()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
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
                        LockMovement = false,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });

            ActionSubmitResult q = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));
            ActionSubmitResult e = unit.Arbiter.Submit(
                new CastActionRequest(
                    2,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.zero, fp.one))));

            Assert.That(q.IsGranted, Is.True);
            Assert.That(e.IsGranted, Is.True);
            Assert.That(unit.ActionRuntimes.Main.Kind, Is.EqualTo(ActionKind.Cast));
            Assert.That(unit.ActionRuntimes.Main.AbilitySlot, Is.EqualTo(0));
            Assert.That(unit.ActionRuntimes.Main.BlocksVoluntaryMove, Is.True);
            Assert.That(unit.ActionRuntimes.Base.Kind, Is.EqualTo(ActionKind.Cast));
            Assert.That(unit.ActionRuntimes.Base.AbilitySlot, Is.EqualTo(2));
            Assert.That(unit.MovementHandler.IsDashActive(10023), Is.True);
        }

        [Test]
        public void MovableHold_AllowsMove_ReleasePreemptsMove()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10011,
                CastModel = new HoldReleaseCastModelDef
                {
                    Hold = new CastStage
                    {
                        StageKey = 1,
                        Def = new DelayStageDef(),
                        DurationTicks = 120,
                        Interruptible = true,
                        LockMovement = false,
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
                CastRange = fp.zero,
                CostPlan = default,
            });

            ActionSubmitResult hold = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));
            ActionSubmitResult move = unit.Arbiter.Submit(
                new MoveActionRequest(new fp2(5, 0), (fp).25m));

            Assert.That(hold.IsGranted, Is.True);
            Assert.That(move.IsGranted, Is.True);
            Assert.That(unit.ActionRuntimes.Main.AbilitySlot, Is.EqualTo(0));
            Assert.That(unit.ActionRuntimes.Main.BlocksVoluntaryMove, Is.False);
            Assert.That(unit.ActionRuntimes.Base.Kind, Is.EqualTo(ActionKind.Move));

            ActionSubmitResult release = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));

            Assert.That(release.IsGranted, Is.True);
            Assert.That(release.Outcome,
                Is.EqualTo(ActionSubmitOutcome.GrantedWithPreemption));
            Assert.That(unit.ActionRuntimes.Main.BlocksVoluntaryMove, Is.True);
            Assert.That(unit.ActionRuntimes.Base.IsOccupied, Is.False);
            Assert.That(unit.Locomotion.CurrentTask.State,
                Is.EqualTo(MovementTaskState.Idle));
        }

        [Test]
        public void HoldTimeout_ReconcilesReleaseResourcesAndRestores()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10011,
                CastModel = new HoldReleaseCastModelDef
                {
                    Hold = Stage(1, 1, lockMovement: false),
                    Release = Stage(2, 30, lockMovement: true),
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))))
                .IsGranted, Is.True);
            Assert.That(unit.Arbiter.Submit(
                new MoveActionRequest(new fp2(5, 0), (fp).25m))
                .IsGranted, Is.True);

            unit.AbilityHandler.TickUpdate();
            unit.Arbiter.RefreshRuntimeStateFromHandlers();

            Assert.That(unit.ActionRuntimes.Main.OccupiedResources,
                Is.EqualTo(ActionResource.MainAction |
                    ActionResource.Ability |
                    ActionResource.Facing));
            Assert.That(unit.ActionRuntimes.Main.BlocksVoluntaryMove, Is.True);
            Assert.That(unit.ActionRuntimes.Base.IsOccupied, Is.False);
        }

        [Test]
        public void SameAbilityStageTransition_MigratesMainToBaseSlot()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10011,
                CastModel = new HoldReleaseCastModelDef
                {
                    Hold = Stage(1, 30, lockMovement: false),
                    Release = new CastStage
                    {
                        StageKey = 2,
                        Def = new DashStageDef
                        {
                            SpeedPerTick = (fp).2m,
                            TotalDistance = (fp)3,
                        },
                        DurationTicks = 15,
                        Interruptible = true,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))))
                .IsGranted, Is.True);
            Assert.That(unit.ActionRuntimes.Main.AbilitySlot, Is.EqualTo(0));
            Assert.That(unit.Arbiter.Submit(
                new MoveActionRequest(new fp2(5, 0), (fp).25m))
                .IsGranted, Is.True);

            ActionSubmitResult release = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));

            Assert.That(release.IsGranted, Is.True);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.False);
            Assert.That(unit.ActionRuntimes.Base.Kind, Is.EqualTo(ActionKind.Cast));
            Assert.That(unit.ActionRuntimes.Base.AbilitySlot, Is.EqualTo(0));
            Assert.That(unit.MovementHandler.IsDashActive(10011), Is.True);
        }

        [Test]
        public void AutomaticDashTransition_MigratesWithoutCancellingSession()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10011,
                CastModel = new HoldReleaseCastModelDef
                {
                    Hold = Stage(1, 1, lockMovement: false),
                    Release = new CastStage
                    {
                        StageKey = 2,
                        Def = new DashStageDef
                        {
                            SpeedPerTick = (fp).2m,
                            TotalDistance = (fp)3,
                        },
                        DurationTicks = 15,
                        Interruptible = true,
                    },
                },
                AimKind = AimKind.Direction,
                CostPlan = default,
            });
            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))))
                .IsGranted, Is.True);

            unit.AbilityHandler.TickUpdate();
            unit.Arbiter.RefreshRuntimeStateFromHandlers();

            Assert.That(unit.AbilityHandler.HasActiveSession(0), Is.True);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.False);
            Assert.That(unit.ActionRuntimes.Base.Kind, Is.EqualTo(ActionKind.Cast));
            Assert.That(unit.ActionRuntimes.Base.AbilitySlot, Is.EqualTo(0));
            Assert.That(unit.MovementHandler.IsDashActive(10011), Is.True);
        }

        [Test]
        public void SequentialRecastWindow_ReleasesMainRuntimeButKeepsSession()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            var model = new SequentialRecastCastModelDef
            {
                FirstImpact = Stage(1, 1, lockMovement: true),
                FirstRecastWindow = Stage(2, 120, lockMovement: false),
                SecondImpact = Stage(3, 1, lockMovement: true),
                SecondRecastWindow = Stage(4, 120, lockMovement: false),
                FinalImpact = Stage(5, 1, lockMovement: true),
            };
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10021,
                CastModel = model,
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });

            ActionSubmitResult result = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));
            Assert.That(result.IsGranted, Is.True);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.True);

            unit.AbilityHandler.TickUpdate();
            unit.Arbiter.RefreshRuntimeStateFromHandlers();

            Assert.That(unit.AbilityHandler.HasActiveSession(0), Is.True);
            Assert.That(unit.AbilityHandler.IsActionStageActive(0), Is.False);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.False);
        }

        [Test]
        public void Planner_DoesNotResubmitEquivalentActiveMove()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            unit.ReplaceIntent(new UnitIntent
            {
                Kind = IntentKind.MoveToPosition,
                TargetPosition = new fp2(5, 0),
                AllowReplan = true,
            });

            unit.Planner.Tick(out ActionRequest first);
            Assert.That(first, Is.TypeOf<MoveActionRequest>());
            Assert.That(unit.Arbiter.Submit(first).IsGranted, Is.True);

            unit.Planner.Tick(out ActionRequest repeated);

            Assert.That(repeated, Is.Null);
            Assert.That(unit.Locomotion.CurrentTask.State,
                Is.EqualTo(MovementTaskState.Active));
        }

        [Test]
        public void MoveReplacement_AtomicallyKeepsNewRoute()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            Assert.That(unit.Arbiter.Submit(
                new MoveActionRequest(new fp2(3, 0), (fp).25m))
                .IsGranted, Is.True);

            ActionSubmitResult replacement = unit.Arbiter.Submit(
                new MoveActionRequest(new fp2(0, 6), (fp).25m));

            Assert.That(replacement.Outcome,
                Is.EqualTo(ActionSubmitOutcome.GrantedWithPreemption));
            Assert.That(unit.Locomotion.CurrentTask.Target.Position,
                Is.EqualTo(new fp2(0, 6)));
            Assert.That(unit.ActionRuntimes.Base.Kind,
                Is.EqualTo(ActionKind.Move));
        }

        [Test]
        public void FailedLockingCast_DoesNotDestroyExistingMove()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 1, new AbilityDef
            {
                AbilityId = 10012,
                CastModel = new CommitCastModelDef
                {
                    Cast = Stage(1, 30, lockMovement: true),
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = new AbilityCostPlan(
                    new AbilityLevelValue(new[] { (fp)10 }),
                    default,
                    AbilityCostTiming.OnSessionStart),
            });
            Assert.That(unit.Arbiter.Submit(
                    new MoveActionRequest(new fp2(5, 0), (fp).25m))
                .IsGranted, Is.True);

            ActionSubmitResult rejected = unit.Arbiter.Submit(
                new CastActionRequest(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));

            Assert.That(rejected.IsGranted, Is.False);
            Assert.That(rejected.RejectReason,
                Is.EqualTo(ActionRejectReason.HandlerRejected));
            Assert.That(unit.ActionRuntimes.Base.Kind,
                Is.EqualTo(ActionKind.Move));
            Assert.That(unit.Locomotion.CurrentTask.State,
                Is.EqualTo(MovementTaskState.Active));
        }

        [Test]
        public void ToggleSecondCommit_EndsSessionWithoutOwningRuntime()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallAbility(unit, 1, new AbilityDef
            {
                AbilityId = 10012,
                CastModel = new ToggleCastModelDef
                {
                    Active = Stage(1, 120, lockMovement: false),
                    ResourcePerTick = fp.zero,
                },
                AimKind = AimKind.None,
                CastRange = fp.zero,
                CostPlan = default,
            });

            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None)).IsGranted, Is.True);
            Assert.That(unit.AbilityHandler.HasActiveSession(1), Is.True);
            Assert.That(unit.AbilityHandler.IsActionStageActive(1), Is.False);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.False);

            ActionSubmitResult off = unit.Arbiter.Submit(
                new CastActionRequest(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None));

            Assert.That(off.IsGranted, Is.True);
            Assert.That(unit.AbilityHandler.HasActiveSession(1), Is.False);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.False);
        }

        [Test]
        public void ToggleActive_DoesNotBlockChargeStartAndIsConsumed()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallVarusHoldAndToggle(unit);

            ActionSubmitResult toggleOn = unit.Arbiter.Submit(
                new CastActionRequest(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None));
            ActionSubmitResult hold = unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))));

            Assert.That(toggleOn.IsGranted, Is.True);
            Assert.That(hold.IsGranted, Is.True);
            Assert.That(unit.AbilityHandler.HasActiveSession(1), Is.False);
            Assert.That(unit.AbilityHandler.HasActiveSession(0), Is.True);
            Assert.That(unit.AbilityHandler.GetActiveRuntime(1).CooldownEndsAtTick,
                Is.EqualTo(1221));
            Assert.That(unit.ActionRuntimes.Main.Kind,
                Is.EqualTo(ActionKind.Cast));
            Assert.That(unit.ActionRuntimes.Main.AbilitySlot, Is.EqualTo(0));
        }

        [Test]
        public void ToggleOnAndOffDuringHold_DoNotPreemptHold()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
            InstallVarusHoldAndToggle(unit);

            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Focus,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))))
                .IsGranted, Is.True);
            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None)).IsGranted, Is.True);
            Assert.That(unit.ActionRuntimes.Main.AbilitySlot, Is.EqualTo(0));
            Assert.That(unit.AbilityHandler.HasActiveSession(0), Is.True);

            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    1,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.None)).IsGranted, Is.True);

            Assert.That(unit.AbilityHandler.HasActiveSession(1), Is.False);
            Assert.That(unit.AbilityHandler.HasActiveSession(0), Is.True);
            Assert.That(unit.ActionRuntimes.Main.Kind,
                Is.EqualTo(ActionKind.Cast));
            Assert.That(unit.ActionRuntimes.Main.AbilitySlot, Is.EqualTo(0));
        }

        [Test]
        public void MobilityBlock_InterruptsAbilityDash()
        {
            UnitType unit = CreateUnit(withPathGrid: false);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
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
                        Interruptible = true,
                    },
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    2,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))))
                .IsGranted, Is.True);

            CrowdControlDefinition definition =
                ScriptableObject.CreateInstance<CrowdControlDefinition>();
            try
            {
                unit.World.CrowdControlDefinitions =
                    new CrowdControlDefinitionRegistry();
                var id = new CrowdControlId(9901);
                definition.Configure(
                    id,
                    CrowdControlIntensity.Medium,
                    CrowdControlDefinition.ControlTagBits.Control,
                    CrowdControlDurationRule.DefaultTenacity,
                    null,
                    new[]
                    {
                        new CrowdControlModuleAuthoring
                        {
                            ModuleId = CrowdControlModuleId.BlockActions,
                            StaticData = (int)UnitActionBlockMask.Mobility,
                        },
                    });
                unit.World.CrowdControlDefinitions.Register(definition);
                Assert.That(unit.CrowdControl.Add(id, 30, default).Added,
                    Is.True);
                unit.RefreshCapabilityState();

                unit.Arbiter.EvaluateCurrentRuntimes();

                Assert.That(unit.ActionRuntimes.Base.IsOccupied, Is.False);
                Assert.That(unit.MovementHandler.IsDashActive(10023), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ControlMove_PreemptsUninterruptibleMovementLockedCast()
        {
            UnitType unit = CreateUnit(withPathGrid: true);
            _tick.BeginTick(21, ExecutionMode.ServerAuthority);
            _tickBegan = true;
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
                CostPlan = default,
            });
            Assert.That(unit.Arbiter.Submit(
                new CastActionRequest(
                    0,
                    AbilitySignalVerb.Commit,
                    AimSnapshot.ForDirection(new fp2(fp.one, fp.zero))))
                .IsGranted, Is.True);

            ActionSubmitResult forced = unit.Arbiter.Submit(
                new MoveActionRequest(
                    new fp2(5, 0),
                    (fp).25m,
                    purpose: MovePurpose.ControlMove));

            Assert.That(forced.IsGranted, Is.True);
            Assert.That(forced.Outcome,
                Is.EqualTo(ActionSubmitOutcome.GrantedWithPreemption));
            Assert.That(unit.AbilityHandler.HasActiveSession(0), Is.False);
            Assert.That(unit.ActionRuntimes.Main.IsOccupied, Is.False);
            Assert.That(unit.ActionRuntimes.Base.Kind, Is.EqualTo(ActionKind.Move));
            Assert.That(unit.ActionRuntimes.Base.IsControlAction, Is.True);
        }

        private static CastStage Stage(
            byte key,
            int duration,
            bool lockMovement)
        {
            return new CastStage
            {
                StageKey = key,
                Def = new DelayStageDef(),
                DurationTicks = duration,
                Interruptible = true,
                LockMovement = lockMovement,
            };
        }

        private static UnitType CreateUnit(bool withPathGrid)
        {
            var world = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
            };
            if (withPathGrid)
            {
                world.PathGrid = new PathGridMap2D();
                world.PathGrid.Initialise(
                    new fp2(-10, -10),
                    new fp2(20, 20),
                    fp.one);
            }
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = withPathGrid ? 981 : 980,
                RuntimeEntityPrefabId = withPathGrid ? 981 : 980,
                UnitKind = UnitKind.Hero,
                Loadout = HandlerLoadout.DefaultHero,
                BaseStats = new StatPreset(),
            };
            return UnitTestFactory.SpawnUnit(
                world,
                prototype,
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
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

        private static void InstallVarusHoldAndToggle(UnitType unit)
        {
            InstallAbility(unit, 0, new AbilityDef
            {
                AbilityId = 10011,
                CastModel = new HoldReleaseCastModelDef
                {
                    Hold = new CastStage
                    {
                        StageKey = 1,
                        Def = new ChargeStageDef
                        {
                            ChargeRatioBlackboardKeyId = 1,
                            MaxChargeTicks = 45,
                            ConsumeToggleSlot = 1,
                            ConsumeToggleCooldownTicks = 1200,
                            EmpoweredBlackboardKeyId = 2,
                        },
                        DurationTicks = 120,
                        Interruptible = false,
                        LockMovement = false,
                    },
                    Release = Stage(2, 1, lockMovement: true),
                },
                AimKind = AimKind.Direction,
                CastRange = fp.zero,
                CostPlan = default,
            });
            InstallAbility(unit, 1, new AbilityDef
            {
                AbilityId = 10012,
                CastModel = new ToggleCastModelDef
                {
                    Active = new CastStage
                    {
                        StageKey = 1,
                        Def = new DelayStageDef(),
                        DurationTicks = 360000,
                        Interruptible = false,
                        LockMovement = false,
                    },
                    ResourcePerTick = fp.zero,
                },
                AimKind = AimKind.None,
                CastRange = fp.zero,
                CostPlan = default,
            });
        }
    }
}
