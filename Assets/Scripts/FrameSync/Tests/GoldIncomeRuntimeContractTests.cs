using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    public sealed class GoldIncomeRuntimeContractTests
    {
        [Test]
        public void EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(2, 500);

            runtime.BeginTick(0);
            GoldIncomeRecordBatch empty = runtime.SealTick(0);
            Assert.AreNotEqual(0UL, empty.Digest.Value);
            Assert.AreEqual(0, empty.Records.Length);
            runtime.ConfirmAcceptedTick(0);

            runtime.BeginTick(1);
            runtime.RequestGoldIncome(1, 25, GoldIncomeReason.UnitKill);
            GoldIncomeRecordBatch income = runtime.SealTick(1);
            Assert.AreEqual(0, income.Records[0].IncomeSequenceInTick);
            Assert.Throws<FrameSyncMoba.Deterministic.DeterministicSimulationException>(
                () => runtime.ConfirmAcceptedTick(2));
            runtime.ConfirmAcceptedTick(1);
            Assert.AreEqual(525, runtime.GetConfirmedAvailableGold(1));
        }

        [Test]
        public void Restore_RejectsTamperedDigest()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(1, 0);
            runtime.BeginTick(0);
            runtime.RequestGoldIncome(0, 10, GoldIncomeReason.UnitKill);
            runtime.SealTick(0);
            GoldIncomeSnapshot snapshot = default;
            runtime.Capture(ref snapshot);
            var batch0 = snapshot.UnconfirmedBatches[0];
            batch0.Digest = new GoldIncomeBatchDigest(1);
            snapshot.UnconfirmedBatches[0] = batch0;

            var restored = new GoldIncomeRuntime();
            Assert.Throws<FrameSyncMoba.Deterministic.DeterministicSimulationException>(
                () => restored.Restore(snapshot));
        }
    }
}
