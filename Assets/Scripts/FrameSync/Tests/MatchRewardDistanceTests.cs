using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class MatchRewardDistanceTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void MinionExperienceRadius_UsesStatToLogicDistanceScale()
        {
            var world = new UnitWorld
            {
                StatDistanceToLogicDistanceScale =
                    (fp)0.01m,
            };
            UnitType minion = Spawn(
                world,
                701,
                UnitKind.Minion,
                new TeamId(2),
                fp.zero,
                100);
            UnitType nearHero = Spawn(
                world,
                702,
                UnitKind.Hero,
                new TeamId(1),
                (fp)11.99m,
                0);
            UnitType farHero = Spawn(
                world,
                703,
                UnitKind.Hero,
                new TeamId(1),
                (fp)12.01m,
                0);
            var statistics =
                new MatchStatisticsRuntime();

            Assert.That(
                nearHero.LifeState,
                Is.EqualTo(LifeState.Alive));
            Assert.That(
                nearHero.StatHandler.CanLevelUp,
                Is.True);
            Assert.That(
                world.GetAllUnits().Count,
                Is.EqualTo(3));
            Assert.That(
                world.StatDistanceToLogicDistanceScale,
                Is.EqualTo((fp)0.01m));
            Assert.That(
                fpmath.lengthsq(
                    nearHero.PhysicsEntity.Transform2D.Position -
                    minion.PhysicsEntity.Transform2D.Position),
                Is.LessThan((fp)144m));

            statistics.Consume(
                new[]
                {
                    new DeathResult
                    {
                        VictimUid = minion.UnitUid,
                        KillerHeroUid = default,
                        AssistantHeroUids =
                            System.Array.Empty<UnitUid>(),
                        DeathSequenceInTick = 1,
                        DeathLogicTick = 10,
                    },
                },
                world);

            Assert.That(
                nearHero.StatHandler.CurrentExperience,
                Is.EqualTo(100),
                "1200 authored distance at scale 0.01 is approximately 12 logic units.");
            Assert.That(
                farHero.StatHandler.CurrentExperience,
                Is.Zero);
        }

        private static UnitType Spawn(
            UnitWorld world,
            int id,
            UnitKind kind,
            TeamId team,
            fp x,
            int baseExperience)
        {
            StatPreset preset =
                UnitTestFactory.CreateDefaultPreset();
            preset.LevelExperience =
                kind == UnitKind.Hero
                    ? LevelExperienceConfig
                        .CreateDefault18()
                    : LevelExperienceConfig.Disabled;
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = id,
                RuntimeEntityPrefabId = id,
                UnitKind = kind,
                Loadout = HandlerLoadout.DefaultHero,
                BaseStats = preset,
                BaseExperienceValue =
                    baseExperience,
            };
            UnitType unit = world.SpawnUnit(
                prototype,
                team,
                10,
                fp.zero,
                fp.zero);
            unit.PhysicsEntity.SetLogicPose(
                new fp2(x, fp.zero),
                new fp2(fp.one, fp.zero));
            return unit;
        }
    }
}
