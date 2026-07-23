using System.Collections.Generic;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitWorldTests
    {
        [Test]
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }

        [Test]
        public void StableReadOrder_IsIndependentOfRegistrationOrder()
        {
            UnitUid[] expected =
            {
                new UnitUid(8, 99, 2),
                new UnitUid(9, 1, 0),
                new UnitUid(9, 1, 1),
                new UnitUid(9, 2, 0),
            };
            UnitUid[] reversed = { expected[3], expected[2], expected[1], expected[0] };
            UnitUid[] interleaved = { expected[2], expected[0], expected[3], expected[1] };

            UnitUid[] firstOrder = RegisterAndRead(reversed);
            UnitUid[] secondOrder = RegisterAndRead(interleaved);

            Assert.That(firstOrder, Is.EqualTo(expected));
            Assert.That(secondOrder, Is.EqualTo(expected));
        }

        [Test]
        public void SuccessfulUnregister_RemovesLookupAndAllowsReregistration()
        {
            var world = new UnitWorld();
            var unitUid = new UnitUid(400, 12, 6);
            var original = UnitTestFactory.CreateUnit(unitUid, UnitKind.Monster, 5, TeamId.Neutral);
            var replacement = UnitTestFactory.CreateUnit(unitUid, UnitKind.Monster, 5, TeamId.Neutral);
            world.RegisterUnit(original);

            world.UnregisterUnit(original);

            Assert.That(world.TryGetUnit(unitUid, out _), Is.False);
            Assert.That(world.GetAllUnits(), Is.Empty);

            world.RegisterUnit(replacement);

            Assert.That(world.TryGetUnit(unitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(replacement));
            Assert.That(world.GetAllUnits().Count, Is.EqualTo(1));
        }

        private static UnitUid[] RegisterAndRead(IReadOnlyList<UnitUid> registrationOrder)
        {
            var world = new UnitWorld();

            for (int index = 0; index < registrationOrder.Count; index++)
            {
                world.RegisterUnit(UnitTestFactory.CreateUnit(registrationOrder[index], UnitKind.Minion, 0, TeamId.Neutral));
            }

            IReadOnlyList<Unit> units = world.GetAllUnits();
            var result = new UnitUid[units.Count];

            for (int index = 0; index < units.Count; index++)
            {
                result[index] = units[index].UnitUid;
            }

            return result;
        }
    }
}