using System.Collections;
using System.Linq;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class AatroxPrefabPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimePrefab_InstantiatesWithModelAndEditorGizmo()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Prefab/Unit/AatroxHeroRuntime.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
                yield return null;
                Assert.That(instance.GetComponent<GameplayUnit>(), Is.Not.Null);
                Assert.That(instance.GetComponent<AbilityHandler>(), Is.Not.Null);
                Assert.That(instance.GetComponent<AatroxAbilityZoneAuthoringGizmo>(), Is.Not.Null);
                Assert.That(instance.GetComponentInChildren<Animator>(true), Is.Not.Null);
                BuffDrivenBoneVisibility wings =
                    instance.GetComponent<
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
                    "Assets/Resources/Prefab/Unit/AatroxHeroRuntime.prefab");
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
                "Assets/Resources/Prefab/Missle/InfernalChainsArea.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab);
            try
            {
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
                Assert.That(instance.GetComponent<LineRenderer>(), Is.Not.Null);
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
                    "Assets/Resources/Prefab/Unit/AatroxHeroRuntime.prefab");
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
                "Assets/Resources/Prefab/Unit/AatroxHeroRuntime.prefab");
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
                float passiveProgress = animator
                    .GetCurrentAnimatorStateInfo(0).normalizedTime;
                animator.Update(.12f);
                float advancedPassiveProgress = animator
                    .GetCurrentAnimatorStateInfo(0).normalizedTime;
                Assert.That(advancedPassiveProgress,
                    Is.GreaterThan(passiveProgress + .01f),
                    "Walk_Passive must keep advancing instead of " +
                    "re-entering every frame.");

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
                float normalProgress = animator
                    .GetCurrentAnimatorStateInfo(0).normalizedTime;
                animator.Update(.12f);
                Assert.That(
                    animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.GreaterThan(normalProgress + .01f),
                    "Walk must keep advancing instead of re-entering " +
                    "every frame.");

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
        public IEnumerator AnimatorController_UltimateEndPlaysExitClip()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Prefab/Unit/AatroxHeroRuntime.prefab");
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
    }
}
