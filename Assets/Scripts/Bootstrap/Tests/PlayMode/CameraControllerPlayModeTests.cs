using System.Collections;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class CameraControllerPlayModeTests
    {
        [Test]
        public void SharedConfig_UsesOppositeBlueAndRedViewDirections()
        {
            MobaCameraPresentationConfig config =
                ScriptableObject.CreateInstance<
                    MobaCameraPresentationConfig>();
            try
            {
                CameraSideSettings blue = config.ResolveSide((byte)1);
                CameraSideSettings red = config.ResolveSide((byte)2);
                Assert.That(red.EulerAngles.y - blue.EulerAngles.y,
                    Is.EqualTo(180f).Within(.001f));
                Assert.That(red.FollowOffset.z,
                    Is.EqualTo(-blue.FollowOffset.z).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [UnityTest]
        public IEnumerator LockedGameplayTarget_FollowsProjectedPoseExactly()
        {
            var target = new GameObject("CameraGameplayTarget");
            var cameraObject = new GameObject("CameraUnderTest");
            try
            {
                PhysicsEntity2D entity =
                    target.AddComponent<PhysicsEntity2D>();
                entity.TeleportLogicPosition(new fp2((fp)4, (fp)7));

                cameraObject.transform.position =
                    new Vector3(-20f, 12f, -20f);
                cameraObject.AddComponent<Camera>();
                CameraController controller =
                    cameraObject.AddComponent<CameraController>();
                controller.SetDebugTarget(target.transform);

                yield return null;

                Assert.That(cameraObject.transform.position.x,
                    Is.EqualTo(4f).Within(.001f));
                Assert.That(cameraObject.transform.position.y,
                    Is.EqualTo(12f).Within(.001f));
                Assert.That(cameraObject.transform.position.z,
                    Is.EqualTo(-3f).Within(.001f));

                entity.SetLogicPosition(new fp2((fp)6, (fp)9));
                yield return null;

                Assert.That(cameraObject.transform.position.x,
                    Is.EqualTo(6f).Within(.001f));
                Assert.That(cameraObject.transform.position.z,
                    Is.EqualTo(-1f).Within(.001f));
            }
            finally
            {
                Object.Destroy(target);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }
    }
}
