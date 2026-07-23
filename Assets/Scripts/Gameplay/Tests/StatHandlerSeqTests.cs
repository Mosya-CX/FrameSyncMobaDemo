using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class StatHandlerSeqTests
    {
        private StatHandler CreateHandler()
        {
            return UnitTestFactory.CreateStatHandler(
                StatTestHelpers.CreateDefaultTable(),
                StatTestHelpers.CreateSimplePreset(),
                StatTestHelpers.DefaultOwnerUid,
                level: 1,
                statGrowthC: 0.5m,
                statGrowthD: 0m);
        }

        [Test]
        public void StatSeq_StartsAt1()
        {
            StatHandler h = CreateHandler();
            StatModifierHandle handle = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);

            Assert.AreEqual(1u, handle.StatSeq);
        }

        [Test]
        public void StatSeq_NeverReusedAfterRemove()
        {
            StatHandler h = CreateHandler();
            StatModifierHandle h1 = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            StatModifierHandle h2 = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);
            StatModifierHandle h3 = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)30m);

            Assert.AreEqual(1u, h1.StatSeq);
            Assert.AreEqual(2u, h2.StatSeq);
            Assert.AreEqual(3u, h3.StatSeq);

            h.RemoveModifier(h2);

            StatModifierHandle h4 = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)40m);
            Assert.AreEqual(4u, h4.StatSeq);
        }

        [Test]
        public void StatSeq_AcrossStatIds_SharedCounter()
        {
            StatHandler h = CreateHandler();
            StatModifierHandle h1 = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            StatModifierHandle h2 = h.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            StatModifierHandle h3 = h.AddModifier(StatId.Armor, StatModifierOperation.FlatAdd, (fp)5m);

            Assert.AreEqual(1u, h1.StatSeq);
            Assert.AreEqual(2u, h2.StatSeq);
            Assert.AreEqual(3u, h3.StatSeq);
        }

        [Test]
        public void StatSeq_InvalidHandle_WhenStatSeqZero()
        {
            StatModifierHandle invalid = default;
            Assert.IsFalse(invalid.IsValid);
            Assert.AreEqual(0u, invalid.StatSeq);
        }
    }
}