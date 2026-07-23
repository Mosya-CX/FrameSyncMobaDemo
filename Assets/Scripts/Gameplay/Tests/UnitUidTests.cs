using System;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitUidTests
    {
        [Test]
        public void SameComponents_ProduceEqualIdentity()
        {
            var first = new UnitUid(1200, 1001, 7);
            var second = new UnitUid(1200, 1001, 7);

            Assert.That(first.SpawnLogicTick, Is.EqualTo(1200));
            Assert.That(first.RuntimeEntityPrefabId, Is.EqualTo(1001));
            Assert.That(first.SpawnSequenceInTick, Is.EqualTo(7));
            Assert.That(first.Equals(second), Is.True);
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void ChangingAnyComponent_ChangesIdentity()
        {
            var baseline = new UnitUid(1200, 1001, 7);

            Assert.That(baseline, Is.Not.EqualTo(new UnitUid(1201, 1001, 7)));
            Assert.That(baseline, Is.Not.EqualTo(new UnitUid(1200, 1002, 7)));
            Assert.That(baseline, Is.Not.EqualTo(new UnitUid(1200, 1001, 8)));
        }

        [Test]
        public void CompareTo_UsesFormalLexicographicOrder()
        {
            var first = new UnitUid(int.MinValue, int.MaxValue, byte.MaxValue);
            var second = new UnitUid(0, int.MinValue, 0);
            var third = new UnitUid(0, int.MinValue, 1);
            var fourth = new UnitUid(0, int.MaxValue, 0);

            Assert.That(first.CompareTo(second), Is.LessThan(0));
            Assert.That(second.CompareTo(third), Is.LessThan(0));
            Assert.That(third.CompareTo(fourth), Is.LessThan(0));
            Assert.That(fourth.CompareTo(third), Is.GreaterThan(0));
            Assert.That(second.CompareTo(second), Is.EqualTo(0));
            Assert.That(first.CompareTo(fourth), Is.LessThan(0));
        }

        [Test]
        public void SortingSameValues_IsIndependentOfInsertionOrder()
        {
            var expected = new[]
            {
                new UnitUid(8, 99, 2),
                new UnitUid(9, 1, 0),
                new UnitUid(9, 1, 1),
                new UnitUid(9, 2, 0),
            };
            var reversed = new[]
            {
                expected[3],
                expected[2],
                expected[1],
                expected[0],
            };
            var interleaved = new[]
            {
                expected[2],
                expected[0],
                expected[3],
                expected[1],
            };

            Array.Sort(reversed);
            Array.Sort(interleaved);

            Assert.That(reversed, Is.EqualTo(expected));
            Assert.That(interleaved, Is.EqualTo(expected));
        }
    }
}
