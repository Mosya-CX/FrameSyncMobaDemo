using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class CombatActionIdentityTests
    {
        [TearDown]
        public void TearDown()
        {
            CombatEvents.Clear();
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void CritRoll_RepeatedAndGlobalRandomInterleavingAreEquivalent()
        {
            var action = new OriginActionId(
                GameplayParticipantId.Explicit(11),
                CombatSourceType.Attack,
                CombatBuiltinSourceId.BasicAttack,
                120,
                4);
            GameplayParticipantId target =
                GameplayParticipantId.Explicit(22);
            fp chance = fp.one / (fp)2;

            bool expected = CombatFairnessKey.RollCrit(
                123456u,
                action,
                target,
                0,
                chance);
            var random = new DeterministicRandomService(123456u);
            _ = random.NextUInt();
            _ = random.NextUInt();
            bool afterUnrelatedDraws = CombatFairnessKey.RollCrit(
                123456u,
                action,
                target,
                0,
                chance);

            Assert.AreEqual(expected, afterUnrelatedDraws);
        }

        [Test]
        public void CritRoll_TechnicalUidRelabelDoesNotChangeResult()
        {
            var action = new OriginActionId(
                GameplayParticipantId.Explicit(101),
                CombatSourceType.Ability,
                10012,
                77,
                5);
            GameplayParticipantId target =
                GameplayParticipantId.Explicit(202);

            bool original = CombatFairnessKey.RollCrit(
                991u,
                action,
                target,
                3,
                fp.one / (fp)3);
            UnitUid ignoredOriginalUid =
                new UnitUid(77, 1001, 0);
            UnitUid ignoredRelabeledUid =
                new UnitUid(77, 9001, 9);
            Assert.AreNotEqual(ignoredOriginalUid, ignoredRelabeledUid);
            bool relabeled = CombatFairnessKey.RollCrit(
                991u,
                action,
                target,
                3,
                fp.one / (fp)3);

            Assert.AreEqual(original, relabeled);
        }

        [Test]
        public void CombatCrit_TechnicalUidRelabelKeepsSettledResult()
        {
            bool original = SettleRelabeledCrit(
                new UnitUid(77, 1001, 0),
                new UnitUid(77, 1002, 1));
            bool relabeled = SettleRelabeledCrit(
                new UnitUid(77, 9001, 9),
                new UnitUid(77, 8001, 4));

            Assert.AreEqual(original, relabeled);
        }

        [Test]
        public void ProbabilisticCrit_InvalidIdentityOrOrdinalFails()
        {
            GameplayParticipantId target =
                GameplayParticipantId.Explicit(2);
            var action = new OriginActionId(
                GameplayParticipantId.Explicit(1),
                CombatSourceType.Attack,
                1,
                1,
                0);

            Assert.Throws<DeterministicSimulationException>(() =>
                CombatFairnessKey.RollCrit(
                    1u,
                    OriginActionId.Invalid,
                    target,
                    0,
                    fp.one / (fp)2));
            Assert.Throws<DeterministicSimulationException>(() =>
                CombatFairnessKey.RollCrit(
                    1u,
                    action,
                    target,
                    -1,
                    fp.one / (fp)2));
            Assert.Throws<DeterministicSimulationException>(() =>
                CombatFairnessKey.RollCrit(
                    1u,
                    action,
                    target,
                    -1,
                    fp.zero));
            Assert.Throws<DeterministicSimulationException>(() =>
                CombatFairnessKey.RollCrit(
                    1u,
                    action,
                    target,
                    -1,
                    fp.one));
        }

        [Test]
        public void ChildEffectOrdinal_DifferentParentEffectsDoNotCollide()
        {
            int first = CombatFairnessKey.ComposeChildEffectOrdinal(
                10,
                9001,
                0);
            int second = CombatFairnessKey.ComposeChildEffectOrdinal(
                11,
                9001,
                0);

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void ProjectileTie_SeedCorpusDoesNotPermanentlyFavorEitherParticipant()
        {
            var action = new OriginActionId(
                GameplayParticipantId.Explicit(1),
                CombatSourceType.Ability,
                1001,
                10,
                0);
            GameplayParticipantId first =
                GameplayParticipantId.Explicit(2);
            GameplayParticipantId second =
                GameplayParticipantId.Explicit(3);
            bool firstWon = false;
            bool secondWon = false;

            for (uint seed = 1; seed <= 256; seed++)
            {
                ulong firstScore = CombatFairnessKey.ProjectileTieScore(
                    seed,
                    action,
                    first);
                ulong secondScore = CombatFairnessKey.ProjectileTieScore(
                    seed,
                    action,
                    second);
                firstWon |= firstScore < secondScore;
                secondWon |= secondScore < firstScore;
            }

            Assert.IsTrue(firstWon);
            Assert.IsTrue(secondWon);
        }

        [Test]
        public void DeferredDamage_CaptureRestorePreservesActionIdentity()
        {
            var world = new UnitWorld();
            var combat = new CombatSystem(world, 0, 0, 88u);
            world.CombatSystem = combat;
            var action = new OriginActionId(
                GameplayParticipantId.Explicit(7),
                CombatSourceType.Ability,
                10014,
                30,
                2);
            DamageRequest damage = UnitTestFactory.CreateDamageRequest(
                new UnitUid(30, 1001, 0),
                new UnitUid(30, 1002, 0),
                (fp)12,
                originActionId: action,
                effectOrdinal: 17);
            combat.DeferRequest(
                CombatRequestKind.Damage,
                null,
                damage,
                null,
                31,
                30);
            CombatSnapshot captured = CombatSnapshot.Default;
            combat.Capture(ref captured);

            var restored = new CombatSystem(world, 0, 0, 88u);
            restored.Restore(captured);
            CombatSnapshot roundTrip = CombatSnapshot.Default;
            restored.Capture(ref roundTrip);

            Assert.AreEqual(
                action,
                roundTrip.DeferredRequests[0]
                    .Damage.Header.OriginActionId);
            Assert.AreEqual(
                17,
                roundTrip.DeferredRequests[0]
                    .Damage.Header.EffectOrdinal);
        }

        [Test]
        public void NegativeEffectOrdinal_IsRejectedAtDamageBoundaries()
        {
            var world = new UnitWorld();
            var combat = new CombatSystem(world, 0, 0, 88u);
            DamageRequest invalid = UnitTestFactory.CreateDamageRequest(
                new UnitUid(30, 1001, 0),
                new UnitUid(30, 1002, 0),
                (fp)12,
                effectOrdinal: -1);

            Assert.Throws<DeterministicSimulationException>(() =>
                combat.DeferRequest(
                    CombatRequestKind.Damage,
                    null,
                    invalid,
                    null,
                    31,
                    30));

            CombatSnapshot snapshot = CombatSnapshot.Default;
            snapshot.DeferredRequests = new[]
            {
                new DeferredCombatRequest
                {
                    ExecuteLogicTick = 31,
                    SourceLogicTick = 30,
                    RequestKind = CombatRequestKind.Damage,
                    Damage = invalid,
                },
            };
            Assert.Throws<DeterministicSimulationException>(() =>
                combat.Restore(snapshot));
        }

        private static bool SettleRelabeledCrit(
            UnitUid sourceUid,
            UnitUid targetUid)
        {
            CombatEvents.Clear();
            var controller = new SimulationTickContextController();
            controller.BeginTick(77, ExecutionMode.ServerAuthority);
            try
            {
                var world = new UnitWorld();
                GameplayParticipantId sourceParticipant =
                    GameplayParticipantId.Explicit(101);
                GameplayParticipantId targetParticipant =
                    GameplayParticipantId.Explicit(202);
                Unit source = UnitTestFactory.CreateUnit(
                    sourceUid,
                    UnitKind.Hero,
                    0,
                    new TeamId(1),
                    gameplayParticipantId: sourceParticipant);
                Unit target = UnitTestFactory.CreateUnit(
                    targetUid,
                    UnitKind.Hero,
                    0,
                    new TeamId(2),
                    gameplayParticipantId: targetParticipant);
                source.World = world;
                target.World = world;
                world.RegisterUnit(source);
                world.RegisterUnit(target);
                source.StatHandler.AddModifier(
                    StatId.CriticalStrikeChance,
                    StatModifierOperation.FlatAdd,
                    fp.one / (fp)2);

                var combat = new CombatSystem(world, 0, 0, 991u);
                world.CombatSystem = combat;
                bool observed = false;
                bool received = false;
                CombatEvents.OnDamageDealt += data =>
                {
                    received = true;
                    observed = data.IsCritical;
                };
                var action = new OriginActionId(
                    sourceParticipant,
                    CombatSourceType.Ability,
                    10012,
                    77,
                    5);
                combat.BeginTick();
                Assert.IsTrue(combat.SubmitDamage(
                    UnitTestFactory.CreateDamageRequest(
                        sourceUid,
                        targetUid,
                        (fp)10,
                        sourceType: CombatSourceType.Ability,
                        sourceId: 10012,
                        recipeId: 10012,
                        originActionId: action,
                        effectOrdinal: 3)));
                combat.SettleActiveRequests();
                Assert.IsTrue(received);
                return observed;
            }
            finally
            {
                controller.EndTick();
            }
        }
    }
}
