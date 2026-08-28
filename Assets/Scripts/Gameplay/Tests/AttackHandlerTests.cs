using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class AttackHandlerTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private UnitPrototype prototype;
        private Unit attacker;
        private Unit target;
        private CombatSystem combat;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
            controller.BeginTick(10, ExecutionMode.ServerAuthority);
            world = new UnitWorld
            {
                StatDefinitionTable = CreateStatTable(),
                AttackSequenceResetIntervalTicks = 3,
            };
            prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
                Name = "TestAttacker",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                BaseStats = CreateStatPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
                Loadout = HandlerLoadout.DefaultHero,
            };

            attacker = world.SpawnUnit(
                prototype, new TeamId(1), 10, fp.zero, fp.zero);
            target = world.SpawnUnit(
                prototype, new TeamId(2), 10, fp.zero, fp.zero);
            combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            combat.BeginTick();
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime()
        {
            ActionSubmitResult started = attacker.Arbiter.Submit(
                new AttackActionRequest(target.UnitUid));
            Assert.That(started.IsGranted, Is.True);
            Assert.That(attacker.AttackHandler.CurrentTargetUid,
                Is.EqualTo(target.UnitUid));
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.True);
            Assert.That(attacker.ActionRuntimes.Main.Kind,
                Is.EqualTo(ActionKind.Attack));

            world.RequestEnterDying(target);
            world.ConfirmUnitDeath(target);
            world.ApplyFormalDeathActionInvalidations(new[]
            {
                new DeathResult
                {
                    VictimUid = target.UnitUid,
                    DeathSequenceInTick = 0,
                    DeathLogicTick = 10,
                },
            });

            Assert.That(attacker.AttackHandler.CurrentTargetUid.IsValid(),
                Is.False);
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);

            var attackSnapshot = default(AttackSnapshot);
            var runtimeSnapshot = default(ActionRuntimeSetSnapshot);
            attacker.AttackHandler.Capture(ref attackSnapshot);
            attacker.ActionRuntimes.Capture(ref runtimeSnapshot);
            attacker.AttackHandler.Restore(attackSnapshot);
            attacker.ActionRuntimes.Restore(runtimeSnapshot);

            Assert.DoesNotThrow(() =>
            {
                attacker.AttackHandler.Resolve(new RollbackContext(
                    10,
                    ExecutionMode.ClientReplay));
                attacker.ActionRuntimes.Resolve();
            });
        }

        [Test]
        public void DespawnTarget_AtomicallyClearsWindupAndMainRuntime()
        {
            ActionSubmitResult started = attacker.Arbiter.Submit(
                new AttackActionRequest(target.UnitUid));
            Assert.That(started.IsGranted, Is.True);

            Assert.That(world.DespawnUnit(new UnitDespawnRequest(
                target.UnitUid,
                UnitDespawnReason.ScriptedCleanup,
                UnitDespawnMode.Destroy)), Is.True);

            Assert.That(attacker.AttackHandler.CurrentTargetUid.IsValid(),
                Is.False);
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);
        }

        [Test]
        public void FormalDeathInvalidation_DoesNotRevokeCommittedAttack()
        {
            int nextReadyTick = CommitCurrentAttack();

            world.RequestEnterDying(target);
            world.ConfirmUnitDeath(target);
            world.ApplyFormalDeathActionInvalidations(new[]
            {
                new DeathResult
                {
                    VictimUid = target.UnitUid,
                    DeathSequenceInTick = 0,
                    DeathLogicTick = 11,
                },
            });

            Assert.That(attacker.AttackHandler.ImpactCommitted, Is.True);
            Assert.That(attacker.AttackHandler.CurrentTargetUid,
                Is.EqualTo(target.UnitUid));
            Assert.That(
                attacker.AttackHandler.Snapshot.NextAttackReadyLogicTick,
                Is.EqualTo(nextReadyTick));
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);
        }

        [Test]
        public void DespawnTarget_DoesNotRevokeCommittedAttack()
        {
            UnitUid targetUid = target.UnitUid;
            int nextReadyTick = CommitCurrentAttack();

            Assert.That(world.DespawnUnit(new UnitDespawnRequest(
                targetUid,
                UnitDespawnReason.ScriptedCleanup,
                UnitDespawnMode.Destroy)), Is.True);

            Assert.That(attacker.AttackHandler.ImpactCommitted, Is.True);
            Assert.That(attacker.AttackHandler.CurrentTargetUid,
                Is.EqualTo(targetUid));
            Assert.That(
                attacker.AttackHandler.Snapshot.NextAttackReadyLogicTick,
                Is.EqualTo(nextReadyTick));
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);
        }

        [Test]
        public void FormalDeathInvalidation_RejectsNonIncreasingSequence()
        {
            world.RequestEnterDying(target);
            world.ConfirmUnitDeath(target);

            Assert.Throws<DeterministicSimulationException>(() =>
                world.ApplyFormalDeathActionInvalidations(new[]
                {
                    new DeathResult
                    {
                        VictimUid = target.UnitUid,
                        DeathSequenceInTick = 1,
                        DeathLogicTick = 10,
                    },
                    new DeathResult
                    {
                        VictimUid = target.UnitUid,
                        DeathSequenceInTick = 0,
                        DeathLogicTick = 10,
                    },
                }));
        }

        [Test]
        public void BeginAndCancel_DoNotConsumeSequence()
        {
            Assert.AreEqual(
                AttackPlanStatus.Ready,
                attacker.AttackHandler.GetAttackPlanStatus(target.UnitUid));

            attacker.AttackHandler.BeginAttack(target.UnitUid);

            Assert.AreEqual(target.UnitUid, attacker.AttackHandler.CurrentTargetUid);
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);

            attacker.AttackHandler.CancelBeforeCommit();

            Assert.IsFalse(attacker.AttackHandler.CurrentTargetUid.IsValid());
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
        }

        private int CommitCurrentAttack()
        {
            ActionSubmitResult started = attacker.Arbiter.Submit(
                new AttackActionRequest(target.UnitUid));
            Assert.That(started.IsGranted, Is.True);

            controller.EndTick();
            controller.BeginTick(11, ExecutionMode.ServerAuthority);
            combat.BeginTick();
            Assert.That(attacker.AttackHandler.CommitAttack(), Is.True);
            attacker.Arbiter.RefreshRuntimeStateFromHandlers();
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);
            return attacker.AttackHandler.Snapshot.NextAttackReadyLogicTick;
        }

        [Test]
        public void BeginAttack_LocksReadyEmpoweredPassiveIntoAnimationSnapshot()
        {
            attacker.AbilityHandler.SetFixedPassive(
                new PassiveAbilityDef
                {
                    AbilityId = 7001,
                    Name = "Test empowered attack",
                    PassiveEffect =
                        new TestEmpoweredAttackPassive(),
                    CooldownByUnitLevel =
                        new[] { 30 },
                });

            attacker.AttackHandler.BeginAttack(
                target.UnitUid);

            AttackSnapshot gameplay =
                Capture(attacker.AttackHandler);
            AttackAnimationSnapshot animation =
                attacker.AttackHandler
                    .GetAnimationSnapshot();
            Assert.IsTrue(gameplay.IsEmpoweredAttack);
            Assert.IsTrue(animation.IsEmpoweredAttack);
            Assert.AreEqual(
                gameplay.AttackStartLogicTick,
                animation.AttackStartLogicTick);

            attacker.AttackHandler.Restore(gameplay);
            Assert.IsTrue(
                attacker.AttackHandler
                    .GetAnimationSnapshot()
                    .IsEmpoweredAttack);
        }

        [Test]
        public void AttackCycle_CancelsChaseAndDoesNotResumeUntilCycleEnds()
        {
            var grid = new PathGridMap2D();
            grid.Initialise(
                new fp2((fp)(-5), (fp)(-5)),
                new fp2((fp)10, (fp)10),
                fp.one);
            attacker.Locomotion =
                new UnitLocomotionAgent(
                    attacker,
                    grid);
            attacker.StatHandler.SetStat(
                StatId.AttackSpeed,
                fp.one);
            target.PhysicsEntity.TeleportLogicPosition(
                new fp2(fp.one / (fp)2, fp.zero));
            attacker.ApplyOrder(
                Order.CreateAttack(
                    target.UnitUid,
                    true));
            controller.EndTick();
            controller.BeginTick(
                11,
                ExecutionMode.ServerAuthority);
            Assert.That(
                attacker.Locomotion.AcceptRouteRequest(
                    RouteMoveRequest.FollowUnit(
                        target.UnitUid,
                        attacker.AttackHandler.CurrentAttackRange,
                        MovePurpose.ChaseForAttack)),
                Is.EqualTo(MoveAcceptResult.Accepted));

            attacker.AttackHandler.BeginAttack(
                target.UnitUid);

            Assert.That(
                attacker.AttackHandler.IsAttackCycleActive,
                Is.True);
            Assert.That(
                attacker.AttackHandler
                    .GetAnimationSnapshot()
                    .IsAttacking,
                Is.True);
            Assert.That(
                attacker.Locomotion.CurrentTask.State,
                Is.EqualTo(MovementTaskState.Idle));
            Assert.That(
                attacker.Locomotion.Evaluate().HasMovement,
                Is.False);

            target.PhysicsEntity.TeleportLogicPosition(
                new fp2((fp)8, fp.zero));
            attacker.Planner.Tick(
                out ActionRequest duringAttack);
            Assert.That(duringAttack, Is.Null);

            controller.EndTick();
            controller.BeginTick(
                42,
                ExecutionMode.ServerAuthority);
            attacker.Planner.Tick(
                out ActionRequest afterAttack);

            Assert.That(
                attacker.AttackHandler.IsAttackCycleActive,
                Is.False);
            Assert.That(
                attacker.AttackHandler
                    .GetAnimationSnapshot()
                    .IsAttacking,
                Is.False);
            Assert.That(afterAttack,
                Is.TypeOf<MoveActionRequest>());
        }

        [Test]
        public void
            ReplaceIntent_WithMoveOrder_CancelsUncommittedAttackWindup()
        {
            // Target in range: start a windup that is not yet committed.
            target.PhysicsEntity.TeleportLogicPosition(
                new fp2(fp.one / (fp)2, fp.zero));
            attacker.StatHandler.SetStat(
                StatId.AttackSpeed,
                fp.one);
            attacker.ApplyOrder(
                Order.CreateAttack(
                    target.UnitUid,
                    true));
            attacker.AttackHandler.BeginAttack(
                target.UnitUid);

            Assert.That(
                attacker.AttackHandler
                    .IsAttackCycleActive,
                Is.True);
            Assert.That(
                attacker.AttackHandler
                    .ImpactCommitted,
                Is.False);

            // A new Move order replaces the intent: the previous behavior
            // terminates and the uncommitted windup is cancelled (Unit
            // Framework v27.3: behavior changes go through Order/Intent).
            attacker.ApplyOrder(
                Order.CreateMove(
                    new fp2((fp)5, (fp)5)));

            Assert.That(
                attacker.AttackHandler
                    .CurrentTargetUid.IsValid(),
                Is.False);
            Assert.That(
                attacker.AttackHandler
                    .IsAttackCycleActive,
                Is.False);
            Assert.That(
                attacker.AttackHandler
                    .ImpactCommitted,
                Is.False);
            Assert.That(
                attacker.Planner.CurrentIntent.Kind,
                Is.EqualTo(
                    IntentKind.MoveToPosition));
        }

        [Test]
        public void AttackRange_RetainsRawStatAndConvertsOnlyAtUseBoundary()
        {
            Assert.That(
                attacker.StatHandler.GetStat(StatId.AttackRange),
                Is.EqualTo((fp)200));
            Assert.That(
                attacker.AttackHandler.CurrentAttackRange,
                Is.EqualTo((fp)200 * (fp)0.01m));
            Assert.That(
                fpmath.abs(
                    attacker.AttackHandler.CurrentAttackRange -
                    (fp)2),
                Is.LessThan((fp)0.000001m));
        }

        [Test]
        public void AttackRange_IncludesTargetCollisionRadiusOnly()
        {
            target.PhysicsEntity.SetLogicShape(
                PhysicsShape2D.CreateCircle(fp2.zero, (fp)0.75m));
            fp reach =
                attacker.AttackHandler.CurrentAttackRange;
            fp targetRadius =
                target.PhysicsEntity.Shape.Radius;
            Assert.That(targetRadius, Is.GreaterThan(fp.zero));
            attacker.PhysicsEntity.TeleportLogicPosition(
                fp2.zero);
            target.PhysicsEntity.TeleportLogicPosition(
                new fp2(reach + targetRadius, fp.zero));

            Assert.That(
                attacker.AttackHandler.GetAttackPlanStatus(
                    target.UnitUid),
                Is.EqualTo(AttackPlanStatus.Ready),
                "The target collision boundary is exactly on attack reach.");

            target.PhysicsEntity.TeleportLogicPosition(
                new fp2(
                    reach + targetRadius +
                    (fp)0.0001m,
                    fp.zero));
            Assert.That(
                attacker.AttackHandler.GetAttackPlanStatus(
                    target.UnitUid),
                Is.EqualTo(AttackPlanStatus.OutOfRange));
        }

        [Test]
        public void AttackSpeedOnePointTwo_AtThirtyTicks_ResolvesFormalTimeline()
        {
            attacker.StatHandler.AddModifier(
                StatId.AttackSpeed,
                StatModifierOperation.FlatAdd,
                (fp)(-28.8m));
            attacker.AttackHandler.WindupRatio = (fp)0.2m;

            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot timeline = Capture(
                attacker.AttackHandler);

            fp resolvedAttackSpeed =
                attacker.StatHandler.GetStat(StatId.AttackSpeed);
            Assert.That(
                System.Math.Abs(
                    resolvedAttackSpeed.RawValue -
                    ((fp)1.2m).RawValue),
                Is.LessThanOrEqualTo(1L));
            Assert.That(
                timeline.ResolvedAttackDurationTicks,
                Is.EqualTo(25));
            Assert.That(
                timeline.ResolvedWindupTicks,
                Is.EqualTo(5));
            Assert.That(
                timeline.AttackStartLogicTick,
                Is.EqualTo(10));
            Assert.That(
                timeline.ImpactLogicTick,
                Is.EqualTo(15));
            Assert.That(
                timeline.NextAttackReadyLogicTick,
                Is.EqualTo(35));
        }

        [Test]
        public void SuccessfulImpact_SubmitsCombatAndConsumesOneSequence()
        {
            int onHitCount = 0;
            OnHitEventData onHit = default;
            CombatEvents.OnHitDealt += data =>
            {
                onHitCount++;
                onHit = data;
            };

            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot begun = Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);

            attacker.AttackHandler.TickUpdate();
            attacker.AttackHandler.TickUpdate();

            Assert.IsTrue(attacker.AttackHandler.ImpactCommitted);
            Assert.AreEqual(1, attacker.AttackHandler.AttackSequenceIndex);
            combat.SettleActiveRequests();
            Assert.AreEqual(1, combat.DamageProcessed);
            Assert.AreEqual(1, onHitCount);
            Assert.AreEqual(attacker.UnitUid, onHit.SourceUid);
            Assert.AreEqual(target.UnitUid, onHit.TargetUid);
        }

        [Test]
        public void MissingCombatOutput_CancelsWithoutConsumingSequence()
        {
            world.CombatSystem = null;
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot begun = Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);

            attacker.AttackHandler.TickUpdate();

            Assert.IsFalse(attacker.AttackHandler.ImpactCommitted);
            Assert.IsFalse(attacker.AttackHandler.CurrentTargetUid.IsValid());
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
        }

        [Test]
        public void SuccessfulSequence_WrapsFromMaxValueToZero()
        {
            AdvanceTo(11);
            var state = new AttackSnapshot
            {
                CurrentTargetUid = target.UnitUid,
                AttackStartLogicTick = 10,
                ImpactLogicTick = 11,
                NextAttackReadyLogicTick = 11,
                AttackSequenceIndex = byte.MaxValue,
                LastSuccessfulAttackLogicTick = -1,
                ResolvedAttackDurationTicks = 1,
                ResolvedWindupTicks = 1,
            };
            attacker.AttackHandler.Restore(state);

            Assert.IsTrue(attacker.AttackHandler.CommitAttack());
            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
            combat.SettleActiveRequests();
        }

        [Test]
        public void SequenceReset_IsLazyAndOccursBeforeNextBegin()
        {
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot begun = Capture(attacker.AttackHandler);
            AdvanceTo(begun.ImpactLogicTick);
            Assert.IsTrue(attacker.AttackHandler.CommitAttack());
            combat.SettleActiveRequests();
            AttackSnapshot committed = Capture(attacker.AttackHandler);
            Assert.AreEqual(1, committed.AttackSequenceIndex);

            AdvanceTo(System.Math.Max(
                committed.NextAttackReadyLogicTick,
                committed.LastSuccessfulAttackLogicTick + 3));
            attacker.AttackHandler.BeginAttack(target.UnitUid);

            Assert.AreEqual(0, attacker.AttackHandler.AttackSequenceIndex);
            Assert.IsFalse(attacker.AttackHandler.ImpactCommitted);
        }

        [Test]
        public void CaptureRestore_PreservesResolvedTimeline()
        {
            attacker.AttackHandler.WindupRatio = (fp)1 / (fp)4;
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            AttackSnapshot captured = Capture(attacker.AttackHandler);

            AttackHandler restored = UnitTestFactory.CreateAttackHandler(attacker);
            restored.Restore(captured);
            AttackSnapshot roundTrip = Capture(restored);

            Assert.AreEqual(captured.CurrentTargetUid, roundTrip.CurrentTargetUid);
            Assert.AreEqual(
                captured.ResolvedAttackDurationTicks,
                roundTrip.ResolvedAttackDurationTicks);
            Assert.AreEqual(
                captured.ResolvedWindupTicks,
                roundTrip.ResolvedWindupTicks);
            Assert.AreEqual(captured.ImpactLogicTick, roundTrip.ImpactLogicTick);
            Assert.AreEqual(
                captured.NextAttackReadyLogicTick,
                roundTrip.NextAttackReadyLogicTick);
        }

        [Test]
        public void AnimationSnapshot_AfterTickCompletion_UsesPublishedContext()
        {
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            controller.EndTick();
            try
            {
                AttackAnimationSnapshot animation =
                    attacker.AttackHandler.GetAnimationSnapshot();

                Assert.IsTrue(animation.IsAttacking);
                Assert.IsFalse(animation.ImpactCommitted);
            }
            finally
            {
                controller.BeginTick(
                    10,
                    ExecutionMode.ServerAuthority);
            }
        }

        [Test]
        public void Resolve_RejectsMissingActiveTarget()
        {
            attacker.AttackHandler.BeginAttack(target.UnitUid);
            world.UnregisterUnit(target);

            Assert.Throws<DeterministicSimulationException>(
                () => attacker.AttackHandler.Resolve(default));
        }

        [Test]
        public void Taunt_ControlAttackBypassesOnlyVoluntaryAttackBlock()
        {
            Unit voluntaryTarget = world.SpawnUnit(
                prototype,
                new TeamId(2),
                10,
                fp.zero,
                fp.zero);
            world.CrowdControlDefinitions =
                new CrowdControlDefinitionRegistry();
            var definition = UnityEngine.ScriptableObject.CreateInstance<
                CrowdControlDefinition>();
            try
            {
                var id = new CrowdControlId(9903);
                definition.Configure(
                    id,
                    CrowdControlIntensity.Medium,
                    CrowdControlDefinition.ControlTagBits.Control |
                        CrowdControlDefinition.ControlTagBits.ForcedBehavior |
                        CrowdControlDefinition.ControlTagBits.Taunt,
                    CrowdControlDurationRule.DefaultTenacity,
                    new[]
                    {
                        new CrowdControlParamAuthoring { Key = "BehaviorId", Type = CrowdControlParamType.Int, Required = true },
                        new CrowdControlParamAuthoring { Key = "Priority", Type = CrowdControlParamType.Short, Required = true },
                        new CrowdControlParamAuthoring { Key = "TargetUnit", Type = CrowdControlParamType.UnitUid, Required = true },
                    },
                    new[]
                    {
                        new CrowdControlModuleAuthoring
                        {
                            ModuleId = CrowdControlModuleId.BlockActions,
                            StaticData = (int)UnitActionBlockMask.VoluntaryAttack,
                        },
                        new CrowdControlModuleAuthoring
                        {
                            ModuleId = CrowdControlModuleId.ForcedBehavior,
                            ParamKey0 = "BehaviorId",
                            ParamKey1 = "Priority",
                            ParamKey2 = "TargetUnit",
                        },
                    });
                world.CrowdControlDefinitions.Register(definition);
                var parameters = new CrowdControlParamWriter();
                parameters.SetInt(
                    ControlParamKeys.BehaviorId,
                    (int)CrowdControlBehaviorKind.AttackTarget);
                parameters.SetShort(ControlParamKeys.Priority, 10);
                parameters.SetUnitUid(
                    ControlParamKeys.TargetUnit,
                    target.UnitUid);
                Assert.That(attacker.CrowdControl.Add(
                    id,
                    30,
                    parameters).Added, Is.True);
                attacker.RefreshCapabilityState();
                Assert.That(attacker.CapabilityState.CanAttack, Is.False);

                ActionSubmitResult voluntary = attacker.Arbiter.Submit(
                    new AttackActionRequest(voluntaryTarget.UnitUid));
                Assert.That(attacker.CrowdControl.TryGetBehaviorOverride(
                    out CrowdControlBehaviorOverride behavior), Is.True);
                Assert.That(behavior.Kind,
                    Is.EqualTo(CrowdControlBehaviorKind.AttackTarget));
                Assert.That(behavior.TargetUnitUid, Is.EqualTo(target.UnitUid));
                ActionSubmitResult forced = attacker.Arbiter.Submit(
                    new AttackActionRequest(target.UnitUid));

                Assert.That(voluntary.IsGranted, Is.False);
                Assert.That(
                    forced.IsGranted,
                    Is.True,
                    forced.RejectReason.ToString());
                Assert.That(attacker.ActionRuntimes.Main.IsControlAction, Is.True);
                attacker.AttackHandler.TickUpdate();
                Assert.That(attacker.AttackHandler.IsAttackCycleActive, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private void AdvanceTo(int tick)
        {
            controller.EndTick();
            controller.BeginTick(tick, ExecutionMode.ServerAuthority);
            combat.BeginTick();
        }

        private static AttackSnapshot Capture(AttackHandler handler)
        {
            AttackSnapshot state = default;
            handler.Capture(ref state);
            return state;
        }

        private sealed class TestEmpoweredAttackPassive :
            PassiveAbilityEffectDef
        {
            public override bool EmpowersBasicAttack => true;
        }

        private static StatDefinitionTable CreateStatTable()
        {
            var table = new StatDefinitionTable();
            AddDefinition(table, StatId.AttackDamage);
            AddDefinition(table, StatId.MaxHealth);
            AddDefinition(table, StatId.AttackSpeed);
            AddDefinition(table, StatId.AttackRange);
            AddDefinition(table, StatId.Armor);
            return table;
        }

        private static void AddDefinition(
            StatDefinitionTable table,
            StatId id)
        {
            table.Add(new StatDefinition
            {
                Id = id,
                DebugName = id.ToString(),
                DefaultBaseValue = fp.zero,
                SupportsLevelGrowth = true,
            });
        }

        private static StatPreset CreateStatPreset()
        {
            var preset = new StatPreset();
            AddStat(preset, StatId.AttackDamage, (fp)100);
            AddStat(preset, StatId.MaxHealth, (fp)500);
            AddStat(preset, StatId.AttackSpeed, (fp)30);
            AddStat(preset, StatId.AttackRange, (fp)200);
            AddStat(preset, StatId.Armor, fp.zero);
            return preset;
        }

        private static void AddStat(
            StatPreset preset,
            StatId id,
            fp value)
        {
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = id,
                BaseValue = value,
                GrowthValue = fp.zero,
            });
        }
    }
}
