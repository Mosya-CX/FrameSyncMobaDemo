using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class FormalGoldRewardConfigTests
    {
        [Test]
        public void FormalConfig_UsesRequestedInitialAndKillGoldValues()
        {
            GlobalGameplayData global =
                AssetDatabase.LoadAssetAtPath<
                    GlobalGameplayData>(
                    "Assets/Config/Formal/GlobalGameplayData.asset");
            UnitRuntimeCatalogAsset units =
                AssetDatabase.LoadAssetAtPath<
                    UnitRuntimeCatalogAsset>(
                    "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset");

            Assert.NotNull(global);
            Assert.NotNull(units);
            Assert.That(
                global.BakeOrThrow().InitialEarnedGold,
                Is.EqualTo(1500));
            AssertReward(units, 1001, 300);
            AssertReward(units, 2001, 21);
            AssertReward(units, 2002, 21);
            AssertReward(units, 2101, 14);
            AssertReward(units, 2102, 14);
        }

        private static void AssertReward(
            UnitRuntimeCatalogAsset units,
            int prototypeId,
            int expectedGold)
        {
            for (int i = 0;
                 i < units.UnitPrototypes.Count;
                 i++)
            {
                UnitPrototypeAuthoring prototype =
                    units.UnitPrototypes[i];
                if (prototype.UnitPrototypeId !=
                    prototypeId)
                {
                    continue;
                }
                Assert.That(
                    prototype.BaseGoldValue,
                    Is.EqualTo(expectedGold));
                return;
            }
            Assert.Fail(
                $"Missing formal UnitPrototype {prototypeId}.");
        }
    }
}
