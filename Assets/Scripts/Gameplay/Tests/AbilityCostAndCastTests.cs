using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class AbilityCostAndCastTests
    {
        private SimulationTickContextController controller;
        private UnitWorld world;
        private Unit caster;

        [SetUp]
        public void SetUp()
        {
            controller =
                new SimulationTickContextController();
            controller.BeginTick(
                20,
                ExecutionMode.ServerAuthority);
            world = new UnitWorld();
            caster = world.SpawnUnit(
                CreatePrototype(),
                new TeamId(1),
                20,
                fp.zero,
                fp.zero);
        }

        [TearDown]
        public void TearDown()
        {
            controller.EndTick();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void SessionStartCost_UsesLevelResourceAndHealth()
        {
            AbilityRuntime runtime = InstallCommitAbility(
                CreateCostPlan(
                    20,
                    10,
                    AbilityCostTiming.OnSessionStart),
                new TestStageDef());

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.None)));

            Assert.AreEqual(
                (fp)80,
                caster.StatHandler.CurrentCastResource);
            Assert.AreEqual(
                (fp)90,
                caster.StatHandler.CurrentHealth);
            Assert.IsTrue(runtime.ActiveSession.CostPaid);
        }

        [Test]
        public void FailedStage_DoesNotConsumeCost()
        {
            InstallCommitAbility(
                CreateCostPlan(
                    20,
                    10,
                    AbilityCostTiming.OnSessionStart),
                new TestStageDef { FailOnEnter = true });

            Assert.IsFalse(caster.AbilityHandler.HandleSignal(
                CommitSignal(AimSnapshot.None)));
            Assert.AreEqual(
                (fp)100,
                caster.StatHandler.CurrentCastResource);
            Assert.AreEqual(
                (fp)100,
                caster.StatHandler.CurrentHealth);
        }

        [Test]
        public void HoldRelease_FirstCommitPaysOnceAndStoresAim()
        {
            AbilityRuntime runtime = InstallHoldAbility(
                CreateCostPlan(
                    25,
                    0,
                    AbilityCostTiming.OnFirstCommit));

            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                new AbilitySignal
                {
                    Slot = 0,
                    Verb = AbilitySignalVerb.Focus,
                    Aim = AimSnapshot.None,
                }));
            Assert.AreEqual(
                (fp)100,
                caster.StatHandler.CurrentCastResource);
            Assert.IsFalse(runtime.ActiveSession.CostPaid);

            AimSnapshot direction =
                AimSnapshot.ForDirection(
                    new fp2(fp.one, fp.zero));
            Assert.IsTrue(caster.AbilityHandler.HandleSignal(
                CommitSignal(direction)));
            Assert.AreEqual(
                (fp)75,
                caster.StatHandler.CurrentCastResource);
            Assert.AreEqual(
                direction,
                runtime.ActiveSession.Aim);
            Assert.IsTrue(runtime.ActiveSession.CostPaid);
        }

        [Test]
        public void InvalidAimRange_ConsumesNothing()
        {
            AbilityRuntime runtime = InstallCommitAbility(
                CreateCostPlan(
                    10,
                    0,
                    AbilityCostTiming.OnSessionStart),
                new TestStageDef(),
                AimKind.Point,
                (fp)2);

            Assert.IsFalse(caster.AbilityHandler.HandleSignal(
                CommitSignal(
                    AimSnapshot.ForPoint(
                        new fp2((fp)3, fp.zero)))));
            Assert.IsNull(runtime.ActiveSession);
            Assert.AreEqual(
                (fp)100,
                caster.StatHandler.CurrentCastResource);
        }

        [Test]
        public void Snapshot_PreservesFirstCommitPayment()
        {
            AbilityRuntime runtime = InstallHoldAbility(
                CreateCostPlan(
                    10,
                    0,
                    AbilityCostTiming.OnFirstCommit));
            caster.AbilityHandler.HandleSignal(
                new AbilitySignal
                {
                    Slot = 0,
                    Verb = AbilitySignalVerb.Focus,
                });
            caster.AbilityHandler.HandleSignal(
                CommitSignal(
                    AimSnapshot.ForDirection(
                        new fp2(fp.one, fp.zero))));

            var snapshot = new AbilityRuntimeSnapshot();
            runtime.Capture(ref snapshot);
            runtime.Restore(snapshot);

            Assert.IsTrue(runtime.ActiveSession.CostPaid);
        }

        private AbilityRuntime InstallCommitAbility(
            AbilityCostPlan cost,
            StageDef stage,
            AimKind aimKind = AimKind.None,
            fp castRange = default)
        {
            var model = new CommitCastModelDef
            {
                Cast = new CastStage
                {
                    StageKey = 1,
                    Def = stage,
                    DurationTicks = 1,
                },
            };
            return Install(
                new AbilityDef
                {
                    AbilityId = 100,
                    Name = "TestCommitAbility",
                    CastModel = model,
                    AimKind = aimKind,
                    CastRange = castRange,
                    CostPlan = cost,
                });
        }

        private AbilityRuntime InstallHoldAbility(
            AbilityCostPlan cost)
        {
            var model = new HoldReleaseCastModelDef
            {
                Hold = new CastStage
                {
                    StageKey = 1,
                    Def = new TestStageDef(),
                    DurationTicks = 10,
                    Interruptible = true,
                },
                Release = new CastStage
                {
                    StageKey = 2,
                    Def = new TestStageDef(),
                    DurationTicks = 1,
                },
            };
            return Install(
                new AbilityDef
                {
                    AbilityId = 101,
                    Name = "TestHoldAbility",
                    CastModel = model,
                    AimKind = AimKind.Direction,
                    CastRange = (fp)5,
                    CostPlan = cost,
                });
        }

        private AbilityRuntime Install(AbilityDef definition)
        {
            var runtime = new AbilityRuntime
            {
                Definition = definition,
                Level = 1,
            };
            var slot = new AbilitySlotRuntime
            {
                SlotIndex = 0,
                ActiveAbilityId = definition.AbilityId,
                AllocatedPoints = 1,
            };
            slot.AddAbility(runtime);
            caster.AbilityHandler.AddSlot(slot);
            return runtime;
        }

        private static AbilitySignal CommitSignal(
            AimSnapshot aim)
        {
            return new AbilitySignal
            {
                Slot = 0,
                Verb = AbilitySignalVerb.Commit,
                Aim = aim,
            };
        }

        private static AbilityCostPlan CreateCostPlan(
            int resource,
            int health,
            AbilityCostTiming timing)
        {
            return new AbilityCostPlan(
                resource > 0
                    ? new AbilityLevelValue(
                        new[] { (fp)resource })
                    : default,
                health > 0
                    ? new AbilityLevelValue(
                        new[] { (fp)health })
                    : default,
                timing);
        }

        private static UnitPrototype CreatePrototype()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)100,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxCastResource,
                BaseValue = (fp)100,
            });
            return new UnitPrototype
            {
                UnitPrototypeId = 1,
                RuntimeEntityPrefabId = 1001,
                UnitKind = UnitKind.Hero,
                BaseStats = preset,
                Loadout = HandlerLoadout.DefaultHero,
            };
        }

        private sealed class TestStageDef : StageDef
        {
            public bool FailOnEnter;

            public override StageResult OnEnter(
                AbilitySession session,
                AbilityRuntime runtime)
            {
                return FailOnEnter
                    ? StageResult.Failed
                    : StageResult.Running;
            }
        }
    }
}
