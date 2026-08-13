using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FrameSyncMoba.Presentation;

namespace FrameSyncMoba.EditorTools
{
    public static class AatroxAnimatorPresentationSetup
    {
        private const string ControllerPath =
            "Assets/Resources/Animation/Aatrox/AatroxAnimator.controller";
        private const string UltimateOutClipPath =
            "Assets/Resources/Animation/Aatrox/AatroxULTOut.anim";

        [MenuItem("FrameSyncMoba/Aatrox/Refresh Presentation Transitions")]
        public static void Refresh()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(
                    ControllerPath);
            AnimationClip ultimateOut =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    UltimateOutClipPath);
            if (controller == null || ultimateOut == null)
                throw new System.InvalidOperationException(
                    "Aatrox AnimatorController or AatroxULTOut clip is missing.");

            EnsureTrigger(controller, "AnimationVariantExit");
            AnimatorStateMachine machine =
                controller.layers[0].stateMachine;
            AnimatorState ultimateOutState =
                FindState(machine, "AatroxULTOut") ??
                machine.AddState(
                    "AatroxULTOut",
                    new Vector3(780f, 610f, 0f));
            ultimateOutState.motion = ultimateOut;
            ultimateOutState.speed = 1f;

            EnsureUltimateOutEntry(machine, ultimateOutState);
            EnsureUltimateOutExit(
                machine,
                ultimateOutState,
                "AatroxIdle",
                false,
                false);
            EnsureUltimateOutExit(
                machine,
                ultimateOutState,
                "AatroxWalk",
                true,
                false);
            EnsureUltimateOutExit(
                machine,
                ultimateOutState,
                "AatroxIdle_Passive",
                false,
                true);
            EnsureUltimateOutExit(
                machine,
                ultimateOutState,
                "AatroxWalk_Passive",
                true,
                true);

            string[] locomotion =
            {
                "AatroxIdle", "AatroxWalk",
                "AatroxIdle_Passive", "AatroxWalk_Passive",
                "AatroxIdle_ULT", "AatroxWalk_ULT",
            };
            for (int i = 0; i < locomotion.Length; i++)
            {
                AnimatorState source = FindState(machine, locomotion[i]);
                if (source == null)
                    continue;
                for (int j = 0; j < source.transitions.Length; j++)
                {
                    AnimatorState destination =
                        source.transitions[j].destinationState;
                    if (destination == null ||
                        !locomotion.Contains(destination.name))
                    {
                        continue;
                    }
                    bool passiveBlend =
                        source.name.Contains("Passive") ||
                        destination.name.Contains("Passive");
                    source.transitions[j].hasFixedDuration = true;
                    source.transitions[j].duration = passiveBlend
                        ? 0.18f
                        : 0.14f;
                }
            }

            AnimatorStateTransition[] anyTransitions =
                machine.anyStateTransitions;
            for (int i = 0; i < anyTransitions.Length; i++)
            {
                AnimatorState destination =
                    anyTransitions[i].destinationState;
                if (destination != null &&
                    destination.name.Contains("Attack_Passive"))
                {
                    anyTransitions[i].hasFixedDuration = true;
                    anyTransitions[i].duration = 0.10f;
                }
            }

            UnitAnimationProfile profile =
                AssetDatabase.LoadAssetAtPath<UnitAnimationProfile>(
                    "Assets/Config/Formal/Animation/" +
                    "AatroxAnimationProfile.asset");
            if (profile != null)
            {
                profile.AnimationVariantExitHash =
                    Animator.StringToHash("AnimationVariantExit");
                EditorUtility.SetDirty(profile);
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[Aatrox] Refreshed passive blends and ultimate-out transition.");
        }

        private static void EnsureTrigger(
            AnimatorController controller,
            string parameterName)
        {
            if (controller.parameters.Any(item =>
                    item.name == parameterName))
            {
                return;
            }
            controller.AddParameter(
                parameterName,
                AnimatorControllerParameterType.Trigger);
        }

        private static void EnsureUltimateOutEntry(
            AnimatorStateMachine machine,
            AnimatorState destination)
        {
            AnimatorStateTransition existing =
                machine.anyStateTransitions.FirstOrDefault(item =>
                    item.destinationState == destination);
            if (existing != null)
                return;
            AnimatorStateTransition transition =
                machine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.08f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                AnimatorConditionMode.If,
                0f,
                "AnimationVariantExit");
            transition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "IsAttacking");
            transition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "IsCasting");
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                0f,
                "LifeState");
        }

        private static void EnsureUltimateOutExit(
            AnimatorStateMachine machine,
            AnimatorState source,
            string destinationName,
            bool moving,
            bool passiveReady)
        {
            AnimatorState destination = FindState(machine, destinationName);
            if (destination == null ||
                source.transitions.Any(item =>
                    item.destinationState == destination))
            {
                return;
            }
            AnimatorStateTransition transition =
                source.AddTransition(destination);
            transition.hasExitTime = true;
            transition.exitTime = 0.78f;
            transition.hasFixedDuration = true;
            transition.duration = 0.10f;
            transition.AddCondition(
                moving
                    ? AnimatorConditionMode.If
                    : AnimatorConditionMode.IfNot,
                0f,
                "IsMoving");
            transition.AddCondition(
                passiveReady
                    ? AnimatorConditionMode.If
                    : AnimatorConditionMode.IfNot,
                0f,
                "IsPassiveReady");
            transition.AddCondition(
                AnimatorConditionMode.IfNot,
                0f,
                "IsAnimationVariantActive");
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                0f,
                "LifeState");
        }

        private static AnimatorState FindState(
            AnimatorStateMachine machine,
            string stateName)
        {
            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == stateName)
                    return states[i].state;
            }
            return null;
        }
    }
}
