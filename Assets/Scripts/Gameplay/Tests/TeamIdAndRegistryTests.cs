using System;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class TeamIdAndRegistryTests
    {
        #region TeamId

        [Test]
        public void TeamId_Default_IsNeutral()
        {
            Assert.That(TeamId.Neutral.Value, Is.EqualTo(0));
        }

        [Test]
        public void TeamId_ConstructedWithByte_StoresValue()
        {
            var team = new TeamId(3);

            Assert.That(team.Value, Is.EqualTo(3));
        }

        [Test]
        public void TeamId_Equality()
        {
            var a = new TeamId(1);
            var b = new TeamId(1);
            var c = new TeamId(2);

            Assert.That(a == b, Is.True);
            Assert.That(a != c, Is.True);
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals(c), Is.False);
        }

        [Test]
        public void TeamId_GetHashCode_Stable()
        {
            var a = new TeamId(5);
            var b = new TeamId(5);

            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        #endregion

        #region TeamRegistry

        [Test]
        public void RegisterTeam_ThenTryGet_ReturnsInfo()
        {
            var registry = new TeamRegistry();
            var teamId = new TeamId(1);

            registry.RegisterTeam(teamId, "Blue");

            Assert.That(registry.TryGetTeam(teamId, out TeamInfo info), Is.True);
            Assert.That(info.TeamId, Is.EqualTo(teamId));
            Assert.That(info.Name, Is.EqualTo("Blue"));
        }

        [Test]
        public void RegisterTeam_DuplicateSameName_IsIdempotent()
        {
            var registry = new TeamRegistry();
            var teamId = new TeamId(1);

            registry.RegisterTeam(teamId, "Blue");
            registry.RegisterTeam(teamId, "Blue");

            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegisterTeam_DuplicateDifferentName_Throws()
        {
            var registry = new TeamRegistry();
            var teamId = new TeamId(1);

            registry.RegisterTeam(teamId, "Blue");

            Assert.Throws<InvalidOperationException>(() => registry.RegisterTeam(teamId, "Red"));
            Assert.That(registry.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryGetTeam_Unregistered_ReturnsFalse()
        {
            var registry = new TeamRegistry();

            Assert.That(registry.TryGetTeam(new TeamId(99), out _), Is.False);
        }

        [Test]
        public void IsRegistered_ReflectsRegistration()
        {
            var registry = new TeamRegistry();
            var team1 = new TeamId(1);
            var team2 = new TeamId(2);

            registry.RegisterTeam(team1, "Blue");

            Assert.That(registry.IsRegistered(team1), Is.True);
            Assert.That(registry.IsRegistered(team2), Is.False);
        }

        [Test]
        public void Count_ReflectsNumberOfTeams()
        {
            var registry = new TeamRegistry();
            registry.RegisterTeam(new TeamId(1), "Blue");
            registry.RegisterTeam(new TeamId(2), "Red");
            registry.RegisterTeam(new TeamId(3), "Neutral");

            Assert.That(registry.Count, Is.EqualTo(3));
        }

        #endregion

        #region Unit.TeamId + GetByTeam

        [Test]
        public void Unit_TeamId_IsSetAtConstruction()
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, new TeamId(2));

            Assert.That(unit.TeamId, Is.EqualTo(new TeamId(2)));
        }

        [Test]
        public void GetUnitsByTeam_ReturnsOnlyMatchingTeam()
        {
            var world = new UnitWorld();
            var blue1 = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, new TeamId(1));
            var blue2 = UnitTestFactory.CreateUnit(new UnitUid(11, 1, 0), UnitKind.Minion, 0, new TeamId(1));
            var red1 = UnitTestFactory.CreateUnit(new UnitUid(12, 1, 0), UnitKind.Hero, 0, new TeamId(2));
            world.RegisterUnit(blue1);
            world.RegisterUnit(blue2);
            world.RegisterUnit(red1);

            var blueTeam = world.GetUnitsByTeam(new TeamId(1));

            Assert.That(blueTeam.Count, Is.EqualTo(2));
            Assert.That(blueTeam[0], Is.SameAs(blue1));
            Assert.That(blueTeam[1], Is.SameAs(blue2));
        }

        [Test]
        public void GetUnitsByTeam_StableUidOrder_IndependentOfRegistrationOrder()
        {
            var first = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Minion, 0, new TeamId(1));
            var second = UnitTestFactory.CreateUnit(new UnitUid(11, 1, 0), UnitKind.Minion, 0, new TeamId(1));
            var third = UnitTestFactory.CreateUnit(new UnitUid(12, 1, 0), UnitKind.Minion, 0, new TeamId(1));

            var reversedWorld = new UnitWorld();
            reversedWorld.RegisterUnit(third);
            reversedWorld.RegisterUnit(second);
            reversedWorld.RegisterUnit(first);

            var result = reversedWorld.GetUnitsByTeam(new TeamId(1));

            Assert.That(result[0], Is.SameAs(first));
            Assert.That(result[1], Is.SameAs(second));
            Assert.That(result[2], Is.SameAs(third));
        }

        [Test]
        public void GetUnitsByTeam_NoMatchingTeam_ReturnsEmpty()
        {
            var world = new UnitWorld();
            world.RegisterUnit(UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, new TeamId(1)));

            var result = world.GetUnitsByTeam(new TeamId(99));

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void GetUnitsByTeam_DoesNotMutateRegistry()
        {
            var world = new UnitWorld();
            world.RegisterUnit(UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 0, new TeamId(1)));

            _ = world.GetUnitsByTeam(new TeamId(1));
            _ = world.GetUnitsByTeam(new TeamId(2));

            Assert.That(world.GetAllUnits().Count, Is.EqualTo(1));
        }

        #endregion
    }
}