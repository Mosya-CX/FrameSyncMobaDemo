using System;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class GlobalUnitPrototypeTableTests
    {
        private StatDefinitionTable table;
        private StatPreset validPreset;

        [SetUp]
        public void SetUp()
        {
            table = StatTestHelpers.CreateDefaultTable();
            validPreset = StatTestHelpers.CreateSimplePreset();
        }

        private UnitPrototype CreatePrototype(int id, StatPreset stats = null)
        {
            return new UnitPrototype
            {
                UnitPrototypeId = id,
                Name = $"Proto_{id}",
                UnitKind = UnitKind.Hero,
                BaseStats = stats,
            };
        }

        [Test]
        public void Add_And_TryGet()
        {
            var globalTable = new GlobalUnitPrototypeTable();
            var proto = CreatePrototype(42, validPreset);

            globalTable.Add(proto);

            Assert.IsTrue(globalTable.TryGet(42, out UnitPrototype found));
            Assert.AreSame(proto, found);
            Assert.AreEqual(1, globalTable.Count);
        }

        [Test]
        public void Add_DuplicateId_Throws()
        {
            var globalTable = new GlobalUnitPrototypeTable();
            globalTable.Add(CreatePrototype(42));

            Assert.Throws<ArgumentException>(() =>
            {
                globalTable.Add(CreatePrototype(42));
            });
        }

        [Test]
        public void TryGet_MissingId_ReturnsFalse()
        {
            var globalTable = new GlobalUnitPrototypeTable();

            Assert.IsFalse(globalTable.TryGet(999, out UnitPrototype found));
            Assert.IsNull(found);
        }

        [Test]
        public void ValidateAll_ValidPasses()
        {
            var globalTable = new GlobalUnitPrototypeTable();
            globalTable.Add(CreatePrototype(1, validPreset));
            globalTable.Add(CreatePrototype(2, validPreset));

            Assert.DoesNotThrow(() => globalTable.ValidateAll(table));
        }

        [Test]
        public void ValidateAll_DuplicateStatId_Throws()
        {
            var badPreset = new StatPreset();
            badPreset.Stats.Add(new StatPresetEntry { StatId = StatId.AttackDamage, BaseValue = (fp)100m });
            badPreset.Stats.Add(new StatPresetEntry { StatId = StatId.AttackDamage, BaseValue = (fp)50m });

            var globalTable = new GlobalUnitPrototypeTable();
            globalTable.Add(CreatePrototype(1, badPreset));

            Assert.Throws<InvalidOperationException>(() => globalTable.ValidateAll(table));
        }

        [Test]
        public void ValidateAll_InvalidStatId_Throws()
        {
            var badPreset = new StatPreset();
            badPreset.Stats.Add(new StatPresetEntry { StatId = (StatId)999, BaseValue = (fp)100m });

            var globalTable = new GlobalUnitPrototypeTable();
            globalTable.Add(CreatePrototype(1, badPreset));

            Assert.Throws<InvalidOperationException>(() => globalTable.ValidateAll(table));
        }

        [Test]
        public void ValidateAll_NullBaseStats_SkipsValidation()
        {
            var globalTable = new GlobalUnitPrototypeTable();
            globalTable.Add(CreatePrototype(1, null));

            Assert.DoesNotThrow(() => globalTable.ValidateAll(table));
        }

        [Test]
        public void ValidateAll_GrowthValueOnNonGrowthStat_Throws()
        {
            // Armor has SupportsLevelGrowth = false
            var badPreset = new StatPreset();
            badPreset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = (fp)30m,
                GrowthValue = (fp)5m, // Armor doesn't support growth
            });

            var globalTable = new GlobalUnitPrototypeTable();
            globalTable.Add(CreatePrototype(1, badPreset));

            Assert.Throws<InvalidOperationException>(() => globalTable.ValidateAll(table));
        }
    }
}