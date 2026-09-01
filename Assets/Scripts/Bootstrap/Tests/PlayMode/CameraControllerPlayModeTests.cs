using System.Collections;
using FrameSyncMoba.Physics;
using FrameSyncMoba.FrameSync;
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

        [Test]
        public void SharedConfig_AppliesAnimationRateIndependentOfGameplayTick()
        {
            MobaCameraPresentationConfig config =
                UnityEditor.AssetDatabase.LoadAssetAtPath<
                    MobaCameraPresentationConfig>(
                    "Assets/Config/Formal/Presentation/" +
                    "MobaCameraPresentationConfig.asset");
            Assert.That(config, Is.Not.Null);
            var cameraObject = new GameObject("AnimationConfigCamera");
            try
            {
                cameraObject.AddComponent<Camera>();
                CameraController controller =
                    cameraObject.AddComponent<CameraController>();
                controller.SetPresentationConfig(config, config.BlueTeamId);

                Assert.That(
                    UnitAnimationSynchronizationSettings
                        .SynchronizationRateHz,
                    Is.EqualTo(
                        config.AnimationSynchronizationRateHz)
                        .Within(0.0001f));
                Assert.That(
                    UnitAnimationSynchronizationSettings
                        .InterpolateProgress,
                    Is.EqualTo(config.InterpolateAnimationProgress));
                Assert.That(config.AnimationSynchronizationRateHz,
                    Is.Not.EqualTo(30f),
                    "Formal animation synchronization must not be an alias of the current Gameplay TickRate.");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator LockedGameplayTarget_SmoothlyFollowsProjectedPose()
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
                    Is.GreaterThan(4f).And.LessThan(6f));
                Assert.That(cameraObject.transform.position.z,
                    Is.GreaterThan(-3f).And.LessThan(-1f));

                float deadline = Time.realtimeSinceStartup + .4f;
                while (Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(cameraObject.transform.position.x,
                    Is.EqualTo(6f).Within(.02f));
                Assert.That(cameraObject.transform.position.z,
                    Is.EqualTo(-1f).Within(.02f));
            }
            finally
            {
                Object.Destroy(target);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LockedGameplayTarget_FacingChurnDoesNotStallFollowPosition()
        {
            var target = new GameObject("TurningCameraGameplayTarget");
            var cameraObject = new GameObject("TurningCameraUnderTest");
            PhysicsPresentationSettings.Configure(true, 0.2f, 100f);
            try
            {
                PhysicsEntity2D entity =
                    target.AddComponent<PhysicsEntity2D>();
                entity.TeleportLogicPosition(fp2.zero);
                cameraObject.transform.position =
                    new Vector3(-20f, 12f, -20f);
                cameraObject.AddComponent<Camera>();
                CameraController controller =
                    cameraObject.AddComponent<CameraController>();
                controller.SetDebugTarget(target.transform);
                yield return null;

                entity.SetLogicPosition(new fp2((fp)10, fp.zero));
                bool faceRight = false;
                float deadline = Time.realtimeSinceStartup + .35f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    entity.SetLogicForward(faceRight
                        ? new fp2(fp.one, fp.zero)
                        : new fp2(-fp.one, fp.zero));
                    faceRight = !faceRight;
                    yield return null;
                }

                Assert.That(target.transform.position.x,
                    Is.EqualTo(10f).Within(.02f));
                Assert.That(cameraObject.transform.position.x,
                    Is.EqualTo(target.transform.position.x).Within(.08f));
                Assert.That(cameraObject.transform.position.z,
                    Is.EqualTo(target.transform.position.z - 10f)
                        .Within(.08f));
            }
            finally
            {
                PhysicsPresentationSettings.Configure(
                    false,
                    0.033333f,
                    6f);
                Object.Destroy(target);
                Object.Destroy(cameraObject);
            }
            yield return null;
        }
    }
}
