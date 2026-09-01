using System.Collections;
using System.Linq;
using System.Reflection;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Mathematics.FixedPoint;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class AatroxPrefabPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimePrefab_InstantiatesWithModelAndEditorGizmo()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Config/Formal/Prefabs/Logic/Unit/AatroxHeroRuntime.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                GameObject view = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab");
                Object.Instantiate(view, instance.transform, false);
                yield return null;
                Assert.That(instance.GetComponent<GameplayUnit>(), Is.Not.Null);
                Assert.That(instance.GetComponent<AbilityHandler>(), Is.Not.Null);
                Assert.That(instance.GetComponent<AatroxAbilityZoneAuthoringGizmo>(), Is.Not.Null);
                Assert.That(instance.GetComponentInChildren<Animator>(true), Is.Not.Null);
                BuffDrivenBoneVisibility wings =
                    instance.GetComponentInChildren<
                        BuffDrivenBoneVisibility>();
                Assert.That(wings, Is.Not.Null);
                Assert.That(wings.VisibleBuffConfigId,
                    Is.EqualTo(12024));
                Assert.That(wings.VisibilityRootCount,
                    Is.EqualTo(4));
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClientUnitOutline_CoversEveryAatroxSubMesh()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                ClientUnitOutline outline =
                    instance.GetComponent<ClientUnitOutline>();
                SkinnedMeshRenderer source =
                    instance.GetComponentInChildren<
                        SkinnedMeshRenderer>(true);
                Assert.That(outline, Is.Not.Null);
                Assert.That(source, Is.Not.Null);
                Assert.That(source.sharedMesh, Is.Not.Null);

                outline.SetHighlighted(
                    true,
                    Color.red);
                yield return null;

                MeshRenderer outlineRenderer = instance
                    .GetComponentsInChildren<MeshRenderer>(true)
                    .Single(item => item.name == "UnitOutline");
                Assert.That(
                    outlineRenderer.sharedMaterials.Length,
                    Is.EqualTo(source.sharedMesh.subMeshCount));
                Assert.That(
                    outlineRenderer.sharedMaterials,
                    Is.All.SameAs(
                        outlineRenderer.sharedMaterials[0]));
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TetherArea_InstantiatesAsStationaryProjectile()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Projectile/InfernalChainsArea.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                GameObject view = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ClientContent/Views/Projectile/InfernalChainsAreaView.prefab");
                Object.Instantiate(view, instance.transform, false);
                yield return null;
                Assert.That(
                    instance.GetComponent<PhysicsEntity2D>(),
                    Is.Not.Null);
                Assert.That(
                    instance.GetComponent<PhysicsEntity2DShapeAuthoring>(),
                    Is.Not.Null);
                ProjectileContainmentZoneAuthoring zone =
                    instance.GetComponent<ProjectileContainmentZoneAuthoring>();
                Assert.That(zone, Is.Not.Null);
                Assert.That(zone.BakeOrThrow().IsValid, Is.True);
                Assert.That(
                    instance.GetComponentInChildren<LineRenderer>(),
                    Is.Not.Null);
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimatorController_RoutesPassiveUltimateAndEmpoweredAttack()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Animator animator =
                    instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                animator.Rebind();
                animator.Update(0f);

                animator.SetBool("IsPassiveReady", true);
                AdvanceAnimator(animator);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxIdle_Passive"),
                    Is.True);

                animator.SetBool(
                    "IsAnimationVariantActive",
                    true);
                AdvanceAnimator(animator);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxIdle_ULT"),
                    Is.True);

                animator.SetBool("IsAttacking", true);
                animator.SetBool("IsEmpoweredAttack", true);
                animator.SetTrigger("AttackStart");
                AdvanceAnimator(animator);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName(
                            "Base Layer." +
                            "AatroxAttack_Passive_ULT"),
                    Is.True);
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimatorController_LocomotionVariantsAdvanceWithoutSelfReentry()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Animator animator =
                    instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                animator.Rebind();
                animator.Update(0f);
                animator.SetInteger("LifeState", 0);
                animator.SetBool("IsAttacking", false);
                animator.SetBool("IsCasting", false);
                animator.SetBool("IsPassiveReady", true);
                animator.SetBool("IsMoving", true);
                animator.SetFloat("MoveSpeed", 5f);
                AdvanceAnimator(animator);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxWalk_Passive"),
                    Is.True);
                CompleteAnimatorTransition(animator);
                AssertLoopMotionTimeChangesPose(
                    animator,
                    "Walk_Passive");

                animator.SetBool("IsAnimationVariantActive", true);
                AdvanceAnimator(animator);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxWalk_ULT"),
                    Is.True);

                animator.SetBool("IsAnimationVariantActive", false);
                animator.SetBool("IsPassiveReady", false);
                AdvanceAnimator(animator);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxWalk"),
                    Is.True);
                CompleteAnimatorTransition(animator);
                AssertLoopMotionTimeChangesPose(
                    animator,
                    "Walk");

                animator.SetBool("IsMoving", false);
                AdvanceAnimator(animator);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxIdle"),
                    Is.True);
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator UnitAnimationDriver_UsesNewLocomotionStateOnChangeFrame()
        {
            GameObject logicPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Config/Formal/Prefabs/Logic/Unit/AatroxHeroRuntime.prefab");
            GameObject viewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab");
            Assert.That(logicPrefab, Is.Not.Null);
            Assert.That(viewPrefab, Is.Not.Null);

            GameObject logic = Object.Instantiate(logicPrefab);
            GameObject view = Object.Instantiate(viewPrefab);
            var world = new UnitWorld { TickRate = 30 };
            try
            {
                GameplayUnit unit = logic.GetComponent<GameplayUnit>();
                MovementHandler movement = unit.MovementHandler;
                UnitPresentationHost host =
                    view.GetComponent<UnitPresentationHost>();
                UnitAnimationDriver driver =
                    view.GetComponent<UnitAnimationDriver>();
                Animator animator =
                    view.GetComponentInChildren<Animator>(true);
                Assert.That(unit, Is.Not.Null);
                Assert.That(movement, Is.Not.Null);
                Assert.That(host, Is.Not.Null);
                Assert.That(driver, Is.Not.Null);
                Assert.That(animator, Is.Not.Null);

                typeof(GameplayUnit).GetProperty(
                        nameof(GameplayUnit.World),
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic)
                    ?.SetValue(unit, world);
                host.Bind(unit);
                movement.SetMoveSpeed((fp)5);
                FieldInfo movingField = typeof(MovementHandler).GetField(
                    "_isMoving",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo driverUpdate = typeof(UnitAnimationDriver).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(movingField, Is.Not.Null);
                Assert.That(driverUpdate, Is.Not.Null);

                animator.Rebind();
                animator.Update(0f);
                AnimationPresentationClock.Publish(
                    world,
                    9,
                    world.TickRate,
                    0.5d);
                driverUpdate.Invoke(driver, null);
                animator.Update(0f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxIdle"),
                    Is.True);

                movingField.SetValue(movement, true);
                AnimationPresentationClock.Publish(
                    world,
                    9,
                    world.TickRate,
                    0.75d);
                driverUpdate.Invoke(driver, null);

                AnimatorStateInfo current =
                    animator.GetCurrentAnimatorStateInfo(0);
                AnimatorStateInfo next =
                    animator.GetNextAnimatorStateInfo(0);
                bool walkResolved =
                    current.IsName("Base Layer.AatroxWalk") ||
                    animator.IsInTransition(0) &&
                    next.IsName("Base Layer.AatroxWalk");
                Assert.That(
                    walkResolved,
                    Is.True,
                    "Driver must resolve the Gameplay-selected Walk state " +
                    "before sampling loop Motion Time on the change frame.");
                Assert.That(
                    animator.GetFloat("MoveSpeed"),
                    Is.EqualTo(5f).Within(0.0001f));
                Assert.That(
                    animator.GetFloat("LoopMotionTime"),
                    Is.GreaterThan(0f));
            }
            finally
            {
                AnimationPresentationClock.Clear(world);
                Object.Destroy(view);
                Object.Destroy(logic);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimatorController_UltimateEndPlaysExitClip()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab");
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Animator animator =
                    instance.GetComponentInChildren<Animator>(true);
                animator.Rebind();
                animator.Update(0f);
                animator.SetInteger("LifeState", 0);
                animator.SetBool("IsAttacking", false);
                animator.SetBool("IsCasting", false);
                animator.SetBool("IsAnimationVariantActive", true);
                AdvanceAnimator(animator);

                animator.SetBool("IsAnimationVariantActive", false);
                animator.SetTrigger("AnimationVariantExit");
                AdvanceAnimator(animator);

                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0)
                        .IsName("Base Layer.AatroxULTOut"),
                    Is.True);
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
        }

        [Test]
        public void OutlineBakeRunsAfterWingVisibility()
        {
            int outlineOrder = typeof(ClientUnitOutline)
                .GetCustomAttributes(
                    typeof(DefaultExecutionOrder),
                    false)
                .Cast<DefaultExecutionOrder>()
                .Single().order;
            int wingOrder = typeof(BuffDrivenBoneVisibility)
                .GetCustomAttributes(
                    typeof(DefaultExecutionOrder),
                    false)
                .Cast<DefaultExecutionOrder>()
                .Single().order;

            Assert.That(outlineOrder, Is.GreaterThan(wingOrder));
        }

        [UnityTest]
        public IEnumerator AttachedVfx_WorldDirectionDoesNotInheritLaterHostTurn()
        {
            var host = new GameObject("VfxDirectionHost");
            var vfx = new GameObject("AttachedDirectionalVfx");
            try
            {
                vfx.transform.SetParent(host.transform, false);
                VfxWorldDirectionLock directionLock =
                    vfx.AddComponent<VfxWorldDirectionLock>();
                directionLock.Begin(Vector3.forward);

                host.transform.rotation =
                    Quaternion.Euler(0f, 90f, 0f);
                yield return null;

                Assert.That(vfx.transform.forward.x,
                    Is.EqualTo(0f).Within(.001f));
                Assert.That(vfx.transform.forward.z,
                    Is.EqualTo(1f).Within(.001f));
            }
            finally
            {
                Object.Destroy(host);
            }
            yield return null;
        }

        private static void AdvanceAnimator(Animator animator)
        {
            animator.Update(.12f);
            animator.Update(.12f);
            animator.Update(.12f);
        }

        private static void CompleteAnimatorTransition(Animator animator)
        {
            for (int i = 0;
                 i < 20 && animator.IsInTransition(0);
                 i++)
            {
                animator.Update(.05f);
            }

            Assert.That(
                animator.IsInTransition(0),
                Is.False,
                "Animator transition did not complete in the test budget.");
        }

        private static void AssertLoopMotionTimeChangesPose(
            Animator animator,
            string stateLabel)
        {
            Transform[] probes =
                animator.GetComponentsInChildren<Transform>(true);
            animator.SetFloat("LoopMotionTime", .15f);
            animator.Update(0f);
            var first = new Quaternion[probes.Length];
            for (int i = 0; i < probes.Length; i++)
                first[i] = probes[i].localRotation;
            animator.SetFloat("LoopMotionTime", .65f);
            animator.Update(0f);
            float maximumAngle = 0f;
            for (int i = 0; i < probes.Length; i++)
            {
                maximumAngle = Mathf.Max(
                    maximumAngle,
                    Quaternion.Angle(
                        first[i],
                        probes[i].localRotation));
            }

            Assert.That(
                maximumAngle,
                Is.GreaterThan(.1f),
                stateLabel +
                " pose must respond to external loop Motion Time.");
        }
    }
}
