using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Physics.Tests
{
    public sealed class UnitCollisionEventBufferTests
    {
        private sealed class Participant : IUnitCollisionParticipant
        {
            public bool CanParticipateInUnitCollision { get; set; } = true;
            public int Enters;
            public int Exits;
            public RuntimeUidQueryValue LastOther;

            public void PublishUnitCollisionEnter(
                RuntimeUidQueryValue otherUid,
                fp2 contactNormal)
            {
                Enters++;
                LastOther = otherUid;
            }

            public void PublishUnitCollisionExit(RuntimeUidQueryValue otherUid)
            {
                Exits++;
                LastOther = otherUid;
            }
        }

        [Test]
        public void DetectCaptureRestore_PreservesEnterExitSemantics()
        {
            var firstOwner = new Participant();
            var secondOwner = new Participant();
            GameObject firstObject = new GameObject("First");
            GameObject secondObject = new GameObject("Second");
            try
            {
                PhysicsEntity2D first = CreateEntity(
                    firstObject,
                    new RuntimeUidQueryValue(1, 1000, 0),
                    1,
                    firstOwner,
                    new fp2(0, 0));
                PhysicsEntity2D second = CreateEntity(
                    secondObject,
                    new RuntimeUidQueryValue(1, 1001, 0),
                    2,
                    secondOwner,
                    new fp2(1, 0));
                var world = new PhysicsWorld();
                world.RegisterUnit(second);
                world.RegisterUnit(first);

                world.DetectUnitCollisionEvents();
                Assert.AreEqual(1, firstOwner.Enters);
                Assert.AreEqual(1, secondOwner.Enters);

                PhysicsRuntimeSnapshot snapshot = default;
                world.Capture(ref snapshot);
                Assert.AreEqual(1, snapshot.CollisionBuffer.PreviousPairs.Count);

                var restored = new PhysicsWorld();
                restored.RegisterUnit(first);
                restored.RegisterUnit(second);
                restored.Restore(snapshot);
                restored.Rebuild();
                restored.DetectUnitCollisionEvents();
                Assert.AreEqual(1, firstOwner.Enters, "Restore must not duplicate Enter.");

                second.SetLogicPosition(new fp2(20, 0));
                restored.DetectUnitCollisionEvents();
                Assert.AreEqual(1, firstOwner.Exits);
                Assert.AreEqual(1, secondOwner.Exits);
            }
            finally
            {
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }

        private static PhysicsEntity2D CreateEntity(
            GameObject gameObject,
            RuntimeUidQueryValue uid,
            byte team,
            Participant owner,
            fp2 position)
        {
            PhysicsEntity2D entity = gameObject.AddComponent<PhysicsEntity2D>();
            entity.SetLogicShape(PhysicsShape2D.CreateCircle(fp2.zero, (fp)1));
            entity.TeleportLogicPosition(position);
            entity.SetQueryInfo(new PhysicsEntityQueryInfo(
                uid, PhysicsEntityKind.Unit, team, owner));
            return entity;
        }
    }
}
