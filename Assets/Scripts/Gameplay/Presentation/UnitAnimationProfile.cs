using System;
using UnityEngine;

namespace FrameSyncMoba.Presentation
{
    /// <summary>
    /// Presentation v13.2 §3.5 — per-unit animation configuration asset.
    /// Contains Animator parameter hashes, attack animation bindings,
    /// and ability stage-to-animation mappings.
    /// 
    /// Stored as a ScriptableObject referenced by the Unit prefab.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitAnimationProfile", menuName = "FrameSyncMoba/Unit Animation Profile")]
    public sealed class UnitAnimationProfile : ScriptableObject
    {
        [Header("Animator Parameters")]
        [Tooltip("Hash for the 'MainKind' integer parameter.")]
        public int MainKindHash;

        [Tooltip("Hash for the 'BaseKind' integer parameter.")]
        public int BaseKindHash;

        [Tooltip("Hash for the 'IsMoving' boolean parameter.")]
        public int IsMovingHash;

        [Tooltip("Hash for the 'AttackTrigger' trigger parameter.")]
        public int AttackTriggerHash;

        [Tooltip("Hash for the 'CastTrigger' trigger parameter.")]
        public int CastTriggerHash;

        [Tooltip("Hash for the 'DeathTrigger' trigger parameter.")]
        public int DeathTriggerHash;

        [Tooltip("Hash for the 'RespawnTrigger' trigger parameter.")]
        public int RespawnTriggerHash;

        [Tooltip("Hash for the 'MoveSpeed' float parameter.")]
        public int MoveSpeedHash;

        [Tooltip("Hash for the 'IsAttacking' boolean parameter.")]
        public int IsAttackingHash;

        [Tooltip("Hash for the 'IsAttackRecovering' boolean parameter.")]
        public int IsAttackRecoveringHash;

        [Tooltip("Hash for the 'IsEmpoweredAttack' boolean parameter.")]
        public int IsEmpoweredAttackHash;

        [Tooltip("Hash for the 'AttackSequenceIndex' integer parameter.")]
        public int AttackSequenceIndexHash;

        [Tooltip("Hash for the 'AttackMotionTime' float parameter.")]
        public int AttackMotionTimeHash;

        [Tooltip("Hash for the 'AttackStart' trigger parameter.")]
        public int AttackStartHash;

        [Tooltip("Hash for the 'IsCasting' boolean parameter.")]
        public int IsCastingHash;

        [Tooltip("Hash for the 'AbilityStageProgress' float parameter.")]
        public int AbilityStageProgressHash;

        [Tooltip("Hash for the 'IsCharging' boolean parameter (movable-charge overlay).")]
        public int IsChargingHash;

        [Tooltip("Hash for the 'LifeState' integer parameter.")]
        public int LifeStateHash;

        [Tooltip("Hash for the 'IsControlled' boolean parameter.")]
        public int IsControlledHash;

        [Tooltip("Hash for the optional 'IsPassiveReady' boolean parameter.")]
        public int IsPassiveReadyHash;

        [Tooltip("Hash for the optional 'IsAnimationVariantActive' boolean parameter.")]
        public int IsAnimationVariantActiveHash;

        [Tooltip("Hash for the optional trigger fired when an animation variant ends.")]
        public int AnimationVariantExitHash;

        [Header("Read-only Gameplay Variants")]
        [Tooltip("Fixed passive AbilityId whose ready state selects optional locomotion/stage variants. Zero disables the mapping.")]
        public int PassiveReadyAbilityId;

        [Tooltip("BuffConfigId whose current presence selects an alternate animation form. Zero disables the mapping.")]
        public int AnimationVariantBuffConfigId;

        [Header("State Names")]
        public int IdleStateHash;
        public int MoveStateHash;
        public int DeathStateHash;
        public int RespawnStateHash;

        [Header("Attack Animation")]
        [Tooltip("Animation plan for normal attacks.")]
        public AttackAnimationPlan NormalAttackPlan;

        [Tooltip("Full-path Animator state hashes in stable attack sequence order.")]
        public int[] AttackStateHashes = Array.Empty<int>();

        [Header("Stage Bindings")]
        [Tooltip("Maps ability stages to animation states.")]
        public StageAnimationBinding[] StageBindings = Array.Empty<StageAnimationBinding>();

        /// <summary>
        /// Default profile with Unity's standard Animator parameter name hashes.
        /// </summary>
        public static UnitAnimationProfile CreateDefault()
        {
            var profile = CreateInstance<UnitAnimationProfile>();
            profile.MainKindHash = Animator.StringToHash("MainKind");
            profile.BaseKindHash = Animator.StringToHash("BaseKind");
            profile.IsMovingHash = Animator.StringToHash("IsMoving");
            profile.AttackTriggerHash = Animator.StringToHash("AttackTrigger");
            profile.CastTriggerHash = Animator.StringToHash("CastTrigger");
            profile.DeathTriggerHash = Animator.StringToHash("DeathTrigger");
            profile.RespawnTriggerHash = Animator.StringToHash("RespawnTrigger");
            profile.MoveSpeedHash = Animator.StringToHash("MoveSpeed");
            profile.IsAttackingHash = Animator.StringToHash("IsAttacking");
            profile.IsAttackRecoveringHash = Animator.StringToHash("IsAttackRecovering");
            profile.IsEmpoweredAttackHash = Animator.StringToHash("IsEmpoweredAttack");
            profile.AttackSequenceIndexHash = Animator.StringToHash("AttackSequenceIndex");
            profile.AttackMotionTimeHash = Animator.StringToHash("AttackMotionTime");
            profile.AttackStartHash = Animator.StringToHash("AttackStart");
            profile.IsCastingHash = Animator.StringToHash("IsCasting");
            profile.AbilityStageProgressHash = Animator.StringToHash("AbilityStageProgress");
            profile.IsChargingHash = Animator.StringToHash("IsCharging");
            profile.LifeStateHash = Animator.StringToHash("LifeState");
            profile.IsControlledHash = Animator.StringToHash("IsControlled");
            profile.IsPassiveReadyHash = Animator.StringToHash("IsPassiveReady");
            profile.IsAnimationVariantActiveHash = Animator.StringToHash("IsAnimationVariantActive");
            profile.AnimationVariantExitHash = Animator.StringToHash("AnimationVariantExit");
            profile.IdleStateHash = Animator.StringToHash("Base Layer.Idle");
            profile.MoveStateHash = Animator.StringToHash("Base Layer.Move");
            profile.DeathStateHash = Animator.StringToHash("Base Layer.Death");
            profile.RespawnStateHash = profile.IdleStateHash;
            profile.NormalAttackPlan = AttackAnimationPlan.Default;
            profile.AttackStateHashes = Array.Empty<int>();
            profile.StageBindings = Array.Empty<StageAnimationBinding>();
            return profile;
        }

        public bool TryGetStageBinding(
            int abilityId,
            int stageIndex,
            out StageAnimationBinding binding)
        {
            StageAnimationBinding[] bindings =
                StageBindings ?? Array.Empty<StageAnimationBinding>();
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].AbilityId == abilityId &&
                    bindings[i].StageIndex == stageIndex)
                {
                    binding = bindings[i];
                    return true;
                }
            }

            binding = default;
            return false;
        }
    }
}
