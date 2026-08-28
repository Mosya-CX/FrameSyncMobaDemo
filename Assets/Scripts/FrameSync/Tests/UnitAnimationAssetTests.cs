using System;
using System.Collections.Generic;
using FrameSyncMoba.Presentation;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
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
            "Assets/ClientContent/Views/Unit/VarusRuntimeView.prefab",
            "Assets/ClientContent/Views/Unit/TestMeleeMinionBlueRuntimeView.prefab",
            "Assets/ClientContent/Views/Unit/TestMeleeMinionRedRuntimeView.prefab",
            "Assets/ClientContent/Views/Unit/TestCasterMinionBlueRuntimeView.prefab",
            "Assets/ClientContent/Views/Unit/TestCasterMinionRedRuntimeView.prefab",
        };

        private static readonly string[]
            RuntimeUnitPrefabs =
        {
            "Assets/Config/Formal/Prefabs/Logic/Unit/VarusRuntime.prefab",
            "Assets/Config/Formal/Prefabs/Logic/Unit/TestMeleeMinionBlueRuntime.prefab",
            "Assets/Config/Formal/Prefabs/Logic/Unit/TestMeleeMinionRedRuntime.prefab",
            "Assets/Config/Formal/Prefabs/Logic/Unit/TestCasterMinionBlueRuntime.prefab",
            "Assets/Config/Formal/Prefabs/Logic/Unit/TestCasterMinionRedRuntime.prefab",
            "Assets/Config/Formal/Prefabs/Logic/Unit/TestTowerBlueRuntime.prefab",
            "Assets/Config/Formal/Prefabs/Logic/Unit/TestTowerRedRuntime.prefab",
        };

        private readonly struct AnimatedUnitCase
        {
            public AnimatedUnitCase(
                string viewPath,
                int runtimePrefabId,
                string controllerPath)
            {
                ViewPath = viewPath;
                RuntimePrefabId = runtimePrefabId;
                ControllerPath = controllerPath;
            }

            public string ViewPath { get; }
            public int RuntimePrefabId { get; }
            public string ControllerPath { get; }
        }

        private static readonly AnimatedUnitCase[] AnimatedUnits =
        {
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/AatroxHeroRuntimeView.prefab",
                1102,
                "Assets/ClientContent/Animation/Aatrox/AatroxAnimator.controller"),
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/VarusRuntimeView.prefab",
                1101,
                "Assets/ClientContent/Animation/Profiles/Varus.controller"),
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/TestMeleeMinionBlueRuntimeView.prefab",
                1201,
                "Assets/ClientContent/Animation/Profiles/MeleeBlue.controller"),
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/TestMeleeMinionRedRuntimeView.prefab",
                1202,
                "Assets/ClientContent/Animation/Profiles/MeleeRed.controller"),
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/TestCasterMinionBlueRuntimeView.prefab",
                1211,
                "Assets/ClientContent/Animation/Profiles/CasterBlue.controller"),
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/TestCasterMinionRedRuntimeView.prefab",
                1212,
                "Assets/ClientContent/Animation/Profiles/CasterRed.controller"),
        };

        private static readonly AnimatedUnitCase[] Structures =
        {
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/TestTowerBlueRuntimeView.prefab",
                1301,
                "Assets/ClientContent/Animation/Turret/TurretBlue.controller"),
            new AnimatedUnitCase(
                "Assets/ClientContent/Views/Unit/TestTowerRedRuntimeView.prefab",
                1302,
                "Assets/ClientContent/Animation/Turret/TurretRed.controller"),
        };

        [Test]
        public void FullMatchAnimationFixtures_HaveCompleteBindableControllers()
        {
            for (int i = 0; i < FixturePrefabs.Length; i++)
                ValidatePrefab(FixturePrefabs[i]);
        }

        [Test]
        public void RuntimeUnits_HaveCompleteRuntimeComposition()
        {
            for (int i = 0;
                 i < RuntimeUnitPrefabs.Length;
                 i++)
            {
                string path =
                    RuntimeUnitPrefabs[i];
                bool isHero =
                    path.Contains("Hero");
                bool isTower =
                    path.Contains("Tower");
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
                        root.GetComponent<BuffHandler>(),
                        Is.Not.Null,
                        path);
                    Assert.That(
                        root.GetComponent<CrowdControlHandler>(),
                        Is.Not.Null,
                        path);
                    if (!isTower)
                    {
                        Assert.That(
                            root.GetComponent<MovementHandler>(),
                            Is.Not.Null,
                            path);
                        Assert.That(
                            root.GetComponent<AttackHandler>(),
                            Is.Not.Null,
                            path);
                    }
                    if (isHero)
                    {
                        Assert.That(
                            root.GetComponent<AbilityHandler>(),
                            Is.Not.Null,
                            path);
                        Assert.That(
                            root.GetComponent<EquipmentHandler>(),
                            Is.Not.Null,
                            path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(
                        root);
                }
            }
        }

        [TestCase(
            "Assets/ClientContent/Animation/Profiles/Varus.controller")]
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
                LoadClip("VarusSpellQ_ChannelingIdle"),
                LoadClip("VarusSpellQChanneling_Walk"),
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
            "Assets/ClientContent/Animation/Profiles/TestUnitAnimationProfile.asset")]
        public void HeroAnimationProfile_MapsAttackAndNeutralAbilityStages(
            string profilePath)
        {
            UnitAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<UnitAnimationProfile>(
                    profilePath);
            Assert.That(profile, Is.Not.Null, profilePath);
            Assert.That(profile.AttackStateHashes, Has.Length.EqualTo(2));
            Assert.That(profile.TryGetStageBinding(10011, 1, out _), Is.True);
            Assert.That(profile.TryGetStageBinding(10011, 2, out _), Is.True);
            Assert.That(profile.TryGetStageBinding(10013, 1, out _), Is.True);
            Assert.That(profile.TryGetStageBinding(10014, 1, out _), Is.True);
        }

        [Test]
        public void FormalAnimatedUnits_BindAttackAndMovePlaybackToGameplayStats()
        {
            UnitRuntimeCatalogAsset unitCatalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset");
            GlobalGameplayData globalData =
                AssetDatabase.LoadAssetAtPath<GlobalGameplayData>(
                    "Assets/Config/Formal/GlobalGameplayData.asset");
            Assert.That(unitCatalog, Is.Not.Null);
            Assert.That(globalData, Is.Not.Null);
            float moveSpeedScale =
                (float)globalData.BakeOrThrow()
                    .MoveSpeedToLogicVelocityScale;

            AssertFormalViewCoverage();
            for (int i = 0; i < AnimatedUnits.Length; i++)
            {
                AnimatedUnitCase item = AnimatedUnits[i];
                UnitPrototypeAuthoring prototype =
                    FindPrototype(
                        unitCatalog,
                        item.RuntimePrefabId);
                Assert.That(
                    prototype.UnitKind,
                    Is.Not.EqualTo(UnitKind.Structure),
                    item.ViewPath);
                float baseLogicMoveSpeed =
                    prototype.Locomotion.BaseMoveSpeed *
                    moveSpeedScale;
                Assert.That(baseLogicMoveSpeed, Is.GreaterThan(0f));

                AnimatorController controller =
                    LoadControllerFromView(item);
                AssertParameter(
                    controller,
                    "AttackMotionTime",
                    AnimatorControllerParameterType.Float);
                AssertParameter(
                    controller,
                    "MoveSpeed",
                    AnimatorControllerParameterType.Float);

                var states = new List<AnimatorState>();
                CollectStates(controller, states);
                int attackStates = 0;
                int moveStates = 0;
                for (int stateIndex = 0;
                     stateIndex < states.Count;
                     stateIndex++)
                {
                    AnimatorState state = states[stateIndex];
                    if (IsAttackState(state))
                    {
                        attackStates++;
                        Assert.That(
                            state.timeParameterActive,
                            Is.True,
                            $"{item.ControllerPath}: {state.name} must use Motion Time.");
                        Assert.That(
                            state.timeParameter,
                            Is.EqualTo("AttackMotionTime"),
                            $"{item.ControllerPath}: {state.name}");
                    }

                    if (!IsMoveState(state))
                        continue;
                    moveStates++;
                    Assert.That(
                        state.speedParameterActive,
                        Is.True,
                        $"{item.ControllerPath}: {state.name} must use MoveSpeed.");
                    Assert.That(
                        state.speedParameter,
                        Is.EqualTo("MoveSpeed"),
                        $"{item.ControllerPath}: {state.name}");
                    Assert.That(
                        state.speed * baseLogicMoveSpeed,
                        Is.EqualTo(1f).Within(0.0001f),
                        $"{item.ControllerPath}: {state.name} must play at 1x " +
                        "for the formal base movement speed.");
                }

                Assert.That(
                    attackStates,
                    Is.GreaterThan(0),
                    item.ControllerPath);
                Assert.That(
                    moveStates,
                    Is.GreaterThan(0),
                    item.ControllerPath);
            }
        }

        [Test]
        public void FormalStructures_HaveNoAttackAnimation()
        {
            UnitRuntimeCatalogAsset unitCatalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset");
            Assert.That(unitCatalog, Is.Not.Null);
            for (int i = 0; i < Structures.Length; i++)
            {
                AnimatedUnitCase item = Structures[i];
                UnitPrototypeAuthoring prototype =
                    FindPrototype(
                        unitCatalog,
                        item.RuntimePrefabId);
                Assert.That(
                    prototype.UnitKind,
                    Is.EqualTo(UnitKind.Structure),
                    item.ViewPath);

                AnimatorController controller =
                    LoadControllerFromView(item);
                var states = new List<AnimatorState>();
                CollectStates(controller, states);
                for (int stateIndex = 0;
                     stateIndex < states.Count;
                     stateIndex++)
                {
                    Assert.That(
                        IsAttackState(states[stateIndex]),
                        Is.False,
                        $"{item.ControllerPath}: Structure must not author an " +
                        $"attack state ({states[stateIndex].name}).");
                }
            }
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

        private static void AssertFormalViewCoverage()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/ClientContent/Views/Unit" });
            Assert.That(
                guids,
                Has.Length.EqualTo(
                    AnimatedUnits.Length + Structures.Length),
                "Every formal Unit view must declare whether it is an " +
                "animated attacker or a Structure without attack animation.");
        }

        private static UnitPrototypeAuthoring FindPrototype(
            UnitRuntimeCatalogAsset catalog,
            int runtimePrefabId)
        {
            for (int i = 0; i < catalog.UnitPrototypes.Count; i++)
            {
                UnitPrototypeAuthoring prototype =
                    catalog.UnitPrototypes[i];
                if (prototype.RuntimeEntityPrefabId == runtimePrefabId)
                    return prototype;
            }

            Assert.Fail(
                $"Missing formal Unit prototype for PrefabId {runtimePrefabId}.");
            return null;
        }

        private static AnimatorController LoadControllerFromView(
            in AnimatedUnitCase item)
        {
            GameObject view = AssetDatabase.LoadAssetAtPath<GameObject>(
                item.ViewPath);
            Assert.That(view, Is.Not.Null, item.ViewPath);
            Animator animator =
                view.GetComponentInChildren<Animator>(true);
            Assert.That(animator, Is.Not.Null, item.ViewPath);
            AnimatorController controller =
                animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null, item.ViewPath);
            Assert.That(
                AssetDatabase.GetAssetPath(controller),
                Is.EqualTo(item.ControllerPath),
                item.ViewPath);
            return controller;
        }

        private static void AssertParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            for (int i = 0;
                 i < controller.parameters.Length;
                 i++)
            {
                AnimatorControllerParameter parameter =
                    controller.parameters[i];
                if (parameter.name != name)
                    continue;
                Assert.That(
                    parameter.type,
                    Is.EqualTo(type),
                    AssetDatabase.GetAssetPath(controller));
                return;
            }

            Assert.Fail(
                $"{AssetDatabase.GetAssetPath(controller)} is missing {name}.");
        }

        private static void CollectStates(
            AnimatorController controller,
            List<AnimatorState> states)
        {
            for (int layerIndex = 0;
                 layerIndex < controller.layers.Length;
                 layerIndex++)
            {
                CollectStates(
                    controller.layers[layerIndex].stateMachine,
                    states);
            }
        }

        private static void CollectStates(
            AnimatorStateMachine machine,
            List<AnimatorState> states)
        {
            ChildAnimatorState[] directStates = machine.states;
            for (int i = 0; i < directStates.Length; i++)
                states.Add(directStates[i].state);
            ChildAnimatorStateMachine[] children =
                machine.stateMachines;
            for (int i = 0; i < children.Length; i++)
                CollectStates(children[i].stateMachine, states);
        }

        private static bool IsAttackState(AnimatorState state) =>
            state.name.IndexOf(
                "Attack",
                StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsMoveState(AnimatorState state) =>
            state.name.IndexOf(
                "Walk",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            state.name.Equals(
                "Move",
                StringComparison.OrdinalIgnoreCase);

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
                    $"Assets/ClientContent/Animation/Varus/{name}.anim");
            Assert.That(clip, Is.Not.Null, name);
            return clip;
        }
    }
}
