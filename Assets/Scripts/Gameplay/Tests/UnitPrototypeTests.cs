using System;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class UnitPrototypeTests
    {
        [Test]
        public void UnitPrototype_DefaultValues()
        {
            var proto = new UnitPrototype();
            Assert.AreEqual(0, proto.UnitPrototypeId);
            Assert.IsNull(proto.Name);
            Assert.AreEqual(0, proto.RuntimeEntityPrefabId);
            Assert.AreEqual(UnitKind.Hero, proto.UnitKind);
            Assert.AreEqual(0, proto.UnitSubKindId);
            Assert.IsNull(proto.BaseStats);
            Assert.AreEqual(0, proto.BaseGoldValue);
            Assert.AreEqual(0, proto.BaseExperienceValue);
        }

        [Test]
        public void UnitPrototype_PreservesAllFields()
        {
            var table = StatTestHelpers.CreateDefaultTable();
            var preset = StatTestHelpers.CreateSimplePreset();

            var proto = new UnitPrototype
            {
                UnitPrototypeId = 42,
                Name = "TestMinion",
                RuntimeEntityPrefabId = 101,
                UnitKind = UnitKind.Minion,
                UnitSubKindId = 5,
                BaseStats = preset,
                BaseGoldValue = 300,
                BaseExperienceValue = 150,
            };

            Assert.AreEqual(42, proto.UnitPrototypeId);
            Assert.AreEqual("TestMinion", proto.Name);
            Assert.AreEqual(101, proto.RuntimeEntityPrefabId);
            Assert.AreEqual(UnitKind.Minion, proto.UnitKind);
            Assert.AreEqual(5, proto.UnitSubKindId);
            Assert.AreSame(preset, proto.BaseStats);
            Assert.AreEqual(300, proto.BaseGoldValue);
            Assert.AreEqual(150, proto.BaseExperienceValue);
        }
    }
}