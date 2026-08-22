using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class CrowdControlHandlerTests
    {
        private SimulationTickContextController tick;
        private UnitWorld world;
        private Unit unit;

        [SetUp]
        public void SetUp()
        {
            world = new UnitWorld
            {
                CrowdControlDefinitions =
                    new CrowdControlDefinitionRegistry(),
            };
            RegisterStandardDefinitions(world);
            unit = UnitTestFactory.CreateUnit(
                new UnitUid(0, 1, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
            unit.World = world;
            tick = new SimulationTickContextController();
            tick.BeginTick(
                100,
                ExecutionMode.ServerAuthority);
        }

        [TearDown]
        public void TearDown()
        {
            tick.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void Add_CreatesIndependentInstances_NoMerge()
        {
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);
            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);

            Assert.That(first.Added, Is.True);
            Assert.That(second.Added, Is.True);
            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(2));
            Assert.That(
                first.Handle.InstanceId,
                Is.Not.EqualTo(
                    second.Handle.InstanceId));
            Assert.That(
                unit.CrowdControl.State.BlockedActions,
                Is.EqualTo(
                    UnitActionBlockMask.VoluntaryMove |
                    UnitActionBlockMask.Turn |
                    UnitActionBlockMask.VoluntaryAttack |
                    UnitActionBlockMask.AbilityCast |
                    UnitActionBlockMask.Mobility |
                    UnitActionBlockMask.ControlMove |
                    UnitActionBlockMask.ControlAttack));
        }

        [Test]
        public void Immunity_BlocksLowMedium_ConsumesOneShot_BypassesHigh()
        {
            CrowdControlImmunityHandle immunity =
                unit.CrowdControl.AddImmunity(
                    new CrowdControlImmunitySpec(
                        new CrowdControlTagQuery(
                            new CrowdControlTagMask(
                                CrowdControlDefinition.ControlTagBits.Control),
                            default,
                            default),
                        100,
                        blockCount: 1,
                        priority: 0));
            Assert.That(immunity.IsValid, Is.True);

            CrowdControlAddResult blocked =
                unit.CrowdControl.Add(
                    CrowdControlIds.Slow,
                    30,
                    SlowParams((fp)0.5m));
            Assert.That(
                blocked.Status,
                Is.EqualTo(
                    CrowdControlAddStatus.BlockedByImmunity));
            Assert.That(
                blocked.BlockingImmunityId,
                Is.EqualTo(
                    immunity.ImmunityId));

            // One-shot immunity consumed: next Low/Medium control lands.
            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CrowdControlIds.Slow,
                    30,
                    SlowParams((fp)0.5m));
            Assert.That(second.Added, Is.True);

            // High (KnockBack) bypasses immunity.
            CrowdControlAddResult knockBack =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.one, fp.zero),
                        2,
                        2,
                        (short)5));
            Assert.That(knockBack.Added, Is.True);
        }

        [Test]
        public void Cleanse_RemovesMatchingNonHigh_RespectsCount()
        {
            unit.CrowdControl.Add(
                CrowdControlIds.Slow,
                30,
                SlowParams((fp)0.3m));
            unit.CrowdControl.Add(
                CrowdControlIds.Slow,
                30,
                SlowParams((fp)0.6m));
            unit.CrowdControl.Add(
                CrowdControlIds.Stun,
                30,
                default);
            unit.CrowdControl.Add(
                CrowdControlIds.KnockBack,
                2,
                KnockBackParams(
                    new fp2(fp.one, fp.zero),
                    2,
                    2,
                    (short)1));

            int removed = unit.CrowdControl.Cleanse(
                new CrowdControlCleanseSpec(
                    new CrowdControlTagQuery(
                        new CrowdControlTagMask(
                            CrowdControlDefinition.ControlTagBits.Control |
                            CrowdControlDefinition.ControlTagBits.Slow),
                        default,
                        default),
                    maxRemoveCount: 1));

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(3));
            Assert.That(
                unit.CrowdControl
                    .State.MoveSlowRatio,
                Is.EqualTo((fp)0.6m));
        }

        [Test]
        public void Unstoppable_SuppressesOutput_AndRejectsForcedMove()
        {
            unit.CrowdControl.Add(
                CrowdControlIds.Stun,
                30,
                default);
            CrowdControlUnstoppableHandle unstoppable =
                unit.CrowdControl.AddUnstoppable(
                    new CrowdControlUnstoppableSpec(50));

            Assert.That(unstoppable.IsValid, Is.True);
            Assert.That(
                unit.CrowdControl.State.BlockedActions,
                Is.EqualTo(
                    UnitActionBlockMask.None));

            CrowdControlAddResult forcedMove =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.one, fp.zero),
                        2,
                        2,
                        (short)5));
            Assert.That(
                forcedMove.Status,
                Is.EqualTo(
                    CrowdControlAddStatus.RejectedByUnstoppable));

            Assert.That(
                unit.CrowdControl.RemoveUnstoppable(
                    unstoppable),
                Is.True);
            Assert.That(
                unit.CrowdControl.State.BlockedActions,
                Is.Not.EqualTo(
                    UnitActionBlockMask.None));
        }

        [Test]
        public void DamageTakenSignal_RemovesSleepInstance()
        {
            unit.CrowdControl.Add(
                CrowdControlIds.Sleep,
                50,
                default);
            Assert.That(
                unit.CrowdControl.State.ActiveTags.HasAny(
                    new CrowdControlTagMask(
                        CrowdControlDefinition.ControlTagBits.Sleep)),
                Is.True);

            unit.CrowdControl.OnDamageTaken(
                new DamageEventData
                {
                    ActualDamage = (fp)10,
                });
            unit.CrowdControl.Advance();

            Assert.That(unit.CrowdControl.Count,
                Is.Zero);
            Assert.That(
                unit.CrowdControl.State.ActiveTags,
                Is.EqualTo(
                    CrowdControlTagMask.None));
        }

        [Test]
        public void Drowsy_OnNaturalExpire_AddsSleepWithConfiguredDuration()
        {
            unit.CrowdControl.Add(
                CrowdControlIds.Drowsy,
                20,
                DrowsyParams(
                    sleepDurationTicks: 30));
            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(1));

            // Expire the Drowsy at tick 150 (added at tick 100, duration 20).
            tick.EndTick();
            tick = new SimulationTickContextController();
            tick.BeginTick(
                150,
                ExecutionMode.ServerAuthority);
            unit.CrowdControl.Advance();

            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(1));
            Assert.That(
                unit.CrowdControl.State.ActiveTags.HasAny(
                    new CrowdControlTagMask(
                        CrowdControlDefinition.ControlTagBits.Sleep)),
                Is.True);

            var instances =
                new System.Collections.Generic.List<
                    CrowdControlInstance>();
            unit.CrowdControl.FillInstances(instances);
            Assert.That(instances.Count,
                Is.EqualTo(1));
            Assert.That(instances[0].ControlId,
                Is.EqualTo(CrowdControlIds.Sleep));
            Assert.That(
                instances[0].ExpireTick -
                SimulationTickContext.Current.Tick,
                Is.EqualTo(30));
        }

        [Test]
        public void Tenacity_ShortensDefaultDuration_IgnoredByIgnoreRule()
        {
            unit.StatHandler.SetStat(
                StatId.Tenacity,
                (fp)0.5m);

            CrowdControlAddResult stun =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    10,
                    default);
            CrowdControlAddResult knockBack =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    10,
                    KnockBackParams(
                        new fp2(fp.one, fp.zero),
                        2,
                        10,
                        (short)1));

            Assert.That(
                unit.CrowdControl.GetRemainingTicks(
                    stun.Handle),
                Is.EqualTo(5));
            Assert.That(
                unit.CrowdControl.GetRemainingTicks(
                    knockBack.Handle),
                Is.EqualTo(10));
        }

        [Test]
        public void ForcedMove_PriorityArbitration_RejectsLower_ReplacesEqual()
        {
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.one, fp.zero),
                        2,
                        2,
                        (short)7));
            CrowdControlAddResult lower =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.zero, fp.one),
                        2,
                        2,
                        (short)3));

            Assert.That(first.Added, Is.True);
            Assert.That(
                lower.Status,
                Is.EqualTo(
                    CrowdControlAddStatus.RejectedByHigherPriority));
            Assert.That(
                unit.CrowdControl.ActiveForcedMoveHandle,
                Is.EqualTo(first.Handle));

            CrowdControlAddResult equal =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.zero, fp.one),
                        2,
                        2,
                        (short)7));
            Assert.That(equal.Added, Is.True);
            Assert.That(
                unit.CrowdControl.ActiveForcedMoveHandle,
                Is.EqualTo(equal.Handle));
            Assert.That(
                unit.CrowdControl.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void ForcedBehavior_WinnerSelected_AndConsumedByPlanner()
        {
            Unit other = UnitTestFactory.CreateUnit(
                new UnitUid(0, 2, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
            other.World = world;

            unit.CrowdControl.Add(
                CrowdControlIds.Taunt,
                30,
                TauntParams(
                    other.UnitUid,
                    priority: 5));
            Assert.That(
                unit.CrowdControl
                    .TryGetBehaviorOverride(
                        out CrowdControlBehaviorOverride taunt),
                Is.True);
            Assert.That(
                taunt.Kind,
                Is.EqualTo(
                    CrowdControlBehaviorKind.AttackTarget));
            Assert.That(
                taunt.TargetUnitUid,
                Is.EqualTo(other.UnitUid));

            unit.CrowdControl.Add(
                CrowdControlIds.Fear,
                30,
                FearParams(
                    new fp2(fp.one, fp.zero),
                    priority: 9));
            Assert.That(
                unit.CrowdControl
                    .TryGetBehaviorOverride(
                        out CrowdControlBehaviorOverride fear),
                Is.True);
            Assert.That(
                fear.Kind,
                Is.EqualTo(
                    CrowdControlBehaviorKind.FleeDirection));

            unit.Planner.SetIntent(default);
            unit.Planner.Tick(
                out ActionRequest request);
            Assert.That(
                request,
                Is.TypeOf<MoveActionRequest>());
            var move = (MoveActionRequest)request;
            Assert.That(
                move.Purpose,
                Is.EqualTo(
                    MovePurpose.ControlMove));
        }

        [Test]
        public void ForcedMoveBehavior_BypassesVoluntaryCapabilityBlock()
        {
            var id = new CrowdControlId(9902);
            var definition = ScriptableObject.CreateInstance<
                CrowdControlDefinition>();
            try
            {
                definition.Configure(
                    id,
                    CrowdControlIntensity.Medium,
                    CrowdControlDefinition.ControlTagBits.Control |
                    CrowdControlDefinition.ControlTagBits.ForcedBehavior,
                    CrowdControlDurationRule.DefaultTenacity,
                    new[]
                    {
                        new CrowdControlParamAuthoring { Key = "BehaviorId", Type = CrowdControlParamType.Int, Required = true },
                        new CrowdControlParamAuthoring { Key = "Priority", Type = CrowdControlParamType.Short, Required = true },
                        new CrowdControlParamAuthoring { Key = "Direction", Type = CrowdControlParamType.Fp2, Required = true },
                    },
                    new[]
                    {
                        new CrowdControlModuleAuthoring
                        {
                            ModuleId = CrowdControlModuleId.BlockActions,
                            StaticData = (int)UnitActionBlockMask.VoluntaryMove,
                        },
                        new CrowdControlModuleAuthoring
                        {
                            ModuleId = CrowdControlModuleId.ForcedBehavior,
                            ParamKey0 = "BehaviorId",
                            ParamKey1 = "Priority",
                            ParamKey3 = "Direction",
                        },
                    });
                world.CrowdControlDefinitions.Register(definition);
                Assert.That(unit.CrowdControl.Add(
                    id,
                    30,
                    FearParams(new fp2(fp.one, fp.zero), 10)).Added,
                    Is.True);
                unit.RefreshCapabilityState();
                Assert.That(unit.CapabilityState.CanMove, Is.False);
                unit.Planner.Tick(out ActionRequest request);
                Assert.That(request, Is.TypeOf<MoveActionRequest>());

                ActionSubmitResult result = unit.Arbiter.Submit(request);

                Assert.That(result.IsGranted, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Snapshot_RoundTrip_PreservesInstancesAndState()
        {
            unit.CrowdControl.Add(
                CrowdControlIds.Stun,
                30,
                default);
            unit.CrowdControl.Add(
                CrowdControlIds.Slow,
                30,
                SlowParams((fp)0.4m));

            CrowdControlHandlerSnapshot snapshot =
                default;
            unit.CrowdControl.Capture(
                ref snapshot);

            unit.CrowdControl.ClearForDeath();
            Assert.That(unit.CrowdControl.Count,
                Is.Zero);

            unit.CrowdControl.Restore(snapshot);
            unit.CrowdControl.Rebuild(default);

            Assert.That(unit.CrowdControl.Count,
                Is.EqualTo(2));
            Assert.That(
                unit.CrowdControl.State.MoveSlowRatio,
                Is.EqualTo((fp)0.4m));
            Assert.That(
                unit.CrowdControl.State.BlockedActions,
                Is.Not.EqualTo(
                    UnitActionBlockMask.None));
        }

        [Test]
        public void
            Restore_ValidatesRestoredForcedMoveHandleNotPreRestoreHandle()
        {
            // Runtime owns forced move A when the snapshot is captured.
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.one, fp.zero),
                        (fp)3m,
                        2,
                        100));
            Assert.That(first.Added, Is.True);
            CrowdControlHandle firstHandle =
                first.Handle;
            Assert.That(
                unit.CrowdControl.ActiveForcedMoveHandle,
                Is.EqualTo(firstHandle));

            CrowdControlHandlerSnapshot snapshot =
                default;
            unit.CrowdControl.Capture(
                ref snapshot);
            Assert.That(
                snapshot.ActiveForcedMoveHandle,
                Is.EqualTo(firstHandle));

            // Before the rollback the runtime owns a DIFFERENT forced move B
            // that the snapshot does not contain.
            unit.CrowdControl.ClearForDeath();
            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.zero, fp.one),
                        (fp)3m,
                        2,
                        100));
            Assert.That(second.Added, Is.True);
            Assert.That(
                second.Handle.InstanceId,
                Is.Not.EqualTo(
                    firstHandle.InstanceId));

            // Restore validates the RESTORED handle (A) against the restored
            // instance list; the pre-restore handle B is irrelevant and must
            // not fail the rollback.
            Assert.DoesNotThrow(
                () => unit.CrowdControl.Restore(
                    snapshot));
            Assert.That(
                unit.CrowdControl.ActiveForcedMoveHandle,
                Is.EqualTo(firstHandle));
            Assert.That(
                unit.CrowdControl.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void
            Restore_ThrowsWhenSnapshotForcedMoveHandleMissingFromInstances()
        {
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.KnockBack,
                    2,
                    KnockBackParams(
                        new fp2(fp.one, fp.zero),
                        (fp)3m,
                        2,
                        100));
            Assert.That(first.Added, Is.True);

            CrowdControlHandlerSnapshot snapshot =
                default;
            unit.CrowdControl.Capture(
                ref snapshot);
            // Corrupt the snapshot: the active forced-move handle now points
            // at an instance that is absent from the restored list. The
            // canonical-state validation must reject this inconsistent state.
            snapshot.Instances.Clear();

            Assert.Throws<
                DeterministicSimulationException>(
                () => unit.CrowdControl.Restore(
                    snapshot));
        }

        [Test]
        public void Lifecycle_DeathClearsButKeepsIds_InitResetsIds()
        {
            CrowdControlAddResult first =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);

            unit.CrowdControl.ClearForDeath();
            Assert.That(unit.CrowdControl.Count,
                Is.Zero);

            CrowdControlAddResult second =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);
            Assert.That(
                second.Handle.InstanceId,
                Is.GreaterThan(
                    first.Handle.InstanceId));

            unit.CrowdControl.ClearForDeath();
            unit.CrowdControl.InitializeForNewRuntime();
            CrowdControlAddResult third =
                unit.CrowdControl.Add(
                    CrowdControlIds.Stun,
                    30,
                    default);
            Assert.That(
                third.Handle.InstanceId,
                Is.EqualTo(1));
        }

        private static void RegisterStandardDefinitions(
            UnitWorld world)
        {
            Register(
                world,
                CrowdControlIds.Stun,
                CrowdControlIntensity.Medium,
                CrowdControlDefinition.ControlTagBits.Control,
                CrowdControlDurationRule.DefaultTenacity,
                null,
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId = CrowdControlModuleId.BlockActions,
                        StaticData = (int)(UnitActionBlockMask.VoluntaryMove | UnitActionBlockMask.Turn | UnitActionBlockMask.VoluntaryAttack | UnitActionBlockMask.AbilityCast | UnitActionBlockMask.Mobility | UnitActionBlockMask.ControlMove | UnitActionBlockMask.ControlAttack),
                    },
                });
            Register(
                world,
                CrowdControlIds.Slow,
                CrowdControlIntensity.Low,
                CrowdControlDefinition.ControlTagBits.Control |
                CrowdControlDefinition.ControlTagBits.Slow,
                CrowdControlDurationRule.DefaultTenacity,
                new[]
                {
                    new CrowdControlParamAuthoring { Key = "MoveSlowRatio", Type = CrowdControlParamType.Fp, Required = true },
                },
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId = CrowdControlModuleId.MaxMoveSlow,
                        ParamKey0 = "MoveSlowRatio",
                    },
                });
            Register(
                world,
                CrowdControlIds.Sleep,
                CrowdControlIntensity.Medium,
                CrowdControlDefinition.ControlTagBits.Control |
                CrowdControlDefinition.ControlTagBits.Sleep,
                CrowdControlDurationRule.DefaultTenacity,
                null,
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId = CrowdControlModuleId.BlockActions,
                        StaticData = (int)(UnitActionBlockMask.VoluntaryMove | UnitActionBlockMask.VoluntaryAttack | UnitActionBlockMask.AbilityCast | UnitActionBlockMask.ControlMove | UnitActionBlockMask.ControlAttack),
                    },
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId = CrowdControlModuleId.RemoveOnSignal,
                        StaticData = 1 << (int)CrowdControlSignalType.ActualDamageTaken,
                    },
                });
            Register(
                world,
                CrowdControlIds.KnockBack,
                CrowdControlIntensity.High,
                CrowdControlDefinition.ControlTagBits.Control |
                CrowdControlDefinition.ControlTagBits.ForcedMove |
                CrowdControlDefinition.ControlTagBits.Displacement |
                CrowdControlDefinition.ControlTagBits.Airborne,
                CrowdControlDurationRule.IgnoreTenacity,
                new[]
                {
                    new CrowdControlParamAuthoring { Key = "Direction", Type = CrowdControlParamType.Fp2, Required = true },
                    new CrowdControlParamAuthoring { Key = "Distance", Type = CrowdControlParamType.Fp, Required = true },
                    new CrowdControlParamAuthoring { Key = "MoveTicks", Type = CrowdControlParamType.Int, Required = true },
                    new CrowdControlParamAuthoring { Key = "ForcedMovePriority", Type = CrowdControlParamType.Short, Required = true },
                },
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId = CrowdControlModuleId.ForcedMoveOnAdd,
                        ParamKey0 = "Direction",
                        ParamKey1 = "Distance",
                        ParamKey2 = "MoveTicks",
                        StaticData = 1,
                    },
                });
            Register(
                world,
                CrowdControlIds.Taunt,
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
                        ModuleId = CrowdControlModuleId.ForcedBehavior,
                        ParamKey0 = "BehaviorId",
                        ParamKey1 = "Priority",
                        ParamKey2 = "TargetUnit",
                    },
                });
            Register(
                world,
                CrowdControlIds.Fear,
                CrowdControlIntensity.Medium,
                CrowdControlDefinition.ControlTagBits.Control |
                CrowdControlDefinition.ControlTagBits.ForcedBehavior |
                CrowdControlDefinition.ControlTagBits.Fear,
                CrowdControlDurationRule.DefaultTenacity,
                new[]
                {
                    new CrowdControlParamAuthoring { Key = "BehaviorId", Type = CrowdControlParamType.Int, Required = true },
                    new CrowdControlParamAuthoring { Key = "Priority", Type = CrowdControlParamType.Short, Required = true },
                    new CrowdControlParamAuthoring { Key = "Direction", Type = CrowdControlParamType.Fp2, Required = true },
                },
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId = CrowdControlModuleId.ForcedBehavior,
                        ParamKey0 = "BehaviorId",
                        ParamKey1 = "Priority",
                        ParamKey3 = "Direction",
                    },
                });
            Register(
                world,
                CrowdControlIds.Drowsy,
                CrowdControlIntensity.Low,
                CrowdControlDefinition.ControlTagBits.Control |
                CrowdControlDefinition.ControlTagBits.Drowsy,
                CrowdControlDurationRule.DefaultTenacity,
                new[]
                {
                    new CrowdControlParamAuthoring
                    {
                        Key = "SleepDurationTicks",
                        Type = CrowdControlParamType.Int,
                        Required = true,
                    },
                },
                new[]
                {
                    new CrowdControlModuleAuthoring
                    {
                        ModuleId =
                            CrowdControlModuleId.AddControlOnNaturalExpire,
                        ParamKey0 = "SleepDurationTicks",
                        StaticData =
                            CrowdControlIds.Sleep.Value,
                    },
                });
        }

        private static void Register(
            UnitWorld world,
            CrowdControlId id,
            CrowdControlIntensity intensity,
            ulong tags,
            CrowdControlDurationRule rule,
            CrowdControlParamAuthoring[] schema,
            CrowdControlModuleAuthoring[] modules)
        {
            var definition =
                ScriptableObject.CreateInstance<
                    CrowdControlDefinition>();
            definition.Configure(
                id,
                intensity,
                tags,
                rule,
                schema,
                modules);
            world.CrowdControlDefinitions.Register(
                definition);
        }

        private static CrowdControlParamWriter SlowParams(
            fp ratio)
        {
            var parameters =
                new CrowdControlParamWriter();
            parameters.SetFp(
                ControlParamKeys.MoveSlowRatio,
                ratio);
            return parameters;
        }

        private static CrowdControlParamWriter KnockBackParams(
            fp2 direction,
            fp distance,
            int moveTicks,
            short priority)
        {
            var parameters =
                new CrowdControlParamWriter();
            parameters.SetFp2(
                ControlParamKeys.Direction,
                direction);
            parameters.SetFp(
                ControlParamKeys.Distance,
                distance);
            parameters.SetInt(
                ControlParamKeys.MoveTicks,
                moveTicks);
            parameters.SetShort(
                ControlParamKeys.ForcedMovePriority,
                priority);
            return parameters;
        }

        private static CrowdControlParamWriter TauntParams(
            UnitUid target,
            short priority)
        {
            var parameters =
                new CrowdControlParamWriter();
            parameters.SetInt(
                ControlParamKeys.BehaviorId,
                (int)
                CrowdControlBehaviorKind.AttackTarget);
            parameters.SetShort(
                ControlParamKeys.Priority,
                priority);
            parameters.SetUnitUid(
                ControlParamKeys.TargetUnit,
                target);
            return parameters;
        }

        private static CrowdControlParamWriter FearParams(
            fp2 direction,
            short priority)
        {
            var parameters =
                new CrowdControlParamWriter();
            parameters.SetInt(
                ControlParamKeys.BehaviorId,
                (int)
                CrowdControlBehaviorKind.FleeDirection);
            parameters.SetShort(
                ControlParamKeys.Priority,
                priority);
            parameters.SetFp2(
                ControlParamKeys.Direction,
                direction);
            return parameters;
        }

        private static CrowdControlParamWriter DrowsyParams(
            int sleepDurationTicks)
        {
            var parameters =
                new CrowdControlParamWriter();
            parameters.SetInt(
                ControlParamKeys.SleepDurationTicks,
                sleepDurationTicks);
            return parameters;
        }
    }
}
