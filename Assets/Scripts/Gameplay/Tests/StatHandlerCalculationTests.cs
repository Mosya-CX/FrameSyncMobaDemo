using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class StatHandlerCalculationTests
    {
        private StatHandler CreateHandler(int level = 1, fp growthC = default, fp growthD = default)
        {
            return UnitTestFactory.CreateStatHandler(
                StatTestHelpers.CreateDefaultTable(),
                StatTestHelpers.CreateSimplePreset(),
                StatTestHelpers.DefaultOwnerUid,
                level,
                growthC,
                growthD);
        }

        [Test]
        public void GetStat_NoModifiers_ReturnsLevelBaseValue()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            Assert.AreEqual((fp)100m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void GetStat_FlatAdd_SumsCorrectly()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)30m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);

            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void GetStat_BaseRatioAdd_AppliesPercentToLevelBase()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.BaseRatioAdd, (fp)0.2m);

            AssertFpNear((fp)120m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void GetStat_FinalRatioAdd_AppliesPercentAfterFlatAndBase()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FinalRatioAdd, (fp)0.1m);

            AssertFpNear((fp)110m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void GetStat_FixedOrder_FlatThenBaseThenFinal()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.BaseRatioAdd, (fp)0.2m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FinalRatioAdd, (fp)0.1m);

            // BeforeFinalRatio = 100 * (1 + 0.2) + 50 = 170
            // FinalValue = 170 * (1 + 0.1) = 187
            AssertFpNear((fp)187m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void GetStat_ClampByStatDefinition_MaxValue()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            // Armor: BaseValue=30, MaxValue=200
            h.AddModifier(StatId.Armor, StatModifierOperation.FlatAdd, (fp)250m);

            Assert.AreEqual((fp)200m, h.GetStat(StatId.Armor));
        }

        [Test]
        public void GetStat_ClampByStatDefinition_MinValue()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            // Armor: BaseValue=30, MinValue=0
            h.AddModifier(StatId.Armor, StatModifierOperation.FlatAdd, (fp)(-50m));

            Assert.AreEqual((fp)0m, h.GetStat(StatId.Armor));
        }

        [Test]
        public void GetStat_LevelGrowthFormula_MatchesDesign()
        {
            // BaseValue=100, GrowthValue=10, Level=3, C=0.5, D=0
            // L = max(3-1, 0) = 2
            // LevelGrowth = 10 * 2 * (0.5 + 0 * 2) = 10 * 2 * 0.5 = 10
            // LevelBaseValue = 100 + 10 = 110
            StatHandler h = CreateHandler(level: 3, growthC: 0.5m, growthD: 0m);

            Assert.AreEqual((fp)110m, h.GetStat(StatId.AttackDamage));
        }

        private static void AssertFpNear(fp expected, fp actual)
        {
            Assert.LessOrEqual(fpmath.abs(actual - expected), (fp)1 / (fp)1000000);
        }

        [Test]
        public void GetStat_DirtyRecompute_LazyOnGetStat()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            Assert.AreEqual((fp)100m, h.GetStat(StatId.AttackDamage));

            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            Assert.AreEqual((fp)150m, h.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void GetStat_Determinism_AddOrderInvariantWithinGroup()
        {
            StatHandler h1 = CreateHandler(level: 1, growthC: 0.5m);
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);
            h1.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)30m);

            StatHandler h2 = CreateHandler(level: 1, growthC: 0.5m);
            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)30m);
            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            h2.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);

            Assert.AreEqual(h1.GetStat(StatId.AttackDamage), h2.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void Level_ChangeMarksAllStatsDirty()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            Assert.AreEqual((fp)100m, h.GetStat(StatId.AttackDamage));

            h.Level = 3;
            // Level 3: LevelBaseValue = 100 + 10*2*(0.5+0) = 110
            Assert.AreEqual((fp)110m, h.GetStat(StatId.AttackDamage));
        }
    }
}
