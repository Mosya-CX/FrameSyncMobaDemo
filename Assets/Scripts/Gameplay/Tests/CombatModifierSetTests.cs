using System.Collections.Generic;
using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class CombatModifierSetTests
    {
        private Unit CreateUnit()
        {
            return UnitTestFactory.CreateUnit(
                new UnitUid(100, 1, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);
        }

        private static CombatModifierRecord CreateRecord(int tick, string key)
        {
            return new CombatModifierRecord
            {
                Id = CombatModifierId.Create(tick, key),
            };
        }

        [Test]
        public void Attach_ReturnsValidHandle()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var record = CreateRecord(1000, "Buff.Berserk.DamageReduction");

            CombatModifierHandle handle = set.Attach(record);

            Assert.IsTrue(handle.IsValid);
            Assert.AreEqual(unit.UnitUid, handle.OwnerUnitUid);
            Assert.AreEqual(record.Id, handle.ModifierId);
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Attach_DuplicateId_ThrowsDeterministicSimulationException()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var record = CreateRecord(1000, "Buff.Berserk.DamageReduction");
            set.Attach(record);

            var duplicate = CreateRecord(1000, "Buff.Berserk.DamageReduction");

            Assert.Throws<DeterministicSimulationException>(() =>
            {
                set.Attach(duplicate);
            });
        }

        [Test]
        public void Detach_ValidHandle_RemovesRecord()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var record = CreateRecord(1000, "Buff.Bersenk.DamageReduction");
            CombatModifierHandle handle = set.Attach(record);

            bool result = set.Detach(handle);

            Assert.IsTrue(result);
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Detach_WrongOwnerUid_ReturnsFalse()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var record = CreateRecord(1000, "Buff.Bersenk.DamageReduction");
            CombatModifierHandle handle = set.Attach(record);

            var wrongHandle = new CombatModifierHandle(
                new UnitUid(999, 1, 0), handle.ModifierId);

            Assert.IsFalse(set.Detach(wrongHandle));
            Assert.AreEqual(1, set.Count);
        }

        [Test]
        public void Detach_AlreadyDetached_ReturnsFalse()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var record = CreateRecord(1000, "Buff.Bersenk.DamageReduction");
            CombatModifierHandle handle = set.Attach(record);

            Assert.IsTrue(set.Detach(handle));
            Assert.IsFalse(set.Detach(handle));
        }

        [Test]
        public void Collect_OutputSortedByModifierId()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);

            // Insert in reverse Id order (tick 300 first, tick 100 last)
            set.Attach(CreateRecord(300, "C"));
            set.Attach(CreateRecord(100, "A"));
            set.Attach(CreateRecord(200, "B"));

            var output = new List<CombatModifierRecord>();
            set.Collect(output);

            Assert.AreEqual(3, output.Count);
            Assert.AreEqual(CombatModifierId.Create(100, "A"), output[0].Id);
            Assert.AreEqual(CombatModifierId.Create(200, "B"), output[1].Id);
            Assert.AreEqual(CombatModifierId.Create(300, "C"), output[2].Id);
        }

        [Test]
        public void Collect_Deterministic_InsertOrderInvariant()
        {
            var unit1 = CreateUnit();
            var set1 = new CombatModifierSet(unit1);
            set1.Attach(CreateRecord(100, "A"));
            set1.Attach(CreateRecord(200, "B"));
            set1.Attach(CreateRecord(300, "C"));

            var unit2 = CreateUnit();
            var set2 = new CombatModifierSet(unit2);
            set2.Attach(CreateRecord(300, "C"));
            set2.Attach(CreateRecord(100, "A"));
            set2.Attach(CreateRecord(200, "B"));

            var out1 = new List<CombatModifierRecord>();
            var out2 = new List<CombatModifierRecord>();
            set1.Collect(out1);
            set2.Collect(out2);

            Assert.AreEqual(out1.Count, out2.Count);
            for (int i = 0; i < out1.Count; i++)
            {
                Assert.AreEqual(out1[i].Id, out2[i].Id);
            }
        }

        [Test]
        public void Clear_RemovesAll()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            set.Attach(CreateRecord(100, "A"));
            set.Attach(CreateRecord(200, "B"));

            set.Clear();

            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Detach_SwapRemove_MaintainsIndexConsistency()
        {
            var unit = CreateUnit();
            var set = new CombatModifierSet(unit);
            var h1 = set.Attach(CreateRecord(100, "A"));
            var h2 = set.Attach(CreateRecord(200, "B"));
            var h3 = set.Attach(CreateRecord(300, "C"));

            // Detach the middle one (triggers swap-remove)
            Assert.IsTrue(set.Detach(h2));

            // Remaining should be detachable
            Assert.IsTrue(set.Detach(h1));
            Assert.IsTrue(set.Detach(h3));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void CombatModifierId_SameTickSameKey_SameId()
        {
            ulong id1 = CombatModifierId.Create(1000, "Buff.Berserk.DamageReduction");
            ulong id2 = CombatModifierId.Create(1000, "Buff.Berserk.DamageReduction");
            Assert.AreEqual(id1, id2);
        }

        [Test]
        public void CombatModifierId_DifferentTick_DifferentId()
        {
            ulong id1 = CombatModifierId.Create(1000, "Buff.Berserk.DamageReduction");
            ulong id2 = CombatModifierId.Create(1001, "Buff.Berserk.DamageReduction");
            Assert.AreNotEqual(id1, id2);
        }
    }
}