using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class PhysicsCollisionResolverTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }

        [Test]
        public void ClampPosition_DoesNotResolveMoverAgainstItself()
        {
            UnitUid uid = new UnitUid(1, 7001, 0);
            FrameSyncMoba.Unit.Unit unit =
                UnitTestFactory.CreateUnit(
                uid,
                UnitKind.Minion,
                0,
                new TeamId(1));
            fp radius = RadiusClassHelper.MediumRadius;
            unit.PhysicsEntity.SetLogicShape(
                PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    radius));
            unit.PhysicsEntity.TeleportLogicPosition(
                fp2.zero);

            var physicsWorld = new PhysicsWorld();
            physicsWorld.RegisterUnit(unit.PhysicsEntity);
            physicsWorld.BuildUnitFinalGrid();
            var resolver = new PhysicsCollisionResolver(
                physicsWorld);
            fp2 desiredPosition =
                new fp2((fp)0.1m, fp.zero);

            fp2 resolved = resolver.ClampPosition(
                desiredPosition,
                fp2.zero,
                radius,
                RadiusClass.Medium,
                uid);

            Assert.That(
                resolved,
                Is.EqualTo(desiredPosition));
        }
    }
}
