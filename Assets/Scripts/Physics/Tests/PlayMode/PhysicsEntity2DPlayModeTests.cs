using System;
using System.Collections;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Physics.PlayModeTests
{
    public sealed class PhysicsEntity2DPlayModeTests
    {
        private GameObject gameObject;
        private PhysicsEntity2D entity;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TestPhysicsEntity2D");
            entity = gameObject.AddComponent<PhysicsEntity2D>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void OrdinaryPositionAndDelta_AdvancePreviousPosition()
        {
            entity.TeleportLogicPosition(Vector(2, 3));
            entity.SetLogicPosition(Vector(5, 7));

            AssertVector(entity.Transform2D.PrevPosition, Vector(2, 3));
            AssertVector(entity.Transform2D.Position, Vector(5, 7));

            entity.ApplyLogicPositionDelta(Vector(-1, 4));

            AssertVector(entity.Transform2D.PrevPosition, Vector(5, 7));
            AssertVector(entity.Transform2D.Position, Vector(4, 11));
        }

        [Test]
        public void Teleport_SetsPreviousAndCurrentToSamePosition()
        {
            entity.SetLogicPosition(Vector(20, -5));

            entity.TeleportLogicPosition(Vector(-4, 9));

            AssertVector(entity.Transform2D.PrevPosition, Vector(-4, 9));
            AssertVector(entity.Transform2D.Position, Vector(-4, 9));
        }

        [Test]
        public void PoseAndForward_NormalizeFacingAndRefreshOffsetBounds()
        {
            entity.SetLogicShape(PhysicsShape2D.CreatePoint(Vector(2, 3)));
            fp2 firstInput = Vector(0, 5);
            fp2 firstForward = fpmath.normalize(firstInput);
            fp2 firstRight = new fp2(firstForward.y, -firstForward.x);

            entity.SetLogicPose(Vector(10, 20), firstInput);

            AssertVector(entity.Transform2D.Forward, firstForward);
            AssertVector(entity.Transform2D.Right, firstRight);
            fp2 firstPoint = Vector(10, 20) + (firstRight * Whole(2)) + (firstForward * Whole(3));
            AssertVector(entity.Bounds.Min, firstPoint);

            fp2 secondInput = Vector(5, 0);
            fp2 secondForward = fpmath.normalize(secondInput);
            fp2 secondRight = new fp2(secondForward.y, -secondForward.x);
            entity.SetLogicForward(secondInput);

            AssertVector(entity.Transform2D.Forward, secondForward);
            AssertVector(entity.Transform2D.Right, secondRight);
            fp2 secondPoint = Vector(10, 20) + (secondRight * Whole(2)) + (secondForward * Whole(3));
            AssertVector(entity.Bounds.Min, secondPoint);
        }

        [Test]
        public void ZeroFacing_PreservesPriorFacingWhilePoseStillMoves()
        {
            entity.SetLogicPose(Vector(1, 2), Vector(0, 1));

            entity.SetLogicPose(Vector(8, 9), default);

            AssertVector(entity.Transform2D.Position, Vector(8, 9));
            AssertVector(entity.Transform2D.PrevPosition, Vector(1, 2));
            AssertVector(entity.Transform2D.Forward, Vector(0, 1));
            AssertVector(entity.Transform2D.Right, Vector(1, 0));
        }

        [Test]
        public void ShapeChange_RefreshesBoundsWithoutChangingPose()
        {
            entity.SetLogicPose(Vector(10, 20), Vector(0, 1));
            PhysicsTransform2D before = entity.Transform2D;

            entity.SetLogicShape(PhysicsShape2D.CreateCircle(Vector(2, 3), Whole(4)));

            AssertTransform(entity.Transform2D, before);
            AssertVector(entity.Bounds.Min, Vector(8, 19));
            AssertVector(entity.Bounds.Max, Vector(16, 27));
        }

        [Test]
        public void SegmentShape_UsesLogicalPoseAndWidthExpandedBounds()
        {
            var transform = new PhysicsTransform2D(
                Vector(10, 20),
                Vector(-100, -100),
                Vector(0, 1),
                Vector(1, 0));
            entity.RestoreLogicSpatialState(
                transform,
                PhysicsShape2D.CreatePoint(default));

            entity.SetLogicShape(
                PhysicsShape2D.CreateSegment(Vector(2, 3), Whole(6), Whole(4), true));

            AssertTransform(entity.Transform2D, transform);
            AssertVector(entity.Bounds.Min, Vector(10, 18));
            AssertVector(entity.Bounds.Max, Vector(14, 28));
        }

        [Test]
        public void RectBounds_RefreshAfterFacingAndPositionChanges()
        {
            entity.SetLogicPose(Vector(10, 20), Vector(0, 1));
            entity.SetLogicShape(PhysicsShape2D.CreateRect(Vector(2, 3), Vector(4, 2), true));

            entity.SetLogicForward(Vector(1, 0));
            entity.ApplyLogicPositionDelta(Vector(5, -7));

            fp2 forward = fpmath.normalize(Vector(1, 0));
            fp2 right = new fp2(forward.y, -forward.x);
            fp2 center = Vector(15, 13) + (right * Whole(2)) + (forward * Whole(3));
            fp2 extents = (fpmath.abs(right) * Whole(4)) + (fpmath.abs(forward) * Whole(2));
            AssertVector(entity.Transform2D.Forward, forward);
            AssertVector(entity.Transform2D.Right, right);
            AssertVector(entity.Bounds.Min, center - extents);
            AssertVector(entity.Bounds.Max, center + extents);
        }

        [Test]
        public void InvalidShapeAssignment_IsFailureAtomic()
        {
            entity.SetLogicPose(Vector(3, 4), Vector(0, 1));
            entity.SetLogicShape(PhysicsShape2D.CreateCircle(default, Whole(2)));
            PhysicsTransform2D transformBefore = entity.Transform2D;
            PhysicsShape2D shapeBefore = entity.Shape;
            PhysicsBounds2D boundsBefore = entity.Bounds;
            var invalid = new PhysicsShape2D(
                PhysicsShapeKind.Segment,
                default,
                fp.zero,
                Whole(-1),
                fp.zero,
                default,
                false);

            Assert.Throws<ArgumentOutOfRangeException>(() => entity.SetLogicShape(in invalid));

            AssertTransform(entity.Transform2D, transformBefore);
            Assert.That(entity.Shape.Kind, Is.EqualTo(shapeBefore.Kind));
            Assert.That(entity.Shape.Radius.RawValue, Is.EqualTo(shapeBefore.Radius.RawValue));
            AssertBounds(entity.Bounds, boundsBefore);
        }

        [Test]
        public void Restore_PreservesExactPoseAndRebuildsBounds()
        {
            var transform = new PhysicsTransform2D(
                Vector(10, 20),
                Vector(30, 5),
                Vector(0, 1),
                Vector(1, 0));
            PhysicsShape2D shape = PhysicsShape2D.CreateCircle(Vector(2, 3), Whole(4), true);

            entity.RestoreLogicSpatialState(transform, shape);

            AssertTransform(entity.Transform2D, transform);
            AssertVector(entity.Bounds.Min, Vector(8, 1));
            AssertVector(entity.Bounds.Max, Vector(34, 27));
        }

        [Test]
        public void Restore_RectPreservesPoseAndRebuildsOrientedBounds()
        {
            var transform = new PhysicsTransform2D(
                Vector(10, 20),
                Vector(30, 5),
                Vector(1, 0),
                Vector(0, -1));
            PhysicsShape2D shape = PhysicsShape2D.CreateRect(Vector(2, 3), Vector(4, 2), true);

            entity.RestoreLogicSpatialState(transform, shape);

            AssertTransform(entity.Transform2D, transform);
            Assert.That(entity.Shape.Kind, Is.EqualTo(PhysicsShapeKind.Rect));
            AssertVector(entity.Shape.HalfExtents, Vector(4, 2));
            AssertVector(entity.Bounds.Min, Vector(11, 14));
            AssertVector(entity.Bounds.Max, Vector(15, 22));
        }

        [UnityTest]
        public IEnumerator LogicalWrites_DoNotSynchronouslyWriteUnityTransform()
        {
            gameObject.transform.SetPositionAndRotation(
                new Vector3(101.5f, -7.25f, 33f),
                Quaternion.Euler(12f, 34f, 56f));
            Vector3 unityPosition = gameObject.transform.position;
            Quaternion unityRotation = gameObject.transform.rotation;

            entity.SetLogicPose(Vector(-100, 200), Vector(3, 4));
            entity.SetLogicShape(PhysicsShape2D.CreateRect(Vector(2, -3), Vector(6, 4), true));
            entity.ApplyLogicPositionDelta(Vector(7, 9));

            Assert.That(gameObject.transform.position, Is.EqualTo(unityPosition));
            Assert.That(gameObject.transform.rotation, Is.EqualTo(unityRotation));
            Assert.That(entity.Transform2D.Position.x.RawValue, Is.Not.EqualTo(fp.zero.RawValue));

            // The presentation projection remains the sole LateUpdate write
            // point; the logical APIs above never touch Transform directly.
            yield return null;
            Assert.That(gameObject.transform.position,
                Is.EqualTo(new Vector3(-93f, 0f, 209f)));
        }

        [UnityTest]
        public IEnumerator PresentationSmoothing_InterpolatesWithoutChangingLogicPose()
        {
            PhysicsPresentationSettings.Configure(true, 0.2f, 100f);
            try
            {
                entity.TeleportLogicPosition(Vector(0, 0));
                yield return null;
                entity.SetLogicPosition(Vector(10, 0));
                fp2 logicPosition = entity.Transform2D.Position;

                yield return null;

                Assert.That(gameObject.transform.position.x,
                    Is.GreaterThan(0f));
                Assert.That(gameObject.transform.position.x,
                    Is.LessThan(10f));
                AssertVector(entity.Transform2D.Position, logicPosition);

                float deadline = Time.realtimeSinceStartup + 1f;
                while (gameObject.transform.position.x < 9.99f &&
                       Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                Assert.That(gameObject.transform.position.x,
                    Is.EqualTo(10f).Within(.01f));
                AssertVector(entity.Transform2D.Position, logicPosition);
            }
            finally
            {
                PhysicsPresentationSettings.Configure(false, 0.033333f, 6f);
            }
        }

        [UnityTest]
        public IEnumerator PresentationSmoothing_FacingChurnDoesNotRestartPositionInterpolation()
        {
            PhysicsPresentationSettings.Configure(true, 0.2f, 100f);
            try
            {
                entity.TeleportLogicPosition(Vector(0, 0));
                yield return null;
                entity.SetLogicPosition(Vector(10, 0));

                float deadline = Time.realtimeSinceStartup + 0.35f;
                bool faceRight = false;
                while (Time.realtimeSinceStartup < deadline)
                {
                    entity.SetLogicForward(
                        faceRight ? Vector(1, 0) : Vector(-1, 0));
                    faceRight = !faceRight;
                    yield return null;
                }

                Assert.That(gameObject.transform.position.x,
                    Is.EqualTo(10f).Within(.02f),
                    "Rotation target churn must not keep restarting the " +
                    "independent position interpolation used by the locked camera.");
                AssertVector(entity.Transform2D.Position, Vector(10, 0));
            }
            finally
            {
                PhysicsPresentationSettings.Configure(false, 0.033333f, 6f);
            }
        }

        private static fp Whole(int value)
        {
            return fp.FromRaw((long)value << 32);
        }

        private static fp2 Vector(int x, int y)
        {
            return new fp2(Whole(x), Whole(y));
        }

        private static void AssertTransform(PhysicsTransform2D actual, PhysicsTransform2D expected)
        {
            AssertVector(actual.Position, expected.Position);
            AssertVector(actual.PrevPosition, expected.PrevPosition);
            AssertVector(actual.Forward, expected.Forward);
            AssertVector(actual.Right, expected.Right);
        }

        private static void AssertBounds(PhysicsBounds2D actual, PhysicsBounds2D expected)
        {
            AssertVector(actual.Min, expected.Min);
            AssertVector(actual.Max, expected.Max);
        }

        private static void AssertVector(fp2 actual, fp2 expected)
        {
            Assert.That(actual.x.RawValue, Is.EqualTo(expected.x.RawValue));
            Assert.That(actual.y.RawValue, Is.EqualTo(expected.y.RawValue));
        }
    }
}
