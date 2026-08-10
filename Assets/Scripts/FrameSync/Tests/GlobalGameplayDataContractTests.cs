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
                "Assets/Config/Formal/GlobalGameplayData.asset");
            Assert.NotNull(asset);
            BakedGlobalGameplayData baked = asset.BakeOrThrow();
            Assert.AreEqual(30, baked.TickRate);
            Assert.Greater(baked.MinionWaveConfig.WaveIntervalTicks, 0);
            Assert.Greater(baked.UnitGridCellSize, Unity.Mathematics.FixedPoint.fp.zero);
            Assert.AreEqual(
                (Unity.Mathematics.FixedPoint.fp)0.01m,
                baked.MoveSpeedToLogicVelocityScale);
            Assert.GreaterOrEqual(baked.EquipmentSellRate, Unity.Mathematics.FixedPoint.fp.zero);
            Assert.LessOrEqual(baked.EquipmentSellRate, Unity.Mathematics.FixedPoint.fp.one);
            Assert.Greater(baked.SnapshotWindowTicks, 1);
            Assert.GreaterOrEqual(baked.MaxPredictionLeadTicks, 0);
            Assert.Less(
                baked.MaxPredictionLeadTicks,
                baked.SnapshotWindowTicks);
            Assert.Greater(baked.AuthorityRecoveryRetryTicks, 0);
            Assert.Greater(
                baked.MaxAuthorityRecoveryAttemptsBeforeDisconnect,
                0);
        }
    }
}
