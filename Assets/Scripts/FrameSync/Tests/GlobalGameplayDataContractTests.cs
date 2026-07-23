using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using UnityEditor;

namespace FrameSyncMoba.FrameSync.Tests
{
    public sealed class GlobalGameplayDataContractTests
    {
        [Test]
        public void ProjectAsset_BakesInspectorFloatsToDeterministicValues()
        {
            GlobalGameplayData asset = AssetDatabase.LoadAssetAtPath<GlobalGameplayData>(
                "Assets/Config/Runtime/GlobalGameplayData.asset");
            Assert.NotNull(asset);
            BakedGlobalGameplayData baked = asset.BakeOrThrow();
            Assert.AreEqual(30, baked.TickRate);
            Assert.Greater(baked.MinionWaveIntervalTicks, 0);
            Assert.Greater(baked.UnitGridCellSize, Unity.Mathematics.FixedPoint.fp.zero);
            Assert.GreaterOrEqual(baked.EquipmentSellRate, Unity.Mathematics.FixedPoint.fp.zero);
            Assert.LessOrEqual(baked.EquipmentSellRate, Unity.Mathematics.FixedPoint.fp.one);
        }
    }
}
