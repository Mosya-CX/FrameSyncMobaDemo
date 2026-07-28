using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class LocalCommandGoldMatchFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void FutureCommand_IsRetainedAndConsumedOnlyAtTargetTick()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 200, UnitKind.Hero);
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld)
            {
                MaxFutureCommandTicks = 6,
            };
            var command = GameplayCommand.CreateMove(
                new CommandHeader(
                    1,
                    10,
                    0,
                    unit.UnitUid,
                    2,
                    GameplayCommandKind.Move,
                    0,
                    0),
                new fp2(6, 0));
            pipeline.SubmitCommand(command);
            var controller = new SimulationTickContextController();

            pipeline.ExecuteTick(controller);
            pipeline.ExecuteTick(controller);

            Assert.That(unit.MovementHandler.Position,
                Is.EqualTo(fp2.zero));
            Assert.That(pipeline.CommandCollector.CommandCount, Is.EqualTo(1));

            pipeline.ExecuteTick(controller);

            Assert.That(unit.MovementHandler.Position.x,
                Is.GreaterThan(fp.zero));
            Assert.That(pipeline.CommandCollector.CommandCount, Is.Zero);
        }

        [Test]
        public void CastIntentAndAction_PreserveCommitVerbAndDirectionAim()
        {
            UnitWorld world = CreateWorld();
            UnitType unit = Spawn(world, 210, UnitKind.Hero);
            AimSnapshot aim = AimSnapshot.ForDirection(new fp2(1, 1));
            unit.Planner.SetIntent(new UnitIntent
            {
                Kind = IntentKind.CastAbility,
                AbilityId = 2,
                AbilityVerb = AbilitySignalVerb.Commit,
                AbilityAim = aim,
                AllowChase = false,
            });
            var controller = new SimulationTickContextController();
            controller.BeginTick(1, ExecutionMode.ServerAuthority);
            try
            {
                unit.Planner.Tick(out ActionRequest request);

                Assert.That(request, Is.TypeOf<CastActionRequest>());
                var cast = (CastActionRequest)request;
                Assert.That(cast.AbilityId, Is.EqualTo(2));
                Assert.That(cast.Verb, Is.EqualTo(AbilitySignalVerb.Commit));
                Assert.That(cast.Aim, Is.EqualTo(aim));
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void NaturalGold_IsTickDerivedCanonicalAndInsideOpenBatch()
        {
            GoldIncomeRuntime firstIncome = CreateIncomeRuntime();
            GoldIncomeRuntime secondIncome = CreateIncomeRuntime();
            MatchRuleRuntime firstRule = CreateRunningRule();
            MatchRuleRuntime secondRule = CreateRunningRule();
            var first = new NaturalGoldIncomeSystem(
                firstIncome, firstRule, 15, 2, 2);
            var second = new NaturalGoldIncomeSystem(
                secondIncome, secondRule, 15, 2, 2);

            firstIncome.BeginTick(15);
            secondIncome.BeginTick(15);
            first.Tick(15);
            second.Tick(15);
            firstIncome.SealTick(15);
            secondIncome.SealTick(15);

            Assert.That(firstIncome.TryGetSealedBatch(
                15, out GoldIncomeRecordBatch batch), Is.True);
            Assert.That(batch.Records.Length, Is.EqualTo(2));
            Assert.That(batch.Records[0].PlayerSlot, Is.EqualTo(0));
            Assert.That(batch.Records[1].PlayerSlot, Is.EqualTo(1));
            Assert.That(batch.Records[0].Amount, Is.EqualTo(2));
            Assert.That(
                firstIncome.GetBatchDigest(15),
                Is.EqualTo(secondIncome.GetBatchDigest(15)));
        }

        [Test]
        public void ClientPredictionCannotEnterEnding_ButServerAuthorityCan()
        {
            UnitWorld world = CreateWorld();
            UnitType blueBase = Spawn(world, 220, UnitKind.Structure);
            UnitType redBase = Spawn(world, 221, UnitKind.Structure);
            var rule = new MatchRuleRuntime(5);
            rule.RegisterBases(blueBase.UnitUid, redBase.UnitUid);
            rule.BeginCountdown(0, 0);
            rule.AdvanceTick(0);
            world.RequestEnterDying(redBase);
            world.ConfirmUnitDeath(redBase);
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld)
            {
                MatchRule = rule,
            };
            var controller = new SimulationTickContextController();

            pipeline.ExecuteTick(controller, ExecutionMode.ClientPrediction);

            Assert.That(rule.CurrentPhase, Is.EqualTo(MatchPhase.Running));

            pipeline.ExecuteTick(controller, ExecutionMode.ServerAuthority);

            Assert.That(rule.CurrentPhase, Is.EqualTo(MatchPhase.Ending));
            Assert.That(rule.WinningTeamId, Is.EqualTo(blueBase.TeamId));
        }

        private static GoldIncomeRuntime CreateIncomeRuntime()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(2, 0);
            return runtime;
        }

        private static MatchRuleRuntime CreateRunningRule()
        {
            var rule = new MatchRuleRuntime(5);
            rule.BeginCountdown(0, 0);
            rule.AdvanceTick(0);
            return rule;
        }

        private static UnitWorld CreateWorld()
        {
            return new UnitWorld
            {
                StatDefinitionTable = new StatDefinitionTable(),
                PhysicsWorld = new PhysicsWorld(),
            };
        }

        private static UnitType Spawn(
            UnitWorld world,
            int prefabId,
            UnitKind kind)
        {
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = prefabId,
                RuntimeEntityPrefabId = prefabId,
                UnitKind = kind,
                BaseStats = new StatPreset(),
                LocomotionProfile = kind == UnitKind.Structure
                    ? LocomotionProfile.DefaultTower
                    : LocomotionProfile.DefaultHero,
            };
            return world.SpawnUnit(
                prototype,
                kind == UnitKind.Structure
                    ? new TeamId((byte)(prefabId % 2 + 1))
                    : TeamId.Neutral,
                0,
                fp.zero,
                fp.zero);
        }
    }
}
