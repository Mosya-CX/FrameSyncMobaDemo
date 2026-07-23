using System;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class CapabilityStateTests
    {
        [Test]
        public void NewlyConstructedUnit_HasAllCapabilitiesTrue()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);

            Assert.That(unit.CapabilityState.CanMove, Is.True);
            Assert.That(unit.CapabilityState.CanAttack, Is.True);
            Assert.That(unit.CapabilityState.CanCast, Is.True);
            Assert.That(unit.CapabilityState.CanTurn, Is.True);
            Assert.That(unit.CapabilityState.IsTargetable, Is.True);
        }

        [Test]
        public void ConfirmUnitDeath_DisablesAllCapabilities()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);

            Assert.That(unit.CapabilityState.CanMove, Is.False);
            Assert.That(unit.CapabilityState.CanAttack, Is.False);
            Assert.That(unit.CapabilityState.CanCast, Is.False);
            Assert.That(unit.CapabilityState.CanTurn, Is.False);
            Assert.That(unit.CapabilityState.IsTargetable, Is.False);
        }

        [Test]
        public void RequestEnterDying_DoesNotDisableCapabilities()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);

            Assert.That(unit.CapabilityState.CanMove, Is.True);
            Assert.That(unit.CapabilityState.CanAttack, Is.True);
            Assert.That(unit.CapabilityState.CanCast, Is.True);
            Assert.That(unit.CapabilityState.CanTurn, Is.True);
            Assert.That(unit.CapabilityState.IsTargetable, Is.True);
        }

        [Test]
        public void BeginRespawn_KeepsCapabilitiesDisabled()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);
            world.BeginRespawn(unit);

            Assert.That(unit.CapabilityState.CanMove, Is.False);
            Assert.That(unit.CapabilityState.CanAttack, Is.False);
            Assert.That(unit.CapabilityState.CanCast, Is.False);
            Assert.That(unit.CapabilityState.CanTurn, Is.False);
            Assert.That(unit.CapabilityState.IsTargetable, Is.False);
        }

        [Test]
        public void CompleteRespawn_ResetsAllCapabilitiesToTrue()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.ConfirmUnitDeath(unit);
            world.BeginRespawn(unit);
            world.CompleteRespawn(unit);

            Assert.That(unit.CapabilityState.CanMove, Is.True);
            Assert.That(unit.CapabilityState.CanAttack, Is.True);
            Assert.That(unit.CapabilityState.CanCast, Is.True);
            Assert.That(unit.CapabilityState.CanTurn, Is.True);
            Assert.That(unit.CapabilityState.IsTargetable, Is.True);
        }

        [Test]
        public void FullDeathRespawnCycle_RestoresCapabilities()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);

            world.RequestEnterDying(unit);
            Assert.That(unit.CapabilityState.CanMove, Is.True);

            world.ConfirmUnitDeath(unit);
            Assert.That(unit.CapabilityState.CanMove, Is.False);
            Assert.That(unit.CapabilityState.IsTargetable, Is.False);

            world.BeginRespawn(unit);
            Assert.That(unit.CapabilityState.CanMove, Is.False);

            world.CompleteRespawn(unit);
            Assert.That(unit.CapabilityState.CanMove, Is.True);
            Assert.That(unit.CapabilityState.IsTargetable, Is.True);
        }

        [Test]
        public void DeathRespawnOnOneUnit_DoesNotAffectAnother()
        {
            var world = new UnitWorld();
            var unitA = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            var unitB = UnitTestFactory.CreateUnit(new UnitUid(11, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unitA);
            world.RegisterUnit(unitB);

            world.RequestEnterDying(unitA);
            world.ConfirmUnitDeath(unitA);

            Assert.That(unitA.CapabilityState.CanMove, Is.False);
            Assert.That(unitA.CapabilityState.IsTargetable, Is.False);
            Assert.That(unitB.CapabilityState.CanMove, Is.True);
            Assert.That(unitB.CapabilityState.IsTargetable, Is.True);
        }

        [Test]
        public void RequestRecoverFromDying_KeepsCapabilitiesEnabled()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral);
            world.RegisterUnit(unit);
            world.RequestEnterDying(unit);
            world.RequestRecoverFromDying(unit);

            Assert.That(unit.CapabilityState.CanMove, Is.True);
            Assert.That(unit.CapabilityState.IsTargetable, Is.True);
        }
    }
}