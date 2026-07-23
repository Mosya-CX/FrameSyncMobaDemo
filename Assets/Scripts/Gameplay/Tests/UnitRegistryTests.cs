using System;
using NUnit.Framework;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class UnitRegistryTests
    {
        [Test]
        public void DuplicateUid_IsRejectedWithoutChangingRegisteredRuntime()
        {
            var registry = new UnitRegistry();
            var unitUid = new UnitUid(100, 7, 2);
            var registered = UnitTestFactory.CreateUnit(unitUid, UnitKind.Hero, 1, TeamId.Neutral);
            var duplicate = UnitTestFactory.CreateUnit(unitUid, UnitKind.Hero, 1, TeamId.Neutral);
            registry.Register(registered);

            Assert.Throws<InvalidOperationException>(() => registry.Register(duplicate));

            Assert.That(registry.GetAll().Count, Is.EqualTo(1));
            Assert.That(registry.GetAll()[0], Is.SameAs(registered));
            Assert.That(registry.TryGet(unitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(registered));
        }

        [Test]
        public void MissingAndAliasUnregister_AreRejectedWithoutMutation()
        {
            var registry = new UnitRegistry();
            var registered = UnitTestFactory.CreateUnit(new UnitUid(200, 3, 4), UnitKind.Minion, 2, TeamId.Neutral);
            var alias = UnitTestFactory.CreateUnit(registered.UnitUid, UnitKind.Minion, 2, TeamId.Neutral);
            var missing = UnitTestFactory.CreateUnit(new UnitUid(201, 3, 4), UnitKind.Minion, 2, TeamId.Neutral);
            registry.Register(registered);

            Assert.Throws<InvalidOperationException>(() => registry.Unregister(alias));
            Assert.Throws<InvalidOperationException>(() => registry.Unregister(missing));

            Assert.That(registry.GetAll().Count, Is.EqualTo(1));
            Assert.That(registry.GetAll()[0], Is.SameAs(registered));
            Assert.That(registry.TryGet(registered.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(registered));
        }

        [Test]
        public void NullMutationInputs_AreRejected()
        {
            var registry = new UnitRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentNullException>(() => registry.Unregister(null));
            Assert.That(registry.GetAll(), Is.Empty);
        }
    }
}