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
            // The bake must follow the authored TickRate instead of any
            // hardcoded editor constant.
            var serialized = new UnityEditor.SerializedObject(asset);
            int authoredTickRate =
                serialized.FindProperty("frameSync.TickRate").intValue;
            Assert.AreEqual(authoredTickRate, baked.TickRate);
            Assert.That(
                baked.TickRate,
                Is.InRange(10, 120),
                "TickRate must stay inside the supported contract range.");
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
