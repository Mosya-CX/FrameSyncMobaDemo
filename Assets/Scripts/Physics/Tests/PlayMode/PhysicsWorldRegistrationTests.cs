using System;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics.Tests
{
    public sealed class PhysicsWorldRegistrationTests
    {
        private GameObject ownerGo;

        [SetUp]
        public void SetUp()
        {
            ownerGo = new GameObject("PhysicsWorldTestOwner");
        }

        [TearDown]
        public void TearDown()
        {
            if (ownerGo != null)
            {
                UnityEngine.Object.DestroyImmediate(ownerGo);
            }
        }

        private PhysicsEntity2D CreateEntity()
        {
            return ownerGo.AddComponent<PhysicsEntity2D>();
        }

        [Test]
        public void RegisterUnit_AddsToUnitEntities()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();

            world.RegisterUnit(entity);

            Assert.That(world.UnitEntities.Count, Is.EqualTo(1));
            Assert.That(world.UnitEntities[0], Is.SameAs(entity));
            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void RegisterProjectile_AddsToProjectileEntities()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();

            world.RegisterProjectile(entity);

            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(1));
            Assert.That(world.ProjectileEntities[0], Is.SameAs(entity));
            Assert.That(world.UnitEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void Unregister_FromUnitList_RemovesEntity()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterUnit(entity);

            world.Unregister(entity);

            Assert.That(world.UnitEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void Unregister_FromProjectileList_RemovesEntity()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterProjectile(entity);

            world.Unregister(entity);

            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void UnregisterUnit_RemovesFromUnitListOnly()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterUnit(entity);

            world.UnregisterUnit(entity);

            Assert.That(world.UnitEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void UnregisterProjectile_RemovesFromProjectileListOnly()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterProjectile(entity);

            world.UnregisterProjectile(entity);

            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void Unregister_NotRegistered_Throws()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();

            Assert.Throws<InvalidOperationException>(() => world.Unregister(entity));
        }

        [Test]
        public void UnregisterUnit_WrongList_Throws()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterProjectile(entity);

            Assert.Throws<InvalidOperationException>(() => world.UnregisterUnit(entity));
        }

        [Test]
        public void RegisterUnit_DuplicateInSameList_Throws()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterUnit(entity);

            Assert.Throws<InvalidOperationException>(() => world.RegisterUnit(entity));
            Assert.That(world.UnitEntities.Count, Is.EqualTo(1));
        }

        [Test]
        public void RegisterUnit_AlreadyInProjectileList_Throws()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();
            world.RegisterProjectile(entity);

            Assert.Throws<InvalidOperationException>(() => world.RegisterUnit(entity));
        }

        [Test]
        public void RegisterNull_Throws()
        {
            var world = new PhysicsWorld();

            Assert.Throws<ArgumentNullException>(() => world.RegisterUnit(null));
            Assert.Throws<ArgumentNullException>(() => world.RegisterProjectile(null));
            Assert.Throws<ArgumentNullException>(() => world.Unregister(null));
        }

        [Test]
        public void MultipleEntities_MaintainInsertionOrder()
        {
            var world = new PhysicsWorld();
            var go1 = new GameObject("E1");
            var go2 = new GameObject("E2");
            var go3 = new GameObject("E3");
            try
            {
                var e1 = go1.AddComponent<PhysicsEntity2D>();
                var e2 = go2.AddComponent<PhysicsEntity2D>();
                var e3 = go3.AddComponent<PhysicsEntity2D>();

                world.RegisterUnit(e1);
                world.RegisterUnit(e2);
                world.RegisterUnit(e3);

                Assert.That(world.UnitEntities[0], Is.SameAs(e1));
                Assert.That(world.UnitEntities[1], Is.SameAs(e2));
                Assert.That(world.UnitEntities[2], Is.SameAs(e3));

                world.Unregister(e2);

                Assert.That(world.UnitEntities.Count, Is.EqualTo(2));
                Assert.That(world.UnitEntities[0], Is.SameAs(e1));
                Assert.That(world.UnitEntities[1], Is.SameAs(e3));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go1);
                UnityEngine.Object.DestroyImmediate(go2);
                UnityEngine.Object.DestroyImmediate(go3);
            }
        }

        [Test]
        public void ClearRuntime_ResetsTransformShapeBoundsQueryInfo()
        {
            var entity = CreateEntity();
            entity.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(100, 7, 3),
                PhysicsEntityKind.Unit,
                1,
                new object()));

            entity.ClearRuntime();

            Assert.That(entity.QueryInfo.IsSet, Is.False);
        }

        [Test]
        public void UnitEntities_And_ProjectileEntities_AreIndependent()
        {
            var world = new PhysicsWorld();
            var go1 = new GameObject("U1");
            var go2 = new GameObject("P1");
            try
            {
                var unit = go1.AddComponent<PhysicsEntity2D>();
                var projectile = go2.AddComponent<PhysicsEntity2D>();

                world.RegisterUnit(unit);
                world.RegisterProjectile(projectile);

                Assert.That(world.UnitEntities.Count, Is.EqualTo(1));
                Assert.That(world.ProjectileEntities.Count, Is.EqualTo(1));
                Assert.That(world.UnitEntities[0], Is.SameAs(unit));
                Assert.That(world.ProjectileEntities[0], Is.SameAs(projectile));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go1);
                UnityEngine.Object.DestroyImmediate(go2);
            }
        }
    }
}