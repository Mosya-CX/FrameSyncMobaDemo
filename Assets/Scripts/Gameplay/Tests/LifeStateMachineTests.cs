using System;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class LifeStateMachineTests
    {
        [Test]
        public void NewlyConstructedUnit_HasLifeStateAlive()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void RequestEnterDying_TransitionsAliveToDying()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);

            world.RequestEnterDying(unit);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Dying));
        }

        [Test]
        public void RequestRecoverFromDying_TransitionsDyingToAlive()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);

            world.RequestRecoverFromDying(unit);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void ConfirmUnitDeath_TransitionsDyingToDead()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);

            world.ConfirmUnitDeath(unit);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Dead));
        }

        [Test]
        public void BeginRespawn_TransitionsDeadToRespawning()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);

            world.BeginRespawn(unit);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Respawning));
        }

        [Test]
        public void CompleteRespawn_TransitionsRespawningToAlive()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);
            world.BeginRespawn(unit);

            world.CompleteRespawn(unit);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void FullDeathRespawnCycle_ReturnsToAlive()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);

            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);
            world.BeginRespawn(unit);
            world.CompleteRespawn(unit);

            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void RequestEnterDying_FromDying_ThrowsAndLeavesStateUnchanged()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);

            Assert.Throws<InvalidOperationException>(() => world.RequestEnterDying(unit));
            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Dying));
        }

        [Test]
        public void RequestEnterDying_FromDead_ThrowsAndLeavesStateUnchanged()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);

            Assert.Throws<InvalidOperationException>(() => world.RequestEnterDying(unit));
            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Dead));
        }

        [Test]
        public void ConfirmUnitDeath_FromAlive_ThrowsAndLeavesStateUnchanged()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);

            Assert.Throws<InvalidOperationException>(() => world.ConfirmUnitDeath(unit));
            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void RequestRecoverFromDying_FromAlive_ThrowsAndLeavesStateUnchanged()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);

            Assert.Throws<InvalidOperationException>(() => world.RequestRecoverFromDying(unit));
            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void BeginRespawn_FromAlive_ThrowsAndLeavesStateUnchanged()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);

            Assert.Throws<InvalidOperationException>(() => world.BeginRespawn(unit));
            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Alive));
        }

        [Test]
        public void CompleteRespawn_FromDead_ThrowsAndLeavesStateUnchanged()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);

            Assert.Throws<InvalidOperationException>(() => world.CompleteRespawn(unit));
            Assert.That(unit.LifeState, Is.EqualTo(LifeState.Dead));
        }

        [Test]
        public void NullUnit_ThrowsArgumentNullException()
        {
            var world = new UnitWorld();

            Assert.Throws<ArgumentNullException>(() => world.RequestEnterDying(null));
            Assert.Throws<ArgumentNullException>(() => world.RequestRecoverFromDying(null));
            Assert.Throws<ArgumentNullException>(() => world.ConfirmUnitDeath(null));
            Assert.Throws<ArgumentNullException>(() => world.BeginRespawn(null));
            Assert.Throws<ArgumentNullException>(() => world.CompleteRespawn(null));
        }

        [Test]
        public void TransitionOnOneUnit_DoesNotAffectAnother()
        {
            var world = new UnitWorld();
            var unitA = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            var unitB = UnitTestFactory.CreateUnit(new UnitUid(11, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unitA);
            world.RegisterUnit(unitB);

            world.RequestEnterDying(unitA);
            world.ConfirmUnitDeath(unitA);

            Assert.That(unitA.LifeState, Is.EqualTo(LifeState.Dead));
            Assert.That(unitB.LifeState, Is.EqualTo(LifeState.Alive));
        }
    }
}