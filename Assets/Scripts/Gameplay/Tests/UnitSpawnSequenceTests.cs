using System;
using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class UnitSpawnSequenceTests
    {
        private SimulationTickContextController controller;

        [SetUp]
        public void SetUp()
        {
            controller = new SimulationTickContextController();
        }

        [TearDown]
        public void TearDown()
        {
            if (controller.IsTickActive)
            {
                controller.EndTick();
            }
        }

        [Test]
        public void AllocateSpawnSequence_MonotonicWithinTick()
        {
            controller.BeginTick(1000, ExecutionMode.ServerAuthority);
            var world = new UnitWorld();

            Assert.AreEqual(0, world.AllocateSpawnSequence());
            Assert.AreEqual(1, world.AllocateSpawnSequence());
            Assert.AreEqual(2, world.AllocateSpawnSequence());
        }

        [Test]
        public void AllocateSpawnSequence_Overflow_ThrowsDeterministicSimulationException()
        {
            controller.BeginTick(1000, ExecutionMode.ServerAuthority);
            var world = new UnitWorld();

            for (int i = 0; i < 256; i++)
            {
                Assert.AreEqual((byte)i, world.AllocateSpawnSequence());
            }

            Assert.Throws<DeterministicSimulationException>(() =>
            {
                world.AllocateSpawnSequence();
            });
        }

        [Test]
        public void AllocateSpawnSequence_TickRollover_ResetsSequence()
        {
            controller.BeginTick(1000, ExecutionMode.ServerAuthority);
            var world = new UnitWorld();

            world.AllocateSpawnSequence();
            world.AllocateSpawnSequence();
            controller.EndTick();

            controller.BeginTick(1001, ExecutionMode.ServerAuthority);
            Assert.AreEqual(0, world.AllocateSpawnSequence());
        }

        [Test]
        public void AllocateSpawnSequence_SameTickSameOrder_SameSequence()
        {
            var world1 = new UnitWorld();
            var world2 = new UnitWorld();

            controller.BeginTick(500, ExecutionMode.ServerAuthority);
            byte a1 = world1.AllocateSpawnSequence();
            byte a2 = world1.AllocateSpawnSequence();
            byte a3 = world1.AllocateSpawnSequence();
            controller.EndTick();

            controller.BeginTick(500, ExecutionMode.ServerAuthority);
            byte b1 = world2.AllocateSpawnSequence();
            byte b2 = world2.AllocateSpawnSequence();
            byte b3 = world2.AllocateSpawnSequence();
            controller.EndTick();

            Assert.AreEqual(a1, b1);
            Assert.AreEqual(a2, b2);
            Assert.AreEqual(a3, b3);
        }

        [Test]
        public void AllocateSpawnSequence_NoActiveTick_Throws()
        {
            var world = new UnitWorld();
            Assert.Throws<InvalidOperationException>(() =>
            {
                world.AllocateSpawnSequence();
            });
        }

        [Test]
        public void UnitPrototypeId_ImmutableAfterConstruction()
        {
            var unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 1, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral,
                unitPrototypeId: 42);

            Assert.AreEqual(42, unit.UnitPrototypeId);
        }

        [Test]
        public void Unit_BaseGoldAndExperience_PreservedAfterConstruction()
        {
            var unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 1, 0),
                UnitKind.Monster,
                5,
                new TeamId(2),
                unitPrototypeId: 7,
                baseGoldValue: 300,
                baseExperienceValue: 150);

            Assert.AreEqual(7, unit.UnitPrototypeId);
            Assert.AreEqual(300, unit.BaseGoldValue);
            Assert.AreEqual(150, unit.BaseExperienceValue);
        }

        [Test]
        public void Unit_DefaultPrototypeAndReward_AreZero()
        {
            var unit = UnitTestFactory.CreateUnit(
                new UnitUid(10, 1, 0),
                UnitKind.Hero,
                0,
                TeamId.Neutral);

            Assert.AreEqual(0, unit.UnitPrototypeId);
            Assert.AreEqual(0, unit.BaseGoldValue);
            Assert.AreEqual(0, unit.BaseExperienceValue);
        }
    }
}