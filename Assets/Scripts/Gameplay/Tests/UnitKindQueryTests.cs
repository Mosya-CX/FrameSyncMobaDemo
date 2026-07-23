using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitKindQueryTests
    {
        [Test]
        public void GetUnitsByKind_ReturnsOnlyMatchingKind()
        {
            var world = new UnitWorld();
            var hero = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 1, TeamId.Neutral);
            var minion = UnitTestFactory.CreateUnit(new UnitUid(11, 2, 0), UnitKind.Minion, 2, TeamId.Neutral);
            var monster = UnitTestFactory.CreateUnit(new UnitUid(12, 3, 0), UnitKind.Monster, 3, TeamId.Neutral);
            world.RegisterUnit(hero);
            world.RegisterUnit(minion);
            world.RegisterUnit(monster);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);

            Assert.That(heroes.Count, Is.EqualTo(1));
            Assert.That(heroes[0], Is.SameAs(hero));
        }

        [Test]
        public void GetUnitsBySubKind_ReturnsOnlyMatchingKindAndSubKind()
        {
            var world = new UnitWorld();
            var meleeMinion = UnitTestFactory.CreateUnit(new UnitUid(10, 2, 0), UnitKind.Minion, 10, TeamId.Neutral);
            var siegeMinion = UnitTestFactory.CreateUnit(new UnitUid(11, 2, 1), UnitKind.Minion, 20, TeamId.Neutral);
            var heroWithSameSubKind = UnitTestFactory.CreateUnit(new UnitUid(12, 1, 0), UnitKind.Hero, 10, TeamId.Neutral);
            world.RegisterUnit(meleeMinion);
            world.RegisterUnit(siegeMinion);
            world.RegisterUnit(heroWithSameSubKind);

            var result = world.GetUnitsBySubKind(UnitKind.Minion, 10);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0], Is.SameAs(meleeMinion));
        }

        [Test]
        public void GetUnitsByKind_ReturnsStableUidOrder_IndependentOfRegistrationOrder()
        {
            var first = UnitTestFactory.CreateUnit(new UnitUid(10, 2, 0), UnitKind.Minion, 1, TeamId.Neutral);
            var second = UnitTestFactory.CreateUnit(new UnitUid(11, 2, 0), UnitKind.Minion, 2, TeamId.Neutral);
            var third = UnitTestFactory.CreateUnit(new UnitUid(12, 2, 0), UnitKind.Minion, 3, TeamId.Neutral);

            var reversedWorld = new UnitWorld();
            reversedWorld.RegisterUnit(third);
            reversedWorld.RegisterUnit(second);
            reversedWorld.RegisterUnit(first);

            var interleavedWorld = new UnitWorld();
            interleavedWorld.RegisterUnit(second);
            interleavedWorld.RegisterUnit(first);
            interleavedWorld.RegisterUnit(third);

            var reversedResult = CollectUids(reversedWorld.GetUnitsByKind(UnitKind.Minion));
            var interleavedResult = CollectUids(interleavedWorld.GetUnitsByKind(UnitKind.Minion));

            Assert.That(reversedResult, Is.EqualTo(new[] { first.UnitUid, second.UnitUid, third.UnitUid }));
            Assert.That(interleavedResult, Is.EqualTo(new[] { first.UnitUid, second.UnitUid, third.UnitUid }));
        }

        [Test]
        public void GetUnitsByKind_WithNoMatchingUnits_ReturnsEmpty()
        {
            var world = new UnitWorld();
            world.RegisterUnit(UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, TeamId.Neutral));

            var result = world.GetUnitsByKind(UnitKind.Structure);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnitsBySubKind_WithNoMatchingUnits_ReturnsEmpty()
        {
            var world = new UnitWorld();
            world.RegisterUnit(UnitTestFactory.CreateUnit(new UnitUid(10, 2, 0), UnitKind.Minion, 1, TeamId.Neutral));

            var result = world.GetUnitsBySubKind(UnitKind.Minion, 999);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Queries_DoNotMutateRegistryOrUnits()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 2, 0), UnitKind.Minion, 5, TeamId.Neutral);
            world.RegisterUnit(unit);

            _ = world.GetUnitsByKind(UnitKind.Minion);
            _ = world.GetUnitsBySubKind(UnitKind.Minion, 5);
            _ = world.GetUnitsByKind(UnitKind.Hero);

            Assert.That(world.GetAllUnits().Count, Is.EqualTo(1));
            Assert.That(world.GetAllUnits()[0], Is.SameAs(unit));
            Assert.That(unit.UnitKind, Is.EqualTo(UnitKind.Minion));
            Assert.That(unit.UnitSubKindId, Is.EqualTo(5));
        }

        [Test]
        public void UnitKind_AndUnitSubKindId_AreImmutableAfterConstruction()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 42, TeamId.Neutral);

            Assert.That(unit.UnitKind, Is.EqualTo(UnitKind.Hero));
            Assert.That(unit.UnitSubKindId, Is.EqualTo(42));
        }

        [Test]
        public void GetUnitsByKind_AfterUnregister_ReflectsCurrentRegistry()
        {
            var world = new UnitWorld();
            var remaining = UnitTestFactory.CreateUnit(new UnitUid(10, 2, 0), UnitKind.Minion, 1, TeamId.Neutral);
            var removed = UnitTestFactory.CreateUnit(new UnitUid(11, 2, 0), UnitKind.Minion, 2, TeamId.Neutral);
            world.RegisterUnit(remaining);
            world.RegisterUnit(removed);

            world.UnregisterUnit(removed);

            var result = world.GetUnitsByKind(UnitKind.Minion);
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0], Is.SameAs(remaining));
        }

        private static UnitUid[] CollectUids(System.Collections.Generic.IReadOnlyList<Unit> units)
        {
            var result = new UnitUid[units.Count];
            for (int index = 0; index < units.Count; index++)
            {
                result[index] = units[index].UnitUid;
            }
            return result;
        }
    }
}