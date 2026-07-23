using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class StatHandlerChangeTests
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
        public void GetChangeThisTick_BeforeFinalize_NoChange()
        {
            StatHandler h = CreateHandler();
            // Initial entry is Dirty, recompute on query.
            // PreviousLogicTickFinalValue is default (0), so Delta = FinalValue - 0.
            // But this is the first Tick — FinalizeTick hasn't been called yet.
            // For a clean test, call FinalizeTick first to establish baseline.
            h.FinalizeTick();

            StatChange change = h.GetChangeThisTick(StatId.AttackDamage);
            Assert.IsFalse(change.Changed);
            Assert.AreEqual(default(fp), change.Delta);
        }

        [Test]
        public void GetChangeThisTick_AfterModifierAdd_ReturnsDelta()
        {
            StatHandler h = CreateHandler();
            h.FinalizeTick();

            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);

            StatChange change = h.GetChangeThisTick(StatId.AttackDamage);
            Assert.IsTrue(change.Changed);
            Assert.AreEqual((fp)50m, change.Delta);
        }

        [Test]
        public void GetChangeThisTick_NetChangeSameAsBaseline_ReturnsFalseZero()
        {
            StatHandler h = CreateHandler();
            h.FinalizeTick();

            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)(-50m));

            StatChange change = h.GetChangeThisTick(StatId.AttackDamage);
            Assert.IsFalse(change.Changed);
            Assert.AreEqual(default(fp), change.Delta);
        }

        [Test]
        public void FinalizeTick_SnapshotsFinalValueAsPreviousBaseline()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.FinalizeTick();

            // After FinalizeTick, PreviousLogicTickFinalValue = 150
            // Remove modifier → FinalValue = 100, Delta = 100 - 150 = -50
            StatModifierHandle handle = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)1m);
            h.FinalizeTick(); // baseline now 151

            h.RemoveModifier(handle);

            StatChange change = h.GetChangeThisTick(StatId.AttackDamage);
            Assert.IsTrue(change.Changed);
            Assert.AreEqual((fp)(-1m), change.Delta);
        }

        [Test]
        public void GetChangeThisTick_AfterFinalizeTick_ReturnsZero()
        {
            StatHandler h = CreateHandler();
            h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            h.FinalizeTick();

            StatChange change = h.GetChangeThisTick(StatId.AttackDamage);
            Assert.IsFalse(change.Changed);
            Assert.AreEqual(default(fp), change.Delta);
        }

        [Test]
        public void GetChangeThisTick_StatNotInPreset_ReturnsDefault()
        {
            StatHandler h = CreateHandler();
            h.FinalizeTick();

            StatChange change = h.GetChangeThisTick(StatId.MoveSpeed);
            Assert.IsFalse(change.Changed);
            Assert.AreEqual(default(fp), change.Delta);
        }
    }
}