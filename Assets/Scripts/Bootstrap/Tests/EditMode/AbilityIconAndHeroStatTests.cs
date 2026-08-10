using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// The test hero catalog must match the Varus base-stat design. Ability
    /// icons stay in the presentation layer and never enter deterministic
    /// config, so they are not asserted here.
    /// </summary>
    [TestFixture]
    public sealed class AbilityIconAndHeroStatTests
    {
        [Test]
        public void TestHeroCatalog_MatchesVarusBaseStats()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<
                UnitRuntimeCatalogAsset>(
                "Assets/Config/Formal/" +
                "FullMatchUnitRuntimeCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            UnitPrototypeAuthoring hero = null;
            for (int i = 0;
                 i < catalog.UnitPrototypes.Count;
                 i++)
            {
                if (catalog.UnitPrototypes[i]
                    .UnitPrototypeId == 1001)
                {
                    hero = catalog.UnitPrototypes[i];
                    break;
                }
            }
            Assert.That(hero, Is.Not.Null);

            float GetBase(int statId)
            {
                for (int i = 0;
                     i < hero.BaseStats.Count;
                     i++)
                    if (hero.BaseStats[i].StatId ==
                        (StatId)statId)
                        return hero.BaseStats[i]
                            .BaseValue;
                return -1f;
            }

            float GetGrowth(int statId)
            {
                for (int i = 0;
                     i < hero.BaseStats.Count;
                     i++)
                    if (hero.BaseStats[i].StatId ==
                        (StatId)statId)
                        return hero.BaseStats[i]
                            .GrowthValue;
                return -1f;
            }

            Assert.That(
                GetBase((int)StatId.MaxHealth),
                Is.EqualTo(600f));
            Assert.That(
                GetGrowth((int)StatId.MaxHealth),
                Is.EqualTo(105f));
            Assert.That(
                GetBase((int)StatId.MaxCastResource),
                Is.EqualTo(320f));
            Assert.That(
                GetGrowth((int)StatId.MaxCastResource),
                Is.EqualTo(40f));
            Assert.That(
                GetBase((int)StatId.AttackDamage),
                Is.EqualTo(62f));
            Assert.That(
                GetGrowth((int)StatId.AttackDamage),
                Is.EqualTo(3.4f));
            Assert.That(
                GetBase((int)StatId.AttackSpeed),
                Is.EqualTo(0.658f));
            Assert.That(
                GetBase((int)StatId.AttackRange),
                Is.EqualTo(575f));
            Assert.That(
                GetBase((int)StatId.MoveSpeed),
                Is.EqualTo(330f));
            Assert.That(
                hero.LevelExperience.CanLevelUp,
                Is.True);
            Assert.That(
                hero.LevelExperience.MaxLevel,
                Is.EqualTo(18));
        }
    }
}
