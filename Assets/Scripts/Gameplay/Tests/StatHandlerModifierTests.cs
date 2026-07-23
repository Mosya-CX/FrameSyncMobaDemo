using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class StatHandlerModifierTests
    {
        private StatHandler handler;

        [SetUp]
        public void SetUp()
        {
            handler = UnitTestFactory.CreateStatHandler(
                StatTestHelpers.CreateDefaultTable(),
                StatTestHelpers.CreateSimplePreset(),
                StatTestHelpers.DefaultOwnerUid,
                level: 1,
                statGrowthC: 0.5m,
                statGrowthD: 0m);
        }

        [Test]
        public void AddModifier_ReturnsValidHandle_WithCorrectStatSeq()
        {
            StatModifierHandle h1 = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            StatModifierHandle h2 = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);

            Assert.AreEqual(1u, h1.StatSeq);
            Assert.AreEqual(2u, h2.StatSeq);
            Assert.AreEqual(StatId.AttackDamage, h1.StatId);
            Assert.IsTrue(h1.IsValid);
        }

        [Test]
        public void AddModifier_StatSeqMonotonicAcrossStatIds()
        {
            StatModifierHandle h1 = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            StatModifierHandle h2 = handler.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            StatModifierHandle h3 = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);

            Assert.AreEqual(1u, h1.StatSeq);
            Assert.AreEqual(2u, h2.StatSeq);
            Assert.AreEqual(3u, h3.StatSeq);
        }

        [Test]
        public void AddModifier_InvalidStatId_Throws()
        {
            Assert.Throws<System.ArgumentException>(() =>
            {
                handler.AddModifier((StatId)999, StatModifierOperation.FlatAdd, (fp)10m);
            });
        }

        [Test]
        public void SetModifierValue_UpdatesAndMarksDirty()
        {
            StatModifierHandle h = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            handler.FinalizeTick();

            bool result = handler.SetModifierValue(h, (fp)50m);

            Assert.IsTrue(result);
            Assert.AreEqual((fp)150m, handler.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void SetModifierValue_WrongOwnerUid_ReturnsFalse()
        {
            StatModifierHandle h = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            var wrongHandle = new StatModifierHandle(new UnitUid(999, 1, 0), h.StatId, h.StatSeq);

            Assert.IsFalse(handler.SetModifierValue(wrongHandle, (fp)50m));
        }

        [Test]
        public void RemoveModifier_RemovesAndMarksDirty()
        {
            StatModifierHandle h = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            handler.FinalizeTick();
            Assert.AreEqual((fp)150m, handler.GetStat(StatId.AttackDamage));

            bool result = handler.RemoveModifier(h);

            Assert.IsTrue(result);
            Assert.AreEqual((fp)100m, handler.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void RemoveModifier_AlreadyRemoved_ReturnsFalse()
        {
            StatModifierHandle h = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);

            Assert.IsTrue(handler.RemoveModifier(h));
            Assert.IsFalse(handler.RemoveModifier(h));
        }

        [Test]
        public void TryGetModifier_ReturnsCorrectView()
        {
            StatModifierHandle h = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)42m);

            Assert.IsTrue(handler.TryGetModifier(h, out StatModifierView view));
            Assert.AreEqual(StatId.AttackDamage, view.StatId);
            Assert.AreEqual(1u, view.StatSeq);
            Assert.AreEqual(StatModifierOperation.FlatAdd, view.Operation);
            Assert.AreEqual((fp)42m, view.Value);
        }

        [Test]
        public void ClearModifiers_RemovesAll()
        {
            handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            handler.AddModifier(StatId.MaxHealth, StatModifierOperation.FlatAdd, (fp)100m);
            handler.FinalizeTick();

            handler.ClearModifiers();

            Assert.AreEqual((fp)100m, handler.GetStat(StatId.AttackDamage));
            Assert.AreEqual((fp)500m, handler.GetStat(StatId.MaxHealth));
        }
    }
}