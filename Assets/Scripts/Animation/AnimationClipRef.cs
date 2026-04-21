using UnityEngine;

[CreateAssetMenu(fileName = "AnimationClipRef", menuName = "Animation/Animation Clip Ref")]
public sealed class AnimationClipRef : ScriptableObject
{
    public int Id;
    public AnimationClip Clip;
}