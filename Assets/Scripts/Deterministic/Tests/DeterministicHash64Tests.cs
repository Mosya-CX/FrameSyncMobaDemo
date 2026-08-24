using NUnit.Framework;

namespace FrameSyncMoba.Deterministic.Tests
{
    [TestFixture]
    public sealed class DeterministicHash64Tests
    {
        [Test]
        public void Compute_RepeatedInputs_ReturnSameValue()
        {
            ulong first = DeterministicHash64.Compute(1, 2, 3, 4, 5);
            ulong second = DeterministicHash64.Compute(1, 2, 3, 4, 5);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void Compute_ChangingEachInput_ChangesValue()
        {
            ulong baseline = DeterministicHash64.Compute(1, 2, 3, 4, 5);

            Assert.AreNotEqual(baseline, DeterministicHash64.Compute(9, 2, 3, 4, 5));
            Assert.AreNotEqual(baseline, DeterministicHash64.Compute(1, 9, 3, 4, 5));
            Assert.AreNotEqual(baseline, DeterministicHash64.Compute(1, 2, 9, 4, 5));
            Assert.AreNotEqual(baseline, DeterministicHash64.Compute(1, 2, 3, 9, 5));
            Assert.AreNotEqual(baseline, DeterministicHash64.Compute(1, 2, 3, 4, 9));
        }
    }
}
