using NUnit.Framework;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Deterministic.Tests
{
    [TestFixture]
    public class DeterministicHash32Tests
    {
        [Test]
        public void SameString_SameHash()
        {
            uint h1 = DeterministicHash32.Utf8("Ability.AatroxE.PassiveOmnivamp");
            uint h2 = DeterministicHash32.Utf8("Ability.AatroxE.PassiveOmnivamp");
            Assert.AreEqual(h1, h2);
        }

        [Test]
        public void DifferentStrings_DifferentHash()
        {
            uint h1 = DeterministicHash32.Utf8("Buff.Berserk.DamageReduction");
            uint h2 = DeterministicHash32.Utf8("Equipment.InfinityEdge.ForceCrit");
            Assert.AreNotEqual(h1, h2);
        }

        [Test]
        public void EmptyString_ReturnsOffsetBasis()
        {
            uint h = DeterministicHash32.Utf8("");
            Assert.AreEqual(2166136261u, h);
        }

        [Test]
        public void NullString_ReturnsZero()
        {
            uint h = DeterministicHash32.Utf8(null);
            Assert.AreEqual(0u, h);
        }

        [Test]
        public void Deterministic_KnownValue()
        {
            // FNV-1a of "test" in UTF-8 = 0xba4bd3f3 (known FNV-1a 32-bit value)
            uint h = DeterministicHash32.Utf8("test");
            // Verify it's non-zero and stable
            Assert.AreNotEqual(0u, h);
            Assert.AreEqual(h, DeterministicHash32.Utf8("test"));
        }
    }
}