using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class UnitPrefabAnimatorTopologyTests
    {
        [Test]
        public void AttackSequence_MapsOntoAuthoredAnimationVariantCount()
        {
            Assert.AreEqual(
                0,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(0, 2));
            Assert.AreEqual(
                1,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(1, 2));
            Assert.AreEqual(
                0,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(2, 2));
            Assert.AreEqual(
                1,
                FrameSyncMoba.FrameSync.UnitAnimationDriver
                    .ResolveAttackAnimationVariant(255, 2));
        }
        private const string UnitPrefabRoot =
            "Assets/ClientContent/Views/Unit";

        [Test]
        public void UnitPrefabs_DoNotEnterLoopingMovementFromAnyState()
        {
            string[] prefabPaths = AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { UnitPrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(prefabPaths, Is.Not.Empty);

            foreach (string prefabPath in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);
                foreach (Animator animator in prefab
                    .GetComponentsInChildren<Animator>(true))
                {
                    AnimatorController controller = animator
                        .runtimeAnimatorController as AnimatorController;
                    if (controller == null)
                    {
                        continue;
                    }
                    foreach (AnimatorControllerLayer layer in
                        controller.layers)
                    {
                        AssertMachine(
                            prefabPath,
                            controller,
                            layer.stateMachine);
                    }
                }
            }
        }

        private static void AssertMachine(
            string prefabPath,
            AnimatorController controller,
            AnimatorStateMachine machine)
        {
            foreach (AnimatorStateTransition transition in
                machine.anyStateTransitions)
            {
                AnimationClip clip = transition.destinationState == null
                    ? null
                    : transition.destinationState.motion as AnimationClip;
                bool isPersistentMovementRoute =
                    clip != null &&
                    clip.isLooping &&
                    transition.conditions.Any(condition =>
                        condition.parameter == "IsMoving");
                Assert.That(isPersistentMovementRoute,
                    Is.False,
                    prefabPath + " / " + controller.name +
                    " must not use AnyState -> " +
                    transition.destinationState?.name +
                    " for looping movement.");
            }

            bool hasMovingParameter = controller.parameters.Any(
                parameter => parameter.name == "IsMoving" &&
                    parameter.type ==
                        AnimatorControllerParameterType.Bool);
            if (hasMovingParameter)
            {
                AnimatorStateTransition[] directTransitions = machine.states
                    .SelectMany(child => child.state.transitions)
                    .ToArray();
                Assert.That(directTransitions.Any(transition =>
                        transition.conditions.Any(condition =>
                            condition.parameter == "IsMoving" &&
                            condition.mode ==
                                AnimatorConditionMode.If)),
                    Is.True,
                    prefabPath + " / " + controller.name +
                    " has no direct route into movement.");
                Assert.That(directTransitions.Any(transition =>
                        transition.conditions.Any(condition =>
                            condition.parameter == "IsMoving" &&
                            condition.mode ==
                                AnimatorConditionMode.IfNot)),
                    Is.True,
                    prefabPath + " / " + controller.name +
                    " has no direct route out of movement.");
            }

            foreach (ChildAnimatorStateMachine child in
                machine.stateMachines)
            {
                AssertMachine(
                    prefabPath,
                    controller,
                    child.stateMachine);
            }
        }
    }
}
