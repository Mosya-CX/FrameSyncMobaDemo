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

        [Header("Attack Animation")]
        [Tooltip("Animation plan for normal attacks.")]
        public AttackAnimationPlan NormalAttackPlan;

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
            profile.NormalAttackPlan = AttackAnimationPlan.Default;
            profile.StageBindings = Array.Empty<StageAnimationBinding>();
            return profile;
        }
    }
}
