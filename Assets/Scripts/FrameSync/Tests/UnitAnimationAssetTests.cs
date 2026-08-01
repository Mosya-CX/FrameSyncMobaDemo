using System;
using System.Collections.Generic;
using FrameSyncMoba.Presentation;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class UnitAnimationAssetTests
    {
        private static readonly string[] RequiredParameters =
        {
            "IsMoving",
            "MoveSpeed",
            "IsAttacking",
            "IsAttackRecovering",
            "AttackSequenceIndex",
            "AttackMotionTime",
            "AttackStart",
            "IsCasting",
            "AbilityStageProgress",
            "LifeState",
            "IsControlled",
        };

        private static readonly string[] FixturePrefabs =
        {
            "Assets/Config/FullMatchTest/Prefabs/TestHeroRuntime.prefab",
            "Assets/Config/FullMatchTest/Prefabs/TestMeleeMinionBlueRuntime.prefab",
            "Assets/Config/FullMatchTest/Prefabs/TestMeleeMinionRedRuntime.prefab",
            "Assets/Config/FullMatchTest/Prefabs/TestCasterMinionBlueRuntime.prefab",
            "Assets/Config/FullMatchTest/Prefabs/TestCasterMinionRedRuntime.prefab",
        };

        private static readonly string[]
            FormalAnimatedPrefabs =
        {
            "Assets/Resources/Prefab/Unit/Unit_Hero_Varus.prefab",
            "Assets/Resources/Prefab/Unit/MeleeMinion_Blue.prefab",
            "Assets/Resources/Prefab/Unit/MeleeMinion_Red.prefab",
            "Assets/Resources/Prefab/Unit/CasterMinion_Blue.prefab",
            "Assets/Resources/Prefab/Unit/CasterMinion_Red.prefab",
        };

        private static readonly string[]
            FormalUnitPrefabs =
        {
            "Assets/Resources/Prefab/Unit/Unit_Hero_Varus.prefab",
            "Assets/Resources/Prefab/Unit/MeleeMinion_Blue.prefab",
            "Assets/Resources/Prefab/Unit/MeleeMinion_Red.prefab",
            "Assets/Resources/Prefab/Unit/CasterMinion_Blue.prefab",
            "Assets/Resources/Prefab/Unit/CasterMinion_Red.prefab",
            "Assets/Resources/Prefab/Unit/Turret_Blue.prefab",
            "Assets/Resources/Prefab/Unit/Turret_Red.prefab",
        };

        [Test]
        public void FullMatchAnimationFixtures_HaveCompleteBindableControllers()
        {
            for (int i = 0; i < FixturePrefabs.Length; i++)
                ValidatePrefab(FixturePrefabs[i]);
        }

        [Test]
        public void FormalAnimatedUnits_HaveCompleteBindableControllers()
        {
            for (int i = 0;
                 i < FormalAnimatedPrefabs.Length;
                 i++)
            {
                ValidatePrefab(
                    FormalAnimatedPrefabs[i]);
            }
        }

        [Test]
        public void FormalUnits_HaveCompleteRuntimeComposition()
        {
            for (int i = 0;
                 i < FormalUnitPrefabs.Length;
                 i++)
            {
                string path =
                    FormalUnitPrefabs[i];
                GameObject root =
                    PrefabUtility.LoadPrefabContents(
                        path);
                try
                {
                    Assert.That(
                        root.GetComponent<
                            FrameSyncMoba.Unit.Unit>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<PhysicsEntity2D>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<StatHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<MovementHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<AttackHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<AbilityHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<BuffHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<CrowdControlHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<EquipmentHandler>(),
                        Is.Not.Null,
                        path);
                    PresentationSocketSet sockets =
                        root.GetComponent<
                            PresentationSocketSet>();
                    Assert.That(sockets, Is.Not.Null, path);
                    Assert.That(sockets.Root, Is.Not.Null, path);
                    Assert.That(sockets.Head, Is.Not.Null, path);
                    Assert.That(sockets.Chest, Is.Not.Null, path);
                    Assert.That(sockets.Hand_R, Is.Not.Null, path);
                    Assert.That(sockets.Hand_L, Is.Not.Null, path);
                    Assert.That(sockets.Foot_R, Is.Not.Null, path);
                    Assert.That(sockets.Foot_L, Is.Not.Null, path);
                    Assert.That(
                        sockets.ProjectileOrigin,
                        Is.Not.Null,
                        path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(
                        root);
                }
            }
        }

        [TestCase(
            "Assets/Config/FullMatchTest/Animation/TestHero.controller")]
        [TestCase(
            "Assets/Resources/Animation/Varus/VarusAnimator.controller")]
        public void HeroController_ContainsEveryAvailableHeroClip(
            string controllerPath)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    controllerPath);
            Assert.That(controller, Is.Not.Null, controllerPath);

            var expected = new HashSet<AnimationClip>
            {
                LoadClip("VarusIdle"),
                LoadClip("VarusWalk"),
                LoadClip("VarusAttack1"),
                LoadClip("VarusAttack2"),
                LoadClip("VarusSpellQ_Channeling"),
                LoadClip("VarusSpellQ_Fire"),
                LoadClip("VarusSpellE"),
                LoadClip("VarusSpellR"),
                LoadClip("VarusDeath"),
            };

            ChildAnimatorState[] states =
                controller.layers[0].stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.motion is AnimationClip clip)
                    expected.Remove(clip);
            }

            Assert.That(
                expected,
                Is.Empty,
                $"{controllerPath} must expose every authored hero clip.");
        }

        [TestCase(
            "Assets/Config/FullMatchTest/Animation/TestUnitAnimationProfile.asset")]
        [TestCase(
            "Assets/Resources/Animation/Varus/VarusAnimationProfile.asset")]
        public void HeroAnimationProfile_MapsAttackAndNeutralAbilityStages(
            string profilePath)
        {
            UnitAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<UnitAnimationProfile>(
                    profilePath);
            Assert.That(profile, Is.Not.Null, profilePath);
            Assert.That(profile.AttackStateHashes, Has.Length.EqualTo(2));
            Assert.That(profile.TryGetStageBinding(10003, 1, out _), Is.True);
            Assert.That(profile.TryGetStageBinding(10003, 2, out _), Is.True);
            Assert.That(profile.TryGetStageBinding(10002, 1, out _), Is.True);
            Assert.That(profile.TryGetStageBinding(10001, 1, out _), Is.True);
        }

        private static void ValidatePrefab(string path)
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(path);
            try
            {
                Animator animator =
                    root.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null, path);
                Assert.That(
                    animator.runtimeAnimatorController,
                    Is.TypeOf<AnimatorController>(),
                    path);
                var controller =
                    (AnimatorController)animator.runtimeAnimatorController;
                var parameters =
                    new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < controller.parameters.Length; i++)
                    parameters.Add(controller.parameters[i].name);
                for (int i = 0; i < RequiredParameters.Length; i++)
                    Assert.That(
                        parameters.Contains(RequiredParameters[i]),
                        Is.True,
                        $"{path} is missing Animator parameter {RequiredParameters[i]}.");

                UnitPresentationHost host =
                    root.GetComponent<UnitPresentationHost>();
                UnitAnimationDriver driver =
                    root.GetComponent<UnitAnimationDriver>();
                Assert.That(host, Is.Not.Null, path);
                Assert.That(host.Profile, Is.Not.Null, path);
                Assert.That(driver, Is.Not.Null, path);

                ValidateClipBindings(
                    animator.transform,
                    controller,
                    path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateClipBindings(
            Transform animatorRoot,
            AnimatorController controller,
            string prefabPath)
        {
            ChildAnimatorState[] states =
                controller.layers[0].stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (!(states[i].state.motion is AnimationClip clip))
                    continue;
                EditorCurveBinding[] bindings =
                    AnimationUtility.GetCurveBindings(clip);
                for (int j = 0; j < bindings.Length; j++)
                {
                    string bindingPath = bindings[j].path;
                    if (string.IsNullOrEmpty(bindingPath))
                        continue;
                    Assert.That(
                        animatorRoot.Find(bindingPath),
                        Is.Not.Null,
                        $"{prefabPath}: clip {clip.name} cannot resolve {bindingPath}.");
                }
            }
        }

        private static AnimationClip LoadClip(string name)
        {
            AnimationClip clip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    $"Assets/Resources/Animation/Varus/{name}.anim");
            Assert.That(clip, Is.Not.Null, name);
            return clip;
        }
    }
}
