using UnityEngine;

[CreateAssetMenu(fileName = "BaseAnimationProfile", menuName = "Animation/Base Animation Profile")]
public sealed class BaseAnimationProfile : ScriptableObject
{
    [Header("Base Clips")]
    public AnimationClip IdleClip;
    public AnimationClip MoveClip;
    public AnimationClip DashClip;
    public AnimationClip[] AttackClips;
    public AnimationClip SiffnessClip;
    public AnimationClip DeadClip;

    [Header("Authored Speeds")]
    [Tooltip("Move 动画资源制作时参考的位移速度（米/秒）")]
    public float AuthoredMoveSpeed = 1.0f;

    [Tooltip("Attack 动画资源制作时参考的攻速倍率，通常为 1")]
    public float AuthoredAttackSpeed = 1.0f;

    [Header("Blend / Fade")]
    public float BaseTransitionDuration = 0.08f;
    public float IdleMoveBlendLerp = 12f;

    [Header("Overlay Defaults")]
    public float OverlayDefaultFadeIn = 0.08f;
    public float OverlayDefaultFadeOut = 0.10f;
}